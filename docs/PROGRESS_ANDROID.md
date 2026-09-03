# PROGRESS_ANDROID — ЗАПАРА (Kotlin + Compose, minSdk 26, branch `android`)

## Phase A0 — Recon (2026-09-03)

**Status:** DONE

### Verification Checklist
- [x] Live endpoints re-measured (group/lecturer XML, openmap, studs/lect.js, XSL) — all HTTP 200
- [x] Counts match Windows recon exactly (420 groups / 11 357 lessons / 3313 = 31 = 18+13 / 718 lecturers / 8 265 lessons / 9 maps)
- [x] Parity probe 5 dates match `ParityService` (09-01→1 odd, 09-03→1 odd, 09-08→2 even)
- [x] `docs/API_ANDROID.md` written (7 sections)

### Raw numbers
- `TimetableGroup50.xml` 5 649 721b, `Last-Modified Fri 28 Aug 2026 10:38:55 GMT`, UTF-8 no BOM
- `TimetableLecturer50.xml` 4 413 327b, `Last-Modified Fri 28 Aug 2026 10:40:22 GMT`
- `openmap.html` 1 916 908b; 9 full JPGs byte-identical to `src/Vograph/maps/` (132 896 … 76 738, 12 Feb 2025)
- `studs.js` 7 448b, `lect.js` 8 024b, `TimetableGroup.xsl` 5 169b — unchanged
- Parity: 09-01 Tue w1 odd / 09-03 Thu w1 odd / 09-04 Fri w1 odd / 09-08 Tue w2 even / 09-15 Tue w3 odd

### Logs
- Fetch via `curl.exe -sS` with `-D` headers, `HEAD` via `curl -sI` for maps
- Parse via regex counts on UTF-8 bytes (`parse.py`), parity via `probe.py` (ceil-days formula)
- Artifacts in Temp only (`%LocalAppData%\Temp\opencode\a0\`), not committed

### Next
- Auto-advance to Phase A1: scaffold `android/` + data layer + unit tests

## Phase A1 — Data layer (2026-09-03)

**Status:** DONE

### Verification Checklist
- [x] Gradle scaffold builds: `:app:testDebugUnitTest` green + `:app:assembleDebug` APK
- [x] 25 unit tests pass (parity 5 dates, fixture parse, intersection, maps, next-pair)
- [x] Counts match Windows recon (fixture mirrors real schema; live-file counts verified in A0)

### Raw numbers
- Toolchain: JDK Temurin 17.0.20.1 (`C:\Android\jdk17`), Gradle 8.7 (`C:\Android\gradle-8.7` + wrapper), SDK `C:\Android\Sdk` (platform-tools 37.0.1, android-34, build-tools 34.0.0), AGP 8.5.2, Kotlin 2.0.21, Compose BOM 2024.06.00, Room 2.6.1 + KSP 2.0.21-1.0.25
- Config: `ru.bgtu_voenmeh.zapara`, minSdk 26, target/compile 34, versionCode 1 / 1.0
- Tests: ParityTest 4 / GroupParserTest 4 / LecturerParserTest 2 / IntersectionTest 4 / MapResolveTest 6 / ScheduleTest 5 = 25, failures 0, errors 0
- Fixture asserts: 2 groups, 3313 = 7 lessons (odd 5 / even 2), `09:00`→`10:35`, `493;`→ГК / `563*;`→УЛК / `ВЦ 280;`→ВЦ / `дистанционно`, lecturer 1287 = 2 lessons + groups `3313/А863С`
- Next-pair on fixture: subject from Wed 09-02 → Mon 09-07; teacher Волченкова → Wed 09-09
- `app-debug.apk` 8 838 225b (debug, Compose unshrunk)

### Logs
- `app/build/test-results/testDebugUnitTest/TEST-*.xml` (6 files, all green)
- DOM parsing via `javax.xml` (works in JVM tests AND on device — `XmlPullParser` avoided: android.jar stubs throw in unit tests)
- `local.properties` (sdk.dir) gitignored; toolchain paths in this file

### Next
- Auto-advance to Phase A2: schedule UI (tabs, dark theme, responsive table, summary)

## Phase A2 — Schedule UI (2026-09-03)

**Status:** DONE

### Verification Checklist
- [x] Tomorrow tab (smart start → Fri 09-04, Thu lessons over) shows 3 rows matching raw XML
- [x] Week odd view matches raw XML (Mon 4 / Tue 4 / Wed 4 / Thu 2 shown)
- [x] Summary odd = 18, byDay exact, byType exact, counts left (per user rule)
- [x] Dark Charon theme + mono font, `ЗАПАРА` header, group dropdown `А863С`
- [x] Screenshots `docs/a2_tomorrow.png`, `docs/a2_week.png`, `docs/a2_summary.png`

### Raw numbers
- Emulator: Pixel 7 AVD, android-34 google_apis x86_64, headless swiftshader; app `ru.bgtu_voenmeh.zapara` installed + launched
- Smart start: Thu 09-03 17:39 local > last lesson end 16:30+15m → Tomorrow Fri 09-04 odd
- Fri odd rows: `09:00 [лек] ВВЕД. В СПЕЦ / Саваровский / 313; / next 18.09` · `10:50 [пр] ОСН. ГОС. УПР. / Хомелева / 526*; / 18.09` · `12:40 [лаб] ФИЗИКА / Попова / 563*; / next 11.09` — all match `TimetableGroup50.xml` dow=5 wc=1 (ASCII dump: 5 lessons total, odd 3)
- Week odd: Mon `09:00 493; / 10:50 — / 12:40 563*; / 14:55 502*;` · Tue `10:50 526*; / 12:40 401*; / 14:55 312*; / 16:45 526*;` · Wed `09:00 526*; / 10:50 — / 12:40 323*; / 14:55 564*;` · Thu `09:00 564*; / 10:50 ВЦ 282;` — match
- Summary: odd 18 (`Пн 4 · Вт 4 · Ср 4 · Чт 2 · Пт 3 · Сб 1`), `пр 9 · лек 8 · лаб 1`
- `app-debug.apk` 10 664 358b
- Note: initial Friday-row confusion resolved — garbled console misattributed Wed `лаб ФИЗИКА 323*;` to Fri; ASCII dump confirmed app is correct

### Logs
- Verification via `adb shell uiautomator dump` text (screenshots unreadable in this console, PNGs kept in `docs/`)
- Room on IO dispatcher only (main-thread access refactored: `nextMap`/`allLessons` precomputed in `render()`)
- Fixes during phase: ExposedDropdownMenu API, Typography monospace mapping, ExposedDropdownMenuBox params

### Next
- Auto-advance to Phase A3: personalization (overrides/homework) + friend traffic lights
