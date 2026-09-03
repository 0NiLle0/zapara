package ru.bgtu_voenmeh.zapara

import android.content.Context
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.junit4.createAndroidComposeRule
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import androidx.lifecycle.ViewModelProvider
import androidx.room.Room
import androidx.test.core.app.ApplicationProvider
import androidx.test.ext.junit.runners.AndroidJUnit4
import org.junit.BeforeClass
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import ru.bgtu_voenmeh.zapara.data.Parity
import ru.bgtu_voenmeh.zapara.data.db.FriendEntity
import ru.bgtu_voenmeh.zapara.data.db.GroupEntity
import ru.bgtu_voenmeh.zapara.data.db.HomeworkEntity
import ru.bgtu_voenmeh.zapara.data.db.LessonEntity
import ru.bgtu_voenmeh.zapara.data.db.MIGRATION_1_2
import ru.bgtu_voenmeh.zapara.data.db.OverrideEntity
import ru.bgtu_voenmeh.zapara.data.db.SettingsEntity
import ru.bgtu_voenmeh.zapara.data.db.ZaparaDatabase
import ru.bgtu_voenmeh.zapara.ui.ScheduleViewModel

// A3 end-to-end: seed DB -> launch -> override/homework/traffic/friends-dialog visible.
@RunWith(AndroidJUnit4::class)
class ScheduleFlowTest {

    @get:Rule
    val compose = createAndroidComposeRule<MainActivity>()

    companion object {
        // Static flag FIRST (before any activity launch): init can never
        // overwrite the seed with a live network fetch.
        @JvmStatic
        @BeforeClass
        fun disableNetwork() {
            ru.bgtu_voenmeh.zapara.data.ScheduleRepository.networkEnabled = false
        }

        private fun seed(db: ZaparaDatabase) {
            db.clearAllTables()
            db.groupDao().upsert(GroupEntity("3313", "А863С"))
            db.groupDao().upsert(GroupEntity("3031", "09С31"))
            // Lessons for every weekday (parity 0 = both) so Today/Tomorrow always show rows.
            val mine = mutableListOf<LessonEntity>()
            val friend = mutableListOf<LessonEntity>()
            for (dow in 1..6) {
                mine.add(
                    LessonEntity(
                        groupId = "3313", dayOfWeek = dow, parity = 0, idx = 1,
                        timeStart = "09:00", timeEnd = "10:35",
                        subjectRaw = "лек ВЫСШ. МАТЕМАТ",
                        subjectNormalized = Parity.normalizeSubject("лек ВЫСШ. МАТЕМАТ"),
                        teacherRaw = "Барт Е.Л.", roomRaw = "493", buildingRaw = "ГК",
                        typeRaw = "лек", classroomRaw = "493;"
                    )
                )
                mine.add(
                    LessonEntity(
                        groupId = "3313", dayOfWeek = dow, parity = 0, idx = 2,
                        timeStart = "10:50", timeEnd = "12:25",
                        subjectRaw = "лек ИСТОРИЯ",
                        subjectNormalized = Parity.normalizeSubject("лек ИСТОРИЯ"),
                        teacherRaw = "Попова В.В.", roomRaw = "526", buildingRaw = "УЛК",
                        typeRaw = "лек", classroomRaw = "526*;"
                    )
                )
                friend.add(
                    LessonEntity(
                        groupId = "3031", dayOfWeek = dow, parity = 0, idx = 1,
                        timeStart = "09:00", timeEnd = "10:35",
                        subjectRaw = "лек ФИЗИКА",
                        subjectNormalized = Parity.normalizeSubject("лек ФИЗИКА"),
                        teacherRaw = "Петров А.Б.", roomRaw = "493", buildingRaw = "ГК",
                        typeRaw = "лек", classroomRaw = "493;"
                    )
                )
            }
            db.lessonDao().insertAll(mine)
            db.lessonDao().insertAll(friend)
            db.settingsDao().save(SettingsEntity(myGroupId = "3313"))
            db.friendDao().insert(
                FriendEntity(groupName = "09С31", colorHex = "#FF6CA5E0", enabled = true, memberNames = "Иван")
            )
            db.overrideDao().insert(
                OverrideEntity(
                    subjectRawNormalized = Parity.normalizeSubject("лек ВЫСШ. МАТЕМАТ"),
                    scope = "global", displayName = "МАТАН", note = null, createdAt = "2026-09-03"
                )
            )
            db.homeworkDao().insert(
                HomeworkEntity(
                    subjectRawNormalized = Parity.normalizeSubject("лек ИСТОРИЯ"),
                    text = "прочитать §5", createdAt = "2026-09-01", targetNthOccurrence = 1,
                    dueDateComputed = "2026-09-04", status = "burning"
                )
            )
        }
    }

    @org.junit.Before
    fun seedBeforeEach() {
        // Reseed before EVERY test (reinstalls/pollution-proof). Runs on main
        // thread, so DB work goes to a background thread.
        var failure: Throwable? = null
        val t = Thread {
            try {
                val ctx: Context = ApplicationProvider.getApplicationContext()
                val db = Room.databaseBuilder(ctx, ZaparaDatabase::class.java, "zapara.db")
                    .addMigrations(MIGRATION_1_2)
                    .build()
                try {
                    seed(db)
                    android.util.Log.i(
                        "ZaparaTest",
                        "seeded groups=${db.groupDao().getAll().size} " +
                            "lessons3313=${db.lessonDao().getAllForGroup("3313").size}"
                    )
                } finally {
                    db.close()
                }
            } catch (e: Throwable) {
                failure = e
            }
        }
        val start = System.currentTimeMillis()
        t.start()
        t.join(60_000)
        android.util.Log.i(
            "ZaparaTest",
            "seed join done alive=${t.isAlive} waited=${System.currentTimeMillis() - start}ms"
        )
        failure?.let { throw RuntimeException("seed failed", it) }
    }

    private fun exists(text: String, substring: Boolean = false): Boolean {
        return try {
            compose.onNodeWithText(text, substring).assertExists()
            true
        } catch (_: AssertionError) {
            false
        }
    }

    private fun logState(tag: String) {
        android.util.Log.i(
            "ZaparaTest",
            "$tag header=${exists("ЗАПАРА")} group=${exists("А863С", true)} " +
                "matan=${exists("МАТАН")} loading=${exists("Загрузка...")} " +
                "error=${exists("Ошибка:", true)} notfound=${exists("Нет занятий")} " +
                "tabWeek=${exists("Неделя")} tabSummary=${exists("Сводка")} " +
                "monday=${exists("ПОНЕДЕЛЬНИК")} oddBadge=${exists("НЕЧЕТНАЯ")} " +
                "rawSubj=${exists("ВЫСШ. МАТЕМАТ", true)} hintGrp=${exists("Группа", true)}"
        )
        try {
            val nodes = compose.onAllNodes(
                androidx.compose.ui.test.SemanticsMatcher.keyIsDefined(
                    androidx.compose.ui.semantics.SemanticsProperties.Text
                )
            ).fetchSemanticsNodes()
            val texts = nodes.mapNotNull { node ->
                try {
                    node.config[androidx.compose.ui.semantics.SemanticsProperties.Text]
                        .joinToString("|") { t: androidx.compose.ui.text.AnnotatedString -> t.text }
                } catch (_: Exception) {
                    null
                }
            }
            android.util.Log.i("ZaparaTest", "$tag tree n=${texts.size} :: " + texts.take(70).joinToString(" ## "))
        } catch (e: Throwable) {
            android.util.Log.i("ZaparaTest", "$tag tree dump failed: $e")
        }
    }

    private fun refreshUi() {
        val vm = ViewModelProvider(compose.activity)[ScheduleViewModel::class.java]
        compose.activity.runOnUiThread { vm.reload() }
    }

    private fun logVm(tag: String) {
        try {
            val vm = ViewModelProvider(compose.activity)[ScheduleViewModel::class.java]
            val s = vm.state
            var mainRan = false
            compose.activity.runOnUiThread { mainRan = true }
            Thread.sleep(2000)
            android.util.Log.i(
                "ZaparaTest",
                "$tag vm tab=${s.tab} loading=${s.loading} error=${s.error} " +
                    "groups=${s.groups.size} gid=${s.groupId} lessons=${s.lessons.size} " +
                    "mainAlive=$mainRan"
            )
        } catch (e: Throwable) {
            android.util.Log.i("ZaparaTest", "$tag vm read failed: $e")
        }
    }

    private fun screenshot(name: String) {
        try {
            val device = androidx.test.uiautomator.UiDevice.getInstance(
                androidx.test.platform.app.InstrumentationRegistry.getInstrumentation()
            )
            device.takeScreenshot(java.io.File("/data/local/tmp", name))
        } catch (_: Exception) {
        }
    }

    private fun device(): androidx.test.uiautomator.UiDevice =
        androidx.test.uiautomator.UiDevice.getInstance(
            androidx.test.platform.app.InstrumentationRegistry.getInstrumentation()
        )

    /** Live in-process semantics tree (reliable here; UiDevice snapshot goes stale). */
    private fun treeTexts(): List<String> {
        return try {
            compose.onAllNodes(
                androidx.compose.ui.test.SemanticsMatcher.keyIsDefined(
                    androidx.compose.ui.semantics.SemanticsProperties.Text
                )
            ).fetchSemanticsNodes().mapNotNull { node ->
                try {
                    node.config[androidx.compose.ui.semantics.SemanticsProperties.Text]
                        .joinToString("|") { t: androidx.compose.ui.text.AnnotatedString -> t.text }
                } catch (_: Exception) {
                    null
                }
            }
        } catch (_: Exception) {
            emptyList()
        }
    }

    private fun waitTreeContains(text: String, timeoutMs: Long = 15_000): Boolean {
        val end = System.currentTimeMillis() + timeoutMs
        do {
            if (treeTexts().any { it.contains(text) }) return true
            Thread.sleep(500)
        } while (System.currentTimeMillis() < end)
        return treeTexts().any { it.contains(text) }
    }

    @Test
    fun renameHomeworkTrafficVisible() {
        refreshUi()
        // Override applied instead of raw subject (row shows "[лек] МАТАН").
        assert(waitTreeContains("МАТАН")) { "no МАТАН row" }
        // Homework block under row.
        assert(waitTreeContains("прочитать")) { "no homework row" }
        // Traffic light label with member names.
        assert(waitTreeContains("09С31")) { "no traffic label" }
        logState("rename")
    }

    @Test
    fun friendsDialogOpens() {
        refreshUi()
        assert(waitTreeContains("Друзья")) { "no friends button" }
        compose.onNodeWithText("Друзья").performClick()
        assert(waitTreeContains("Друзья (до 5)")) { "no friends dialog" }
        assert(waitTreeContains("Всегда все светофоры")) { "no always-show toggle" }
    }
}
