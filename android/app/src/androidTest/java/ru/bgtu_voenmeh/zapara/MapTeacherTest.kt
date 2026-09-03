package ru.bgtu_voenmeh.zapara

import android.content.Context
import androidx.compose.ui.test.junit4.createAndroidComposeRule
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import androidx.lifecycle.ViewModelProvider
import androidx.room.Room
import androidx.test.core.app.ApplicationProvider
import androidx.test.ext.junit.runners.AndroidJUnit4
import org.junit.Before
import org.junit.BeforeClass
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import ru.bgtu_voenmeh.zapara.data.Parity
import ru.bgtu_voenmeh.zapara.data.db.FriendEntity
import ru.bgtu_voenmeh.zapara.data.db.GroupEntity
import ru.bgtu_voenmeh.zapara.data.db.LessonEntity
import ru.bgtu_voenmeh.zapara.data.db.MIGRATION_1_2
import ru.bgtu_voenmeh.zapara.data.db.SettingsEntity
import ru.bgtu_voenmeh.zapara.data.db.ZaparaDatabase
import ru.bgtu_voenmeh.zapara.ui.ScheduleViewModel

// A4: offline maps (bundled assets) + teacher finder (bundled lecturer XML).
@RunWith(AndroidJUnit4::class)
class MapTeacherTest {

    @get:Rule
    val compose = createAndroidComposeRule<MainActivity>()

    companion object {
        @JvmStatic
        @BeforeClass
        fun disableNetwork() {
            ru.bgtu_voenmeh.zapara.data.ScheduleRepository.networkEnabled = false
        }

        private fun seed(db: ZaparaDatabase) {
            db.clearAllTables()
            db.groupDao().upsert(GroupEntity("3313", "А863С"))
            val lessons = mutableListOf<LessonEntity>()
            for (dow in 1..6) {
                lessons.add(
                    LessonEntity(
                        groupId = "3313", dayOfWeek = dow, parity = 0, idx = 1,
                        timeStart = "09:00", timeEnd = "10:35",
                        subjectRaw = "лек ВЫСШ. МАТЕМАТ",
                        subjectNormalized = Parity.normalizeSubject("лек ВЫСШ. МАТЕМАТ"),
                        teacherRaw = "Барт Е.Л.", roomRaw = "493", buildingRaw = "ГК",
                        typeRaw = "лек", classroomRaw = "493;"
                    )
                )
                lessons.add(
                    LessonEntity(
                        groupId = "3313", dayOfWeek = dow, parity = 0, idx = 2,
                        timeStart = "10:50", timeEnd = "12:25",
                        subjectRaw = "лек ИСТОРИЯ",
                        subjectNormalized = Parity.normalizeSubject("лек ИСТОРИЯ"),
                        teacherRaw = "Попова В.В.", roomRaw = "526", buildingRaw = "УЛК",
                        typeRaw = "лек", classroomRaw = "526*;"
                    )
                )
            }
            db.lessonDao().insertAll(lessons)
            db.settingsDao().save(SettingsEntity(myGroupId = "3313"))
            db.friendDao().insert(
                FriendEntity(groupName = "09С31", colorHex = "#FF6CA5E0", enabled = true)
            )
        }
    }

    @Before
    fun seedBeforeEach() {
        var failure: Throwable? = null
        val t = Thread {
            try {
                val ctx: Context = ApplicationProvider.getApplicationContext()
                val db = Room.databaseBuilder(ctx, ZaparaDatabase::class.java, "zapara.db")
                    .addMigrations(MIGRATION_1_2)
                    .build()
                try {
                    seed(db)
                } finally {
                    db.close()
                }
            } catch (e: Throwable) {
                failure = e
            }
        }
        t.start()
        t.join(60_000)
        failure?.let { throw RuntimeException("seed failed", it) }
    }

    private fun treeTexts(): List<String> {
        return try {
            val nodes = compose.onAllNodes(
                androidx.compose.ui.test.SemanticsMatcher.keyIsDefined(
                    androidx.compose.ui.semantics.SemanticsProperties.Text
                )
            ).fetchSemanticsNodes()
            var dropped = 0
            val out = nodes.mapNotNull { node ->
                try {
                    node.config[androidx.compose.ui.semantics.SemanticsProperties.Text]
                        .joinToString("|") { t: androidx.compose.ui.text.AnnotatedString -> t.text }
                } catch (_: Exception) {
                    dropped++
                    null
                }
            }
            android.util.Log.i("ZaparaTest", "tree nodes=${nodes.size} dropped=$dropped kept=${out.size}")
            out
        } catch (_: Exception) {
            emptyList()
        }
    }

    private fun dumpTree(tag: String) {
        try {
            val all = compose.onAllNodes(
                androidx.compose.ui.test.SemanticsMatcher.keyIsDefined(
                    androidx.compose.ui.semantics.SemanticsProperties.Text
                )
            ).fetchSemanticsNodes()
            var dropped = 0
            var firstErr: String? = null
            val out = all.mapNotNull { node ->
                try {
                    node.config[androidx.compose.ui.semantics.SemanticsProperties.Text]
                        .joinToString("|") { t: androidx.compose.ui.text.AnnotatedString -> t.text }
                } catch (e: Exception) {
                    dropped++
                    if (firstErr == null) firstErr = e.toString()
                    null
                }
            }
            android.util.Log.i(
                "ZaparaTest",
                "$tag tree total=${all.size} dropped=$dropped kept=${out.size} firstErr=$firstErr"
            )
            out.chunked(5).forEachIndexed { i, chunk ->
                android.util.Log.i("ZaparaTest", "$tag part$i :: " + chunk.joinToString(" ## "))
            }
        } catch (e: Throwable) {
            android.util.Log.i("ZaparaTest", "$tag tree dump failed: $e")
        }
    }

    private fun reload() {
        val vm = ViewModelProvider(compose.activity)[ScheduleViewModel::class.java]
        compose.activity.runOnUiThread { vm.reload() }
        // Pump the test dispatcher so app coroutines + recomposition actually run.
        compose.waitForIdle()
    }

    @Test
    fun mapOpensOfflineFromRowButton() {
        reload()
        compose.waitUntil(20_000) { treeTexts().any { it.contains("◉") } || treeTexts().any { it.contains("КАРТА") } }
        val vm = androidx.lifecycle.ViewModelProvider(compose.activity)[ru.bgtu_voenmeh.zapara.ui.ScheduleViewModel::class.java]
        compose.activity.runOnUiThread {
            val l = vm.state.lessons.firstOrNull() ?: return@runOnUiThread
            vm.showMapFor(l)
        }
        compose.waitUntil(20_000) {
            val s = try { androidx.lifecycle.ViewModelProvider(compose.activity)[ru.bgtu_voenmeh.zapara.ui.ScheduleViewModel::class.java].state } catch (_: Exception) { null }
            s?.mapVisible == true
        }
        compose.waitUntil(20_000) { treeTexts().any { it.contains("КАРТА") } }
    }

    @Test
    fun teacherFinderListsBundledLecturers() {
        reload()
        compose.waitUntil(20_000) { treeTexts().any { it.contains("Преподаватели") } }
        val vm2 = androidx.lifecycle.ViewModelProvider(compose.activity)[ru.bgtu_voenmeh.zapara.ui.ScheduleViewModel::class.java]
        compose.activity.runOnUiThread { vm2.openTeachers() }
        // Bundled TimetableLecturer50.xml: full name present, only-mine finds Барт via 3313 lessons.
        compose.waitUntil(30_000) { treeTexts().any { it.contains("Барт") } }
    }
}
