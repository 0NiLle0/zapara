package ru.bgtu_voenmeh.zapara.ui

import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.background
import androidx.compose.foundation.horizontalScroll
import androidx.compose.foundation.verticalScroll
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ExperimentalLayoutApi
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.itemsIndexed
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.shadow
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import ru.bgtu_voenmeh.zapara.data.Lesson
import ru.bgtu_voenmeh.zapara.data.Homework
import ru.bgtu_voenmeh.zapara.data.Parity
import ru.bgtu_voenmeh.zapara.ui.theme.BorderDim
import ru.bgtu_voenmeh.zapara.ui.theme.Bronze
import ru.bgtu_voenmeh.zapara.ui.theme.Marble
import ru.bgtu_voenmeh.zapara.ui.theme.MarbleDim
import ru.bgtu_voenmeh.zapara.ui.theme.Obsidian
import ru.bgtu_voenmeh.zapara.ui.theme.Panel
import ru.bgtu_voenmeh.zapara.ui.theme.PanelAlt
import ru.bgtu_voenmeh.zapara.ui.theme.Patina

@Composable
fun ZaparaApp(vm: ScheduleViewModel) {
    val s = vm.state
    var groupSelectOpen by remember { mutableStateOf(false) }
    var mapSelectOpen by remember { mutableStateOf(false) }
    Column(
        modifier = Modifier
            .fillMaxSize()
            .background(Obsidian)
            .padding(10.dp)
    ) {
        // Header — single aligned line (app title dropped: launcher already shows it)
        Column {
            Text(
                "Группа ${s.groupName.ifEmpty { "—" }} · ${if (s.parityText == "НЕЧЕТНАЯ") "нечетная" else "четная"} неделя",
                color = MarbleDim, fontSize = 11.sp, maxLines = 1,
                overflow = androidx.compose.ui.text.style.TextOverflow.Ellipsis,
                modifier = Modifier.fillMaxWidth()
            )
            Row(
                modifier = Modifier
                    .fillMaxWidth()
                    .horizontalScroll(rememberScrollState()),
                horizontalArrangement = Arrangement.spacedBy(4.dp),
                verticalAlignment = Alignment.CenterVertically
            ) {
                TextButton(onClick = { vm.openDialog(UiDialog.Settings) }) {
                    Text("Настройки", color = Bronze, fontSize = 11.sp, maxLines = 1)
                }
                TextButton(onClick = { vm.openDialog(UiDialog.Friends) }) {
                    Text("Друзья", color = Bronze, fontSize = 11.sp, maxLines = 1)
                }
                TextButton(onClick = { vm.openTeachers() }) {
                    Text("Преподаватели", color = Bronze, fontSize = 11.sp, maxLines = 1)
                }
                TextButton(onClick = { mapSelectOpen = true }) {
                    Text("Карта", color = Bronze, fontSize = 11.sp, maxLines = 1)
                }
            }
        }
        Spacer(Modifier.height(8.dp))
        // Group selector — opens a search dialog (popup menus misplace on some devices)
        SelectField(
            value = s.groups.firstOrNull { it.id == s.groupId }?.name,
            placeholder = "Выбрать группу",
            onClick = { groupSelectOpen = true },
            modifier = Modifier.fillMaxWidth()
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
        // Standalone "Карта" button = building/floor chooser (per-lesson ◉ still jumps to its map).
        if (mapSelectOpen) {
            SearchSelectDialog(
                title = "Карта корпуса",
                searchLabel = "Поиск карты (ГК/УЛК)",
                items = vm.allMapPairs(),
                onSelect = { vm.selectMapByFile(it.first); mapSelectOpen = false },
                onDismiss = { mapSelectOpen = false }
            )
        }
        Spacer(Modifier.height(8.dp))
        // Tabs
        Row(
            modifier = Modifier.horizontalScroll(rememberScrollState()),
            horizontalArrangement = Arrangement.spacedBy(8.dp)
        ) {
            TabButton("Вчера", s.tab == Tab.Yesterday) { vm.selectTab(Tab.Yesterday) }
            TabButton("Сегодня", s.tab == Tab.Today) { vm.selectTab(Tab.Today) }
            TabButton("Завтра", s.tab == Tab.Tomorrow) { vm.selectTab(Tab.Tomorrow) }
            TabButton("Неделя", s.tab == Tab.Week) { vm.selectTab(Tab.Week) }
            TabButton("Сводка", s.tab == Tab.Summary) { vm.selectTab(Tab.Summary) }
        }
        Spacer(Modifier.height(8.dp))
        // Date + parity + week
        Row(verticalAlignment = Alignment.CenterVertically) {
            if (s.dateText.isNotEmpty()) Text(s.dateText, color = MarbleDim, fontSize = 11.sp)
            Spacer(Modifier.width(8.dp))
            Text(
                s.parityText, color = Bronze, fontSize = 10.sp, fontWeight = FontWeight.SemiBold,
                modifier = Modifier
                    .padding(0.dp)
            )
            Spacer(Modifier.width(8.dp))
            if (s.weekText.isNotEmpty()) Text(s.weekText, color = MarbleDim, fontSize = 10.sp)
        }
        if (s.tab == Tab.Week) {
            Spacer(Modifier.height(8.dp))
            Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                TabButton("Нечетная", s.weekParity == 1) { vm.selectWeekParity(1) }
                TabButton("Четная", s.weekParity == 2) { vm.selectWeekParity(2) }
            }
        }
        Spacer(Modifier.height(8.dp))
        if (s.mapVisible) {
            val curMap = s.currentMap
            val mapFloors = remember(curMap) {
                curMap?.takeIf { it.hasMap }?.let { vm.floorsForBuilding(it.building) }.orEmpty()
            }
            val floorIdx = mapFloors.indexOf(curMap?.floor)
            MapCard(
                current = curMap,
                file = s.mapPath?.let { java.io.File(it) }?.takeIf { it.exists() },
                canFloorUp = curMap?.hasMap == true && floorIdx in 0 until mapFloors.lastIndex,
                canFloorDown = curMap?.hasMap == true && floorIdx > 0,
                onFloorUp = { vm.mapFloorStep(1) },
                onFloorDown = { vm.mapFloorStep(-1) },
                onFullscreen = { vm.setFullscreen(true) },
                onClose = { vm.toggleMap() }
            )
            Spacer(Modifier.height(8.dp))
        }
        when {
            s.loading -> Text("Загрузка...", color = MarbleDim, fontSize = 11.sp)
            s.error != null -> Text("Ошибка: ${s.error}", color = Marble, fontSize = 11.sp)
            s.tab == Tab.Week -> WeekView(vm)
            s.tab == Tab.Summary -> SummaryView(s.summary)
            else -> DayView(vm)
        }
    }
    // Dialogs live at screen level so they open from any tab (day/week/summary),
    // not just DayView where they used to be scoped.
    when (val d = s.dialog) {
        is UiDialog.Rename -> {
            val l = s.lessons.getOrNull(d.lessonIndex)
            if (l != null) {
                RenameDialog(
                    lesson = l,
                    initialName = d.initialName,
                    initialNote = d.initialNote,
                    initialGlobal = true,
                    onSave = { name, note, global -> vm.saveRename(l, name, note, global) },
                    onReset = { vm.resetRename(l) },
                    onDismiss = { vm.closeDialog() }
                )
            }
        }
        is UiDialog.Homework -> {
            val l = s.lessons.getOrNull(d.lessonIndex)
            if (l != null) {
                HomeworkDialog(
                    lesson = l,
                    duePreview = { n -> vm.hwDuePreview(l, n) },
                    onSave = { text, n -> vm.saveHomework(l, text, n) },
                    onDismiss = { vm.closeDialog() }
                )
            }
        }
        is UiDialog.HomeworkEdit -> {
            val l = s.lessons.getOrNull(d.lessonIndex)
            val hw = s.hwMap.values.flatten().firstOrNull { it.id == d.hwId }
            if (l != null && hw != null) {
                HomeworkDialog(
                    lesson = l,
                    duePreview = { n -> vm.hwDuePreview(l, n) },
                    onSave = { text, n -> vm.updateHomework(hw.id, text, n) },
                    onDismiss = { vm.closeDialog() },
                    initialText = hw.text,
                    initialN = hw.n
                )
            }
        }
        is UiDialog.Friends -> {
            FriendsDialog(
                friends = s.friends,
                allGroups = s.groups,
                alwaysShow = s.alwaysShow,
                invertParity = s.invert,
                onToggleAlwaysShow = { vm.toggleAlwaysShow(it) },
                onToggleInvert = { vm.toggleInvert(it) },
                onAdd = { vm.addFriend(it) },
                onRemove = { vm.removeFriend(it) },
                onSaveNames = { f, names -> vm.saveMemberNames(f, names) },
                onDismiss = { vm.closeDialog() }
            )
        }
        is UiDialog.Settings -> {
            SettingsDialog(vm, onDismiss = { vm.closeDialog() })
        }
        is UiDialog.Teachers -> {
            TeacherDialog(
                groupName = s.groupName,
                myGroupName = s.groupName,
                query = s.teacherQuery,
                onQuery = { vm.teacherQuery(it) },
                onlyMy = s.teacherOnlyMy,
                onOnlyMy = { vm.teacherOnlyMy(it) },
                teachers = s.teacherList,
                totalTeachers = s.teacherTotal,
                weekParity = s.teacherWeekParity,
                onWeekParity = { vm.teacherWeekParity(it) },
                selected = s.teacherSelected,
                onSelect = { vm.selectTeacher(it) },
                onBack = { vm.deselectTeacher() },
                details = s.teacherDetails,
                isMy = { vm.isMyTeacher(it) },
                onDismiss = { vm.closeDialog() }
            )
        }
        is UiDialog.None -> {}
    }
    if (s.fullscreenMap) {
        FullscreenMap(
            current = s.currentMap,
            file = s.mapPath?.let { java.io.File(it) }?.takeIf { it.exists() },
            onClose = { vm.setFullscreen(false) }
        )
    }
}

@Composable
private fun TabButton(text: String, selected: Boolean, onClick: () -> Unit) {
    if (selected) {
        androidx.compose.material3.Button(
            onClick = onClick,
            colors = ButtonDefaults.buttonColors(containerColor = PanelAlt, contentColor = Bronze),
            border = BorderStroke(1.5.dp, Bronze)
        ) { Text(text, fontSize = 11.sp) }
    } else {
        OutlinedButton(
            onClick = onClick,
            colors = ButtonDefaults.outlinedButtonColors(contentColor = Marble),
            border = BorderStroke(1.5.dp, BorderDim)
        ) { Text(text, fontSize = 11.sp) }
    }
}

@Composable
private fun DayView(vm: ScheduleViewModel) {
    val s = vm.state
    android.util.Log.d("ZaparaApp", "DayView compose tab=${s.tab} lessons=${s.lessons.size}")
    val lessons = s.lessons
    if (lessons.isEmpty()) {
        Text("Нет занятий", color = MarbleDim, fontSize = 11.sp)
        return
    }
    Column(
        modifier = Modifier
            .fillMaxSize()
            .verticalScroll(rememberScrollState()),
        verticalArrangement = Arrangement.spacedBy(6.dp)
    ) {
        // Header row mirrors LessonCard columns exactly (№ 22 / time 54 / subject weight / room 68)
        // so nothing squeezes or scrolls on 360dp.
        Card(
            colors = CardDefaults.cardColors(containerColor = PanelAlt),
            border = BorderStroke(1.dp, BorderDim)
        ) {
            Row(
                Modifier.padding(7.dp, 4.dp),
                verticalAlignment = Alignment.CenterVertically
            ) {
                Text("№", color = Bronze, fontSize = 10.sp, fontWeight = FontWeight.SemiBold, modifier = Modifier.width(22.dp), maxLines = 1)
                Text("Время", color = Bronze, fontSize = 10.sp, fontWeight = FontWeight.SemiBold, modifier = Modifier.width(54.dp), maxLines = 1)
                Text("Предмет", color = Bronze, fontSize = 10.sp, fontWeight = FontWeight.SemiBold, modifier = Modifier.weight(1f), maxLines = 1, overflow = androidx.compose.ui.text.style.TextOverflow.Ellipsis)
                Text("Ауд.", color = Bronze, fontSize = 10.sp, fontWeight = FontWeight.SemiBold, modifier = Modifier.width(68.dp), maxLines = 1)
            }
        }
        lessons.forEachIndexed { order, l ->
            LessonCard(
                number = order + 1,
                lesson = l,
                displayName = vm.displayName(l),
                note = vm.displayNote(l),
                next = s.nextMap[l.subjectNormalized] ?: "—",
                dots = s.traffic.getOrElse(order) { emptyList() },
                homeworks = s.hwMap[l.subjectNormalized].orEmpty(),
                onRename = { vm.openRename(l) },
                onHomework = { vm.openDialog(UiDialog.Homework(order)) },
                onMap = { vm.showMapFor(l) },
                onHwToggle = { hw -> vm.toggleHomework(hw.id, hw.status != "done") },
                onHwEdit = { hw -> vm.openDialog(UiDialog.HomeworkEdit(order, hw.id)) },
                onHwDelete = { hw -> vm.deleteHomework(hw.id) }
            )
        }
    }
}

@OptIn(ExperimentalLayoutApi::class)
@Composable
private fun TrafficDots(dots: List<TrafficDot>) {
    // Wrap to next line on narrow screens — was fixed 80dp width causing overflow
    androidx.compose.foundation.layout.FlowRow(
        horizontalArrangement = Arrangement.spacedBy(8.dp),
        verticalArrangement = Arrangement.spacedBy(2.dp),
        modifier = Modifier.fillMaxWidth()
    ) {
        if (dots.isEmpty()) {
            Row(verticalAlignment = Alignment.CenterVertically) { OffDot(); Spacer(Modifier.width(4.dp)); Text("— нет рядом", color = MarbleDim, fontSize = 9.sp) }
        } else {
            dots.take(5).forEach { d ->
                Row(verticalAlignment = Alignment.CenterVertically, modifier = Modifier.padding(vertical = 1.dp)) {
                    if (d.score < 0) {
                        OffDot()
                    } else {
                        val (fill, glow) = when {
                            d.score >= 100 -> Patina to Patina
                            d.score >= 75 -> androidx.compose.ui.graphics.Color(0xFFA8E6A0) to androidx.compose.ui.graphics.Color(0xFFA8E6A0)
                            d.score >= 50 -> androidx.compose.ui.graphics.Color(0xFFF2C55C) to androidx.compose.ui.graphics.Color(0xFFF2C55C)
                    else -> androidx.compose.ui.graphics.Color(0xFF6CA5E0) to androidx.compose.ui.graphics.Color(0xFF6CA5E0)
                    }
                    Box(
                        modifier = Modifier
                            .width(10.dp)
                            .height(10.dp)
                            .shadow(4.dp, CircleShape, true, glow, glow)
                            .background(fill, CircleShape)
                    )
                    }
                    Spacer(Modifier.width(4.dp))
                    val label = if (d.memberNames.isBlank()) d.friendGroup
                    else "${d.friendGroup} (${d.memberNames})"
                    Text(
                        label, color = MarbleDim, fontSize = 9.sp, maxLines = 1, overflow = androidx.compose.ui.text.style.TextOverflow.Ellipsis
                    )
                }
            }
        }
    }
}

@Composable
private fun OffDot() {
    Box(
        modifier = Modifier
            .width(12.dp)
            .height(12.dp)
            .background(
                androidx.compose.ui.graphics.Color(0xFF1E252E),
                androidx.compose.foundation.shape.CircleShape
            )
    )
}

@Composable
private fun LessonCard(
    number: Int,
    lesson: Lesson,
    displayName: String,
    note: String,
    next: String,
    dots: List<TrafficDot>,
    homeworks: List<Homework>,
    onRename: () -> Unit,
    onHomework: () -> Unit,
    onMap: () -> Unit,
    onHwToggle: (Homework) -> Unit,
    onHwEdit: (Homework) -> Unit,
    onHwDelete: (Homework) -> Unit
) {
    Card(
        colors = CardDefaults.cardColors(containerColor = Panel),
        border = BorderStroke(1.dp, BorderDim)
    ) {
        Column(Modifier.padding(7.dp)) {
            // Top row — subject + time/classroom inline, wraps only subject, other cols fixed
            Row(verticalAlignment = Alignment.Top) {
                Text(number.toString(), color = MarbleDim, fontSize = 11.sp, modifier = Modifier.width(22.dp))
                Text(
                    if (lesson.timeStart.isEmpty()) "—" else "${lesson.timeStart}\n${lesson.timeEnd}",
                    color = Marble, fontSize = 11.sp, lineHeight = 12.sp, modifier = Modifier.width(54.dp)
                )
                Column(Modifier.weight(1f).padding(end = 6.dp)) {
                    val subj = buildString {
                        if (lesson.typeRaw.isNotEmpty()) append("[${lesson.typeRaw}] ")
                        append(displayName.ifEmpty { "—" })
                    }
                    Text(
                        subj, color = Marble, fontSize = 11.sp,
                        fontWeight = if (displayName != lesson.subjectRaw) FontWeight.SemiBold else FontWeight.Normal
                    )
                    if (displayName != lesson.subjectRaw) {
                        Text(lesson.subjectRaw, color = MarbleDim, fontSize = 9.sp, maxLines = 1, overflow = androidx.compose.ui.text.style.TextOverflow.Ellipsis)
                    }
                    if (note.isNotEmpty()) {
                        Text(note, color = Bronze, fontSize = 9.sp, fontStyle = androidx.compose.ui.text.font.FontStyle.Italic, maxLines = 2, overflow = androidx.compose.ui.text.style.TextOverflow.Ellipsis)
                    }
                    Text(
                        lesson.teacherRaw.ifEmpty { "—" },
                        color = MarbleDim, fontSize = 10.sp, maxLines = 1, overflow = androidx.compose.ui.text.style.TextOverflow.Ellipsis
                    )
                }
                Column(horizontalAlignment = Alignment.End, modifier = Modifier.width(68.dp)) {
                    Text(
                        lesson.classroomRaw.ifEmpty { "—" },
                        color = MarbleDim, fontSize = 11.sp, maxLines = 1, overflow = androidx.compose.ui.text.style.TextOverflow.Ellipsis
                    )
                    Text(next, color = Patina, fontSize = 9.sp, maxLines = 1, overflow = androidx.compose.ui.text.style.TextOverflow.Ellipsis)
                }
            }
            Spacer(Modifier.height(6.dp))
            Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.SpaceBetween, modifier = Modifier.fillMaxWidth()) {
                Box(modifier = Modifier.weight(1f)) {
                    TrafficDots(dots)
                }
                Row(horizontalArrangement = Arrangement.spacedBy(6.dp)) {
                    GhostBtn(text = "✎", onClick = onRename, fontSize = 15.sp)
                    GhostBtn(text = "+", onClick = onHomework, fontSize = 15.sp)
                    GhostBtn(text = String(Character.toChars(0x25C9)), onClick = onMap, fontSize = 15.sp)
                }
            }
            homeworks.forEach { hw ->
                HomeworkRow(hw = hw, onToggle = { onHwToggle(hw) }, onEdit = { onHwEdit(hw) }, onDelete = { onHwDelete(hw) })
            }
        }
    }
}

@Composable
private fun WeekView(vm: ScheduleViewModel) {
    val s = vm.state
    LazyColumn(verticalArrangement = Arrangement.spacedBy(6.dp)) {
        for (dow in 1..6) {
            item {
                DayCard(dow = dow, lessons = vm.weekLessons(dow, s.weekParity))
            }
        }
    }
}

@Composable
private fun DayCard(dow: Int, lessons: List<Lesson>) {
    Card(
        colors = CardDefaults.cardColors(containerColor = Panel),
        border = BorderStroke(1.dp, BorderDim)
    ) {
        Column(Modifier.padding(7.dp)) {
            Text(
                Parity.dayNumberToTitle(dow).uppercase(),
                color = Bronze, fontSize = 10.sp, fontWeight = FontWeight.SemiBold
            )
            Spacer(Modifier.height(4.dp))
            if (lessons.isEmpty()) {
                Text("Нет занятий", color = MarbleDim, fontSize = 10.sp)
            } else {
                lessons.forEach { l ->
                    Row(Modifier.padding(vertical = 2.dp)) {
                        Text(l.timeStart, color = Marble, fontSize = 10.sp, modifier = Modifier.width(52.dp))
                        Text(
                            l.subjectRaw.ifEmpty { "—" },
                            color = Marble, fontSize = 10.sp, modifier = Modifier.weight(1f)
                        )
                        Text(
                            l.classroomRaw.ifEmpty { "—" },
                            color = MarbleDim, fontSize = 10.sp, modifier = Modifier.width(64.dp)
                        )
                    }
                }
            }
        }
    }
}

@Composable
private fun SummaryView(sections: List<SummarySection>) {
    LazyColumn(verticalArrangement = Arrangement.spacedBy(10.dp)) {
        sections.forEach { sec ->
            item { SummaryCard(sec) }
        }
    }
}

@Composable
private fun SummaryCard(sec: SummarySection) {
    Card(
        colors = CardDefaults.cardColors(containerColor = Panel),
        border = BorderStroke(1.dp, BorderDim)
    ) {
        Column(Modifier.padding(10.dp)) {
            Text(
                "${sec.title.uppercase()} — ${sec.total} пар",
                color = Bronze, fontSize = 11.sp, fontWeight = FontWeight.Bold
            )
            Spacer(Modifier.height(6.dp))
            Text(
                "По дням: " + sec.byDay.joinToString(" · ") { "${it.first} ${it.second}" },
                color = Marble, fontSize = 10.sp
            )
            Spacer(Modifier.height(4.dp))
            Text(
                "По типу: " + sec.byType.joinToString(" · ") { "${it.first} ${it.second}" },
                color = MarbleDim, fontSize = 10.sp
            )
            Spacer(Modifier.height(4.dp))
            Text("По предметам:", color = MarbleDim, fontSize = 9.sp)
            sec.bySubject.forEach { (name, count) ->
                Row(Modifier.fillMaxWidth()) {
                    Text(count.toString(), color = Bronze, fontSize = 10.sp, modifier = Modifier.width(30.dp))
                    Text(name, color = Marble, fontSize = 10.sp, modifier = Modifier.weight(1f))
                }
            }
            Spacer(Modifier.height(4.dp))
            Text("По преподавателям:", color = MarbleDim, fontSize = 9.sp)
            sec.byTeacher.forEach { (name, count) ->
                Row(Modifier.fillMaxWidth()) {
                    Text(count.toString(), color = Patina, fontSize = 10.sp, modifier = Modifier.width(30.dp))
                    Text(name, color = Marble, fontSize = 10.sp, modifier = Modifier.weight(1f))
                }
            }
            if (sec.roomsLine.isNotEmpty()) {
                Spacer(Modifier.height(4.dp))
                Text("Аудитории: ${sec.roomsLine}", color = MarbleDim, fontSize = 9.sp)
            }
        }
    }
}


