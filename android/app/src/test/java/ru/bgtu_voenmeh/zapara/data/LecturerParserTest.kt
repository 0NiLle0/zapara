package ru.bgtu_voenmeh.zapara.data

import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

class LecturerParserTest {

    private val parsed by lazy { LecturerParser.parse(LECTURER_FIXTURE) }

    @Test
    fun lecturersAndLessons() {
        assertEquals(1, parsed.lecturers.size)
        assertEquals("1287", parsed.lecturers[0].id)
        assertEquals("Барт Елена Леонидовна", parsed.lecturers[0].name)
        assertEquals(2, parsed.lessons.size)
    }

    @Test
    fun groupsParsed() {
        val mon = parsed.lessons.first { it.dayOfWeek == 1 }
        assertEquals(2, mon.groups.size)
        assertTrue(mon.groups.any { it.idGroup == "3313" && it.number == "А863С" })
        assertEquals("493", mon.roomRaw)
        assertEquals("ГК", mon.buildingRaw)
    }
}
