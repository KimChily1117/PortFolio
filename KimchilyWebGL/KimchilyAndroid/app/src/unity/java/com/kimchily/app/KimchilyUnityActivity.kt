package com.kimchily.app

import android.content.res.Configuration
import android.graphics.Color
import android.graphics.drawable.GradientDrawable
import android.os.Build
import android.os.Bundle
import android.view.Gravity
import android.view.KeyEvent
import android.view.View
import android.widget.Button
import android.widget.FrameLayout
import android.widget.LinearLayout
import android.widget.PopupMenu
import android.widget.TextView
import com.unity3d.player.UnityPlayerActivity

class KimchilyUnityActivity : UnityPlayerActivity() {
    private lateinit var loading: LinearLayout
    private lateinit var status: TextView
    private lateinit var exit: Button
    private lateinit var overlay: FrameLayout
    private lateinit var orientationButton: Button
    private var orientationMode = WorldOrientation.LANDSCAPE
    private var wasClosing = false
    private val observer: (SessionState) -> Unit = { render(it) }

    override fun onCreate(savedInstanceState: Bundle?) {
        orientationMode = WorldOrientation.restore(getSharedPreferences(DISPLAY_PREFERENCES, MODE_PRIVATE)
            .getString(ORIENTATION_PREFERENCE, null))
        super.onCreate(savedInstanceState)
        // Unity's cold startup can request its build-time orientation. The native choice remains authoritative.
        requestedOrientation = orientationMode.androidOrientation
        overlay = FrameLayout(this)
        loading = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL; gravity = Gravity.CENTER
            setPadding(dp(24), dp(24), dp(24), dp(24))
            setBackgroundColor(Color.rgb(18, 42, 45))
        }
        status = TextView(this).apply {
            setTextColor(Color.WHITE); textSize = 18f; gravity = Gravity.CENTER
        }
        loading.addView(status)
        overlay.addView(loading, FrameLayout.LayoutParams(-1, -1))
        val toolbar = LinearLayout(this).apply {
            orientation = LinearLayout.HORIZONTAL
            gravity = Gravity.CENTER_VERTICAL
            setPadding(dp(12), dp(8), dp(12), dp(8))
        }
        orientationButton = toolbarButton().apply { setOnClickListener { showOrientationChoices() } }
        toolbar.addView(orientationButton, LinearLayout.LayoutParams(-2, dp(48)))
        toolbar.addView(View(this), LinearLayout.LayoutParams(0, 1, 1f))
        exit = toolbarButton().apply {
            text = "나가기"; isAllCaps = false
            contentDescription = "월드에서 나가 홈으로 돌아가기"
            setOnClickListener { requestExit() }
        }
        toolbar.addView(exit, LinearLayout.LayoutParams(-2, dp(48)).apply { leftMargin = dp(8) })
        overlay.addView(toolbar, FrameLayout.LayoutParams(-1, -2, Gravity.TOP))
        // Only the buttons intercept playing touches. Leave the lower screen to Unity's movement controls.
        overlay.setOnApplyWindowInsetsListener { _, insets ->
            var left = insets.systemWindowInsetLeft
            var top = insets.systemWindowInsetTop
            var right = insets.systemWindowInsetRight
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.P) {
                insets.displayCutout?.let {
                    left = maxOf(left, it.safeInsetLeft)
                    top = maxOf(top, it.safeInsetTop)
                    right = maxOf(right, it.safeInsetRight)
                }
            }
            toolbar.setPadding(dp(12) + left, dp(8) + top, dp(12) + right, dp(8))
            insets
        }
        updateOrientationLabel()
        addContentView(overlay, FrameLayout.LayoutParams(-1, -1))
        overlay.requestApplyInsets()
        UnityHostBridge.observe(observer)
    }

    override fun setRequestedOrientation(requestedOrientation: Int) {
        // Unity internal requests must not overwrite the display mode selected in the native toolbar.
        super.setRequestedOrientation(orientationMode.androidOrientation)
    }

    override fun onConfigurationChanged(newConfig: Configuration) {
        // The superclass updates the existing UnityPlayer; do not recreate it or reopen the world.
        super.onConfigurationChanged(newConfig)
        if (::overlay.isInitialized) overlay.requestApplyInsets()
    }

    private fun showOrientationChoices() {
        PopupMenu(this, orientationButton).apply {
            WorldOrientation.values().forEachIndexed { index, mode ->
                menu.add(0, index, index, if (mode == WorldOrientation.AUTOMATIC) "자동 회전" else "${mode.label} 화면").apply {
                    isCheckable = true
                    isChecked = mode == orientationMode
                }
            }
            menu.setGroupCheckable(0, true, true)
            setOnMenuItemClickListener { item ->
                orientationMode = WorldOrientation.values()[item.itemId]
                getSharedPreferences(DISPLAY_PREFERENCES, MODE_PRIVATE).edit()
                    .putString(ORIENTATION_PREFERENCE, orientationMode.preference).apply()
                requestedOrientation = orientationMode.androidOrientation
                updateOrientationLabel()
                true
            }
            show()
        }
    }

    private fun updateOrientationLabel() {
        orientationButton.text = "화면: ${orientationMode.label}"
        orientationButton.contentDescription = "화면 방향 선택, 현재 ${orientationMode.label}"
    }

    private fun toolbarButton() = Button(this).apply {
        isAllCaps = false
        textSize = 14f
        minWidth = dp(72)
        minimumWidth = dp(72)
        minHeight = dp(48)
        minimumHeight = dp(48)
        setPadding(dp(14), 0, dp(14), 0)
        setTextColor(Color.WHITE)
        background = GradientDrawable().apply {
            setColor(Color.argb(232, 18, 42, 45))
            cornerRadius = dp(12).toFloat()
            setStroke(dp(1), Color.rgb(95, 144, 132))
        }
    }

    private companion object {
        const val DISPLAY_PREFERENCES = "world_display"
        const val ORIENTATION_PREFERENCE = "orientation"
    }

    override fun onResume() {
        super.onResume()
        if (UnityHostBridge.state.phase == SessionPhase.IDLE) UnityHostBridge.showHome(this)
        else UnityHostBridge.unityActivityResumed()
    }
    override fun onPause() { UnityHostBridge.unityActivityPaused(); super.onPause() }
    override fun onDestroy() { UnityHostBridge.removeObserver(observer); super.onDestroy() }
    override fun onBackPressed() { requestExit() }

    // UnityPlayerActivity forwards key events to Unity, bypassing Activity.onBackPressed.
    override fun dispatchKeyEvent(event: KeyEvent): Boolean {
        if (event.keyCode == KeyEvent.KEYCODE_BACK) {
            if (event.action == KeyEvent.ACTION_UP && !event.isCanceled) requestExit()
            return true
        }
        return super.dispatchKeyEvent(event)
    }

    private fun requestExit() {
        if (UnityHostBridge.returnHomeAfterStartupFailure(this)) return
        if (UnityHostBridge.state.phase == SessionPhase.IDLE) UnityHostBridge.showHome(this)
        else UnityHostBridge.closeWorld()
    }

    private fun render(state: SessionState) {
        loading.visibility = if (state.phase == SessionPhase.IN_WORLD) View.GONE else View.VISIBLE
        status.text = state.message +
            if (state.phase == SessionPhase.OPENING) "\n${(state.progress * 100).toInt()}%" else ""
        exit.isEnabled = state.phase != SessionPhase.CLOSING
        exit.text = if (state.phase == SessionPhase.FAILED && !state.runtimeReady) "홈으로" else "나가기"
        if (wasClosing && state.phase == SessionPhase.IDLE) UnityHostBridge.showHome(this)
        wasClosing = state.phase == SessionPhase.CLOSING
    }

    private fun dp(value: Int) = (value * resources.displayMetrics.density).toInt()
}
