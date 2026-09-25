package com.kimchily.app

import org.junit.Assert.*
import org.junit.Test

class WorldSessionTest {
    private var sequence = 0
    private val sent = mutableListOf<HostMessage>()
    private val session = WorldSession({ "request-${++sequence}" }, { sent.add(it) })
    private fun ready() = session.receive(HostMessage("RuntimeReady"))

    @Test fun openWaitsForReadyAndDuplicateReadyDoesNotSendTwice() {
        assertTrue(session.open("demo", "builtin-v1"))
        assertTrue(sent.isEmpty())
        assertFalse(session.open("demo", "builtin-v1"))
        ready(); ready()
        assertEquals(listOf("OpenWorld"), sent.map { it.type })
        assertEquals(SessionPhase.OPENING, session.state.phase)
    }

    @Test fun unrelatedWorldOrRequestCannotCompleteAnOpen() {
        session.open("demo", "builtin-v1"); ready()
        session.receive(HostMessage("WorldReady", "stale", "demo"))
        session.receive(HostMessage("WorldReady", "request-1", "another-world"))
        assertEquals(SessionPhase.OPENING, session.state.phase)
        session.receive(HostMessage("WorldReady", "request-1", "demo"))
        assertEquals(SessionPhase.IN_WORLD, session.state.phase)
    }

    @Test fun cancelledOpenCannotInterruptTheCloseAcknowledgement() {
        session.open("demo", "builtin-v1"); ready(); session.close()
        assertFalse(session.close())
        session.receive(HostMessage("WorldFailed", "request-1", "demo", code = "CANCELLED"))
        assertEquals(SessionPhase.CLOSING, session.state.phase)
        session.receive(HostMessage("WorldClosed", "request-1", "demo"))
        assertEquals(SessionPhase.CLOSING, session.state.phase)
        session.receive(HostMessage("WorldClosed", "request-2", "demo"))
        assertEquals(SessionPhase.IDLE, session.state.phase)
    }

    @Test fun backBeforeRuntimeReadyQueuesOnlyClose() {
        session.open("demo", "builtin-v1"); session.close(); ready()
        assertEquals(listOf("CloseWorld"), sent.map { it.type })
    }

    @Test fun warmReentryRequiresAFreshHandshake() {
        session.open("demo", "builtin-v1"); ready(); session.close()
        session.receive(HostMessage("WorldClosed", "request-2", "demo"))
        session.open("demo", "builtin-v1")
        assertEquals(2, sent.size)
        assertEquals(SessionPhase.WAITING_RUNTIME, session.state.phase)
        ready()
        assertEquals("request-3", sent.last().requestId)
    }

    @Test fun oldProtocolAndLateReadyAfterTimeoutAreIgnored() {
        session.open("demo", "builtin-v1")
        session.receive(HostMessage("RuntimeReady", protocolVersion = 2))
        assertTrue(sent.isEmpty())
        session.startupTimedOut(); ready()
        assertEquals(SessionPhase.FAILED, session.state.phase)
        assertTrue(sent.isEmpty())
        assertTrue(session.dismissStartupFailure())
        assertEquals(SessionPhase.IDLE, session.state.phase)
    }

    @Test fun runtimeCloseRequestUsesANewIdAndIgnoresDuplicates() {
        session.open("demo", "builtin-v1"); ready()
        session.receive(HostMessage("WorldReady", "request-1", "demo"))
        session.receive(HostMessage("CloseRequested", "request-1", "demo"))
        session.receive(HostMessage("CloseRequested", "request-1", "demo"))
        assertEquals(SessionPhase.CLOSING, session.state.phase)
        assertEquals("request-2", sent.last().requestId)
        assertEquals(2, sent.size)
    }

    private fun published(revision: String = "r1", hash: String = "a".repeat(64)) =
        WorldTarget("town", revision, "https://host/worlds/town/$revision/world.json", hash)

    @Test fun publishedMetadataIsSentUnchangedAfterHandshake() {
        val target = published()
        assertTrue(session.requestOpen(target)); ready()
        assertEquals(target.worldId, sent.single().worldId)
        assertEquals(target.revisionId, sent.single().revisionId)
        assertEquals(target.manifestUrl, sent.single().manifestUrl)
        assertEquals(target.manifestSha256, sent.single().manifestSha256)
    }

    @Test fun newRevisionWaitsForMatchingCloseBeforeOpening() {
        session.requestOpen(published()); ready()
        session.receive(HostMessage("WorldReady", "request-1", "town", "r1"))
        session.requestOpen(published("r2"))
        assertEquals(listOf("OpenWorld", "CloseWorld"), sent.map { it.type })
        session.receive(HostMessage("WorldClosed", "request-2", "town", "r2"))
        assertEquals(SessionPhase.CLOSING, session.state.phase)
        session.receive(HostMessage("WorldClosed", "request-2", "town", "r1"))
        assertEquals(SessionPhase.OPENING, session.state.phase)
        assertEquals("r2", sent.last().revisionId)
        assertEquals(published("r2").manifestUrl, sent.last().manifestUrl)
        session.receive(HostMessage("WorldReady", "request-1", "town", "r1"))
        assertEquals(SessionPhase.OPENING, session.state.phase)
    }

    @Test fun identicalLinkDoesNotReloadButNewHashDoes() {
        session.requestOpen(published()); ready()
        session.requestOpen(published())
        assertEquals(1, sent.size)
        session.requestOpen(published(hash = "b".repeat(64)))
        assertEquals("CloseWorld", sent.last().type)
    }

    @Test fun latestLinkWinsDuringCloseAndCloseFailureDoesNotOpenIt() {
        session.requestOpen(published()); ready()
        session.requestOpen(published("r2")); session.requestOpen(published("r3"))
        session.receive(HostMessage("WorldFailed", "request-2", "town", "r1", code = "UNLOAD_FAILED"))
        assertEquals(SessionPhase.FAILED, session.state.phase)
        assertEquals(2, sent.size)
        session.requestOpen(published("r4"))
        assertEquals("CloseWorld", sent.last().type)
        session.receive(HostMessage("WorldClosed", "request-3", "town", "r1"))
        assertEquals("r4", sent.last().revisionId)
    }

    @Test fun multipleQueuedLinksOpenOnlyTheLastRevisionWithoutReturningHome() {
        val phases = mutableListOf<SessionPhase>()
        val observed = WorldSession({ "observed-${++sequence}" }, { sent.add(it) }, { phases.add(it.phase) })
        observed.requestOpen(published()); observed.receive(HostMessage("RuntimeReady"))
        observed.requestOpen(published("r2")); observed.requestOpen(published("r3"))
        observed.receive(HostMessage("WorldClosed", "observed-2", "town", "r1"))
        assertEquals(listOf("OpenWorld", "CloseWorld", "OpenWorld"), sent.map { it.type })
        assertEquals("r3", sent.last().revisionId)
        assertEquals(published("r3").manifestUrl, sent.last().manifestUrl)
        assertFalse(phases.contains(SessionPhase.IDLE))
    }

    @Test fun switchDuringStartupClosesFirstAndExplicitExitCancelsPendingEntry() {
        session.requestOpen(published()); session.requestOpen(published("r2")); ready()
        assertEquals(listOf("CloseWorld"), sent.map { it.type })
        assertFalse(session.close()) // Also cancels queued r2 while the close is already in flight.
        session.receive(HostMessage("WorldClosed", "request-2", "town", "r1"))
        assertEquals(SessionPhase.IDLE, session.state.phase)
        assertEquals(1, sent.size)
    }

    @Test fun startupTimeoutDiscardsQueuedMetadataAndOnlySendsTheRetriedTarget() {
        session.requestOpen(published()); session.requestOpen(published("r2"))
        session.startupTimedOut(); ready()
        assertEquals(SessionPhase.FAILED, session.state.phase)
        assertTrue(sent.isEmpty())
        session.requestOpen(published("r3")); ready()
        assertEquals(1, sent.size)
        assertEquals("OpenWorld", sent.single().type)
        assertEquals("r3", sent.single().revisionId)
        assertEquals(published("r3").manifestUrl, sent.single().manifestUrl)
    }
}
