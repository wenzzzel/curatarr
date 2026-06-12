# curatarr

A reconciliation tool that sits above an *arr media stack and keeps a curated copy of your library in sync with the originals.

## The idea

Many *arr setups keep two parallel media trees:

- **Source** — the originals that Sonarr / Radarr / Bazarr manage. Subtitle matching, upgrades, and metadata all happen here.
- **Destination** — a re-encoded, normalised copy (e.g. h264 for wide device support) that the media server (Jellyfin / Emby) actually serves.

The two trees drift over time: upgrades land in source but the destination still holds the old version, subtitles get out of sync, files go missing on one side and not the other. **curatarr** compares the two trees and surfaces what's out of step, so you can fix it.

## Stack

- ASP.NET Core + Blazor Server (UI), styled with [MudBlazor](https://mudblazor.com)
- EF Core + SQLite (state)
- Runs as a Docker container alongside the rest of the stack

## Running locally

```sh
dotnet run --project src/Curatarr
```

Configuration lives in `src/Curatarr/appsettings.json` and can be overridden with environment variables (standard ASP.NET conventions). The SQLite file is created on first run and is gitignored.

## Running in Docker

Pull from Docker Hub (or build locally with `docker build -t curatarr .`), then run with a persistent `/config` volume plus your source and destination trees mounted read-only:

```sh
docker run -d \
  --name curatarr \
  -p 9595:8080 \
  -v /path/to/curatarr/config:/config \
  -v /path/to/source:/media/source:ro \
  -v /path/to/destination:/media/destination:ro \
  -e Sonarr__Url=http://sonarr:8989 \
  -e Sonarr__ApiKey=your-api-key \
  -e SeriesSource__Root=/media/source \
  -e SeriesDestination__Root=/media/destination \
  wenzzzel/curatarr:latest
```

All settings can be overridden with environment variables using `__` as the section separator (e.g. `Sonarr__ApiKey`, `ConnectionStrings__Curatarr`). The SQLite database lives at `/config/curatarr.db` by default, so anything mounted at `/config` persists across container restarts.

The mounted `/config` directory must be writable by UID `1654` (the `app` user in Microsoft's .NET runtime image).

### Docker Compose

Equivalent setup as a `docker-compose.yml`:

```yaml
services:
  curatarr:
    image: wenzzzel/curatarr:latest
    container_name: curatarr
    ports:
      - "9595:8080"
    volumes:
      - /path/to/curatarr/config:/config
      - /path/to/source:/media/source:ro
      - /path/to/destination:/media/destination:ro
    environment:
      Sonarr__Url: http://sonarr:8989
      Sonarr__ApiKey: your-api-key
      SeriesSource__Root: /media/source
      SeriesDestination__Root: /media/destination
    restart: unless-stopped
```

Start it with `docker compose up -d`. Swap `image:` for `build: .` to build from a local checkout instead of pulling from Docker Hub.

## Status

Early development. The schema and integrations are still taking shape — expect things to move around.
