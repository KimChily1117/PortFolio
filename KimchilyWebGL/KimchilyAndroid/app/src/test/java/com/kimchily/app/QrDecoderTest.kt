package com.kimchily.app

import com.google.zxing.BinaryBitmap
import com.google.zxing.DecodeHintType
import com.google.zxing.RGBLuminanceSource
import com.google.zxing.common.HybridBinarizer
import com.google.zxing.qrcode.QRCodeReader
import org.junit.Assert.assertEquals
import org.junit.Test

/** Publisher-compatible fixtures plus an actual Kimchily publication; use normal camera detection. */
class QrDecoderTest {
    private data class Fixture(val image: String, val revision: String, val sha256: String) {
        val expected = "kimchily://world?manifest=http%3A%2F%2F192.168.0.4%3A8787%2Fworlds%2Fmy-first-world%2F" +
            revision + "%2Fworld.json&sha256=" + sha256
    }
    private val first = Fixture("/qr-published-v1.png", "20260918T125907580Z-19496263",
        "b79c79941ee2d4b5cd790a427095c126b58a496717732b6b466e2e16bb64bcf1")
    private val second = Fixture("/qr-published-v2.png", "20260918T134207063Z-489f333e",
        "efe48a1515f37e5da29f39f34869a9744a1d92fed67b7610aa9ec2d20f604107")
    private val kimchily = Fixture("/qr-published-kimchily.png", "20260918T142553418Z-00e2fd13",
        "bd183018a7c9fd375d556b8798e73c14a8d2dce091760f53cf27c1cc56432a22")

    @Test fun firstPublishedQrDecodesThroughNormalCameraDetector() { verify(first, 1, 0) }
    @Test fun secondPublishedQrRegressionDecodesWithoutPureBarcodeOrTryHarder() { verify(second, 1, 0) }
    @Test fun actualKimchilyPublicationDecodesThroughNormalCameraDetector() { verify(kimchily, 1, 0) }

    @Test fun publishedQrsRemainReadableAtSmallerSizesAndQuarterTurns() {
        for (fixture in listOf(first, second, kimchily)) for (scale in listOf(1, 2, 4)) for (turns in 0..3) {
            verify(fixture, scale, turns)
        }
    }

    private fun verify(fixture: Fixture, divisor: Int, turns: Int) {
        // ImageIO is available on this host-JVM test runtime, but intentionally absent
        // from the Android compilation API. Keep it outside the application's API surface.
        val image = javaClass.getResourceAsStream(fixture.image).use { stream ->
            Class.forName("javax.imageio.ImageIO").getMethod("read", java.io.InputStream::class.java).invoke(null, stream)
        }
        val originalWidth = image.javaClass.getMethod("getWidth").invoke(image) as Int
        val originalHeight = image.javaClass.getMethod("getHeight").invoke(image) as Int
        val originals = image.javaClass.getMethod("getRGB", Integer.TYPE, Integer.TYPE, Integer.TYPE,
            Integer.TYPE, IntArray::class.java, Integer.TYPE, Integer.TYPE)
            .invoke(image, 0, 0, originalWidth, originalHeight, null, 0, originalWidth) as IntArray
        val width = originalWidth / divisor
        val height = originalHeight / divisor
        assertEquals(width, height)
        var pixels = IntArray(width * height) { index ->
            originals[(index / width) * divisor * originalWidth + (index % width) * divisor]
        }
        repeat(turns) {
            val before = pixels
            pixels = IntArray(width * height) { index ->
                val x = index % width
                val y = index / width
                before[(height - 1 - x) * width + y]
            }
        }
        val hints = mapOf<DecodeHintType, Any>(DecodeHintType.CHARACTER_SET to "UTF-8")
        val decoded = QRCodeReader().decode(BinaryBitmap(HybridBinarizer(
            RGBLuminanceSource(width, height, pixels))), hints).text
        assertEquals("${fixture.image}, divisor=$divisor, quarterTurns=$turns", fixture.expected, decoded)
        assertEquals(fixture.revision, WorldLink.parse(decoded, allowHttp = true).revisionId)
    }
}
