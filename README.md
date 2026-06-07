# curatarr

A reconciliation tool that sits above an *arr media stack and keeps a curated copy of your library in sync with the originals.

## The idea

Many *arr setups keep two parallel media trees:

- **Source** — the originals that Sonarr / Radarr / Bazarr manage. Subtitle matching, upgrades, and metadata all happen here.
- **Destination** — a re-encoded, normalised copy (e.g. h264 for wide device support) that the media server (Jellyfin / Emby) actually serves.

The two trees drift over time: upgrades land in source but the destination still holds the old version, subtitles get out of sync, files go missing on one side and not the other. **curatarr** compares the two trees and surfaces what's out of step, so you can fix it.

## Stack

- ASP.NET Core + Blazor Server (UI)
- EF Core + SQLite (state)
- Runs as a Docker container alongside the rest of the stack

## Running locally

```sh
dotnet run --project src/Curatarr
```

Configuration lives in `src/Curatarr/appsettings.json` and can be overridden with environment variables (standard ASP.NET conventions). The SQLite file is created on first run and is gitignored.

## Status

Early development. The schema and integrations are still taking shape — expect things to move around.
