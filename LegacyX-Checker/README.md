# LEGACY-X Checker

A small Windows program a player runs when staff (Admin, Manager or Owner) ask for a check. The website makes a one-time code
(Checks page, or *Staff → Ask for a check* on a profile); the player types it in, sees who asked, agrees, and the program scans
this PC and sends back a short result. **A result is not a verdict:** staff read it and decide. Nothing here bans anyone.

> **Status:** written without a compiler (the cloud session had no .NET), so build it first and fix anything the compiler says.
> The scan is deliberately simple and the rules list (`rules.json`) is empty of real cheats until staff fill it in from verified samples.

## Build and run (Windows, .NET 8 SDK)

```
cd LegacyX-Checker
dotnet build -c Release
dotnet run
```

Publish one file for players:

```
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

`rules.json` is copied next to the program. Put it beside the `.exe` when you share the file.

**Without a signature (what we do for now).** The program is not signed, so Windows shows "Windows protected your PC" on first run and some
antivirus may flag it. To keep that as small as possible and make it safe for players:
- Build it plain: no trimming, no packer/protector, no obfuscation (those are what antivirus dislikes). The publish command above is right.
- Every player message includes the file's SHA-256 and how to check it (`Get-FileHash .\LegacyX-Checker.exe`), and the click path for the warning
  (More info → Run anyway). Players who do not trust it can compare the fingerprint, or read this source.
- Give it to a few players first. If Defender flags it, submit the file at https://www.microsoft.com/wdsi/filesubmission as a false positive.
- Send it only from our own address (the personal download or a Discord message from staff), never from a mirror.
Signing later removes the warning; it changes nothing in how the program works.

**Sign the `.exe`** (a code-signing certificate). A scanner that reads files looks like malware to antivirus and SmartScreen; unsigned
builds get warnings and false alarms. Sign it, and submit it to Microsoft Defender as a false positive if it is still flagged.

## Giving it to players

1. Build the single file (above) and put `LegacyX-Checker.exe` and `rules.json` in a zip named **`LegacyX-Checker.zip`**.
2. **While the site is behind the countdown**, players cannot open `legacyx.cc/downloads/...` (the gate serves nobody but the team). Send them
   the zip yourself (Discord direct message).
3. **After launch**, put the zip on the web server, outside the repository, where a deploy does not remove it:
   ```
   sudo mkdir -p /var/www/legacyx/downloads
   sudo cp LegacyX-Checker.zip /var/www/legacyx/downloads/
   ```
   The site finds `https://legacyx.cc/downloads/LegacyX-Checker.zip` by itself: the "Ask for a check" window then shows a download link
   and a message ready to paste to the player. (Or set `VITE_CHECKER_DOWNLOAD_URL` when building the site to use any other address.)
4. **A personal download for each check (recommended).** Put the signed `LegacyX-Checker.exe` (and `rules.json`) somewhere on the API server and set
   `CHECKER_EXE_PATH` / `CHECKER_RULES_PATH` in the API's `.env`, then restart it. After that every check can give the player
   `…/api/v1/checks/code/<code>/download`: a zip made on the spot with the program, the rules and a `check.json` that holds *that* check's code.
   The player unzips it and runs the program; the code is already filled in and checked, so they only read and agree. The link works only
   while the check is waiting and not expired, at most 3 times. The program deletes `check.json` after it has sent its result.
   Run it from the unzipped folder, not from inside the zip (`check.json` must sit next to the `.exe`).
5. Every new build: replace the zip, and keep `rules.json` next to the `.exe`.

## What it does

| Scanner | Looks at | Finds |
| --- | --- | --- |
| `FileScanner` | file names on every fixed drive; for programs, a SHA-256 when `rules.json` has hashes | name hit = suspicion, known file name or hash = detection |
| `ContentAnalyzer` | what an unsigned program (.exe, .dll, .sys) *is*, not its name: whether it reads another program's memory (from its import table, or its text if it is a .NET program), and whether it names CS2 or carries CS2 offsets; protector sections (VMProtect, Themida …) | memory access + two or more CS2 offsets = detection; memory access + names CS2 = suspicion; protected and unsigned = suspicion |
| `ProcessScanner` | running programs: name and window title | name hit = suspicion |
| `TraceScanner` | Prefetch (what ran recently, needs administrator) and the Recent list | name hit = suspicion; Prefetch off or empty = suspicion of tampering |
| `SteamScanner` | `Steam\config\loginusers.vdf` | Steam IDs, so the site can say whether the player who was asked is on this PC |

### How the content check decides

Signed programs and everything under the Windows folder are skipped. For an unsigned program the checker reads only its headers and
import table first (cheap). Only a program that opens other processes (`OpenProcess` with `ReadProcessMemory`, `WriteProcessMemory`,
`CreateRemoteThread` …) is read further, up to 64 MB, looking for the words in `rules.json`: `gameMarkers` (cs2.exe, client.dll …)
and `offsetMarkers` (dwEntityList, dwLocalPlayerPawn … the names a CS2 cheat reads). Debuggers, Cheat Engine, trainers and some
overlays also read memory, so a hit is a reason to look, not a verdict. Edit the three lists in `rules.json` as new cheats appear.

## What it sends

`POST /api/v1/checks/code/:code/report` with exactly: `consent`, `checkerVersion`, `steamIds`, `filesScanned`, `durationSeconds`,
`findings[{name, kind, confidence, path?, note?}]`. Paths are shortened (the Windows user name becomes `***`). The server rejects any
other field. File contents, screenshots, passwords, browser data and keystrokes are never read or sent. The same list is written to
`LegacyX-Checker-report.txt` on the Desktop so the player can read it.

## Why did it not flag my test file?

Run the checker on that one file and it says exactly why it was or was not flagged (signed? imports memory functions? CS2 names inside?):

```
LegacyX-Checker.exe --explain "C:\path\to\the\file.exe"
```

A window shows the steps and a copy is written to the Desktop (`LegacyX-Checker-explain.txt`). Typical reasons a real cheat is missed: it is
signed; it is not a Windows program (a .jar, .py, .lua, .ahk or a config); it reads memory through a driver or by calling Windows directly (no
imported memory functions); it loads the real code from the internet later; or its CS2 names are encrypted. The content check is a net for the common
cases, not a guarantee.

## As an installer (.msi)

`..\LegacyX-Checker-Installer` builds `LegacyX-Checker.msi` (WiX 4, fetched by the build):

```
cd LegacyX-Checker-Installer
.\build-msi.ps1
```

The result is `LegacyX-Checker-Installer\bin\Release\LegacyX-Checker.msi` (the script prints its SHA-256). It installs for the current user only
(`%LOCALAPPDATA%\LegacyX-Checker`, no administrator), starts the checker when it finishes, and the checker uninstalls it (`msiexec /x`) and sweeps
what is left once the result is sent. There is no code inside the installer: the player types the code. The rules are inside the program.
A new installer needs the same `UpgradeCode` (`Package.wxs`) and a higher `Version` to replace an older one.

## One check, one program

A check's code works once, and so does the program that carries it: after the result has been **sent** and the window is closed, the program deletes
itself a few seconds later through a hidden `cmd` command (permanent delete, no Recycle Bin, a second try if a file was locked): `LegacyX-Checker.exe`,
`rules.json`, `check.json`, the unpacked folder if it is then empty, and any `LegacyX-Checker*.zip` on the Desktop, in Downloads, next to the program
and inside its folder. The report on the Desktop stays. If sending failed, nothing is deleted so the player can try again. It only acts on a program
named `LegacyX-Checker.exe`, so `dotnet run` never removes anything.

If a copy is still on the PC afterwards (deletion blocked, antivirus, someone copied it back), it does not work: sending leaves a `used.flag` next to
the program and one in `%LOCALAPPDATA%\LegacyX-Checker`, and a program that finds one shows "already been used", tries to delete itself again and quits.
The server also accepts every code once, so a leftover program has nothing to send to.

## Settings

`checker.json` next to the program: `{ "apiUrl": "https://api.legacyx.cc" }` (the default) points it at another server, for example a test one. Use the **API** address, not the website's.

## Not done yet

- Real cheat signatures in `rules.json`, and fetching them from the website so they can change without a new release.
- Reading Amcache / ShimCache / the USN journal (deeper traces of deleted programs).
- Checking loaded modules inside `cs2.exe`.
- A signed installer.


## How a file is judged

A name is never a finding on its own: names are easy to change and easy to fake. An unsigned program is given points for what it does:
reaches into another program's memory (3), pushes code into another program (3), uses kernel routines for it (3), then, if it does one of those,
looks for programs by name (1), draws a see-through window (1), sends mouse or keyboard input (1), is packed (1), names `cs2.exe` and friends (2) and
carries two or more CS2 offsets (4). A **detection** needs a CS2 target and 7 points (6 if its name or folder looks like a cheat's); a **suspicion**
is 5 points with a CS2 target or 6 without. Signed programs, the Windows folder and the checker's own files are left alone. Running programs are judged
the same way by their file. Prefetch and Recent only name programs, so they are not judged; the checker reports only if Prefetch is off, empty or unreadable.
`LegacyX-Checker.exe --explain "<file>"` shows every point.
