package ru.bgtu_voenmeh.zapara.data

import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import ru.bgtu_voenmeh.zapara.data.db.HomeworkDao
import ru.bgtu_voenmeh.zapara.data.db.HomeworkEntity
import ru.bgtu_voenmeh.zapara.data.db.OverrideDao
import ru.bgtu_voenmeh.zapara.data.db.OverrideEntity
import java.time.LocalDate

class FakeOverrideDao : OverrideDao {
    val items = mutableListOf<OverrideEntity>()
    private var seq = 1L
    override fun getAll(): List<OverrideEntity> = items.toList()
    override fun insert(e: OverrideEntity): Long {
        val id = seq++
        items.add(e.copy(id = id))
        return id
    }
    override fun deleteByKey(norm: String, scope: String): Int {
        val n = items.count { it.subjectRawNormalized == norm && it.scope == scope }
        items.removeAll { it.subjectRawNormalized == norm && it.scope == scope }
        return n
    }
    override fun deleteById(id: Long): Int {
        val n = items.count { it.id == id }
        items.removeAll { it.id == id }
        return n
    }
}

class FakeHomeworkDao : HomeworkDao {
    val items = mutableListOf<HomeworkEntity>()
    private var seq = 1L
    override fun getAll(): List<HomeworkEntity> = items.sortedBy { it.dueDateComputed }.toList()
    override fun getById(id: Long): HomeworkEntity? = items.firstOrNull { it.id == id }
    override fun insert(e: HomeworkEntity): Long {
        val id = seq++
        items.add(e.copy(id = id))
        return id
    }
    override fun update(e: HomeworkEntity) {
        val i = items.indexOfFirst { it.id == e.id }
        if (i >= 0) items[i] = e
    }
    override fun deleteById(id: Long): Int {
        val n = items.count { it.id == id }
        items.removeAll { it.id == id }
        return n
    }
}

class OverrideServiceTest {

    @Test
    fun globalWinsOverWeekday() {
        val svc = OverrideService(FakeOverrideDao())
        svc.addOrUpdate("лек ВЫСШ. МАТЕМАТ", "weekday:1", "Матан Пн", null)
        assertEquals("Матан Пн", svc.displayName("лек ВЫСШ. МАТЕМАТ", 1))
        assertEquals("лек ВЫСШ. МАТЕМАТ", svc.displayName("лек ВЫСШ. МАТЕМАТ", 2))
        svc.addOrUpdate("лек ВЫСШ. МАТЕМАТ", "global", "МАТАН!!!", "сдать!")
        assertEquals("МАТАН!!!", svc.displayName("лек ВЫСШ. МАТЕМАТ", 1))
        assertEquals("МАТАН!!!", svc.displayName("лек ВЫСШ. МАТЕМАТ", 2))
        assertEquals("сдать!", svc.note("лек ВЫСШ. МАТЕМАТ", 1))
        assertEquals(2, svc.all().size)
        svc.remove(svc.all().first { it.scope == "global" }.id)
        assertEquals("Матан Пн", svc.displayName("лек ВЫСШ. МАТЕМАТ", 1))
    }
}

class HomeworkServiceTest {

    private val lessons by lazy { GroupParser.parse(GROUP_FIXTURE).lessons }
    private val ctx = SchedCtx("3313", LocalDate.of(2026, 9, 1), 2, false)

    private fun svc(dao: FakeHomeworkDao = FakeHomeworkDao()) = HomeworkService(
        dao,
        lessonsFor = { gid, dow, parity ->
            lessons.filter { it.groupId == gid && it.dayOfWeek == dow && (it.parity == parity || it.parity == 0) }
        },
        ctx = { ctx }
    )

    @Test
    fun dueN2() {
        val s = svc()
        val due = s.computeDueDate(Parity.normalizeSubject("лек ВЫСШ. МАТЕМАТ"), LocalDate.of(2026, 9, 1), 2)
        // Norm includes type prefix: Mon 09-07 even 09:00 "лек ..." = 1st, Mon 09-14 odd 09:00 = 2nd
        // (Wed 09-09 even 14:55 is "пр ...", different norm)
        assertEquals(LocalDate.of(2026, 9, 14), due)
    }

    @Test
    fun statuses() {
        val s = svc()
        val due = LocalDate.of(2026, 9, 14)
        val norm = Parity.normalizeSubject("лек ВЫСШ. МАТЕМАТ")
        val created = LocalDate.of(2026, 9, 1)
        assertEquals("burning_urgent", s.computeStatus(norm, created, 2, due, false, LocalDate.of(2026, 9, 14)))
        assertEquals("burning", s.computeStatus(norm, created, 2, due, false, LocalDate.of(2026, 9, 13)))
        assertEquals("approaching", s.computeStatus(norm, created, 2, due, false, LocalDate.of(2026, 9, 2)))
        assertEquals("overdue", s.computeStatus(norm, created, 2, due, false, LocalDate.of(2026, 9, 15)))
        assertEquals("done", s.computeStatus(norm, created, 2, due, true, LocalDate.of(2026, 9, 2)))
        assertEquals("far", s.computeStatus(norm, created, 2, LocalDate.of(2026, 12, 1), false, LocalDate.of(2026, 9, 2)))
    }

    @Test
    fun crudAndRecompute() {
        val dao = FakeHomeworkDao()
        val s = svc(dao)
        val id = s.addHomework("лек ВЫСШ. МАТЕМАТ", "с. 10 № 5", 2, LocalDate.of(2026, 9, 1))
        assertEquals(1, dao.items.size)
        assertEquals(LocalDate.of(2026, 9, 14).toString(), dao.items[0].dueDateComputed)
        val list = s.forSubject("лек ВЫСШ. МАТЕМАТ")
        assertEquals(1, list.size)
        assertEquals("с. 10 № 5", list[0].text)
        s.markDone(id, true)
        assertEquals("done", dao.getById(id)!!.status)
        s.markDone(id, false)
        assertTrue(dao.getById(id)!!.status != "done")
        s.delete(id)
        assertTrue(dao.items.isEmpty())
    }
}
