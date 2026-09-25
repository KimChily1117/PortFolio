package com.kimchily.app

import android.content.Intent
import android.graphics.Color
import android.graphics.Typeface
import android.graphics.drawable.GradientDrawable
import android.os.Bundle
import android.text.InputType
import android.view.Gravity
import android.view.View
import android.widget.Button
import android.widget.EditText
import android.widget.LinearLayout
import android.widget.ScrollView
import android.widget.TextView
import android.widget.Toast
import androidx.activity.ComponentActivity
import com.journeyapps.barcodescanner.ScanContract
import com.journeyapps.barcodescanner.ScanOptions

class MainActivity : ComponentActivity() {
    private lateinit var status: TextView
    private lateinit var enter: Button
    private val observer: (SessionState) -> Unit = { render(it) }
    private val ink = Color.rgb(21, 49, 49)
    private val muted = Color.rgb(87, 110, 108)
    private val qrScanner = registerForActivityResult(ScanContract()) { result ->
        if (result.contents.isNullOrBlank()) showLinkMessage("스캔을 취소했어요. 링크를 붙여넣어 입장할 수도 있어요.")
        else openLink(result.contents)
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        window.statusBarColor = Color.rgb(18, 42, 45)
        window.navigationBarColor = Color.rgb(18, 42, 45)
        val page = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            setPadding(dp(24), dp(32), dp(24), dp(32))
            background = GradientDrawable(GradientDrawable.Orientation.TOP_BOTTOM,
                intArrayOf(Color.rgb(239, 248, 243), Color.rgb(248, 246, 238)))
        }
        setContentView(ScrollView(this).apply { isFillViewport = true; addView(page) })
        page.addView(label("KIMCHILY", 16, Color.rgb(17, 123, 104), true))
        page.addView(label("새로운 공간으로\n함께 들어가요", 32, ink, true).apply { setPadding(0, dp(24), 0, dp(12)) })
        page.addView(label("마음에 드는 월드를 고르고,\n나만의 이야기를 시작해 보세요.", 16, muted))
        space(page, 30)

        val card = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            setPadding(dp(24), dp(24), dp(24), dp(24))
            background = rounded(Color.WHITE, 24)
            elevation = dp(3).toFloat()
        }
        page.addView(card)
        card.addView(label("FIRST WORLD", 12, Color.rgb(17, 123, 104), true))
        card.addView(label("김칠리 스퀘어", 25, ink, true).apply { setPadding(0, dp(12), 0, dp(8)) })
        card.addView(label("첫 번째 공간의 캐릭터를 둘러보세요.\n월드를 나가면 이 화면으로 돌아와요.", 15, muted))
        space(card, 24)
        enter = Button(this).apply {
            text = "샘플 월드 입장"
            isAllCaps = false
            textSize = 16f
            setTextColor(Color.WHITE)
            background = rounded(Color.rgb(17, 123, 104), 14)
            setOnClickListener {
                if (UnityHostBridge.state.phase == SessionPhase.IN_WORLD) UnityLauncher.launch(this@MainActivity)
                else UnityHostBridge.openDemo(this@MainActivity)
            }
        }
        card.addView(enter, LinearLayout.LayoutParams(-1, dp(54)))
        space(page, 24)

        val published = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            setPadding(dp(20), dp(20), dp(20), dp(20))
            background = rounded(Color.WHITE, 20)
        }
        page.addView(published)
        published.addView(label("만든 월드로 입장", 22, ink, true))
        published.addView(label("에디터에서 게시한 QR을 스캔하거나 월드 링크를 붙여넣어 주세요.", 14, muted))
        space(published, 12)
        published.addView(Button(this).apply {
            text = "월드 QR 스캔"; isAllCaps = false
            setOnClickListener {
                qrScanner.launch(ScanOptions().setDesiredBarcodeFormats(ScanOptions.QR_CODE)
                    .setPrompt("Kimchily 월드 QR을 화면 안에 맞춰 주세요.")
                    .setBeepEnabled(false).setBarcodeImageEnabled(false).setOrientationLocked(false))
            }
        }, LinearLayout.LayoutParams(-1, dp(52)))
        val linkInput = EditText(this).apply {
            hint = "kimchily://world?..."
            contentDescription = "게시된 월드 링크"
            inputType = InputType.TYPE_CLASS_TEXT or InputType.TYPE_TEXT_VARIATION_URI
            setSingleLine(true)
            maxLines = 1
            textSize = 14f
            filters = arrayOf(android.text.InputFilter.LengthFilter(4096))
        }
        published.addView(linkInput, LinearLayout.LayoutParams(-1, dp(56)))
        published.addView(Button(this).apply {
            text = "링크로 입장"; isAllCaps = false
            setOnClickListener { openLink(linkInput.text.toString()) }
        }, LinearLayout.LayoutParams(-1, dp(52)))
        space(page, 24)
        status = label("", 14, muted).apply { setPadding(dp(16), dp(16), dp(16), dp(16)); background = rounded(Color.rgb(226, 239, 231), 14) }
        page.addView(status)
        space(page, 24)
        page.addView(label("작은 시작, 넓어지는 세상", 13, muted).apply { gravity = Gravity.CENTER })
        UnityHostBridge.observe(observer)
        if (savedInstanceState == null) handleWorldIntent(intent)
    }

    override fun onNewIntent(intent: Intent) {
        super.onNewIntent(intent)
        setIntent(intent)
        handleWorldIntent(intent)
    }

    private fun handleWorldIntent(intent: Intent) {
        if (intent.action != Intent.ACTION_VIEW) return
        val link = intent.dataString ?: return
        // Consume once; returning from Unity or restoring this Activity must not reopen an old QR.
        intent.data = null
        openLink(link)
    }

    private fun openLink(value: String) {
        val target = try { WorldLink.parse(value, allowHttp = BuildConfig.DEBUG) }
            catch (error: IllegalArgumentException) {
                showLinkMessage(error.message ?: "월드 링크를 확인해 주세요.")
                return
            }
        if (!UnityLauncher.available) {
            showLinkMessage("월드 링크를 확인했어요 (${target.worldId}). Unity 포함 앱에서 입장할 수 있어요.")
            return
        }
        if (!UnityHostBridge.openWorld(this, target)) showLinkMessage("월드에 입장하지 못했어요. 잠시 후 다시 시도해 주세요.")
    }

    private fun showLinkMessage(value: String) {
        if (::status.isInitialized) status.text = value
        Toast.makeText(this, value, Toast.LENGTH_LONG).show()
    }

    override fun onDestroy() { UnityHostBridge.removeObserver(observer); super.onDestroy() }

    // Back from the native home backgrounds the task instead of destroying a warm Unity Activity.
    override fun onBackPressed() { moveTaskToBack(true) }

    private fun render(state: SessionState) {
        if (!UnityLauncher.available) {
            status.text = "Android 단독 미리보기\nUnity 실행기가 아직 연결되지 않아 월드에 입장할 수 없어요."
            enter.isEnabled = false
            enter.alpha = 0.45f
            return
        }
        status.text = state.message + if (state.code.isNotEmpty()) "\n(${state.code})" else ""
        enter.isEnabled = state.phase in setOf(SessionPhase.IDLE, SessionPhase.FAILED, SessionPhase.IN_WORLD)
        enter.alpha = if (enter.isEnabled) 1f else 0.55f
        enter.text = if (state.phase == SessionPhase.IN_WORLD) "월드로 돌아가기" else "샘플 월드 입장"
    }

    private fun label(value: String, size: Int, color: Int, bold: Boolean = false) = TextView(this).apply {
        text = value; textSize = size.toFloat(); setTextColor(color)
        setLineSpacing(dp(4).toFloat(), 1f)
        if (bold) setTypeface(typeface, Typeface.BOLD)
    }
    private fun rounded(color: Int, radius: Int) = GradientDrawable().apply { setColor(color); cornerRadius = dp(radius).toFloat() }
    private fun space(parent: LinearLayout, height: Int) { parent.addView(View(this), LinearLayout.LayoutParams(1, dp(height))) }
    private fun dp(value: Int) = (value * resources.displayMetrics.density).toInt()
}
