package ru.bgtu_voenmeh.zapara.data

import android.content.Context
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext

// Lecturer schedule from bundled assets (offline-first). Network refresh lands in A5.
class LecturerStore(private val context: Context) {

    @Volatile
    private var data: ParsedLecturerSchedule? = null

    suspend fun load(): ParsedLecturerSchedule = withContext(Dispatchers.IO) {
        data ?: run {
            val xml = context.assets.open("TimetableLecturer50.xml")
                .bufferedReader(Charsets.UTF_8).readText()
            LecturerParser.parse(xml).also { data = it }
        }
    }

    fun isLoaded(): Boolean = data != null

    fun lecturers(): List<LecturerInfo> = data?.lecturers.orEmpty()

    fun lessonsFor(lecturerId: String): List<LecturerLesson> =
        data?.lessons?.filter { it.lecturerId == lecturerId }
            ?.sortedWith(compareBy({ it.dayOfWeek }, { it.parity }, { it.timeStart }))
            .orEmpty()

    /** Ids + short names of teachers leading [groupId] (matches Windows GetMyTeacherIds). */
    fun myTeacherIds(groupLessons: List<Lesson>): Set<String> {
        val shorts = groupLessons
            .flatMap { it.teacherRaw.split(";") }
            .map { it.trim() }.filter { it.isNotEmpty() && it != "—" }
        val ids = mutableSetOf<String>()
        val lecturers = lecturers()
        for (short in shorts) {
            val lastName = short.split(" ").firstOrNull()?.trimEnd('.').orEmpty()
            for (lect in lecturers) {
                if (lastName.isNotEmpty() && lect.name.contains(lastName, ignoreCase = true)) {
                    ids.add(lect.id)
                    ids.add(lect.name)
                    break
                }
            }
            ids.add(short)
        }
        return ids
    }

    fun search(query: String, onlyMy: Boolean, myIds: Set<String>): List<LecturerInfo> {
        var list = lecturers().asSequence()
        if (onlyMy) {
            list = list.filter { l ->
                l.id in myIds || l.name in myIds ||
                    myIds.any { id ->
                        val last = id.split(" ").firstOrNull()?.trimEnd('.').orEmpty()
                        last.isNotEmpty() && l.name.contains(last, ignoreCase = true)
                    }
            }
        }
        val q = query.trim().lowercase()
        if (q.isNotEmpty()) {
            list = list.filter { l ->
                l.name.lowercase().contains(q) || l.id.contains(q) ||
                    l.kafedra.lowercase().contains(q) ||
                    lessonsFor(l.id).any { it.disciplineRaw.lowercase().contains(q) }
            }
        }
        return list.sortedBy { it.name }.take(100).toList()
    }
}
