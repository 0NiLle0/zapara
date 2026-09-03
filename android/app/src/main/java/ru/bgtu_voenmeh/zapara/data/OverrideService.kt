package ru.bgtu_voenmeh.zapara.data

import ru.bgtu_voenmeh.zapara.data.db.OverrideDao
import ru.bgtu_voenmeh.zapara.data.db.OverrideEntity
import java.time.LocalDate

// Port of Vograph.Core OverrideService. Global scope wins over weekday scope.
class OverrideService(private val dao: OverrideDao) {

    fun addOrUpdate(subjectRaw: String, scope: String, displayName: String, note: String?) {
        val norm = Parity.normalizeSubject(subjectRaw)
        dao.deleteByKey(norm, scope)
        dao.insert(
            OverrideEntity(
                subjectRawNormalized = norm, scope = scope,
                displayName = displayName, note = note,
                createdAt = LocalDate.now().toString()
            )
        )
    }

    fun displayName(subjectRaw: String, dayOfWeek: Int): String {
        val found = displayNameByNorm(Parity.normalizeSubject(subjectRaw), dayOfWeek)
        return found.ifEmpty { subjectRaw }
    }

    fun displayNameByNorm(norm: String, dayOfWeek: Int): String {
        val all = dao.getAll().filter { it.subjectRawNormalized == norm }
        all.firstOrNull { it.scope == "global" }?.let { return it.displayName }
        all.firstOrNull { it.scope == "weekday:$dayOfWeek" }?.let { return it.displayName }
        return ""
    }

    fun note(subjectRaw: String, dayOfWeek: Int): String {
        return noteByNorm(Parity.normalizeSubject(subjectRaw), dayOfWeek)
    }

    fun noteByNorm(norm: String, dayOfWeek: Int): String {
        val all = dao.getAll().filter { it.subjectRawNormalized == norm }
        val ov = all.firstOrNull { it.scope == "global" }
            ?: all.firstOrNull { it.scope == "weekday:$dayOfWeek" }
        return ov?.note.orEmpty()
    }

    fun remove(id: Long) {
        dao.deleteById(id)
    }

    fun all(): List<OverrideEntity> = dao.getAll()
}
