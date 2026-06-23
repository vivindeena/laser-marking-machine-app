# Laser Marking QR App

Lean Windows WinForms app for EZCAD2-based QR marking workflows.

The app is the source of truth for part data, user access, serial validation, duplicate prevention, QR construction, and production logging. EZCAD2 remains the marking engine and reads the generated QR payload from a text file.

## Deployment

This project targets .NET 8 Windows WinForms and is intended to be published as self-contained Windows folders:

- `win-x64`
- `win-x86`

The marking PC does not need a separate .NET install when using the published folders.

Build on a Windows machine with the .NET SDK installed:

```powershell
.\scripts\publish-win.ps1
```

Release zips are written to `dist\`.

## Default Paths

- QR output: `C:\Laser\QRDATA.TXT`
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

1. Confirm the current part and vendor on screen.
2. Enter the serial number.
3. Press `MARK` or hit Enter.
4. The app validates the serial, blocks duplicates, builds QR data, writes `QRDATA.TXT`, logs the mark, clears the serial field, and focuses the next entry.

The operator cannot edit part, vendor, QR format, or template settings.

## Setter Flow

1. Click `Setter Login`.
2. Log in with a Setter or Admin account.
3. Select or create a part.
4. Set vendor, plant, customer, QR format, and template path.
5. Press `Save` to store the part.
6. Press `Set Active` to activate the part and copy its `.ezd` template into the active template folder.

Setter access automatically logs out after 2 minutes of inactivity.

## User Management

Only Admin users can open `Users` from the setter screen. Admins can create users, change passwords, and set roles.

## QR Format

Default format:

```text
{VendorCode}|{PartNumber}|{Serial}
```

Supported placeholders:

- `{VendorCode}`
- `{PartNumber}`
- `{PlantCode}`
- `{CustomerCode}`
- `{QRPrefix}`
- `{Serial}`

Default serial validation:

```regex
^\d{6}$
```

## EZCAD2 Setup

See [docs/EZCAD_SETUP.md](docs/EZCAD_SETUP.md).

## v1 Boundaries

- No EZCAD UI automation.
- No SDK integration.
- No OCR/barcode reader dependency.
- No database server.
- Optional external command hook is available but disabled by default.
