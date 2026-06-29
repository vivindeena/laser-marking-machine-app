# Laser Marking QR App

Lean Windows WinForms app for EZCAD2-based QR marking workflows.

The app is the source of truth for part data, user access, generated serials, engraving text construction, and production logging. EZCAD2 remains the marking engine and reads the generated payload from a text file.

## Deployment

This project targets .NET 8 Windows WinForms and is intended to be published as self-contained Windows executables:

- `win-x64`
- `win-x86`

The marking PC does not need a separate .NET install when using the published executables.

Build on a Windows machine with the .NET SDK installed:

```powershell
.\scripts\publish-win.ps1
```

Release executables are written to `dist\`.

## Download From GitHub Releases

The repository creates a GitHub Release after each successful build on `main`.

1. Open the GitHub repo.
2. Go to `Releases`.
3. Open the latest `Laser Marking App Build ...` release.
4. Download one of the assets:
   - `LaserMarkingApp-win-x64.exe`
   - `LaserMarkingApp-win-x86.exe`

Copy the `.exe` to the marking PC and run it.

## Download From GitHub Actions

The repository also keeps downloadable Windows app executables on each workflow run. Pull request builds are temporary preview builds and expire after 30 days.

1. Open the GitHub repo.
2. Go to the pull request or to `Actions`.
3. Open the successful `Build Windows App` workflow run.
4. Download one of the artifacts:
   - Pull requests: `laser-marking-machine-app-pr-<number>-win-x64` or `laser-marking-machine-app-pr-<number>-win-x86`
   - Main/manual runs: `laser-marking-machine-app-<branch>-win-x64` or `laser-marking-machine-app-<branch>-win-x86`

Each artifact contains the architecture-specific single executable. Production downloads from `main` are also published permanently under GitHub Releases.

## Default Paths

- QR output: `C:\Laser\QRDATA.TXT`
- EZD template source folder: `D:\QUALITY-3`
- Active template folder: `C:\Laser\ActiveTemplate`
- Database: `%LOCALAPPDATA%\LaserMarkingApp\laser_marking.db`

## Default Users

The first run creates these local users:

| Username | Password | Role |
| --- | --- | --- |
| `operator` | `operator123` | Operator |
| `setter` | `setter123` | Setter |
| `admin` | `admin123` | Admin |

Change these before production use from `Setter Login` using the `admin` account, then open `Users`.

## Operator Flow

1. Confirm the current part and item code on screen.
2. Enter the heat / lot number, for example `26-4B-21`.
3. Press `MARK` or hit Enter.
4. The app generates the next global serial, builds the full engraving string, writes `QRDATA.TXT`, logs the mark, clears the heat / lot field, and focuses the next entry.

The operator cannot edit part, item code, generated serial, date fields, QR format, or template settings.

## Setter Flow

1. Click `Setter Login`.
2. Log in with a Setter account.
3. Select the part that should run.
4. Press `Load` to review the selected part details.
5. Press `Set Active` to activate the part and copy its `.ezd` template into the active template folder.

Setters cannot create, edit, or delete part master data.

## Admin Part Management

Admin users can create, update, and delete part master records from the setter screen.

1. Click `Setter Login`.
2. Log in with an Admin account.
3. Use `New`, `Save`, or `Delete` to manage part records.
4. Use `Set Active` to activate the required production part.

Setter access automatically logs out after 2 minutes of inactivity.

## User Management

Only Admin users can open `Users` from the setter screen. Admins can create users, change passwords, and set roles.

## QR Format

The app writes the full EZCAD text payload in this format:

```text
CustomerItemCode$PartNumber$DatePrefixSerial$Date$MonthLabel$Material$HeatLot$Revision$Product$Supplier$
```

Example:

```text
7201097$B3F02301$26F-1705$27.06.2026$JUN-26$FG260$26-4F-3204$#.0$FLYWHEEL$SREERAMENGG$
```

The serial number is generated globally across all parts. The date prefix uses `YYM-`, where `A=Jan`, `B=Feb`, through `L=Dec`.

## EZCAD2 Setup

See [docs/EZCAD_SETUP.md](docs/EZCAD_SETUP.md).

## v1 Boundaries

- No EZCAD UI automation.
- No SDK integration.
- No OCR/barcode reader dependency.
- No database server.
- Optional external command hook is available but disabled by default.
