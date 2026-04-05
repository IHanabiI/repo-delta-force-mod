# REPO Delta Force Mod

REPO Delta Force Mod is a Havoc-themed gameplay mod for REPO.

## Features

- Havoc opening event
- Military terminal tool item
- Flight recorder special valuable
- Air drop case special valuable

## Repository Layout

- `source/RepoDeltaForceMod.RuntimeRecovered`
  - main BepInEx runtime source
- `docs`
  - retained design and implementation notes
- `scripts`
  - optional local export and sync helpers

## Local Setup

1. Copy `Directory.Repo.props.user.example` to `Directory.Repo.props.user`.
2. Edit `Directory.Repo.props.user` so `RepoGameDir` and `BepInExDirectory` match your machine.
3. Build the runtime project:

```powershell
dotnet build .\source\RepoDeltaForceMod.RuntimeRecovered\RepoDeltaForceMod.csproj -c Release
```

## Notes

- `Directory.Repo.props` contains generic defaults only.
- Machine-specific paths belong in `Directory.Repo.props.user`, which is ignored by git.
- The PowerShell scripts in `scripts/` are optional helpers and require explicit paths when used.
