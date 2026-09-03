package ru.bgtu_voenmeh.zapara.ui

import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.Checkbox
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import ru.bgtu_voenmeh.zapara.data.Friend
import ru.bgtu_voenmeh.zapara.data.GroupInfo
import ru.bgtu_voenmeh.zapara.data.Homework
import ru.bgtu_voenmeh.zapara.data.Lesson
import ru.bgtu_voenmeh.zapara.ui.theme.BorderDim
import ru.bgtu_voenmeh.zapara.ui.theme.Bronze
import ru.bgtu_voenmeh.zapara.ui.theme.Cinnabar
import ru.bgtu_voenmeh.zapara.ui.theme.Marble
import ru.bgtu_voenmeh.zapara.ui.theme.MarbleDim
import ru.bgtu_voenmeh.zapara.ui.theme.Panel
import ru.bgtu_voenmeh.zapara.ui.theme.PanelAlt

@Composable
fun RenameDialog(
    lesson: Lesson,
    initialName: String,
    initialNote: String,
    initialGlobal: Boolean,
    onSave: (displayName: String, note: String, global: Boolean) -> Unit,
    onReset: () -> Unit,
    onDismiss: () -> Unit
) {
    var name by remember { mutableStateOf(initialName) }
    var note by remember { mutableStateOf(initialNote) }
    var global by remember { mutableStateOf(initialGlobal) }
    AlertDialog(
        onDismissRequest = onDismiss,
        containerColor = Panel,
        title = { Text("Переименование", color = Bronze, fontSize = 12.sp) },
        text = {
            Column {
                Text("Оригинал: ${lesson.subjectRaw}", color = MarbleDim, fontSize = 10.sp)
                Spacer(Modifier.height(6.dp))
                OutlinedTextField(
                    value = name, onValueChange = { name = it },
                    label = { Text("Новое название", fontSize = 10.sp) },
                    modifier = Modifier.fillMaxWidth()
                )
                Spacer(Modifier.height(6.dp))
                OutlinedTextField(
                    value = note, onValueChange = { note = it },
                    label = { Text("Примечание", fontSize = 10.sp) },
                    modifier = Modifier.fillMaxWidth()
                )
                Spacer(Modifier.height(6.dp))
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Checkbox(checked = global, onCheckedChange = { global = true })
                    Text("Глобально", color = Marble, fontSize = 11.sp)
                    Spacer(Modifier.width(12.dp))
                    Checkbox(checked = !global, onCheckedChange = { global = false })
                    Text("Только в этот день", color = Marble, fontSize = 11.sp)
                }
            }
        },
        confirmButton = {
            TextButton(onClick = { onSave(name, note, global) }) {
                Text("Сохранить", color = Bronze, fontSize = 11.sp)
            }
        },
        dismissButton = {
            Row {
                TextButton(onClick = onReset) { Text("Сбросить", color = MarbleDim, fontSize = 11.sp) }
                TextButton(onClick = onDismiss) { Text("Отмена", color = MarbleDim, fontSize = 11.sp) }
            }
        }
    )
}

@Composable
fun HomeworkDialog(
    lesson: Lesson,
    duePreview: (Int) -> String,
    onSave: (text: String, n: Int) -> Unit,
    onDismiss: () -> Unit
) {
    var text by remember { mutableStateOf("") }
    var nText by remember { mutableStateOf("1") }
    val n = nText.toIntOrNull()?.coerceIn(1, 10) ?: 1
    AlertDialog(
        onDismissRequest = onDismiss,
        containerColor = Panel,
        title = { Text("Домашнее задание", color = Bronze, fontSize = 12.sp) },
        text = {
            Column {
                Text("Предмет: ${lesson.subjectRaw}", color = MarbleDim, fontSize = 10.sp)
                Spacer(Modifier.height(6.dp))
                OutlinedTextField(
                    value = text, onValueChange = { text = it },
                    label = { Text("Текст задания", fontSize = 10.sp) },
                    modifier = Modifier.fillMaxWidth()
                )
                Spacer(Modifier.height(6.dp))
                OutlinedTextField(
                    value = nText,
                    onValueChange = { v -> nText = v.filter { it.isDigit() }.take(2) },
                    label = { Text("Через сколько занятий (1..10)", fontSize = 10.sp) },
                    modifier = Modifier.fillMaxWidth()
                )
                Spacer(Modifier.height(4.dp))
                Text(duePreview(n), color = Bronze, fontSize = 10.sp)
            }
        },
        confirmButton = {
            TextButton(onClick = { onSave(text, n) }, enabled = text.isNotBlank()) {
                Text("Сохранить", color = Bronze, fontSize = 11.sp)
            }
        },
        dismissButton = {
            TextButton(onClick = onDismiss) { Text("Отмена", color = MarbleDim, fontSize = 11.sp) }
        }
    )
}

@Composable
fun FriendsDialog(
    friends: List<Friend>,
    allGroups: List<GroupInfo>,
    alwaysShow: Boolean,
    invertParity: Boolean,
    onToggleAlwaysShow: (Boolean) -> Unit,
    onToggleInvert: (Boolean) -> Unit,
    onAdd: (groupName: String) -> Unit,
    onRemove: (Friend) -> Unit,
    onSaveNames: (Friend, String) -> Unit,
    onDismiss: () -> Unit
) {
    var picked by remember { mutableStateOf(allGroups.firstOrNull()?.name.orEmpty()) }
    AlertDialog(
        onDismissRequest = onDismiss,
        containerColor = Panel,
        title = { Text("Друзья (до 5)", color = Bronze, fontSize = 12.sp) },
        text = {
            Column {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Checkbox(checked = alwaysShow, onCheckedChange = onToggleAlwaysShow)
                    Text("Всегда все светофоры", color = Marble, fontSize = 11.sp)
                }
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Checkbox(checked = invertParity, onCheckedChange = onToggleInvert)
                    Text("Инвертировать четность", color = Marble, fontSize = 11.sp)
                }
                LazyColumn(modifier = Modifier.height(220.dp)) {
                    items(friends, key = { it.groupName }) { f ->
                        var names by remember(f.groupName) { mutableStateOf(f.memberNames) }
                        Card(
                            colors = CardDefaults.cardColors(containerColor = PanelAlt),
                            border = BorderStroke(1.dp, BorderDim),
                            modifier = Modifier
                                .fillMaxWidth()
                                .padding(vertical = 3.dp)
                        ) {
                            Column(Modifier.padding(6.dp)) {
                                Row(verticalAlignment = Alignment.CenterVertically) {
                                    Text("● ${f.groupName}", color = Marble, fontSize = 11.sp, modifier = Modifier.weight(1f))
                                    TextButton(onClick = { onRemove(f) }) {
                                        Text("✕", color = MarbleDim, fontSize = 11.sp)
                                    }
                                }
                                OutlinedTextField(
                                    value = names,
                                    onValueChange = {
                                        names = it
                                        onSaveNames(f, it)
                                    },
                                    label = { Text("Имена товарищей", fontSize = 9.sp) },
                                    modifier = Modifier.fillMaxWidth()
                                )
                            }
                        }
                    }
                }
                if (friends.size < 5) {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        OutlinedTextField(
                            value = picked,
                            onValueChange = { picked = it },
                            label = { Text("Группа", fontSize = 9.sp) },
                            modifier = Modifier.weight(1f)
                        )
                        Spacer(Modifier.width(6.dp))
                        OutlinedButton(
                            onClick = { onAdd(picked) },
                            colors = ButtonDefaults.outlinedButtonColors(contentColor = Marble),
                            border = BorderStroke(1.dp, BorderDim)
                        ) { Text("+", fontSize = 11.sp) }
                    }
                }
            }
        },
        confirmButton = {
            TextButton(onClick = onDismiss) { Text("Готово", color = Bronze, fontSize = 11.sp) }
        }
    )
}

@Composable
fun HomeworkRow(hw: Homework, onToggle: () -> Unit, onDelete: () -> Unit) {
    val fg = when (hw.status) {
        "approaching", "done" -> MarbleDim
        "burning", "burning_urgent" -> Bronze
        "overdue" -> Cinnabar
        else -> MarbleDim
    }
    val border = when (hw.status) {
        "burning_urgent" -> Bronze
        "overdue" -> Cinnabar
        else -> BorderDim
    }
    Card(
        colors = CardDefaults.cardColors(
            containerColor = if (hw.status == "done") Panel else PanelAlt
        ),
        border = BorderStroke(1.dp, border),
        modifier = Modifier
            .fillMaxWidth()
            .padding(top = 6.dp),
        onClick = onToggle
    ) {
        Row(
            Modifier.padding(6.dp, 3.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            val label = when (hw.status) {
                "done" -> "✓ ${hw.text}"
                "overdue" -> "⚠ ${hw.text}"
                else -> "● ${hw.text}"
            }
            Text(
                label, color = fg, fontSize = 11.sp,
                modifier = Modifier.weight(1f),
                textDecoration = if (hw.status == "done")
                    androidx.compose.ui.text.style.TextDecoration.LineThrough else null
            )
            Text(
                hw.due?.let { "%02d.%02d".format(it.dayOfMonth, it.monthValue) } ?: "—",
                color = MarbleDim, fontSize = 9.sp
            )
            TextButton(onClick = onDelete) { Text("✕", color = MarbleDim, fontSize = 10.sp) }
        }
    }
}
