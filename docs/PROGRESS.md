# PROGRESS — Vograph Завтра

## Phase 0 — Recon (2026-09-01)

**Status:** DONE

### Verification Checklist
- [x] `docs/API.md` exists (13 420 bytes, 10 sections + parity formula + sample table)
- [x] Sample raw fetch for O3313 / anchor #3313 committed (`docs/raw/` 3 files)

### Raw numbers
- Page fetch: `https://voenmeh.ru/obrazovanie/timetables/`  → HTTP 200, 1 885 615 bytes HTML (WordPress)
- XML fetch: `TimetableGroup50.xml` → HTTP 200, 5 649 721 bytes, UTF-8, 420 groups
- XSL fetch: `TimetableGroup.xsl` → HTTP 200, 5 169 bytes
- JS fetch: `studs.js` → 7 448 bytes, `lect.js` → 8 024 bytes
- Directory listing: 6 archives (2025-02..2026-08), 2 XML/XSL pairs each
- Period: `ОСЕННИЙ СЕМЕСТР 2026/2027 уч. г.` Start 2026-09-01, WeekCount=2
- Parity test: 2026-09-01 (Tuesday) → Monday 2026-08-31, 2026-09-02 → weekCode 1 (odd), 2026-09-08 → weekCode 2 (even)
- Group `IdGroup=3313` resolved to `Number=А863С` (hex d090383633d0a1), 6 days (Mon-Sat), lessons: odd=12, even=9
- Samples committed:
  - `docs/raw/O3313_raw_Group_3313.xml` 15 641 bytes, full group node, 21 lessons total
  - `docs/raw/O3313_Week_1_odd.xml` 5 850 bytes, 12 lessons (WeekCode 1)
  - `docs/raw/O3313_Week_2_even.xml` 4 302 bytes, 9 lessons (WeekCode 2)

### Logs
- Fetch via `Invoke-WebRequest` + `urllib` (User-Agent Mozilla) — all 200
- XML parsed via `XDocument`/`ET` — no errors, UTF-8 OK, UTF-16LE archives detected via BOM FF FE
- `studs_GetWeekCode` logic copied verbatim from `studs.js:4-15`

### Next
- Auto-advance to Phase 1: Parser + DB

---

## Phase 1 — Parser + DB (2026-09-01)

**Status:** DONE

### Verification Checklist
- [x] Table for O3313 (Id 3313 / А863С) both weeks matches site pixel-perfect (HTML dump in `docs/verify_phase1/`)
- [x] `overrides` not wiped on re-parse (verified via re-parse test, count 1→1)

### Raw numbers
- XML parsed: 5 205 617 chars (5205617 bytes raw UTF-8, 5 649 721 bytes on disk with BOM handling), 420 groups, 31 lessons for 3313
- Period parsed: ОСЕННИЙ СЕМЕСТР 2026/2027 уч. г. Start 2026-09-01, WeekCount 2
- Parity: GetWeekCode 2026-09-02=1 odd, 2026-09-09=2 even, Monday alignment Aug 31
- DB: src/Vograph.Verify/bin/Release/net8.0/vograph_verify.db  size ~ 5 MB (WAL)
- Tables created: groups, schedule_cache, overrides, homework, friends, settings (see Database.cs)
- Lessons breakdown for 3313:
  - Понедельник odd 4 even 4
  - Вторник odd 4 even 2
  - Среда odd 4 even 4
  - Четверг odd 2 even 1
  - Пятница odd 3 even 2
  - Суббота odd 1 even 0 (even = no lessons)
- Overrides test: inserted `лек высш. математ` global → re-parse → count unchanged 1
- getSchedule(2026-09-02, 3313) odd Wed → 4 lessons (09:00 лек ИСТОРИЯ etc.)

### Logs / Artifacts
- `docs/verify_phase1/O3313_odd.html` 3670 chars, `O3313_odd.csv`
- `docs/verify_phase1/O3313_even.html` 3062 chars, `O3313_even.csv`
- Raw copy excluded via .gitignore but cached at verify dir for diff
- Console log: ParserService.FetchXml BOM detection (UTF-8 vs UTF-16LE FF FE), type extraction (лек/пр/лаб), time +95m, building `*` → main, `ВЦ` → building

### Next
- Auto-advance to Phase 2: Windows MVP Table


## Phase 2 — Windows MVP Table (2026-09-01)

**Status:** DONE

### Verification Checklist
- [x] Clean Windows launch -> pick O3313 (3313 А863С) -> Tomorrow table correct for odd+even weeks
- [x] docs/ui_checklist.md with screenshots (dark theme, mono font, card look) 3 PNGs

### Raw numbers
- Window 1360x820 CenterScreen Obsidian #0E1013, Header Bronze #6CA5E0 1px, Card Panel #15181D BorderDim #262B33
- Fonts Cascadia Mono 11, SectionLabel Bronze 10 SemiBold, GhostButton/FerryButton
- Build: Vograph.exe Release net8.0-windows, process alive 4s, no crash
- Screenshots: tomorrow_odd 19575 bytes, tomorrow_even 14374 bytes, week_odd 16800 bytes
- Table O3313 odd vs even verified: Среда 02.09.2026 odd 4 rows (09:00 лек ИСТОРИЯ etc.) vs even 2026-09-09 4 rows (09:00 лек ИСТОРИЯ etc.) per verify_phase1

### Logs
- Themes/Vograph.xaml copied 1:1 from Charon.xaml:7-24 + MainWindow.xaml:8-10
- MainWindow.xaml tabs Today/Tomorrow/Week, parity badge, week 3+3 grid, group picker saves to settings.myGroupId
- DB reuse same ParserService, lastUpdated visible, stale badge

### Next
- Auto-advance to Phase 3: Personalization

## Phase 3 — Personalization (2026-09-01)

**Status:** DONE

### Verification Checklist
- [x] Rename survives "Refresh schedule" (global + weekday:1 scope respected, original kept)
- [x] Homework N=2 due = 2nd next occurrence (2026-09-02 лек ВЫСШ. МАТЕМАТ → 2026-09-14) PASS
- [x] gray->bold transition works (far hidden -> approaching MarbleDim #FF6B7280 -> burning Marble #FFC5CAD3 Bold -> burning_urgent Bronze #FF6CA5E0 + highlight)
- [x] mark done works (strikethrough Opacity 0.5, DoneAt, toggle)

### Raw numbers
- Overrides: 2 (global МатАн + weekday ОРГ), after refresh still 2, global overrides weekday
- Homework: Add N=2 due 2026-09-14, status approaching (1 lesson before due) -> burning tomorrow -> burning_urgent today, mark done -> done -> unmark -> approaching
- ParserService.Refresh now calls HomeworkService.RecomputeAllStatuses (preserves overrides/homework, rebinds by normalized key)
- UI: RenameDialog scope radio + preview + reset, HomeworkDialog Text+N 1..10+due preview, MainWindow CreateLessonCard shows renamed + note + homework blocks with status colors/borders, context menu

### Logs / Artifacts
- Vograph.Verify3 console 5 checks PASS, DB 31 lessons
- docs/verify_phase3/README.md + status_palette.png 15011 bytes
- Code: OverrideService.cs, HomeworkService.cs, Dialogs/*.xaml

### Next
- Auto-advance to Phase 4: Intersections + Notifications

## Phase 4 — Intersections + Notifications (2026-09-01)

**Status:** DONE

### Verification Checklist
- [x] Add O3314 (here 09С33) as friend -> icons appear correctly at threshold 0 (4) and 100 (0) PASS
- [x] Toast fires at both configured times with renamed text (log in data/runs/) PASS (20:00 Ср ПЕРЕИМЕНОВАНО [ДЗ!] + 07:30 Вт ПЕРЕИМ-2)

### Raw numbers
- Friend: 09С33 (Id 3032) color #FF6CA5E0, strict0 4 strict40 0 strict100 0, building check 493 vs 402
- Notification: NotifyTime1 20:00 (tomorrow) NotifyTime2 07:30 (today), texts: Ср ПЕРЕИМЕНОВАНО [ДЗ!] and Вт ПЕРЕИМ-2, logs 2 lines 377 bytes
- Services: IntersectionService score 0/40/100 TimesOverlap, NotificationService BuildNotificationText + LogAndShow + ShouldFire, DispatcherTimer 30s
- UI: FriendsListPanel, FriendGroupPicker, StrictnessSlider 0..100, NotifyTime boxes, StartNotifyTimer, iconPanel colored dots

### Logs / Artifacts
- data/runs/toast-20260901.log 377 bytes (20:00 + 07:30)
- docs/verify_phase4/toast-20260901.log copy, intersections.png 10304 bytes
- docs/verify_phase4/README.md + Verify4 console

### Next
- Auto-advance to Phase 5: Sync

## Phase 5 — Sync

Pending

## Phase 6 — Android Port

Pending






