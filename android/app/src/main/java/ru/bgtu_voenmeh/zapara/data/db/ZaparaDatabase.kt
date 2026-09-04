package ru.bgtu_voenmeh.zapara.data.db

import androidx.room.Dao
import androidx.room.Database
import androidx.room.Entity
import androidx.room.Insert
import androidx.room.OnConflictStrategy
import androidx.room.PrimaryKey
import androidx.room.Query
import androidx.room.RoomDatabase
import androidx.room.Update

// Room schema mirrors Windows Database.cs (v2: overrides/homework/strictness/alwaysShow).

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

@Entity(tableName = "settings")
data class SettingsEntity(
    @PrimaryKey val id: Int = 1,
    val myGroupId: String? = null,
    val parityInvert: Boolean = false,
    val language: String = "ru",
    val periodStart: String? = null, // ISO yyyy-MM-dd
    val weekCount: Int = 2,
    val periodTitle: String? = null,
    val lastFetchedAt: String? = null,
    val intersectionStrictness: Int = 25,
    val alwaysShowAllTrafficLights: Boolean = false,
    val notifyEnabled: Boolean = true,
    val notifyTime1: String? = "20:00", // evening: tomorrow's lessons
    val notifyTime2: String? = "07:30" // morning: today's lessons
)

@Entity(tableName = "overrides")
data class OverrideEntity(
    @PrimaryKey(autoGenerate = true) val id: Long = 0,
    val subjectRawNormalized: String,
    val scope: String, // "global" | "weekday:N"
    val displayName: String,
    val note: String? = null,
    val createdAt: String
)

@Entity(tableName = "homework")
data class HomeworkEntity(
    @PrimaryKey(autoGenerate = true) val id: Long = 0,
    val subjectRawNormalized: String,
    val text: String,
    val createdAt: String, // ISO
    val targetNthOccurrence: Int,
    val dueDateComputed: String? = null, // ISO date
    val status: String = "pending",
    val doneAt: String? = null
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

    @Update
    fun update(friend: FriendEntity)

    @Query("SELECT * FROM friends")
    fun getAll(): List<FriendEntity>

    @Query("DELETE FROM friends WHERE id = :id")
    fun delete(id: Long)
}

@Dao
interface SettingsDao {
    @Query("SELECT * FROM settings WHERE id = 1 LIMIT 1")
    fun get(): SettingsEntity?

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    fun save(settings: SettingsEntity)
}

@Dao
interface OverrideDao {
    @Query("SELECT * FROM overrides")
    fun getAll(): List<OverrideEntity>

    @Insert
    fun insert(e: OverrideEntity): Long

    @Query("DELETE FROM overrides WHERE subjectRawNormalized = :norm AND scope = :scope")
    fun deleteByKey(norm: String, scope: String): Int

    @Query("DELETE FROM overrides WHERE id = :id")
    fun deleteById(id: Long): Int
}

@Dao
interface HomeworkDao {
    @Query("SELECT * FROM homework ORDER BY dueDateComputed")
    fun getAll(): List<HomeworkEntity>

    @Query("SELECT * FROM homework WHERE id = :id LIMIT 1")
    fun getById(id: Long): HomeworkEntity?

    @Insert
    fun insert(e: HomeworkEntity): Long

    @Update
    fun update(e: HomeworkEntity)

    @Query("DELETE FROM homework WHERE id = :id")
    fun deleteById(id: Long): Int
}

val MIGRATION_1_2 = object : androidx.room.migration.Migration(1, 2) {
    override fun migrate(db: androidx.sqlite.db.SupportSQLiteDatabase) {
        db.execSQL(
            "CREATE TABLE IF NOT EXISTS overrides (id INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL, " +
                "subjectRawNormalized TEXT NOT NULL, scope TEXT NOT NULL, displayName TEXT NOT NULL, " +
                "note TEXT, createdAt TEXT NOT NULL)"
        )
        db.execSQL(
            "CREATE TABLE IF NOT EXISTS homework (id INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL, " +
                "subjectRawNormalized TEXT NOT NULL, text TEXT NOT NULL, createdAt TEXT NOT NULL, " +
                "targetNthOccurrence INTEGER NOT NULL, dueDateComputed TEXT, status TEXT NOT NULL, doneAt TEXT)"
        )
        db.execSQL("ALTER TABLE settings ADD COLUMN intersectionStrictness INTEGER NOT NULL DEFAULT 25")
        db.execSQL("ALTER TABLE settings ADD COLUMN alwaysShowAllTrafficLights INTEGER NOT NULL DEFAULT 0")
    }
}

val MIGRATION_2_3 = object : androidx.room.migration.Migration(2, 3) {
    override fun migrate(db: androidx.sqlite.db.SupportSQLiteDatabase) {
        db.execSQL("ALTER TABLE settings ADD COLUMN notifyEnabled INTEGER NOT NULL DEFAULT 1")
        db.execSQL("ALTER TABLE settings ADD COLUMN notifyTime1 TEXT DEFAULT '20:00'")
        db.execSQL("ALTER TABLE settings ADD COLUMN notifyTime2 TEXT DEFAULT '07:30'")
    }
}

@Database(
    entities = [GroupEntity::class, LessonEntity::class, FriendEntity::class, SettingsEntity::class, OverrideEntity::class, HomeworkEntity::class],
    version = 3,
    exportSchema = false
)
abstract class ZaparaDatabase : RoomDatabase() {
    abstract fun groupDao(): GroupDao
    abstract fun lessonDao(): LessonDao
    abstract fun friendDao(): FriendDao
    abstract fun settingsDao(): SettingsDao
    abstract fun overrideDao(): OverrideDao
    abstract fun homeworkDao(): HomeworkDao
}
