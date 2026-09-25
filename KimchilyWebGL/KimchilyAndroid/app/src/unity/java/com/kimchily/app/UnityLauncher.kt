package com.kimchily.app

import android.app.Activity
import android.content.Intent
import com.unity3d.player.UnityPlayer

object UnityLauncher {
    const val available = true
    fun launch(activity: Activity) {
        activity.startActivity(Intent(activity, KimchilyUnityActivity::class.java)
            .addFlags(Intent.FLAG_ACTIVITY_REORDER_TO_FRONT or Intent.FLAG_ACTIVITY_SINGLE_TOP))
    }
    fun send(json: String) { UnityPlayer.UnitySendMessage("KimchilyHostBridge", "Receive", json) }
}
