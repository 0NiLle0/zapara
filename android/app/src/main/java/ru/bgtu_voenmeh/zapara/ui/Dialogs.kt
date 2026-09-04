package ru.bgtu_voenmeh.zapara.ui

import android.content.Intent
import android.net.Uri
import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.Checkbox
import androidx.compose.material3.LinearProgressIndicator
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.window.Dialog
import androidx.compose.ui.window.DialogProperties
import androidx.compose.ui.unit.TextUnit
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import ru.bgtu_voenmeh.zapara.data.AutoUpdate
import ru.bgtu_voenmeh.zapara.data.Friend
import ru.bgtu_voenmeh.zapara.data.GroupInfo
import ru.bgtu_voenmeh.zapara.data.Homework
import ru.bgtu_voenmeh.zapara.data.Lesson
import ru.bgtu_voenmeh.zapara.data.Notifications
import ru.bgtu_voenmeh.zapara.ui.theme.BorderDim
import ru.bgtu_voenmeh.zapara.ui.theme.Bronze
import ru.bgtu_voenmeh.zapara.ui.theme.Cinnabar
import ru.bgtu_voenmeh.zapara.ui.theme.Marble
import ru.bgtu_voenmeh.zapara.ui.theme.MarbleDim
import ru.bgtu_voenmeh.zapara.ui.theme.Panel
import ru.bgtu_voenmeh.zapara.ui.theme.PanelAlt
import ru.bgtu_voenmeh.zapara.ui.theme.Patina

@Composable
fun SettingsDialog(vm: ScheduleViewModel, onDismiss: () -> Unit) {
    val s = vm.state
    val u = vm.updateUi
    val ctx = LocalContext.current
    var groupSelectOpen by remember { mutableStateOf(false) }
    // Fresh verdict on every open: a stored "up to date" may predate newer releases.
    LaunchedEffect(Unit) {
        val cur = vm.updateUi
        if (!cur.checking && !cur.downloading && !cur.hasUpdate && cur.readyFile == null) {
            vm.checkUpdateManual()
        }
    }
    AlertDialog(
        onDismissRequest = onDismiss,
        containerColor = Panel,
        title = { Text("Настройки", color = Bronze, fontSize = 12.sp, fontWeight = FontWeight.SemiBold) },
        text = {
            Column(Modifier.verticalScroll(rememberScrollState())) {
                Text("ГРУППА", color = Bronze, fontSize = 10.sp, fontWeight = FontWeight.SemiBold)
                Spacer(Modifier.height(4.dp))
                SelectField(
                    value = s.groups.firstOrNull { it.id == s.groupId }?.name,
                    placeholder = "Выбрать группу",
                    onClick = { groupSelectOpen = true },
                    modifier = Modifier.fillMaxWidth()
                )
                Spacer(Modifier.height(6.dp))
                OutlinedButton(
                    onClick = { vm.refresh(); onDismiss() },
                    colors = ButtonDefaults.outlinedButtonColors(contentColor = Marble),
                    border = BorderStroke(1.dp, BorderDim),
                    modifier = Modifier.fillMaxWidth()
                ) { Text("Обновить расписание", fontSize = 11.sp) }
                Spacer(Modifier.height(10.dp))
                Text("УВЕДОМЛЕНИЯ", color = Bronze, fontSize = 10.sp, fontWeight = FontWeight.SemiBold)
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Checkbox(checked = s.notifEnabled, onCheckedChange = { vm.setNotifEnabled(it) })
                    Text("Уведомления о парах", color = Marble, fontSize = 11.sp)
                }
                if (!vm.notifPermissionGranted()) {
                    Text("Нет разрешения на уведомления", color = Cinnabar, fontSize = 10.sp)
                    TextButton(onClick = {
                        try {
                            ctx.startActivity(
                                Intent(android.provider.Settings.ACTION_APP_NOTIFICATION_SETTINGS)
                                    .putExtra(android.provider.Settings.EXTRA_APP_PACKAGE, ctx.packageName)
                            )
                        } catch (_: Exception) {}
                    }) { Text("Открыть настройки системы", color = Bronze, fontSize = 11.sp) }
                } else if (!vm.canScheduleExact()) {
                    Text("Без точных будильников сработает неточно", color = Cinnabar, fontSize = 10.sp)
                    TextButton(onClick = {
                        try {
                            ctx.startActivity(Intent(android.provider.Settings.ACTION_REQUEST_SCHEDULE_EXACT_ALARM))
                        } catch (_: Exception) {}
                    }) { Text("Разрешить точные будильники", color = Bronze, fontSize = 11.sp) }
                }
                var nt1 by remember(s.notifTime1) { mutableStateOf(s.notifTime1 ?: "20:00") }
                var nt2 by remember(s.notifTime2) { mutableStateOf(s.notifTime2 ?: "07:30") }
                var ntErr by remember { mutableStateOf<String?>(null) }
                Row(verticalAlignment = Alignment.CenterVertically) {
                    OutlinedTextField(
                        value = nt1, onValueChange = { nt1 = it; ntErr = null },
                        label = { Text("Время 1", fontSize = 9.sp) },
                        singleLine = true, modifier = Modifier.weight(1f)
                    )
                    Spacer(Modifier.width(6.dp))
                    OutlinedTextField(
                        value = nt2, onValueChange = { nt2 = it; ntErr = null },
                        label = { Text("Время 2", fontSize = 9.sp) },
                        singleLine = true, modifier = Modifier.weight(1f)
                    )
                    Spacer(Modifier.width(6.dp))
                    OutlinedButton(
                        onClick = {
                            if (!Notifications.isValidTime(nt1.trim()) || !Notifications.isValidTime(nt2.trim())) {
                                ntErr = "Формат ЧЧ:ММ"
                            } else {
                                vm.saveNotifTimes(nt1.trim(), nt2.trim())
                                ntErr = null
                            }
                        },
                        colors = ButtonDefaults.outlinedButtonColors(contentColor = Marble),
                        border = BorderStroke(1.dp, BorderDim)
                    ) { Text("OK", fontSize = 11.sp) }
                }
                if (ntErr != null) Text(ntErr!!, color = Cinnabar, fontSize = 10.sp)
                Text("Время 1 — пары завтра, время 2 — пары сегодня.", color = MarbleDim, fontSize = 9.sp)
                TextButton(onClick = { vm.testNotification() }) {
                    Text("Показать тестовое", color = Bronze, fontSize = 11.sp)
                }
                Spacer(Modifier.height(10.dp))
                Text("ПРИЛОЖЕНИЕ", color = Bronze, fontSize = 10.sp, fontWeight = FontWeight.SemiBold)
                Text("Версия ${AutoUpdate.CURRENT_TAG}", color = MarbleDim, fontSize = 10.sp)
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Checkbox(checked = u.auto, onCheckedChange = { vm.setAutoUpdate(it) })
                    Text("Автообновление", color = Marble, fontSize = 11.sp)
                }
                when {
                    u.checking -> Text("Проверка...", color = MarbleDim, fontSize = 11.sp)
                    u.downloading -> {
                        if (u.progress >= 0f) {
                            LinearProgressIndicator(
                                progress = { u.progress },
                                modifier = Modifier.fillMaxWidth()
                            )
                            val total = if (u.totalBytes > 0) "${u.totalBytes / 1024} КБ" else "? КБ"
                            Text(
                                "Загрузка ${(u.progress * 100).toInt()}% · ${u.doneBytes / 1024} / $total",
                                color = MarbleDim, fontSize = 10.sp
                            )
                        } else {
                            LinearProgressIndicator(modifier = Modifier.fillMaxWidth())
                            Text("Загрузка...", color = MarbleDim, fontSize = 10.sp)
                        }
                        TextButton(onClick = { vm.cancelUpdateDownload() }) {
                            Text("Отмена", color = MarbleDim, fontSize = 11.sp)
                        }
                    }
                    u.readyFile != null -> {
                        Text("Готово: ${u.tag} — откройте установщик", color = Patina, fontSize = 11.sp)
                        Row {
                            TextButton(onClick = { vm.installReady() }) {
                                Text("Установить", color = Bronze, fontSize = 11.sp)
                            }
                            if (u.htmlUrl != null) {
                                val url = u.htmlUrl
                                TextButton(onClick = {
                                    try {
                                        ctx.startActivity(Intent(Intent.ACTION_VIEW, Uri.parse(url)))
                                    } catch (_: Exception) {}
                                }) { Text("В браузере", color = MarbleDim, fontSize = 11.sp) }
                            }
                        }
                    }
                    u.hasUpdate -> {
                        Text("Доступно ${u.tag} (у вас ${AutoUpdate.CURRENT_TAG})", color = Marble, fontSize = 11.sp)
                        Row {
                            TextButton(onClick = { vm.startUpdateDownload() }) {
                                Text("Скачать и установить", color = Bronze, fontSize = 11.sp)
                            }
                            if (u.htmlUrl != null) {
                                val url = u.htmlUrl
                                TextButton(onClick = {
                                    try {
                                        ctx.startActivity(Intent(Intent.ACTION_VIEW, Uri.parse(url)))
                                    } catch (_: Exception) {}
                                }) { Text("В браузере", color = MarbleDim, fontSize = 11.sp) }
                            }
                        }
                    }
                    else -> {
                        if (u.upToDate) {
                            Text(
                                "У вас последняя" + if (u.tag.isNotEmpty()) " — ${u.tag}" else "",
                                color = MarbleDim, fontSize = 11.sp
                            )
                        }
                        if (u.error != null) Text(u.error, color = Cinnabar, fontSize = 11.sp)
                    }
                }
                // Progress log only while working or on error — not as a stale echo of the status.
                if (u.log.isNotEmpty() && (u.checking || u.downloading || u.error != null)) {
                    Text(u.log, color = MarbleDim, fontSize = 9.sp)
                }
                // Re-check is always available (except mid-check/download) — no dead ends.
                if (!u.checking && !u.downloading) {
                    TextButton(onClick = { vm.checkUpdateManual() }) {
                        Text("Проверить обновление", color = Bronze, fontSize = 11.sp)
                    }
                }
            }
        },
        confirmButton = {
            TextButton(onClick = onDismiss) { Text("Готово", color = Bronze, fontSize = 11.sp) }
        }
    )
    if (groupSelectOpen) {
        SearchSelectDialog(
            title = "Группа",
            searchLabel = "Поиск группы",
            items = s.groups.map { it.id to it.name },
            onSelect = { vm.selectGroup(it.first); groupSelectOpen = false },
            onDismiss = { groupSelectOpen = false }
        )
    }
}

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
    onDismiss: () -> Unit,
    initialText: String = "",
    initialN: Int = 1
) {
    var text by remember { mutableStateOf(initialText) }
    var nText by remember { mutableStateOf(initialN.coerceIn(1, 10).toString()) }
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
    var friendPicked by remember { mutableStateOf<Pair<String, String>?>(null) }
    var friendSelectOpen by remember { mutableStateOf(false) }
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
                        SelectField(
                            value = friendPicked?.second,
                            placeholder = "Выбрать группу",
                            onClick = { friendSelectOpen = true },
                            modifier = Modifier.weight(1f)
                        )
                        Spacer(Modifier.width(6.dp))
                        OutlinedButton(
                            onClick = {
                                friendPicked?.let { onAdd(it.second); friendPicked = null }
                            },
                            enabled = friendPicked != null,
                            colors = ButtonDefaults.outlinedButtonColors(contentColor = Marble),
                            border = BorderStroke(1.dp, BorderDim)
                        ) { Text("+", fontSize = 11.sp) }
                    }
                }
                if (friendSelectOpen) {
                    SearchSelectDialog(
                        title = "Группа",
                        searchLabel = "Поиск группы",
                        items = allGroups
                            .filter { g -> friends.none { it.groupName == g.name } }
                            .map { it.id to it.name },
                        onSelect = { friendPicked = it; friendSelectOpen = false },
                        onDismiss = { friendSelectOpen = false }
                    )
                }
            }
        },
        confirmButton = {
            TextButton(onClick = onDismiss) { Text("Готово", color = Bronze, fontSize = 11.sp) }
        }
    )
}

// Charon button styles: every labeled button gets a visible frame.
// Ghost = secondary (BorderDim frame), Ferry = primary (PanelAlt fill + Bronze frame).
@Composable
fun GhostBtn(
    text: String,
    onClick: () -> Unit,
    modifier: Modifier = Modifier,
    enabled: Boolean = true,
    fontSize: TextUnit = 11.sp,
    compact: Boolean = false
) {
    OutlinedButton(
        onClick = onClick,
        enabled = enabled,
        modifier = modifier,
        colors = ButtonDefaults.outlinedButtonColors(contentColor = Marble, disabledContentColor = MarbleDim),
        border = BorderStroke(1.5.dp, BorderDim),
        contentPadding = if (compact) PaddingValues(horizontal = 8.dp, vertical = 6.dp)
        else PaddingValues(horizontal = 16.dp, vertical = 10.dp)
    ) { Text(text, fontSize = fontSize, maxLines = 1) }
}

@Composable
fun FerryBtn(
    text: String,
    onClick: () -> Unit,
    modifier: Modifier = Modifier,
    enabled: Boolean = true,
    fontSize: TextUnit = 11.sp,
    compact: Boolean = false
) {
    OutlinedButton(
        onClick = onClick,
        enabled = enabled,
        modifier = modifier,
        colors = ButtonDefaults.outlinedButtonColors(
            containerColor = PanelAlt,
            contentColor = Bronze,
            disabledContentColor = MarbleDim
        ),
        border = BorderStroke(1.5.dp, Bronze),
        contentPadding = if (compact) PaddingValues(horizontal = 8.dp, vertical = 6.dp)
        else PaddingValues(horizontal = 16.dp, vertical = 10.dp)
    ) { Text(text, fontSize = fontSize, maxLines = 1) }
}

@Composable
fun SelectField(
    value: String?,
    placeholder: String,
    onClick: () -> Unit,
    modifier: Modifier = Modifier
) {
    Card(
        onClick = onClick,
        colors = CardDefaults.cardColors(containerColor = PanelAlt),
        border = BorderStroke(1.dp, BorderDim),
        modifier = modifier
    ) {
        Row(
            Modifier.padding(10.dp, 12.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            Text(
                value ?: placeholder,
                color = if (value != null) Marble else MarbleDim,
                fontSize = 11.sp, maxLines = 1,
                overflow = androidx.compose.ui.text.style.TextOverflow.Ellipsis,
                modifier = Modifier.weight(1f)
            )
            Spacer(Modifier.width(6.dp))
            Text("▼", color = MarbleDim, fontSize = 10.sp)
        }
    }
}

@Composable
fun SearchSelectDialog(
    title: String,
    searchLabel: String,
    items: List<Pair<String, String>>,
    onSelect: (Pair<String, String>) -> Unit,
    onDismiss: () -> Unit
) {
    var q by remember { mutableStateOf("") }
    val filtered = remember(items, q) {
        val s = q.trim()
        if (s.isEmpty()) items
        else items.filter { it.second.contains(s, ignoreCase = true) || it.first.contains(s, ignoreCase = true) }
    }
    Dialog(onDismissRequest = onDismiss) {
        Card(
            colors = CardDefaults.cardColors(containerColor = Panel),
            border = BorderStroke(1.dp, BorderDim),
            modifier = Modifier.fillMaxWidth()
        ) {
            Column(Modifier.fillMaxWidth().padding(10.dp)) {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Text(
                        title, color = Bronze, fontSize = 12.sp, fontWeight = FontWeight.SemiBold,
                        modifier = Modifier.weight(1f)
                    )
                    TextButton(onClick = onDismiss) { Text("✕", color = MarbleDim, fontSize = 11.sp) }
                }
                Spacer(Modifier.height(6.dp))
                OutlinedTextField(
                    value = q,
                    onValueChange = { q = it },
                    label = { Text(searchLabel, fontSize = 10.sp) },
                    leadingIcon = { Text("⌕", color = MarbleDim, fontSize = 12.sp) },
                    trailingIcon = {
                        if (q.isNotEmpty()) TextButton(onClick = { q = "" }) {
                            Text("✕", color = MarbleDim, fontSize = 10.sp)
                        }
                    },
                    singleLine = true,
                    modifier = Modifier.fillMaxWidth()
                )
                Text(
                    "${filtered.size}/${items.size}", color = MarbleDim, fontSize = 9.sp,
                    modifier = Modifier.padding(horizontal = 4.dp, vertical = 4.dp)
                )
                LazyColumn(modifier = Modifier.heightIn(max = 380.dp)) {
                    if (filtered.isEmpty()) {
                        item { Text("Не найдено", color = MarbleDim, fontSize = 11.sp, modifier = Modifier.padding(8.dp)) }
                    } else {
                        items(filtered, key = { it.first }) { item ->
                            Card(
                                onClick = { onSelect(item) },
                                colors = CardDefaults.cardColors(containerColor = PanelAlt),
                                border = BorderStroke(1.dp, BorderDim),
                                modifier = Modifier.fillMaxWidth().padding(vertical = 3.dp)
                            ) {
                                Text(
                                    item.second, color = Marble, fontSize = 11.sp,
                                    modifier = Modifier.padding(10.dp, 8.dp)
                                )
                            }
                        }
                    }
                }
            }
        }
    }
}

@Composable
fun HomeworkRow(hw: Homework, onToggle: () -> Unit, onEdit: () -> Unit, onDelete: () -> Unit) {
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
                hw.due?.let {
                    if (it.year != java.time.LocalDate.now().year) "%02d.%02d.%d".format(it.dayOfMonth, it.monthValue, it.year)
                    else "%02d.%02d".format(it.dayOfMonth, it.monthValue)
                } ?: "—",
                color = MarbleDim, fontSize = 9.sp
            )
            TextButton(
                onClick = onEdit,
                contentPadding = androidx.compose.foundation.layout.PaddingValues(12.dp, 8.dp)
            ) { Text("✎", color = MarbleDim, fontSize = 14.sp) }
            TextButton(
                onClick = onDelete,
                contentPadding = androidx.compose.foundation.layout.PaddingValues(12.dp, 8.dp)
            ) { Text("✕", color = MarbleDim, fontSize = 15.sp) }
        }
    }
}
