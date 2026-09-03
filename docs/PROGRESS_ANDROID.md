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
