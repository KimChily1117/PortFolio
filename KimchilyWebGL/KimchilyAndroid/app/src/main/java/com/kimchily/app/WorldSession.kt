package com.kimchily.app

/** No Android dependencies: this is the single owner of the host/runtime protocol state. */
data class HostMessage(
    val type: String,
    val requestId: String = "",
    val worldId: String = "",
    val revisionId: String = "",
    val message: String = "",
    val code: String = "",
    val progress: Double = 0.0,
    val protocolVersion: Int = 1,
    val manifestUrl: String = "",
    val manifestSha256: String = ""
)

enum class SessionPhase { IDLE, WAITING_RUNTIME, OPENING, IN_WORLD, CLOSING, FAILED }

data class SessionState(
    val phase: SessionPhase = SessionPhase.IDLE,
    val worldId: String = "",
    val revisionId: String = "",
    val requestId: String = "",
    val message: String = "월드를 선택해 주세요.",
    val code: String = "",
    val progress: Double = 0.0,
    val runtimeReady: Boolean = false
)

class WorldSession(
    private val newRequestId: () -> String,
    private val send: (HostMessage) -> Unit,
    private val changed: (SessionState) -> Unit = {}
) {
    var state = SessionState()
        private set
    private var pending: HostMessage? = null
    private var currentTarget: WorldTarget? = null
    private var queuedTarget: WorldTarget? = null

    fun open(worldId: String, revisionId: String): Boolean {
        if (worldId.isBlank() || state.phase !in setOf(SessionPhase.IDLE, SessionPhase.FAILED)) return false
        return requestOpen(WorldTarget(worldId, revisionId))
    }

    /** The latest requested revision wins, but no new world opens before the old one closes. */
    fun requestOpen(target: WorldTarget): Boolean {
        if (target.worldId.isBlank() || target.revisionId.isBlank()) return false
        if (state.phase == SessionPhase.IDLE || (state.phase == SessionPhase.FAILED && !state.runtimeReady)) {
            queuedTarget = null
            beginOpen(target)
        } else if (target == currentTarget && state.phase in setOf(
                SessionPhase.WAITING_RUNTIME, SessionPhase.OPENING, SessionPhase.IN_WORLD)) {
            return true
        } else {
            queuedTarget = target
            if (state.phase != SessionPhase.CLOSING) beginClose()
        }
        return true
    }

    private fun beginOpen(target: WorldTarget, runtimeReady: Boolean = false) {
        currentTarget = target
        val command = HostMessage("OpenWorld", newRequestId(), target.worldId, target.revisionId,
            manifestUrl = target.manifestUrl, manifestSha256 = target.manifestSha256)
        pending = command
        // Always handshake, including reentry into a warm Unity Activity.
        // A queued switch can reuse the live runtime that just acknowledged WorldClosed.
        update(SessionState(SessionPhase.WAITING_RUNTIME, target.worldId, target.revisionId, command.requestId,
            "월드 실행기를 준비하고 있어요.", runtimeReady = runtimeReady))
        flushWhenReady()
    }

    fun close(): Boolean {
        queuedTarget = null
        return beginClose()
    }

    private fun beginClose(): Boolean {
        if (state.phase == SessionPhase.IDLE || state.phase == SessionPhase.CLOSING) return false
        val command = HostMessage("CloseWorld", newRequestId(), state.worldId, state.revisionId)
        pending = command
        update(state.copy(phase = SessionPhase.CLOSING, requestId = command.requestId,
            message = "월드를 정리하고 있어요.", code = ""))
        flushWhenReady()
        return true
    }

    fun receive(event: HostMessage) {
        if (event.protocolVersion != 1) return
        if (event.type == "RuntimeReady") {
            if (state.phase !in setOf(SessionPhase.WAITING_RUNTIME, SessionPhase.CLOSING) || state.runtimeReady) return
            update(state.copy(runtimeReady = true))
            flushWhenReady()
            return
        }
        if (event.requestId.isBlank() || event.requestId != state.requestId) return
        if (event.worldId.isNotBlank() && event.worldId != state.worldId) return
        if (event.revisionId.isNotBlank() && event.revisionId != state.revisionId) return
        when (event.type) {
            "WorldProgress" -> if (state.phase == SessionPhase.OPENING) {
                update(state.copy(progress = event.progress.coerceIn(0.0, 1.0),
                    message = event.message.ifBlank { "월드를 불러오고 있어요." }))
            }
            "WorldReady" -> if (state.phase == SessionPhase.OPENING) {
                update(state.copy(phase = SessionPhase.IN_WORLD, progress = 1.0, message = "월드에 입장했어요."))
            }
            "WorldFailed" -> if (state.phase in setOf(SessionPhase.OPENING, SessionPhase.CLOSING)) {
                pending = null
                queuedTarget = null
                update(state.copy(phase = SessionPhase.FAILED, code = event.code,
                    message = event.message.ifBlank { "월드를 실행하지 못했어요." }))
            }
            "WorldClosed" -> if (state.phase == SessionPhase.CLOSING) {
                pending = null
                val next = queuedTarget
                queuedTarget = null
                currentTarget = null
                if (next == null) update(SessionState(message = "월드에서 나왔어요."))
                else beginOpen(next, runtimeReady = true)
            }
            "CloseRequested" -> if (state.phase in setOf(SessionPhase.OPENING, SessionPhase.IN_WORLD)) close()
        }
    }

    fun startupTimedOut() {
        if (state.runtimeReady || state.phase !in setOf(SessionPhase.WAITING_RUNTIME, SessionPhase.CLOSING)) return
        pending = null
        queuedTarget = null
        update(state.copy(phase = SessionPhase.FAILED, code = "RUNTIME_TIMEOUT",
            message = "월드 실행기의 응답이 없어요. 홈에서 다시 시도해 주세요."))
    }

    /** No world command was delivered; returning home is safe even if Unity never became ready. */
    fun dismissStartupFailure(): Boolean {
        if (state.phase != SessionPhase.FAILED || state.runtimeReady) return false
        pending = null
        queuedTarget = null
        currentTarget = null
        update(SessionState(message = "월드 실행기를 시작하지 못했어요."))
        return true
    }

    private fun flushWhenReady() {
        if (!state.runtimeReady) return
        val command = pending ?: return
        pending = null
        if (command.type == "OpenWorld") {
            update(state.copy(phase = SessionPhase.OPENING, message = "월드를 불러오고 있어요."))
        }
        send(command)
    }

    private fun update(next: SessionState) {
        state = next
        changed(next)
    }
}
