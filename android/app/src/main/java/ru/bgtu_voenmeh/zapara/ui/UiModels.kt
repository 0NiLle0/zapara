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
    data object Friends : UiDialog
    data object Teachers : UiDialog
}
