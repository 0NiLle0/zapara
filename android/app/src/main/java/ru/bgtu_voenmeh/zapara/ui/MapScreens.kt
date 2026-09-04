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
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.ExposedDropdownMenuBox
import androidx.compose.material3.ExposedDropdownMenuDefaults
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
import androidx.compose.ui.focus.FocusRequester
import androidx.compose.ui.focus.focusRequester
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
import java.io.File

@Composable
fun ZoomableMapImage(file: File?, contentDesc: String) {
    var scale by remember { mutableFloatStateOf(1f) }
    var offset by remember { mutableStateOf(Offset.Zero) }
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
    Box(
        modifier = Modifier
            .fillMaxWidth()
            .height(260.dp)
            .transformable(state),
        contentAlignment = Alignment.Center
    ) {
        if (bitmap != null) {
            Image(
                bitmap = bitmap,
                contentDescription = contentDesc,
                modifier = Modifier.graphicsLayer(
                    scaleX = scale, scaleY = scale,
                    translationX = offset.x, translationY = offset.y
                )
            )
        } else {
            Text("Карта не загружена", color = MarbleDim, fontSize = 10.sp)
        }
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun MapCard(
    maps: List<MapInfo>,
    current: MapInfo?,
    file: File?,
    onPick: (MapInfo) -> Unit,
    onFullscreen: () -> Unit,
    onClose: () -> Unit
) {
    var expanded by remember { mutableStateOf(false) }
    var mapQuery by remember { mutableStateOf("") }
    val filteredMaps = remember(maps, mapQuery) {
        val q = mapQuery.trim()
        if (q.isEmpty()) maps
        else maps.filter { it.title.contains(q, ignoreCase = true) || it.fileName.contains(q, ignoreCase = true) }
    }
    val mapSearchFocus = remember { FocusRequester() }
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
                    modifier = Modifier.weight(1f)
                )
                TextButton(onClick = onClose) { Text("✕", color = MarbleDim, fontSize = 10.sp) }
            }
            ExposedDropdownMenuBox(expanded = expanded, onExpandedChange = {
                expanded = !expanded
                if (!expanded) mapQuery = ""
            }) {
                OutlinedTextField(
                    value = current?.title ?: "Все карты",
                    onValueChange = {}, readOnly = true,
                    label = { Text("Карта корпуса", fontSize = 9.sp) },
                    trailingIcon = { ExposedDropdownMenuDefaults.TrailingIcon(expanded = expanded) },
                    modifier = Modifier.menuAnchor().fillMaxWidth()
                )
                ExposedDropdownMenu(
                    expanded = expanded,
                    onDismissRequest = { expanded = false; mapQuery = "" }
                ) {
                    OutlinedTextField(
                        value = mapQuery, onValueChange = { mapQuery = it },
                        label = { Text("Поиск карты", fontSize = 9.sp) },
                        leadingIcon = { Text("⌕", color = MarbleDim, fontSize = 12.sp) },
                        trailingIcon = {
                            if (mapQuery.isNotEmpty()) TextButton(onClick = { mapQuery = "" }) {
                                Text("✕", color = MarbleDim, fontSize = 10.sp)
                            }
                        },
                        singleLine = true,
                        modifier = Modifier
                            .fillMaxWidth()
                            .padding(horizontal = 8.dp, vertical = 4.dp)
                            .focusRequester(mapSearchFocus)
                    )
                    if (filteredMaps.isEmpty()) {
                        Text("Не найдено", color = MarbleDim, fontSize = 11.sp, modifier = Modifier.padding(12.dp))
                    } else {
                        filteredMaps.forEach { m ->
                            DropdownMenuItem(
                                text = { Text(m.title, color = Marble, fontSize = 11.sp) },
                                onClick = { onPick(m); expanded = false; mapQuery = "" }
                            )
                        }
                    }
                }
            }
            LaunchedEffect(expanded) {
                if (expanded) {
                    try { mapSearchFocus.requestFocus() } catch (_: Exception) { }
                }
            }
            Spacer(Modifier.height(6.dp))
            ZoomableMapImage(file = file, contentDesc = current?.title ?: "карта")
            Spacer(Modifier.height(6.dp))
            Row {
                OutlinedButton(
                    onClick = onFullscreen,
                    colors = ButtonDefaults.outlinedButtonColors(contentColor = Marble),
                    border = BorderStroke(1.dp, BorderDim)
                ) { Text("⛶ На весь экран", fontSize = 10.sp) }
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
    query: String,
    onQuery: (String) -> Unit,
    onlyMy: Boolean,
    onOnlyMy: (Boolean) -> Unit,
    teachers: List<LecturerInfo>,
    selected: LecturerInfo?,
    onSelect: (LecturerInfo) -> Unit,
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
                    modifier = Modifier.fillMaxWidth()
                )
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Checkbox(checked = onlyMy, onCheckedChange = onOnlyMy)
                    Text("Только мои", color = Marble, fontSize = 11.sp)
                    Spacer(Modifier.width(8.dp))
                    Text("${teachers.size}", color = Bronze, fontSize = 10.sp)
                }
                Row(Modifier.weight(1f)) {
                    LazyColumn(modifier = Modifier.weight(1f)) {
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
                    Spacer(Modifier.width(6.dp))
                    LazyColumn(modifier = Modifier.weight(1.4f)) {
                        if (selected == null) {
                            item { Text("Выберите преподавателя", color = MarbleDim, fontSize = 11.sp) }
                        } else {
                            item {
                                Text(selected.name, color = Marble, fontSize = 13.sp, fontWeight = FontWeight.Bold)
                                Text(
                                    if (isMy(selected)) "Ведет у вашей группы" else "Не ведет у вашей группы",
                                    color = MarbleDim, fontSize = 9.sp
                                )
                                Spacer(Modifier.height(6.dp))
                            }
                            val bySubj = details.groupBy { it.disciplineRaw }.toSortedMap()
                            bySubj.forEach { (subj, list) ->
                                item {
                                    Card(
                                        colors = CardDefaults.cardColors(containerColor = PanelAlt),
                                        border = BorderStroke(1.dp, BorderDim),
                                        modifier = Modifier.fillMaxWidth().padding(vertical = 4.dp)
                                    ) {
                                        Column(Modifier.padding(7.dp)) {
                                            Text(subj, color = Bronze, fontSize = 11.sp, fontWeight = FontWeight.SemiBold)
                                            list.sortedWith(compareBy({ it.dayOfWeek }, { it.parity }, { it.timeStart }))
                                                .forEach { l ->
                                                    val day = Parity.dayNumberToTitle(l.dayOfWeek)
                                                    val par = if (l.parity == 1) "нечет" else if (l.parity == 2) "чет" else "—"
                                                    val groups = l.groups.map { it.number }.filter { it.isNotEmpty() }.take(4)
                                                        .joinToString(", ")
                                                    Text(
                                                        "$day ${l.timeStart}-${l.timeEnd} ($par) · ${l.classroomRaw.ifBlank { "—" }} · $groups",
                                                        color = Marble, fontSize = 10.sp
                                                    )
                                                }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
