package ru.bgtu_voenmeh.zapara.data

import android.content.Context
import android.content.Intent
import androidx.core.content.FileProvider
import org.json.JSONArray
import java.io.File
import java.io.FileOutputStream
import java.io.IOException
import java.net.HttpURLConnection
import java.net.URL

object AutoUpdate {
    const val CURRENT_TAG = "android-v1.2.10"
    private const val OWNER = "0NiLle0"
    private const val REPO = "zapara"
    private const val PREFS = "zapara"
    private const val KEY_AUTO = "auto_update"

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

    class DownloadCancelled : IOException("cancelled")

    /** Download a release asset with progress. Throws on HTTP error or [DownloadCancelled]. */
    fun downloadAsset(
        url: String,
        dest: File,
        onProgress: (done: Long, total: Long) -> Unit,
        isCancelled: () -> Boolean
    ) {
        val conn = (URL(url).openConnection() as HttpURLConnection).apply {
            requestMethod = "GET"
            setRequestProperty("User-Agent", "Zapara-AutoUpdate/1.0")
            connectTimeout = 10000
            readTimeout = 30000
            instanceFollowRedirects = true
        }
        if (conn.responseCode !in 200..299) throw IOException("HTTP ${conn.responseCode}")
        val total = conn.contentLengthLong.takeIf { it > 0 } ?: -1L
        dest.parentFile?.mkdirs()
        val tmp = File(dest.parent, dest.name + ".part")
        try {
            conn.inputStream.use { inp ->
                FileOutputStream(tmp).use { out ->
                    val buf = ByteArray(8192)
                    var done = 0L
                    while (true) {
                        if (isCancelled()) throw DownloadCancelled()
                        val n = inp.read(buf)
                        if (n < 0) break
                        out.write(buf, 0, n)
                        done += n
                        onProgress(done, total)
                    }
                }
            }
            if (!tmp.renameTo(dest)) {
                tmp.copyTo(dest, overwrite = true)
                tmp.delete()
            }
        } catch (e: Exception) {
            try { tmp.delete() } catch (_: Exception) {}
            throw e
        } finally {
            conn.disconnect()
        }
    }

    fun apkFileFor(ctx: Context, tag: String): File =
        File(File(ctx.cacheDir, "updates"), "ZAPARA_${tag}_android.apk")

    /** System installer intent for a downloaded APK (still needs one user tap — OS requirement). */
    fun installIntent(ctx: Context, file: File): Intent {
        val uri = FileProvider.getUriForFile(ctx, "${ctx.packageName}.fileprovider", file)
        return Intent(Intent.ACTION_VIEW).apply {
            setDataAndType(uri, "application/vnd.android.package-archive")
            addFlags(Intent.FLAG_ACTIVITY_NEW_TASK or Intent.FLAG_GRANT_READ_URI_PERMISSION)
        }
    }

    fun isAutoUpdateEnabled(ctx: Context): Boolean =
        ctx.getSharedPreferences(PREFS, Context.MODE_PRIVATE).getBoolean(KEY_AUTO, true)

    fun setAutoUpdateEnabled(ctx: Context, value: Boolean) {
        ctx.getSharedPreferences(PREFS, Context.MODE_PRIVATE).edit().putBoolean(KEY_AUTO, value).apply()
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
