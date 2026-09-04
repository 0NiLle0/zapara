package ru.bgtu_voenmeh.zapara.data

import org.json.JSONArray
import java.net.HttpURLConnection
import java.net.URL

object AutoUpdate {
    const val CURRENT_TAG = "android-v1.0"
    private const val OWNER = "0NiLle0"
    private const val REPO = "zapara"

    data class UpdateInfo(val tag: String, val htmlUrl: String, val apkUrl: String?, val publishedAt: String)

    fun getLatest(channel: String = "android"): UpdateInfo? {
        val pfx = if (channel == "windows") "windows-" else "android-"
        val url = URL("https://api.github.com/repos/$OWNER/$REPO/releases?per_page=20")
        val conn = (url.openConnection() as HttpURLConnection).apply {
            requestMethod = "GET"
            setRequestProperty("User-Agent", "Zapara-AutoUpdate/1.0")
            setRequestProperty("Accept", "application/vnd.github+json")
            connectTimeout = 8000; readTimeout = 8000
        }
        if (conn.responseCode !in 200..299) return null
        val json = conn.inputStream.bufferedReader().readText()
        val arr = JSONArray(json)
        for (i in 0 until arr.length()) {
            val o = arr.getJSONObject(i)
            val tag = o.getString("tag_name")
            if (!tag.startsWith(pfx, ignoreCase = true)) continue
            val html = o.getString("html_url")
            val published = o.optString("published_at", "")
            var apk: String? = null
            val assets = o.optJSONArray("assets")
            if (assets != null) {
                for (j in 0 until assets.length()) {
                    val a = assets.getJSONObject(j)
                    val name = a.getString("name")
                    if (name.endsWith(".apk", ignoreCase = true)) {
                        apk = a.getString("browser_download_url")
                        if (name.contains("ZAPARA", ignoreCase = true)) break
                    }
                }
            }
            return UpdateInfo(tag, html, apk, published)
        }
        return null
    }

    fun isNewer(latest: String, current: String = CURRENT_TAG): Boolean {
        fun ver(t: String): String = when {
            "-v" in t -> t.substringAfter("-v")
            "-" in t -> t.substringAfter("-")
            else -> t
        }.trimStart('v','V')
        return try {
            val a = ver(latest).split(".").map { it.toIntOrNull() ?: 0 }
            val b = ver(current).split(".").map { it.toIntOrNull() ?: 0 }
            for (k in 0 until maxOf(a.size, b.size)) {
                val av = a.getOrElse(k){0}; val bv = b.getOrElse(k){0}
                if (av != bv) return av > bv
            }
            latest != current
        } catch (_: Exception) { latest != current }
    }
}
