package com.kimchily.app

import android.content.pm.ActivityInfo
import java.io.File
import javax.xml.parsers.DocumentBuilderFactory
import org.junit.Assert.*
import org.junit.Test
import org.w3c.dom.Element

class WorldOrientationTest {
    @Test fun firstLaunchAndUnknownPreferenceUseLandscape() {
        assertEquals(WorldOrientation.LANDSCAPE, WorldOrientation.restore(null))
        assertEquals(WorldOrientation.LANDSCAPE, WorldOrientation.restore(""))
        assertEquals(WorldOrientation.LANDSCAPE, WorldOrientation.restore("outdated-value"))
    }

    @Test fun everyExplicitUserPreferenceSurvivesAWorldReentry() {
        WorldOrientation.values().forEach { mode ->
            assertEquals(mode, WorldOrientation.restore(mode.preference))
        }
    }

    @Test fun lockedModesAllowBothDirectionsOnTheirAxis() {
        assertEquals(ActivityInfo.SCREEN_ORIENTATION_SENSOR_LANDSCAPE, WorldOrientation.LANDSCAPE.androidOrientation)
        assertEquals(ActivityInfo.SCREEN_ORIENTATION_SENSOR_PORTRAIT, WorldOrientation.PORTRAIT.androidOrientation)
        assertEquals(ActivityInfo.SCREEN_ORIENTATION_FULL_SENSOR, WorldOrientation.AUTOMATIC.androidOrientation)
    }

    @Test fun unityRotationIsHandledWithoutDestroyingTheUnityActivity() {
        val activity = activity("unity", ".KimchilyUnityActivity")
        assertEquals("sensorLandscape", attribute(activity, "screenOrientation"))
        assertEquals("false", attribute(activity, "resizeableActivity"))
        assertEquals("singleTop", attribute(activity, "launchMode"))
        assertEquals("false", attribute(activity, "exported"))
        assertHandlesRotation(activity)
    }

    @Test fun bothHomeVariantsPreserveInputsDuringRotation() {
        listOf("main", "unity").forEach { variant ->
            val activity = activity(variant, ".MainActivity")
            assertEquals("fullSensor", attribute(activity, "screenOrientation"))
            assertHandlesRotation(activity)
        }
    }

    private fun assertHandlesRotation(activity: Element) {
        val handled = attribute(activity, "configChanges").split('|').toSet()
        assertTrue("Rotation must not recreate the Activity", handled.containsAll(
            setOf("orientation", "screenSize", "screenLayout", "smallestScreenSize")))
    }

    private fun activity(variant: String, name: String): Element {
        val manifest = File("src/$variant/AndroidManifest.xml")
        val factory = DocumentBuilderFactory.newInstance().apply { isNamespaceAware = true }
        val activities = factory.newDocumentBuilder().parse(manifest).getElementsByTagName("activity")
        return (0 until activities.length).map { activities.item(it) as Element }
            .single { attribute(it, "name") == name }
    }

    private fun attribute(element: Element, name: String) =
        element.getAttributeNS("http://schemas.android.com/apk/res/android", name)
}
