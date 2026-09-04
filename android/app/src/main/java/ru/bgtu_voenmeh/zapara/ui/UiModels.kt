package ru.bgtu_voenmeh.zapara.ui

import ru.bgtu_voenmeh.zapara.data.Homework

data class TrafficDot(
    val friendGroup: String,
    val memberNames: String,
    val score: Int,
    val teacher: String,
    val room: String
)

sealed interface UiDialog {
    data object None : UiDialog
    data class Rename(val lessonIndex: Int, val initialName: String, val initialNote: String) : UiDialog
    data class Homework(val lessonIndex: Int) : UiDialog
    data class HomeworkEdit(val lessonIndex: Int, val hwId: Long) : UiDialog
    data object Friends : UiDialog
    data object Teachers : UiDialog
    data object Settings : UiDialog
}

data class UpdateUiState(
    val checking: Boolean = false,
    val tag: String = "",
    val apkUrl: String? = null,
    val htmlUrl: String? = null,
    val hasUpdate: Boolean = false,
    val upToDate: Boolean = false,
    val error: String? = null,
    val downloading: Boolean = false,
    val progress: Float = -1f,
    val doneBytes: Long = 0L,
    val totalBytes: Long = -1L,
    val readyFile: String? = null,
    val auto: Boolean = true
)
