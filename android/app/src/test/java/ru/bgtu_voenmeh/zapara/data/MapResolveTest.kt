package ru.bgtu_voenmeh.zapara.data

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

class MapResolveTest {

    @Test
    fun starIsUlk() {
        val mi = MapResolve.resolve("331*;")!!
        assertEquals("УЛК", mi.building)
        assertEquals(3, mi.floor)
        assertEquals("karta-ulk.-3-etazh-2022.jpg", mi.fileName)
        assertTrue(mi.hasMap)
    }

    @Test
    fun noStarIsGk() {
        val mi = MapResolve.resolve("324;")!!
        assertEquals("ГК", mi.building)
        assertEquals(3, mi.floor)
        assertEquals("karta-glavnyj-korpus-3-etazh-2022.jpg", mi.fileName)
    }

    @Test
    fun vcMapsToGk() {
        val mi = MapResolve.resolve("ВЦ 372*;")!!
        assertEquals("ВЦ", mi.building)
        assertEquals(3, mi.floor)
        assertEquals("karta-glavnyj-korpus-3-etazh-2022.jpg", mi.fileName)
    }

    @Test
    fun remoteAndEmpty() {
        val remote = MapResolve.resolve("дистанционно")!!
        assertTrue(remote.isRemote)
        assertFalse(remote.hasMap)
        assertNull(MapResolve.resolve(""))
        assertNull(MapResolve.resolve(null))
    }

    @Test
    fun floorClamp() {
        assertEquals(5, MapResolve.resolve("507*а;")!!.floor) // УЛК keeps 5
        assertEquals(1, MapResolve.resolve("101;")!!.floor) // ГК 1
    }

    @Test
    fun coordsLookup() {
        val coords = mapOf(
            "УЛК 3" to mapOf("320" to CoordsRect(0.52, 0.42, 0.07, 0.05))
        )
        val exact = MapResolve.findCoords(coords, "УЛК", 3, "320")
        assertEquals(0.52, exact!!.x, 0.0)
        val byDigits = MapResolve.findCoords(coords, "УЛК", 3, "320*")
        assertEquals(0.52, byDigits!!.x, 0.0)
        assertNull(MapResolve.findCoords(coords, "УЛК", 3, "999"))
        assertNull(MapResolve.findCoords(coords, "ГК", 3, "320"))
    }
}
