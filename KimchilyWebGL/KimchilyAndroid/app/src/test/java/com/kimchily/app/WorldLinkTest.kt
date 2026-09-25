package com.kimchily.app

import org.junit.Assert.*
import org.junit.Test
import java.net.URLEncoder

class WorldLinkTest {
    private val hash = "abcdef01".repeat(8)
    private fun link(manifest: String, checksum: String = hash) =
        "kimchily://world?manifest=${URLEncoder.encode(manifest, "UTF-8")}&sha256=$checksum"
    private fun rejected(value: String, allowHttp: Boolean = false) {
        try { WorldLink.parse(value, allowHttp); fail("Accepted invalid world link: $value") }
        catch (_: IllegalArgumentException) { /* expected */ }
    }

    @Test fun parsesPublishedRevisionAndNormalizesHash() {
        val url = "https://worlds.example.com/worlds/town_1/rev-2026/world.json"
        assertEquals(WorldTarget("town_1", "rev-2026", url, hash), WorldLink.parse(link(url, hash.uppercase())))
    }

    @Test fun httpRequiresExplicitDevelopmentOptIn() {
        val url = "http://192.168.0.10:8787/worlds/town/r1/world.json"
        rejected(link(url))
        assertEquals(url, WorldLink.parse(link(url), allowHttp = true).manifestUrl)
    }

    @Test fun rejectsCredentialsFragmentsQueriesAndInvalidPorts() {
        listOf("https://u:p@host", "https://host:0", "https://host:65536", "https://host:",
            "https:///", "file://host", "javascript://host").forEach {
            rejected(link("$it/worlds/town/r1/world.json"))
        }
        rejected(link("https://host/worlds/town/r1/world.json?token=value"))
        rejected(link("https://host/worlds/town/r1/world.json#fragment"))
    }

    @Test fun requiresExactUnencodedBoundedContentPath() {
        listOf("/other/town/r1/world.json", "/worlds/town/r1/other.json", "/worlds/town/r1/world.json/",
            "/worlds/town/../world.json", "/worlds/town/%72%31/world.json", "/worlds/town/a%2Fb/world.json",
            "/worlds/town/r.1/world.json", "/worlds/town//world.json", "/worlds/${"a".repeat(81)}/r1/world.json")
            .forEach { rejected(link("https://host$it")) }
    }

    @Test fun neverNormalizesAlternatePathsIntoValidWorldAddresses() {
        listOf("/worlds/./town/r1/world.json", "/worlds/old/../town/r1/world.json",
            "/worlds/town/r1/./world.json", "/worlds/town/r1/x/../world.json",
            "//worlds/town/r1/world.json", "/worlds/town/r1//world.json",
            "/worlds\\town\\r1\\world.json", "/worlds/town/r1\\world.json",
            "/worlds/town/r1/%77orld.json", "/worlds/town/%2e%2e/world.json",
            "/worlds/town/a%5Cb/world.json", "/worlds/town/a%252fb/world.json",
            "/worlds/town/r1/world.json%00", "/worlds/town/r1/world.json;extra",
            "/worlds/town/r1/world.json?", "/worlds/town/r1/world.json#")
            .forEach { rejected(link("https://host$it")) }
    }

    @Test fun acceptsMaximumIdentifiersButRejectsDoubleEncodedManifestAndEncodedAuthority() {
        val id = "a".repeat(80)
        assertEquals(id, WorldLink.parse(link("https://host/worlds/$id/$id/world.json")).worldId)
        rejected(link("https://host/worlds/town/${"r".repeat(81)}/world.json"))
        rejected(link(URLEncoder.encode("https://host/worlds/town/r1/world.json", "UTF-8")))
        rejected(link("https://h%6fst/worlds/town/r1/world.json"))
        rejected(link("https://host/worlds/town/r1/world.json").replace("//world?", "//%77orld?"))
    }

    @Test fun rejectsOtherSchemesHostsDuplicateAndUnknownFields() {
        val valid = link("https://host/worlds/town/r1/world.json")
        listOf(valid.replace("kimchily:", "https:"), valid.replace("//world?", "//world.evil?"),
            valid.replace("//world?", "//user@world?"), valid.replace("//world?", "//world:80?"),
            valid.replace("//world?", "//world/path?"), "$valid#fragment", "$valid&sha256=$hash",
            "$valid&unexpected=value", valid.replace("sha256=", "sha= "), "kimchily://world", "", "x".repeat(4097))
            .forEach { rejected(it) }
    }

    @Test fun rejectsInvalidHashOrBadEncoding() {
        val url = "https://host/worlds/town/r1/world.json"
        listOf("", "abc", "z".repeat(64), "a".repeat(65)).forEach { rejected(link(url, it)) }
        rejected("kimchily://world?manifest=%ZZ&sha256=$hash")
        rejected(link("https://host/worlds/town/r1/world.json\n"))
        rejected(link("https://host/worlds/town/r1/world.json").replace("sha256=", "sha256=%00"))
    }
}
