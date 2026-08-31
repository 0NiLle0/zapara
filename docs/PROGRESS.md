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

## Phase 1 — Parser + DB

Pending

## Phase 2 — Windows MVP Table

Pending

## Phase 3 — Personalization

Pending

## Phase 4 — Intersections + Notifications

Pending

## Phase 5 — Sync

Pending

## Phase 6 — Android Port

Pending

