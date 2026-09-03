package ru.bgtu_voenmeh.zapara.data

// Minimal fixture mirroring TimetableGroup50.xml schema (group 3313 А863С + one extra group).
const val GROUP_FIXTURE = """<Timetable>
  <Period Title="ОСЕННИЙ СЕМЕСТР 2026/2027 уч. г." StartYear="2026" StartMonth="9" StartDay="1" />
  <Weeks WeekCount="2" />
  <Group Number="А863С" IdGroup="3313">
    <Days>
      <Day Title="Понедельник">
        <GroupLessons>
          <Lesson>
            <DayTitle>Понедельник</DayTitle>
            <WeekCode>1</WeekCode>
            <Time>9:00 Нечетная</Time>
            <Discipline>лек ВЫСШ. МАТЕМАТ</Discipline>
            <Lecturers><Lecturer><IdLecturer>1287</IdLecturer><ShortName>Барт Е.Л.</ShortName></Lecturer></Lecturers>
            <Classroom>493;</Classroom>
          </Lesson>
          <Lesson>
            <DayTitle>Понедельник</DayTitle>
            <WeekCode>1</WeekCode>
            <Time>12:40 Нечетная</Time>
            <Discipline>пр ОСН РОС ГОС</Discipline>
            <Lecturers><Lecturer><IdLecturer>1609</IdLecturer><ShortName>Лысенко Е.М.</ShortName></Lecturer></Lecturers>
            <Classroom>563*;</Classroom>
          </Lesson>
          <Lesson>
            <DayTitle>Понедельник</DayTitle>
            <WeekCode>2</WeekCode>
            <Time>9:00 Четная</Time>
            <Discipline>лек ВЫСШ. МАТЕМАТ</Discipline>
            <Lecturers><Lecturer><IdLecturer>1287</IdLecturer><ShortName>Барт Е.Л.</ShortName></Lecturer></Lecturers>
            <Classroom>493;</Classroom>
          </Lesson>
        </GroupLessons>
      </Day>
      <Day Title="Вторник">
        <GroupLessons>
          <Lesson>
            <DayTitle>Вторник</DayTitle>
            <WeekCode>1</WeekCode>
            <Time>10:50 Нечетная</Time>
            <Discipline>пр ЭК ПО ФК И СПОРТУ</Discipline>
            <Lecturers />
            <Classroom></Classroom>
          </Lesson>
        </GroupLessons>
      </Day>
      <Day Title="Среда">
        <GroupLessons>
          <Lesson>
            <DayTitle>Среда</DayTitle>
            <WeekCode>1</WeekCode>
            <Time>9:00 Нечетная</Time>
            <Discipline>лек ИСТОРИЯ</Discipline>
            <Lecturers><Lecturer><IdLecturer>1111</IdLecturer><ShortName>Попова В.В.</ShortName></Lecturer></Lecturers>
            <Classroom>526*;</Classroom>
          </Lesson>
          <Lesson>
            <DayTitle>Среда</DayTitle>
            <WeekCode>2</WeekCode>
            <Time>14:55 Четная</Time>
            <Discipline>пр ВЫСШ. МАТЕМАТ</Discipline>
            <Lecturers><Lecturer><IdLecturer>1131</IdLecturer><ShortName>Волченкова Н.М.</ShortName></Lecturer></Lecturers>
            <Classroom>ВЦ 280;</Classroom>
          </Lesson>
        </GroupLessons>
      </Day>
      <Day Title="Суббота">
        <GroupLessons>
          <Lesson>
            <DayTitle>Суббота</DayTitle>
            <WeekCode>1</WeekCode>
            <Time>12:40 Нечетная</Time>
            <Discipline>лек ФК И СПОРТ</Discipline>
            <Lecturers><Lecturer><IdLecturer>2222</IdLecturer><ShortName>Петров А.Б.</ShortName></Lecturer></Lecturers>
            <Classroom>дистанционно</Classroom>
          </Lesson>
        </GroupLessons>
      </Day>
    </Days>
  </Group>
  <Group Number="09С31" IdGroup="3031" />
</Timetable>"""

const val LECTURER_FIXTURE = """<Timetable>
  <Period Title="ОСЕННИЙ СЕМЕСТР 2026/2027 уч. г." StartYear="2026" StartMonth="9" StartDay="1" />
  <Weeks WeekCount="2" />
  <Lecturer IdLecturer="1287" LecturerName="Барт Елена Леонидовна" Kafedra="Б1">
    <Days>
      <Day Title="Понедельник">
        <LecturerLessons>
          <Lesson>
            <DayTitle>Понедельник</DayTitle>
            <WeekCode>1</WeekCode>
            <Time>9:00 Нечетная</Time>
            <Discipline>лек ВЫСШ. МАТЕМАТ</Discipline>
            <Classroom>493;</Classroom>
            <Groups>
              <Group><IdGroup>3313</IdGroup><Number>А863С</Number></Group>
              <Group><IdGroup>3048</IdGroup><Number>А864С</Number></Group>
            </Groups>
          </Lesson>
        </LecturerLessons>
      </Day>
      <Day Title="Вторник">
        <LecturerLessons>
          <Lesson>
            <DayTitle>Вторник</DayTitle>
            <WeekCode>1</WeekCode>
            <Time>10:50 Нечетная</Time>
            <Discipline>пр ВЫСШ. МАТЕМАТ</Discipline>
            <Classroom>Е452Б;</Classroom>
            <Groups>
              <Group><IdGroup>9999</IdGroup><Number>Е452Б</Number></Group>
            </Groups>
          </Lesson>
        </LecturerLessons>
      </Day>
    </Days>
  </Lecturer>
</Timetable>"""
