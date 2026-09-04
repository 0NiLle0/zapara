package ru.bgtu_voenmeh.zapara.ui

import android.graphics.BitmapFactory
import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.Image
import androidx.compose.foundation.gestures.rememberTransformableState
import androidx.compose.foundation.gestures.transformable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.Checkbox
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableFloatStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clipToBounds
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.graphics.asImageBitmap
import androidx.compose.ui.graphics.graphicsLayer
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.compose.ui.window.Dialog
import androidx.compose.ui.window.DialogProperties
import ru.bgtu_voenmeh.zapara.data.LecturerInfo
import ru.bgtu_voenmeh.zapara.data.LecturerLesson
import ru.bgtu_voenmeh.zapara.data.MapInfo
import ru.bgtu_voenmeh.zapara.data.Parity
import ru.bgtu_voenmeh.zapara.ui.theme.BorderDim
import ru.bgtu_voenmeh.zapara.ui.theme.Bronze
import ru.bgtu_voenmeh.zapara.ui.theme.Marble
import ru.bgtu_voenmeh.zapara.ui.theme.MarbleDim
import ru.bgtu_voenmeh.zapara.ui.theme.Obsidian
import ru.bgtu_voenmeh.zapara.ui.theme.Panel
import ru.bgtu_voenmeh.zapara.ui.theme.PanelAlt
import ru.bgtu_voenmeh.zapara.ui.theme.Patina
import java.io.File

@Composable
fun ZoomableMapImage(file: File?, contentDesc: String, resetSignal: Int = 0) {
    var scale by remember { mutableFloatStateOf(1f) }
    var offset by remember { mutableStateOf(Offset.Zero) }
    // "Сбросить вид" button bumps resetSignal — bring the map back after panning away
    LaunchedEffect(resetSignal) {
        if (resetSignal > 0) {
            scale = 1f
            offset = Offset.Zero
        }
    }
    val state = rememberTransformableState { zoomChange, panChange, _ ->
        scale = (scale * zoomChange).coerceIn(0.4f, 4f)
        offset += panChange
    }
    val bitmap = remember(file?.absolutePath) {
        try {
            file?.takeIf { it.exists() }?.let { BitmapFactory.decodeFile(it.absolutePath)?.asImageBitmap() }
        } catch (_: Exception) {
            null
        }
    }
    // clipToBounds + Fit: a full-size JPG must not bleed over the buttons below.
    Box(
        modifier = Modifier
            .fillMaxWidth()
            .height(260.dp)
            .clipToBounds()
            .transformable(state),
        contentAlignment = Alignment.Center
    ) {
        if (bitmap != null) {
            Image(
                bitmap = bitmap,
                contentDescription = contentDesc,
                contentScale = ContentScale.Fit,
                modifier = Modifier
                    .fillMaxSize()
                    .graphicsLayer(
                        scaleX = scale, scaleY = scale,
                        translationX = offset.x, translationY = offset.y
                    )
            )
        } else {
            Text("Карта не загружена", color = MarbleDim, fontSize = 10.sp)
        }
    }
}

@Composable
fun MapCard(
    current: MapInfo?,
    file: File?,
    canFloorUp: Boolean,
    canFloorDown: Boolean,
    onFloorUp: () -> Unit,
    onFloorDown: () -> Unit,
    onFullscreen: () -> Unit,
    onClose: () -> Unit
) {
    // Map follows the next lesson automatically (◉ button on a lesson) — no manual picker.
    var resetSignal by remember { mutableStateOf(0) }
    Card(
        colors = CardDefaults.cardColors(containerColor = Panel),
        border = BorderStroke(1.dp, BorderDim),
        modifier = Modifier.fillMaxWidth()
    ) {
        Column(Modifier.padding(7.dp)) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Text("КАРТА", color = Bronze, fontSize = 10.sp, fontWeight = FontWeight.SemiBold)
                Spacer(Modifier.width(8.dp))
                Text(
                    current?.title ?: "—",
                    color = Marble, fontSize = 10.sp, fontWeight = FontWeight.SemiBold,
                    maxLines = 1, overflow = androidx.compose.ui.text.style.TextOverflow.Ellipsis,
                    modifier = Modifier.weight(1f)
                )
                TextButton(onClick = onClose) { Text("✕", color = MarbleDim, fontSize = 10.sp) }
            }
            if (current == null) {
                Text(
                    "Нажмите ◉ у пары — карта подберется сама",
                    color = MarbleDim, fontSize = 10.sp,
                    modifier = Modifier.padding(vertical = 4.dp)
                )
            } else if (!current.hasMap) {
                // Remote lesson or unknown room — no map exists, don't show an empty viewer.
                Text(
                    if (current.isRemote) "Дистанционно — карта не нужна"
                    else "Для аудитории ${current.classroomRaw.ifBlank { "—" }} карты нет",
                    color = MarbleDim, fontSize = 10.sp,
                    modifier = Modifier.padding(vertical = 4.dp)
                )
            } else {
                Spacer(Modifier.height(6.dp))
                ZoomableMapImage(file = file, contentDesc = current.title, resetSignal = resetSignal)
                Spacer(Modifier.height(6.dp))
                Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    OutlinedButton(
                        onClick = onFullscreen,
                        colors = ButtonDefaults.outlinedButtonColors(contentColor = Marble),
                        border = BorderStroke(1.5.dp, BorderDim)
                    ) { Text("⛶ На весь экран", fontSize = 10.sp) }
                    OutlinedButton(
                        onClick = { resetSignal++ },
                        colors = ButtonDefaults.outlinedButtonColors(contentColor = Marble),
                        border = BorderStroke(1.5.dp, BorderDim)
                    ) { Text("Сбросить вид", fontSize = 10.sp) }
                }
                Spacer(Modifier.height(6.dp))
                Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    GhostBtn(text = "Этаж ↑", onClick = onFloorUp, enabled = canFloorUp, modifier = Modifier.weight(1f))
                    GhostBtn(text = "Этаж ↓", onClick = onFloorDown, enabled = canFloorDown, modifier = Modifier.weight(1f))
                }
            }
        }
    }
}

@Composable
fun FullscreenMap(current: MapInfo?, file: File?, onClose: () -> Unit) {
    Dialog(
        onDismissRequest = onClose,
        properties = DialogProperties(usePlatformDefaultWidth = false)
    ) {
        Card(
            colors = CardDefaults.cardColors(containerColor = Obsidian),
            modifier = Modifier.fillMaxSize().padding(8.dp)
        ) {
            Column(Modifier.fillMaxSize().padding(8.dp)) {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Text(
                        current?.title ?: "Карта",
                        color = Marble, fontSize = 12.sp, fontWeight = FontWeight.Bold,
                        modifier = Modifier.weight(1f)
                    )
                    TextButton(onClick = onClose) { Text("✕ Закрыть", color = Bronze, fontSize = 11.sp) }
                }
                ZoomableMapImage(file = file, contentDesc = current?.title ?: "карта")
            }
        }
    }
}

@Composable
fun TeacherDialog(
    groupName: String,
    myGroupName: String,
    query: String,
    onQuery: (String) -> Unit,
    onlyMy: Boolean,
    onOnlyMy: (Boolean) -> Unit,
    teachers: List<LecturerInfo>,
    totalTeachers: Int = 0,
    weekParity: Int = 0,
    onWeekParity: (Int) -> Unit = {},
    selected: LecturerInfo?,
    onSelect: (LecturerInfo) -> Unit,
    onBack: () -> Unit,
    details: List<LecturerLesson>,
    isMy: (LecturerInfo) -> Boolean,
    onDismiss: () -> Unit
) {
    Dialog(onDismissRequest = onDismiss, properties = DialogProperties(usePlatformDefaultWidth = false)) {
        Card(
            colors = CardDefaults.cardColors(containerColor = Panel),
            border = BorderStroke(1.dp, BorderDim),
            modifier = Modifier.fillMaxSize().padding(10.dp)
        ) {
            if (selected == null) {
                // Full-width teacher list
                Column(Modifier.fillMaxSize().padding(10.dp)) {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Text("Преподаватели", color = Bronze, fontSize = 12.sp, fontWeight = FontWeight.SemiBold)
                        Spacer(Modifier.width(8.dp))
                        Text(groupName, color = MarbleDim, fontSize = 10.sp, modifier = Modifier.weight(1f))
                        TextButton(onClick = onDismiss) { Text("✕", color = MarbleDim, fontSize = 11.sp) }
                    }
                    OutlinedTextField(
                        value = query, onValueChange = onQuery,
                        label = { Text("Поиск", fontSize = 10.sp) },
                        singleLine = true,
                        modifier = Modifier.fillMaxWidth()
                    )
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Checkbox(checked = onlyMy, onCheckedChange = onOnlyMy)
                        Text("Только мои", color = Marble, fontSize = 11.sp)
                        Spacer(Modifier.width(8.dp))
                        Text(
                            if (totalTeachers > 0 && teachers.size < totalTeachers) "${teachers.size} из $totalTeachers"
                            else "${teachers.size}",
                            color = Bronze, fontSize = 10.sp
                        )
                    }
                    LazyColumn(modifier = Modifier.weight(1f)) {
                        if (teachers.isEmpty()) {
                            item { Text("Не найдено", color = MarbleDim, fontSize = 11.sp) }
                        }
                        items(teachers, key = { it.id }) { t ->
                            Card(
                                colors = CardDefaults.cardColors(containerColor = PanelAlt),
                                border = BorderStroke(1.dp, BorderDim),
                                modifier = Modifier.fillMaxWidth().padding(vertical = 3.dp),
                                onClick = { onSelect(t) }
                            ) {
                                Column(Modifier.padding(7.dp)) {
                                    Text(t.name, color = Marble, fontSize = 11.sp, fontWeight = FontWeight.SemiBold)
                                    Text(
                                        t.kafedra.ifBlank { "ID ${t.id}" },
                                        color = MarbleDim, fontSize = 9.sp
                                    )
                                }
                            }
                        }
                    }
                }
            } else {
                // Full-width week view of the selected teacher
                val todayDow = remember { java.time.LocalDate.now().dayOfWeek.value }
                Column(Modifier.fillMaxSize().padding(10.dp)) {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        GhostBtn(text = "← Назад", onClick = onBack, fontSize = 13.sp)
                        Spacer(Modifier.weight(1f))
                        TextButton(onClick = onDismiss) { Text("✕", color = MarbleDim, fontSize = 11.sp) }
                    }
                    Spacer(Modifier.height(2.dp))
                    Text(
                        selected.name, color = Marble, fontSize = 13.sp, fontWeight = FontWeight.Bold,
                        maxLines = 2, overflow = androidx.compose.ui.text.style.TextOverflow.Ellipsis
                    )
                    Text(
                        if (isMy(selected)) "Ведет у вашей группы" else "Не ведет у вашей группы",
                        color = if (isMy(selected)) Patina else MarbleDim, fontSize = 9.sp
                    )
                    Text("● зеленым — пары вашей группы", color = MarbleDim, fontSize = 9.sp)
                    Spacer(Modifier.height(6.dp))
                    Row(horizontalArrangement = Arrangement.spacedBy(6.dp)) {
                        if (weekParity == 0) FerryBtn(text = "Обе", onClick = {}, compact = true) else GhostBtn(text = "Обе", onClick = { onWeekParity(0) }, compact = true)
                        if (weekParity == 1) FerryBtn(text = "Нечет", onClick = {}, compact = true) else GhostBtn(text = "Нечет", onClick = { onWeekParity(1) }, compact = true)
                        if (weekParity == 2) FerryBtn(text = "Чет", onClick = {}, compact = true) else GhostBtn(text = "Чет", onClick = { onWeekParity(2) }, compact = true)
                    }
                    Spacer(Modifier.height(6.dp))
                    LazyColumn(
                        modifier = Modifier.weight(1f),
                        verticalArrangement = Arrangement.spacedBy(6.dp)
                    ) {
                        for (dow in 1..6) {
                            val dayLessons = details.filter {
                                it.dayOfWeek == dow && (weekParity == 0 || it.parity == 0 || it.parity == weekParity)
                            }.sortedBy { it.timeStart }
                            item(key = "dow$dow") {
                                TeacherDayCard(
                                    dow = dow,
                                    lessons = dayLessons,
                                    isToday = dow == todayDow,
                                    myGroupName = myGroupName
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
private fun TeacherDayCard(
    dow: Int,
    lessons: List<LecturerLesson>,
    isToday: Boolean,
    myGroupName: String
) {
    Card(
        colors = CardDefaults.cardColors(containerColor = Panel),
        border = BorderStroke(1.dp, if (isToday) Bronze else BorderDim)
    ) {
        Column(Modifier.padding(7.dp)) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Text(
                    Parity.dayNumberToTitle(dow).uppercase(),
                    color = Bronze, fontSize = 10.sp, fontWeight = FontWeight.SemiBold,
                    modifier = Modifier.weight(1f)
                )
                if (isToday) Text("сегодня", color = Patina, fontSize = 9.sp, fontWeight = FontWeight.SemiBold)
            }
            Spacer(Modifier.height(4.dp))
            if (lessons.isEmpty()) {
                Text("Нет занятий", color = MarbleDim, fontSize = 10.sp)
            } else {
                lessons.forEach { l ->
                    val mine = l.groups.any { it.number == myGroupName }
                    Row(Modifier.padding(vertical = 2.dp)) {
                        Text(
                            "${l.timeStart}\n${l.timeEnd}",
                            color = Marble, fontSize = 10.sp, lineHeight = 11.sp,
                            modifier = Modifier.width(52.dp)
                        )
                        Column(Modifier.weight(1f)) {
                            Text(
                                (if (mine) "● " else "") + l.disciplineRaw.ifEmpty { "—" },
                                color = if (mine) Patina else Marble, fontSize = 11.sp,
                                fontWeight = if (mine) FontWeight.SemiBold else FontWeight.Normal
                            )
                            val groups = l.groups.map { it.number }.filter { it.isNotEmpty() }.take(4)
                                .joinToString(", ")
                            Text(
                                "${l.classroomRaw.ifBlank { "—" }}" + if (groups.isNotEmpty()) " · $groups" else "",
                                color = MarbleDim, fontSize = 9.sp
                            )
                        }
                        Text(
                            if (l.parity == 1) "нечет" else if (l.parity == 2) "чет" else "обе",
                            color = Bronze, fontSize = 9.sp, modifier = Modifier.width(38.dp)
                        )
                    }
                }
            }
        }
    }
}
