package com.kimchily.app

import android.app.Activity

/** Standalone UI build: there is deliberately no simulated Unity success callback. */
object UnityLauncher {
    const val available = false
    fun launch(activity: Activity) = Unit
    fun send(json: String) = Unit
}
