package ru.bgtu_voenmeh.zapara.data.db

import androidx.room.Dao
import androidx.room.Database
import androidx.room.Entity
import androidx.room.Insert
import androidx.room.OnConflictStrategy
import androidx.room.PrimaryKey
import androidx.room.Query
import androidx.room.RoomDatabase

// Room schema mirrors Windows Database.cs. Repositories + migrations land in A2.

@Entity(tableName = "groups")
data class GroupEntity(
    @PrimaryKey val id: String,
    val name: String,
    val url: String = ""
)

@Entity(tableName = "schedule_cache")
data class LessonEntity(
    @PrimaryKey(autoGenerate = true) val id: Long = 0,
    val groupId: String,
    val dayOfWeek: Int,
    val parity: Int,
    val idx: Int,
    val timeStart: String = "",
    val timeEnd: String = "",
    val subjectRaw: String = "",
    val subjectNormalized: String = "",
    val teacherRaw: String = "",
    val roomRaw: String = "",
    val buildingRaw: String = "",
    val typeRaw: String = "",
    val classroomRaw: String = ""
)

@Entity(tableName = "friends")
data class FriendEntity(
    @PrimaryKey(autoGenerate = true) val id: Long = 0,
    val groupName: String,
    val colorHex: String,
    val enabled: Boolean = true,
    val memberNames: String = ""
)

@Dao
interface GroupDao {
    @Insert(onConflict = OnConflictStrategy.REPLACE)
    fun upsert(group: GroupEntity)

    @Query("SELECT * FROM groups ORDER BY name")
    fun getAll(): List<GroupEntity>

    @Query("SELECT * FROM groups WHERE id = :id LIMIT 1")
    fun getById(id: String): GroupEntity?
}

@Dao
interface LessonDao {
    @Insert
    fun insertAll(lessons: List<LessonEntity>)

    @Query("DELETE FROM schedule_cache WHERE groupId = :groupId")
    fun clearForGroup(groupId: String)

    @Query(
        "SELECT * FROM schedule_cache WHERE groupId = :groupId AND dayOfWeek = :dow " +
            "AND (parity = :parity OR parity = 0) ORDER BY idx, timeStart"
    )
    fun getLessons(groupId: String, dow: Int, parity: Int): List<LessonEntity>

    @Query("SELECT * FROM schedule_cache WHERE groupId = :groupId ORDER BY dayOfWeek, parity, idx")
    fun getAllForGroup(groupId: String): List<LessonEntity>
}

@Dao
interface FriendDao {
    @Insert
    fun insert(friend: FriendEntity): Long

    @Query("SELECT * FROM friends")
    fun getAll(): List<FriendEntity>

    @Query("DELETE FROM friends WHERE id = :id")
    fun delete(id: Long)
}

@Database(
    entities = [GroupEntity::class, LessonEntity::class, FriendEntity::class],
    version = 1,
    exportSchema = false
)
abstract class ZaparaDatabase : RoomDatabase() {
    abstract fun groupDao(): GroupDao
    abstract fun lessonDao(): LessonDao
    abstract fun friendDao(): FriendDao
}
