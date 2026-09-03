# ЗАПАРА (ZAPARA)

Расписание БГТУ «Военмех» без блужданий по катакомбам ГК / УЛК.

Windows MVP (WPF, .NET 8). Завтра по умолчанию, вчера / сегодня / неделя, сводка, преподаватели, офлайн-карты корпусов, друзья-светофоры, ДЗ, уведомления, LAN-синхронизация без сервера.

> Раньше — кодовое имя `VOG-ZAVTRA` / `Vograph`. Кодовый неймспейс `Vograph.*` пока сохранен ради истории, видимое имя — **ЗАПАРА**.

## Что умеет

- **Вчера / Сегодня / Завтра / Неделя** — умный старт: если пары сегодня еще не прошли, открывается сегодня, иначе завтра.
- **Номер недели + чет/нечет** — неделя с 1 сентября = неделя 1 = нечетная, плюс ручная инверсия.
- **Столбец «След. пара»** — дата следующего занятия по предмету + вторая строка с датой у того же препода, если отличается.
- **Сводка** — нечет / чет / обе недели: по дням, типам, предметам, преподам, аудиториям.
- **Преподаватели** — поиск по расписанию преподов (`TimetableLecturer50.xml`, 718 / 8265 пар), где и когда ведет, подсветка своей группы.
- **Карты** — `voenmeh.ru/openmap`, ГК 1–4 + УЛК 1–5, встроены в приложение, работают офлайн. `*` = УЛК (проверено). Зум, панорама, на весь экран (`⛶`, `Esc`). Скрыта по умолчанию, открывается кнопкой `◉` в строке или кнопкой `Карта`.
- **Друзья-светофоры** — до 5 групп: в той же аудитории / на том же этаже / в том же корпусе / в вузе / нет на месте (потухший). Клик — карточка + переход на карту. Режим «всегда все» или «только непустые». Имена товарищей справа от группы.
- **Переименования + ДЗ** — переживают обновление расписания, статусы far / approaching / burning / overdue / done.
- **Уведомления** — 2 времени, текст с переименованиями и горящим ДЗ.
- **Синхронизация** — только LAN: JSON `zapara-sync-*.json` + QR + `http://<ip>:8765/sync`. Без облака.

## Быстрый старт

```powershell
dotnet build src\Vograph\Vograph.csproj -c Release
dotnet publish src\Vograph\Vograph.csproj -c Release -r win-x64 --self-contained false
.\app\Vograph.exe
```

Требуется .NET 8 Runtime. Только PowerShell 5.1 / Windows 10-11, без WSL/Docker.

## Источники данных

- Студенты: `https://voenmeh.ru/obrazovanie/timetables/` → `.../_voenmeh_grafics/TimetableGroup50.xml` (5.6 МБ, 420 групп, UTF-8).
- Преподаватели: `https://voenmeh.ru/prepodavatelyam/raspisanie-prepodavatelej/` → `.../TimetableLecturer50.xml` (4.4 МБ, бандл + кэш).
- Карты: `https://voenmeh.ru/openmap/` → `karta-*-2022.jpg` (ГК 1–4, УЛК 1–5) + `maps/coords.json` для точной подсветки (правится вручную).
- Кэш: `%LocalAppData%\Zapara\` (пока `%LocalAppData%\Vograph\` — миграция пути в планах), SQLite `vograph.db` (WAL).

## Структура

```text
src/Vograph/                 # WPF: MainWindow, Dialogs, Themes, maps/, Helpers
src/Vograph.Core/            # Models + Services: Parser, Parity, Schedule, Homework, Intersection, Map, Lecturer, Sync, I18n
docs/API.md                  # разбор XML/XSL/parity/openmap/lecturer
docs/PROGRESS.md             # верификация по фазам (сырые цифры/логи)
docs/dist/                   # zip сборки
data/runs/                   # autorefresh / toast / sync логи
```

## Статус

MVP Windows собирается (`0 errors, 2x CS4014`), `app\` запускается. Дальше — Android-порт с тем же `Core` и те же четности/карты.

Приватный репозиторий: `https://github.com/0NiLle0/zapara`
