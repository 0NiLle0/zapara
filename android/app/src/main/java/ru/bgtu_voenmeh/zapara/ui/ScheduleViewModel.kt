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
import ru.bgtu_voenmeh.zapara.data.Lesson
import ru.bgtu_voenmeh.zapara.data.Parity
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
    val summary: List<SummarySection> = emptyList(),
    val loading: Boolean = true,
    val error: String? = null
)

private val RU = Locale("ru")

class ScheduleViewModel(app: Application) : AndroidViewModel(app) {
    private val repo = ScheduleRepository.get(app)
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
        val summary = if (tab == Tab.Summary) buildSummary(all) else emptyList()
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
            summary = summary, loading = false, error = null
        )
    }

    private fun buildSummary(all: List<Lesson>): List<SummarySection> {
        fun section(title: String, list: List<Lesson>): SummarySection {
            val byDay = list.groupBy { it.dayOfWeek }.toSortedMap()
                .map { (d, v) -> Parity.dayNumberToTitle(d).take(2) to v.size }
            val byType = list.groupBy { it.typeRaw.ifBlank { "—" } }
                .map { (k, v) -> k to v.size }.sortedByDescending { it.second }
            val bySubject = list.groupBy { it.subjectRaw.ifBlank { "—" } }
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
