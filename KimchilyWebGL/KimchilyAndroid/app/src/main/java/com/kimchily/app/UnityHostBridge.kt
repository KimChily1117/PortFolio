package com.kimchily.app

import android.app.Activity
import android.content.Intent
import android.os.Handler
import android.os.Looper
import org.json.JSONObject
import java.util.UUID

/** All protocol state and UI notifications are confined to Android's main thread. */
object UnityHostBridge {
    private val main = Handler(Looper.getMainLooper())
    private val listeners = linkedSetOf<(SessionState) -> Unit>()
    private val session = WorldSession(
        { UUID.randomUUID().toString() },
        { UnityLauncher.send(encode(it)) },
        { next -> listeners.toList().forEach { it(next) } }
    )
    private var handshakeGeneration = 0
    val state: SessionState get() = session.state

    fun observe(listener: (SessionState) -> Unit) { listeners.add(listener); listener(state) }
    fun removeObserver(listener: (SessionState) -> Unit) { listeners.remove(listener) }

    fun openDemo(activity: Activity): Boolean {
        return openWorld(activity, WorldTarget("demo", "builtin-v1"))
    }

    fun openWorld(activity: Activity, target: WorldTarget): Boolean {
        if (!UnityLauncher.available || !session.requestOpen(target)) return false
        UnityLauncher.launch(activity)
        return true
    }

    fun closeWorld() { session.close() }

    fun unityActivityPaused() { handshakeGeneration++ }

    fun unityActivityResumed() {
        if (state.runtimeReady || state.phase !in setOf(SessionPhase.WAITING_RUNTIME, SessionPhase.CLOSING)) return
        val generation = ++handshakeGeneration
        var attempts = 0
        val started = android.os.SystemClock.elapsedRealtime()
        val retry = object : Runnable {
            override fun run() {
                if (generation != handshakeGeneration || state.runtimeReady ||
                    state.phase !in setOf(SessionPhase.WAITING_RUNTIME, SessionPhase.CLOSING)) return
                if (android.os.SystemClock.elapsedRealtime() - started >= 12_000) {
                    session.startupTimedOut()
                    return
                }
                if (attempts++ < 8) {
                    UnityLauncher.send(encode(HostMessage("Initialize", UUID.randomUUID().toString())))
                }
                main.postDelayed(this, 1_000)
            }
        }
        main.post(retry)
    }

    fun returnHomeAfterStartupFailure(activity: Activity): Boolean {
        if (!session.dismissStartupFailure()) return false
        showHome(activity)
        return true
    }

    fun showHome(activity: Activity) {
        // Keep the Unity Activity alive below the native home; never call Quit or finish here.
        activity.startActivity(Intent(activity, MainActivity::class.java)
            .addFlags(Intent.FLAG_ACTIVITY_REORDER_TO_FRONT or Intent.FLAG_ACTIVITY_SINGLE_TOP))
    }

    /** Called by C# AndroidJavaClass("com.kimchily.app.UnityHostBridge").CallStatic(...). */
    @JvmStatic
    fun onUnityEvent(json: String) {
        main.post {
            val event = runCatching {
                val value = JSONObject(json)
                HostMessage(type = value.getString("type"), requestId = value.optString("requestId"),
                    worldId = value.optString("worldId"), revisionId = value.optString("revisionId"),
                    message = value.optString("message"), code = value.optString("code"),
                    progress = value.optDouble("progress", 0.0), protocolVersion = value.optInt("protocolVersion", -1))
            }.getOrNull() ?: return@post
            session.receive(event)
        }
    }

    private fun encode(value: HostMessage): String = JSONObject()
        .put("protocolVersion", value.protocolVersion).put("type", value.type)
        .put("requestId", value.requestId).put("worldId", value.worldId)
        .put("revisionId", value.revisionId).put("message", value.message)
        .put("manifestUrl", value.manifestUrl).put("manifestSha256", value.manifestSha256)
        .put("code", value.code).put("progress", value.progress).toString()
}
