package com.kimchily.app

import android.content.pm.ActivityInfo

/** The native host owns display rotation; changing this policy never opens or reloads a world. */
enum class WorldOrientation(val preference: String, val label: String, val androidOrientation: Int) {
    AUTOMATIC("auto", "자동", ActivityInfo.SCREEN_ORIENTATION_FULL_SENSOR),
    LANDSCAPE("landscape", "가로", ActivityInfo.SCREEN_ORIENTATION_SENSOR_LANDSCAPE),
    PORTRAIT("portrait", "세로", ActivityInfo.SCREEN_ORIENTATION_SENSOR_PORTRAIT);

    companion object {
        fun restore(value: String?): WorldOrientation = values().firstOrNull { it.preference == value } ?: LANDSCAPE
    }
}
