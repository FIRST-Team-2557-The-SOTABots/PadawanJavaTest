# FRC Research & Debugging Assessment

A self-contained Windows test app for FRC first-years. It has two jobs:

1. **Prove independent research.** An embedded browser forces Google's text-only
   Web results, so there is no AI Overview to copy from. AI tools and answer sites
   are blocked, and every attempt is logged.
2. **Check debugging skill.** Ten Java snippets ask the student to find the bug and
   explain the fix. Leadership grades those afterwards.

Students get a name/team screen, a 30-minute timer, 20 questions, and a browser.
Mentors get JSON, Markdown, and CSV results.

No Node, Python, or .NET installation is needed to run the published `.exe`.

---

## Requirements

- Windows 10 or 11, x64
- Microsoft Edge **WebView2 Runtime** (already present on Windows 11 and most
  Windows 10 machines; if missing, the app tells you and links the installer:
  <https://go.microsoft.com/fwlink/p/?LinkId=2124703>)
- An internet connection for the research section

---

## First run (mentor checklist)

1. Copy the published folder to the machine that will run the test.
2. Run `FrcResearchTest.exe`.
3. The app asks you to set a **mentor PIN** (at least 4 characters). It is stored
   as a salted SHA-256 hash in `config.json`, not as plain text.
   - The PIN travels with the folder. Set it once, then copy the whole folder to
     the other lab machines if you want the same PIN everywhere.
4. Review `questions.json` before the first real session (see below).
5. Do one dry run yourself: start a session, run a search, submit, and confirm
   files appear in `Documents\FRC Research Test\results\`.

### Mentor menu

Press **Ctrl+Shift+F12** at any time, or click **Mentor setup / exit** on the start
screen / **Mentor options** after submitting. It is PIN-protected and can:

- open `config.json`
- open `questions.json`
- open the results folder
- change the PIN
- exit the app

Alt+F4 and the normal close path also require the PIN.

---

## What the browser does

- **Every Google search is rewritten to the Web-only view** (`udm=14`). This
  includes searches typed into Google's own search box, not just the address bar.
  That matters: a google.com search normally still shows an AI Overview even if
  the address-bar query includes the parameter.
- **AI Mode links** (`udm=50`) are redirected to the Web view instead of loading.
- Image, video, news, and shopping searches are left alone.
- **Blocked:** the domains in `config.blockedDomains` plus `bing.com/chat`,
  `file://`, `edge://`, and `chrome://` URLs.
- **Disabled:** DevTools, right-click menu, downloads, popups, permission prompts,
  password saving, and autofill.
- **Fresh profile per student**, stored under
  `%LOCALAPPDATA%\FRCResearchTest\profiles\<name>-<timestamp>`. No history, no
  Google login, no personalization carried between testers.
- **Logged:** every navigation, search query, address-bar input, blocked attempt,
  and download attempt. Blocked AI attempts are counted and flagged in the result.

### UdM=14 caveat

`udm=14` is Google's Web-filter URL parameter. It has worked since May 2024 and was
still working at the time this was built, but Google has never documented it as a
stable interface. If it breaks:

- change `searchEngineUrl` in `config.json` to another engine, for example
  - `https://html.duckduckgo.com/html/?q={query}`
  - `https://search.brave.com/search?q={query}`
- or tell students to append `-ai` to the query.

Run `FrcResearchTest.exe --selftest --network` to check the live rewrite.

---

## Results

Default folder: `Documents\FRC Research Test\results\` (override with
`resultsFolder` in `config.json`; environment variables like `%USERPROFILE%` are
expanded, and a network share works).

Each session writes:

| File | Purpose |
|---|---|
| `summary.csv` | One row per student, append-only. Open in Excel/Sheets. |
| `<student>-<time>.json` | Everything: answers, flags, per-question checks, stats. |
| `<student>-<time>.md` | Human-readable review file. Section B snippets, answers, and rubrics. |
| `<student>-<time>.log.jsonl` | Append-only activity log: navigations, searches, blocked attempts. |

### Scoring

- **Section A (research)** is auto-scored. Credit for a question requires **both**:
  1. the answer matches that question's keyword rule, and
  2. the cited URL was actually opened in this session and is on
     `allowedCitationDomains`.
- `research_correct` in the CSV counts keyword matches. `research_effective`
  counts matches with a verified citation. Both are out of 10.
- **Section B (programming)** is not auto-scored. The app writes the answer and
  the intended rubric into the `.md` and `.json` files; leadership grades it.
- The app never declares a final pass/fail. That is intentional: half the test is
  human-graded.
- `researchPassCorrect` (default 8) is only used to add the
  `research_below_pass` flag.

### Flags you may see

| Flag | Meaning |
|---|---|
| `ai_attempts:N` | N navigations to blocked AI/answer sites |
| `unanswered:N` | N questions left blank |
| `no_searches` | No Google search was ever performed |
| `research_below_pass` | Effective research score below `researchPassCorrect` |
| `single_source` | Three or more correct answers all cite the same host |
| `very_fast` | Submitted in under four minutes |
| `auto_submitted` | Timer expired |

Flags are prompts to look, not proof of cheating.

---

## `config.json` reference

| Key | Default | Notes |
|---|---|---|
| `timeLimitMinutes` | `30` | Timer for the whole test |
| `kiosk` | `true` | Borderless, full-screen, always-on-top |
| `showScoreOnSubmit` | `false` | Show Section A score to the student after submitting |
| `homeUrl` | `https://www.google.com/` | Browser home |
| `searchEngineUrl` | `https://www.google.com/search?q={query}&udm=14` | `{query}` is URL-encoded |
| `resultsFolder` | `""` | Empty = Documents folder above |
| `mentorPinSalt` / `mentorPinHash` | `""` | Set through the UI; do not hand-edit |
| `researchPassCorrect` | `8` | Used only for the flag |
| `allowedCitationDomains` | WPILib/CTRE/REV/GitHub/FIRST/Chief Delphi | Suffix match; `docs.wpilib.org` matches `wpilib.org` |
| `blockedDomains` | common AI tools | Host suffix match; subdomains are included |

`config.json` is read from the folder that contains `FrcResearchTest.exe`. If that
folder is read-only (for example, `C:\Program Files`), move the app to a normal
folder or a USB stick.

---

## `questions.json` reference

The app reloads this file every time a student presses **Start**, so edits do not
require a rebuild.

### Research question

```json
{
  "id": "R1",
  "type": "research",
  "points": 1,
  "prompt": "Which method ...?",
  "requireCitation": true,
  "autoScore": true,
  "accept": { "all": ["schedule"], "any": [] }
}
```

- `accept.all`: every keyword must appear in the answer.
- `accept.any`: at least one keyword must appear.
- Matching is case-insensitive and ignores all whitespace, so `atSetpoint()`,
  `at setpoint`, and `AT SETPOINT` all match the keyword `atsetpoint`.
- Use `all` and `any` together if you want "must mention X, and at least one of
  Y/Z".
- Keep keywords specific. A keyword like `timer` will match almost anything.
- `points` is displayed on the card. Scoring currently counts questions, so leave
  all questions at 1 point unless you change the scoring code.

### Programming question

```json
{
  "id": "P1",
  "type": "code",
  "points": 1,
  "language": "java",
  "prompt": "What is wrong, and how do you fix it?",
  "snippet": "public boolean isFive(int value) {\n    if (value = 5) { ... }\n}",
  "rubric": "Mentor-only expected answer.",
  "requireCitation": false,
  "autoScore": false
}
```

- `snippet` is shown in a monospaced pane with line numbers. Use `\n` for newlines.
- `rubric` is never shown to the student; it is written into the review file.
- Keep each snippet to a single intended bug.

### Validating edits

```powershell
FrcResearchTest.exe --selftest
```

This checks the JSON, the `udm=14` rewrite, keyword grading, citation matching,
export, window construction, and WebView2 startup. The report is written to
`%TEMP%\frc-research-selftest.txt` and the exit code is 0 on success.

Add `--network` to also perform a real Google search and confirm the Web-only
redirect:

```powershell
FrcResearchTest.exe --selftest --network
```

---

## Suggested grading workflow

1. After the session, open `summary.csv`. Sort by `research_effective`.
2. Open the `.md` file for each student. The research section shows exactly what
   was answered, the cited URL, and whether the citation was verified.
3. Grade Section B in the same `.md` file against the rubric. Add your own score
   column wherever you track it.
4. Treat `ai_attempts`, `single_source`, and `very_fast` as reasons to spot-check,
   not automatic fails.
5. Grant AI access only when both sections are satisfactory.

---

## Building and publishing

Requires the .NET 10 SDK (development machine only).

```powershell
# run from source
dotnet run

# tests
dotnet run -- --selftest
dotnet run -- --selftest --network

# single-file Windows build
.\publish.ps1
```

`publish.ps1` produces a self-contained single-file build in `publish\`:

```powershell
dotnet publish FrcResearchTest.csproj -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:DebugType=None -o publish
```

The `publish` folder is what you copy to student machines. It contains the exe,
`config.json`, `questions.json`, and this README.

### Distribution notes

- The exe is unsigned. Windows SmartScreen will show "Windows protected your PC"
  on first run: **More info -> Run anyway**. If the school can trust a local
  certificate, sign the exe to avoid this.
- Set the mentor PIN on one machine, then copy the whole folder. The hashed PIN
  comes with it.
- For a shared results location, point `resultsFolder` at a network share and
  make sure students have write access.

---

## Limitations and integrity

- **A local app cannot stop a phone or another browser.** It can stop the embedded
  browser, log attempts, and make cheating inconvenient. Real enforcement needs
  supervision or Windows **Assigned Access** (kiosk mode) locked to this exe:
  `ms-settings:assignedaccess`.
- Keyword grading is approximate. A student can land on the right page without
  understanding it; the exported answer text is the check.
- Google may show a consent or captcha page on a brand-new profile. The student
  can solve it; it is logged as a normal navigation.
- AI Overviews can still appear in Google's other surfaces (images, Lens, the
  mobile app). This app only controls its own browser.
- The browser profile is intentionally thrown away between students. It is not
  deleted automatically (WebView2 keeps file locks); clean
  `%LOCALAPPDATA%\FRCResearchTest\profiles` occasionally if disk space matters.

---

## Project layout

| File | Responsibility |
|---|---|
| `MainForm.cs` | Screens, timer, start/submit flow, mentor hotkey, result assembly |
| `BrowserPanel.cs` | WebView2 host, lockdown, navigation logging, blocklist |
| `SearchRewriter.cs` | `udm=14` enforcement and address-bar search URLs |
| `QuestionCard.cs` | One research or programming question card |
| `Scoring.cs` | Keyword matching |
| `VisitLog.cs` | Visited-page tracking and citation verification |
| `ResultsExporter.cs` | JSON, Markdown, and CSV output |
| `SessionLogger.cs` | JSONL activity log |
| `MentorDialogs.cs` | PIN, set-PIN, and mentor menu dialogs |
| `SelfTest.cs` | `--selftest` checks |
| `config.json` / `questions.json` | Runtime content, editable without a rebuild |
