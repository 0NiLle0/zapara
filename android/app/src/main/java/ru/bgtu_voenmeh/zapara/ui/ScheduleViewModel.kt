package ru.bgtu_voenmeh.zapara.ui

import android.app.Application
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.ViewModel
import androidx.lifecycle.ViewModelProvider
import androidx.lifecycle.viewModelScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import ru.bgtu_voenmeh.zapara.data.GroupInfo
import ru.bgtu_voenmeh.zapara.data.Friend
import ru.bgtu_voenmeh.zapara.data.Homework
import ru.bgtu_voenmeh.zapara.data.HomeworkService
import ru.bgtu_voenmeh.zapara.data.IntersectionService
import ru.bgtu_voenmeh.zapara.data.Lesson
import ru.bgtu_voenmeh.zapara.data.OverrideService
import ru.bgtu_voenmeh.zapara.data.Parity
import ru.bgtu_voenmeh.zapara.data.SchedCtx
import ru.bgtu_voenmeh.zapara.data.Schedule
import ru.bgtu_voenmeh.zapara.data.ScheduleRepository
import java.time.DayOfWeek
import java.time.LocalDate
import java.time.format.TextStyle
import java.util.Locale

enum class Tab { Yesterday, Today, Tomorrow, Week, Summary }

data class SummarySection(
    val title: String,
    val total: Int,
    val byDay: List<Pair<String, Int>>,
    val byType: List<Pair<String, Int>>,
    val bySubject: List<Pair<String, Int>>,
    val byTeacher: List<Pair<String, Int>>,
    val roomsLine: String
)

data class ScheduleUiState(
    val tab: Tab = Tab.Tomorrow,
    val weekParity: Int = 1, // 1 odd, 2 even for week view
    val groups: List<GroupInfo> = emptyList(),
    val groupId: String = "",
    val groupName: String = "—",
    val dateText: String = "—",
    val parityText: String = "—",
    val weekText: String = "",
    val     lessons: List<Lesson> = emptyList(),
    val allLessons: List<Lesson> = emptyList(),
    /** subjectNormalized -> "dd.MM eee" for current day rows (computed off-main-thread) */
    val nextMap: Map<String, String> = emptyMap(),
    /** traffic dots aligned with [lessons] order */
    val traffic: List<List<TrafficDot>> = emptyList(),
    /** subjectNormalized -> visible homework (far hidden) */
    val hwMap: Map<String, List<Homework>> = emptyMap(),
    /** "norm|dow" -> display name (precomputed off-main-thread) */
    val displayMap: Map<String, String> = emptyMap(),
    /** "norm|dow" -> note */
    val noteMap: Map<String, String> = emptyMap(),
    val friends: List<Friend> = emptyList(),
    val alwaysShow: Boolean = false,
    val invert: Boolean = false,
    val dialog: UiDialog = UiDialog.None,
    val summary: List<SummarySection> = emptyList(),
    val loading: Boolean = true,
    val error: String? = null
)

private val RU = Locale("ru")

class ScheduleViewModel(app: Application) : AndroidViewModel(app) {
    private val repo = ScheduleRepository.get(app)
    private val overrides = OverrideService(repo.db.overrideDao())
    private val homework = HomeworkService(
        repo.db.homeworkDao(),
        lessonsFor = { gid, dow, parity ->
            repo.allForGroup(gid).filter { it.dayOfWeek == dow && (it.parity == parity || it.parity == 0) }
        },
        ctx = {
            val st = repo.settings()
            SchedCtx(st.myGroupId.orEmpty(), st.periodStart, st.weekCount, st.parityInvert)
        }
    )
    var state by mutableStateOf(ScheduleUiState())
        private set

    init {
        viewModelScope.launch {
            try {
                withContext(Dispatchers.IO) { repo.ensureData() }
                val groups = withContext(Dispatchers.IO) { repo.groups() }
                val s = withContext(Dispatchers.IO) { repo.settings() }
                val gid = s.myGroupId?.takeIf { id -> groups.any { it.id == id } }
                    ?: groups.firstOrNull { it.id == "3313" }?.id // test group default
                    ?: groups.firstOrNull()?.id.orEmpty()
                // Smart start: today if lessons remain, else tomorrow.
                val startTab = withContext(Dispatchers.IO) { smartStartTab(gid) }
                render(startTab, gid, 1, groups, gid)
            } catch (e: Exception) {
                state = state.copy(loading = false, error = e.message ?: "load error")
            }
        }
    }

    fun selectTab(tab: Tab) {
        if (state.loading) return
        viewModelScope.launch { render(tab, state.groupId, state.weekParity, state.groups, state.groupId) }
    }

    fun selectWeekParity(parity: Int) {
        viewModelScope.launch { render(Tab.Week, state.groupId, parity, state.groups, state.groupId) }
    }

    fun selectGroup(groupId: String) {
        viewModelScope.launch {
            val s = withContext(Dispatchers.IO) { repo.settings() }
            withContext(Dispatchers.IO) { repo.saveSettings(s.copy(myGroupId = groupId)) }
            render(state.tab, groupId, state.weekParity, state.groups, groupId)
        }
    }

    fun refresh() {
        viewModelScope.launch {
            state = state.copy(loading = true, error = null)
            try {
                withContext(Dispatchers.IO) { repo.refresh() }
                val groups = withContext(Dispatchers.IO) { repo.groups() }
                render(state.tab, state.groupId, state.weekParity, groups, state.groupId)
            } catch (e: Exception) {
                state = state.copy(loading = false, error = e.message ?: "refresh error")
            }
        }
    }

    /** Re-render from current DB without network (used by tests). */
    fun reload() {        viewModelScope.launch {
            try {
                val groups = withContext(Dispatchers.IO) { repo.groups() }
                val s = withContext(Dispatchers.IO) { repo.settings() }
                val gid = state.groupId.ifEmpty {
                    s.myGroupId?.takeIf { id -> groups.any { it.id == id } }
                        ?: groups.firstOrNull { it.id == "3313" }?.id
                        ?: groups.firstOrNull()?.id.orEmpty()
                }
                render(state.tab, gid, state.weekParity, groups, gid)
            } catch (e: Exception) {
                state = state.copy(loading = false, error = e.message ?: "reload error")
            }
        }
    }

    private fun smartStartTab(groupId: String): Tab {
        return try {
            val today = LocalDate.now()
            val lessons = repo.lessonsFor(groupId, today)
            val last = lessons.maxByOrNull { it.timeEnd }
            val end = runCatching { java.time.LocalTime.parse(last?.timeEnd) }.getOrNull()
            if (last != null && end != null &&
                java.time.LocalTime.now().isBefore(end.plusMinutes(15))
            ) Tab.Today else Tab.Tomorrow
        } catch (_: Exception) {
            Tab.Tomorrow
        }
    }

    private suspend fun render(tab: Tab, groupId: String, weekParity: Int, groups: List<GroupInfo>, gid: String) {
        val s = withContext(Dispatchers.IO) { repo.settings() }
        val id = gid.ifEmpty { groups.firstOrNull()?.id.orEmpty() }
        val name = groups.firstOrNull { it.id == id }?.name ?: "—"
        val date = when (tab) {
            Tab.Yesterday -> LocalDate.now().minusDays(1)
            Tab.Today -> LocalDate.now()
            Tab.Tomorrow -> LocalDate.now().plusDays(1)
            else -> LocalDate.now()
        }
        val odd = Parity.isOddWeek(date, s.periodStart, s.weekCount, s.parityInvert)
        val dateText = "%02d.%02d.%d · %s".format(
            date.dayOfMonth, date.monthValue, date.year,
            date.dayOfWeek.getDisplayName(TextStyle.FULL, RU).replaceFirstChar { it.uppercase() }
        )
        val all = withContext(Dispatchers.IO) { repo.allForGroup(id) }
        val dayLessons = if (tab == Tab.Week || tab == Tab.Summary) emptyList()
        else Schedule.lessonsForDate(all, id, date, s.periodStart, s.weekCount, s.parityInvert)
        val summary = if (tab == Tab.Summary) withContext(Dispatchers.IO) { buildSummary(all) } else emptyList()
        val (traffic, hwMap) = withContext(Dispatchers.IO) {
            homework.recomputeAll()
            val dots = if (tab == Tab.Week || tab == Tab.Summary) emptyList()
            else dayLessons.map { l -> trafficFor(l, date, s) }
            val hw = dayLessons
                .map { it.subjectNormalized }.distinct()
                .associateWith { norm ->
                    homework.forSubjectByNorm(norm)
                        .filter { it.status != "far" }
                        .sortedBy { hwOrder(it.status) }
                }
            dots to hw
        }
        val (displayMap, noteMap) = withContext(Dispatchers.IO) {
            val keys = dayLessons.map { "${it.subjectNormalized}|${it.dayOfWeek}" }.distinct()
            // Store only non-empty display names so UI falls back to raw subject.
            keys.associateWith { key ->
                val norm = key.substringBefore("|")
                val dow = key.substringAfter("|").toIntOrNull() ?: 0
                overrides.displayNameByNorm(norm, dow)
            }.filterValues { it.isNotEmpty() } to keys.associateWith { key ->
                val norm = key.substringBefore("|")
                val dow = key.substringAfter("|").toIntOrNull() ?: 0
                overrides.noteByNorm(norm, dow)
            }
        }
        val nextMap = if (tab == Tab.Week || tab == Tab.Summary) emptyMap()
        else dayLessons.associate { l ->
            val d = Schedule.nextOccurrenceBySubject(
                all, id, l.subjectNormalized, date, s.periodStart, s.weekCount, s.parityInvert
            )
            l.subjectNormalized to if (d == null) "—" else "%02d.%02d %s".format(
                d.dayOfMonth, d.monthValue,
                d.dayOfWeek.getDisplayName(TextStyle.SHORT, RU)
            )
        }
        state = ScheduleUiState(
            tab = tab, weekParity = weekParity, groups = groups,
            groupId = id, groupName = name,
            dateText = if (tab == Tab.Week || tab == Tab.Summary) "" else dateText,
            parityText = if (odd) "НЕЧЕТНАЯ" else "ЧЕТНАЯ",
            weekText = "неделя ${Parity.weekNumber(date, s.periodStart)}",
            lessons = dayLessons,
            allLessons = all,
            nextMap = nextMap,
            traffic = traffic,
            hwMap = hwMap,
            displayMap = displayMap,
            noteMap = noteMap,
            friends = withContext(Dispatchers.IO) { repoFriends() },
            alwaysShow = s.alwaysShowAllTrafficLights,
            invert = s.parityInvert,
            dialog = state.dialog,
            summary = summary, loading = false, error = null
        )
    }

    private fun repoFriends(): List<Friend> =
        repo.db.friendDao().getAll().map {
            Friend(it.groupName, it.colorHex, it.enabled, it.memberNames)
        }

    private fun trafficFor(
        lesson: Lesson,
        date: LocalDate,
        s: ScheduleRepository.SettingsState
    ): List<TrafficDot> {
        val friends = repoFriends().filter { it.enabled }.take(5)
        if (friends.isEmpty()) return emptyList()
        val groups = repo.groups().associate { it.name to it.id }
        val inters = IntersectionService.intersections(
            my = lesson, date = date, friends = friends,
            strictness = s.intersectionStrictness,
            periodStart = s.periodStart, weekCount = s.weekCount, invert = s.parityInvert,
            lessonsFor = { fid, dow, parity ->
                repo.allForGroup(fid).filter { it.dayOfWeek == dow && (it.parity == parity || it.parity == 0) }
            },
            resolveId = { name -> groups[name] }
        )
        return if (s.alwaysShowAllTrafficLights) {
            val byGroup = inters.associateBy { it.friendGroupName }
            friends.map { f ->
                val hit = byGroup[f.groupName]
                if (hit != null) TrafficDot(f.groupName, f.memberNames, hit.score, hit.teacher, hit.room)
                else TrafficDot(f.groupName, f.memberNames, -1, "", "")
            }
        } else {
            inters.map { hit ->
                val f = friends.firstOrNull { it.groupName == hit.friendGroupName }
                TrafficDot(hit.friendGroupName, f?.memberNames.orEmpty(), hit.score, hit.teacher, hit.room)
            }
        }
    }

    private fun hwOrder(status: String): Int = when (status) {
        "burning_urgent" -> 0
        "burning" -> 1
        "overdue" -> 2
        "approaching" -> 3
        "far" -> 4
        "done" -> 5
        else -> 6
    }

    // ---- Dialog actions (all IO) ----

    fun openDialog(d: UiDialog) {
        state = state.copy(dialog = d)
    }

    fun openRename(lesson: Lesson) {
        viewModelScope.launch {
            val (name, note) = withContext(Dispatchers.IO) {
                overrides.displayName(lesson.subjectRaw, lesson.dayOfWeek) to
                    overrides.note(lesson.subjectRaw, lesson.dayOfWeek)
            }
            val idx = state.lessons.indexOf(lesson).takeIf { it >= 0 } ?: return@launch
            state = state.copy(dialog = UiDialog.Rename(idx, name, note))
        }
    }

    fun closeDialog() {
        state = state.copy(dialog = UiDialog.None)
    }

    fun displayName(lesson: Lesson): String =
        state.displayMap["${lesson.subjectNormalized}|${lesson.dayOfWeek}"] ?: lesson.subjectRaw

    fun displayNote(lesson: Lesson): String =
        state.noteMap["${lesson.subjectNormalized}|${lesson.dayOfWeek}"].orEmpty()

    fun saveRename(lesson: Lesson, displayName: String, note: String, global: Boolean) {
        viewModelScope.launch {
            withContext(Dispatchers.IO) {
                overrides.addOrUpdate(
                    lesson.subjectRaw,
                    if (global) "global" else "weekday:${lesson.dayOfWeek}",
                    displayName.ifBlank { lesson.subjectRaw }, note.ifBlank { null }
                )
            }
            closeDialog()
            render(state.tab, state.groupId, state.weekParity, state.groups, state.groupId)
        }
    }

    fun resetRename(lesson: Lesson) {
        viewModelScope.launch {
            withContext(Dispatchers.IO) {
                val norm = Parity.normalizeSubject(lesson.subjectRaw)
                overrides.all().filter { it.subjectRawNormalized == norm }
                    .forEach { overrides.remove(it.id) }
            }
            closeDialog()
            render(state.tab, state.groupId, state.weekParity, state.groups, state.groupId)
        }
    }

    fun hwDuePreview(lesson: Lesson, n: Int): String {
        return try {
            val st = repo.settings()
            val due = homework.computeDueDate(
                Parity.normalizeSubject(lesson.subjectRaw), LocalDate.now(), n.coerceIn(1, 10)
            )
            if (due == null) "Срок: — (нет занятий)"
            else "Срок: %02d.%02d.%d".format(due.dayOfMonth, due.monthValue, due.year)
        } catch (_: Exception) {
            "Срок: —"
        }
    }

    fun saveHomework(lesson: Lesson, text: String, n: Int) {
        viewModelScope.launch {
            withContext(Dispatchers.IO) {
                homework.addHomework(lesson.subjectRaw, text, n, LocalDate.now())
            }
            closeDialog()
            render(state.tab, state.groupId, state.weekParity, state.groups, state.groupId)
        }
    }

    fun toggleHomework(id: Long, done: Boolean) {
        viewModelScope.launch {
            withContext(Dispatchers.IO) { homework.markDone(id, done) }
            render(state.tab, state.groupId, state.weekParity, state.groups, state.groupId)
        }
    }

    fun deleteHomework(id: Long) {
        viewModelScope.launch {
            withContext(Dispatchers.IO) { homework.delete(id) }
            render(state.tab, state.groupId, state.weekParity, state.groups, state.groupId)
        }
    }

    fun addFriend(groupName: String) {
        viewModelScope.launch {
            withContext(Dispatchers.IO) {
                val existing = repo.db.friendDao().getAll()
                if (existing.size >= 5 || existing.any { it.groupName == groupName }) return@withContext
                val colors = listOf("#FF6CA5E0", "#FF98C379", "#FFE06C75", "#FFC678DD", "#FFF2C55C")
                repo.db.friendDao().insert(
                    ru.bgtu_voenmeh.zapara.data.db.FriendEntity(
                        groupName = groupName,
                        colorHex = colors[existing.size % colors.size],
                        enabled = true
                    )
                )
            }
            render(state.tab, state.groupId, state.weekParity, state.groups, state.groupId)
        }
    }

    fun removeFriend(friend: Friend) {
        viewModelScope.launch {
            withContext(Dispatchers.IO) {
                val e = repo.db.friendDao().getAll().firstOrNull { it.groupName == friend.groupName }
                if (e != null) repo.db.friendDao().delete(e.id)
            }
            render(state.tab, state.groupId, state.weekParity, state.groups, state.groupId)
        }
    }

    fun saveMemberNames(friend: Friend, names: String) {
        viewModelScope.launch {
            withContext(Dispatchers.IO) {
                val e = repo.db.friendDao().getAll().firstOrNull { it.groupName == friend.groupName }
                if (e != null && e.memberNames != names) {
                    repo.db.friendDao().update(e.copy(memberNames = names))
                }
            }
            // light refresh (no full render to keep typing smooth — caller re-renders on focus loss)
            render(state.tab, state.groupId, state.weekParity, state.groups, state.groupId)
        }
    }

    fun toggleAlwaysShow(value: Boolean) {
        viewModelScope.launch {
            withContext(Dispatchers.IO) {
                val st = repo.settings()
                repo.saveSettings(st.copy(alwaysShowAllTrafficLights = value))
            }
            render(state.tab, state.groupId, state.weekParity, state.groups, state.groupId)
        }
    }

    fun toggleInvert(value: Boolean) {
        viewModelScope.launch {
            withContext(Dispatchers.IO) {
                val st = repo.settings()
                repo.saveSettings(st.copy(parityInvert = value))
            }
            render(state.tab, state.groupId, state.weekParity, state.groups, state.groupId)
        }
    }

    private suspend fun buildSummary(all: List<Lesson>): List<SummarySection> {
        // Resolve override display names off-main-thread.
        val displayOf: (Lesson) -> String = { l ->
            overrides.displayNameByNorm(l.subjectNormalized, l.dayOfWeek).ifEmpty { l.subjectRaw }
        }
        fun section(title: String, list: List<Lesson>): SummarySection {
            val byDay = list.groupBy { it.dayOfWeek }.toSortedMap()
                .map { (d, v) -> Parity.dayNumberToTitle(d).take(2) to v.size }
            val byType = list.groupBy { it.typeRaw.ifBlank { "—" } }
                .map { (k, v) -> k to v.size }.sortedByDescending { it.second }
            val bySubject = list.groupBy { l -> displayOf(l).ifBlank { "—" } }
                .map { (k, v) -> k to v.size }.sortedByDescending { it.second }
            val byTeacher = list.filter { it.teacherRaw.isNotBlank() && it.teacherRaw != "—" }
                .flatMap { l -> l.teacherRaw.split(";").map { it.trim() }.filter { it.isNotEmpty() } }
                .groupingBy { it }.eachCount().toList().sortedByDescending { it.second }
            val rooms = list.filter { it.classroomRaw.isNotBlank() }
                .groupingBy { it.classroomRaw.trimEnd(';', ' ') }.eachCount()
                .toList().sortedByDescending { it.second }
                .joinToString(" · ") { "${it.first} ${it.second}" }
            return SummarySection(title, list.size, byDay, byType, bySubject, byTeacher, rooms)
        }
        val odd = all.filter { it.parity == 1 }
        val even = all.filter { it.parity == 2 }
        return listOf(
            section("Нечетная", odd),
            section("Четная", even),
            section("Обе недели", all)
        )
    }

    fun weekLessons(dow: Int, parity: Int): List<Lesson> {
        return state.allLessons
            .filter { it.dayOfWeek == dow && (it.parity == parity || it.parity == 0) }
            .sortedBy { it.timeStart }
    }

    fun nextPairText(lesson: Lesson, from: LocalDate): String {
        // Legacy helper (kept for tests of pure logic in Schedule.*). UI uses state.nextMap.
        return state.nextMap[lesson.subjectNormalized] ?: "—"
    }

    @Suppress("unused")
    fun currentDateForTab(): LocalDate = when (state.tab) {
        Tab.Yesterday -> LocalDate.now().minusDays(1)
        Tab.Today -> LocalDate.now()
        Tab.Tomorrow -> LocalDate.now().plusDays(1)
        else -> LocalDate.now()
    }
}

class ScheduleVmFactory(private val app: Application) : ViewModelProvider.Factory {
    @Suppress("UNCHECKED_CAST")
    override fun <T : ViewModel> create(modelClass: Class<T>): T {
        return ScheduleViewModel(app) as T
    }
}
