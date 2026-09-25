package com.kimchily.app

import java.net.URI
import java.net.URLDecoder
import java.util.Locale

data class WorldTarget(
    val worldId: String,
    val revisionId: String,
    val manifestUrl: String = "",
    val manifestSha256: String = ""
)

/** Shared by QR, pasted links and ACTION_VIEW. Does not depend on Android URI parsing. */
object WorldLink {
    private val manifestPath = Regex("^/worlds/([A-Za-z0-9_-]{1,80})/([A-Za-z0-9_-]{1,80})/world\\.json$")
    private val hash = Regex("^[A-Fa-f0-9]{64}$")

    fun parse(value: String, allowHttp: Boolean = false): WorldTarget {
        val link = value.trim()
        require(link.length in 1..4096 && link.none { it <= ' ' || it == '\u007f' }) {
            "월드 링크가 비어 있거나 너무 길어요."
        }
        val uri = parseUri(link)
        require(uri.scheme.equals("kimchily", ignoreCase = true) && uri.rawAuthority == "world" &&
            uri.rawPath.isNullOrEmpty() && uri.rawFragment == null) { "Kimchily 월드 QR 또는 링크를 사용해 주세요." }
        val fields = linkedMapOf<String, String>()
        for (part in (uri.rawQuery ?: "").split('&')) {
            val pair = part.split('=', limit = 2)
            require(pair.size == 2 && pair[0] in setOf("manifest", "sha256") && !fields.containsKey(pair[0])) {
                "월드 링크의 주소 또는 검증 정보가 올바르지 않아요."
            }
            fields[pair[0]] = try { URLDecoder.decode(pair[1], "UTF-8") }
                catch (_: IllegalArgumentException) { throw IllegalArgumentException("월드 링크의 인코딩이 올바르지 않아요.") }
        }
        require(fields.keys == setOf("manifest", "sha256")) { "월드 링크에 주소와 검증 정보가 필요해요." }
        val manifest = fields.getValue("manifest")
        val checksum = fields.getValue("sha256")
        require(hash.matches(checksum)) { "월드 링크의 검증 정보가 올바르지 않아요." }
        require(manifest.length in 1..2048 && manifest.none { it <= ' ' || it == '\u007f' }) { "월드 주소가 올바르지 않아요." }
        val url = parseUri(manifest)
        val scheme = url.scheme?.lowercase(Locale.ROOT)
        require(scheme == "https" || (allowHttp && scheme == "http")) {
            "HTTPS 월드 주소가 필요해요. HTTP는 개발용 앱에서만 사용할 수 있어요."
        }
        require(!url.host.isNullOrBlank() && url.rawUserInfo == null && url.rawFragment == null &&
            url.rawQuery == null && (url.port == -1 || url.port in 1..65535) &&
            !url.rawAuthority.endsWith(':')) { "월드 주소에 허용되지 않는 정보가 있어요." }
        val path = manifestPath.matchEntire(url.rawPath ?: "")
            ?: throw IllegalArgumentException("월드 주소의 경로가 올바르지 않아요.")
        return WorldTarget(path.groupValues[1], path.groupValues[2], manifest, checksum.lowercase(Locale.ROOT))
    }

    private fun parseUri(value: String): URI = try { URI(value) }
        catch (_: Exception) { throw IllegalArgumentException("월드 링크 형식을 확인해 주세요.") }
}
