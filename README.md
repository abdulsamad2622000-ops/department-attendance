# Hobbies Club Attendance

Fully offline attendance system for **Hobbies Club** and **AI Club**, Karachi.
Built by **NovaStackHub**.

## Features
- Kiosk: check-in / check-out by PIN, face (camera) or fingerprint
- Self-registration with face photo and two fingerprints
- One club at a time, missed check-out handling, 1-minute double-scan guard
- Admin panel: Dashboard, Members, Reports (Excel/CSV), Users, Activity log, Backup/Restore

## Tech
.NET 10, WPF (MVVM, CommunityToolkit.Mvvm), EF Core + SQLite, Serilog, Emgu CV (YuNet + SFace), xUnit.

## Projects
| Project | Purpose |
|---|---|
| src/HobbiesClub.Core | Entities, enums, interfaces, models |
| src/HobbiesClub.Data | DbContext, migrations, paths |
| src/HobbiesClub.Services | Business logic |
| src/HobbiesClub.Biometrics | Face (Emgu CV) and fingerprint services |
| src/HobbiesClub.App | WPF app |
| tests/HobbiesClub.Tests | Unit tests |

## Data location (client PC)
C:\ProgramData\HobbiesClub\ (Data, Backups, Logs). Never inside the app folder; the installer never deletes it.

## Build and test
    dotnet build
    dotnet test
    dotnet run --project src/HobbiesClub.App

## Release
1. Update AppVersion in installer/HobbiesClub.iss and add an entry to CHANGELOG.md.
2. Publish: dotnet publish src/HobbiesClub.App -c Release -r win-x64 --self-contained true -o publish/HobbiesClub
3. Build installer with Inno Setup 6: ISCC.exe installer/HobbiesClub.iss
4. Output: installer-output/HobbiesClub-Setup-{version}.exe
5. Tag the commit: git tag v{version} and git push --tags

## Dev notes
- HOBBIES_FAKE_FACE=1 uses the fake face scanner (no camera).
- HOBBIES_DEV=1 enables the fake fingerprint scanner on the kiosk.

