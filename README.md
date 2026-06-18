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

Pull from Docker Hub (or build locally with `docker build -t curatarr .`), then run with a persistent `/config` volume plus your source trees mounted read-only and your destination trees mounted writable (the scheduled cleanup task deletes excessive subtitles from the destination):

```sh
docker run -d \
  --name curatarr \
  --user 1000:1000 \
  -p 9595:8080 \
  -v /path/to/curatarr/config:/config \
  -v /path/to/series/source:/media/series-source:ro \
  -v /path/to/series/destination:/media/series-destination \
  -v /path/to/movies/source:/media/movie-source:ro \
  -v /path/to/movies/destination:/media/movie-destination \
  -e Sonarr__Url=http://sonarr:8989 \
  -e Sonarr__ApiKey=your-sonarr-api-key \
  -e Radarr__Url=http://radarr:7878 \
  -e Radarr__ApiKey=your-radarr-api-key \
  -e Bazarr__Url=http://bazarr:6767 \
  -e Bazarr__ApiKey=your-bazarr-api-key \
  -e SeriesSource__Root=/media/series-source \
  -e SeriesDestination__Root=/media/series-destination \
  -e MovieSource__Root=/media/movie-source \
  -e MovieDestination__Root=/media/movie-destination \
  wenzzzel/curatarr:latest
```

All settings can be overridden with environment variables using `__` as the section separator (e.g. `Sonarr__ApiKey`, `ConnectionStrings__Curatarr`). The SQLite database lives at `/config/curatarr.db` by default, so anything mounted at `/config` persists across container restarts.

The container runs as `1000:1000` in the examples above so that it can write to the destination trees (the cleanup task deletes excessive subtitles). Adjust the UID and GID to match whichever user owns your destination files — typically the same one the rest of your *arr stack runs as. The `/config` directory must also be writable by that same UID, so either create it with the right ownership ahead of time or run the container as a user that already has access.

### Docker Compose

Equivalent setup as a `docker-compose.yml`:

```yaml
services:
  curatarr:
    image: wenzzzel/curatarr:latest
    container_name: curatarr
    user: "1000:1000"
    ports:
      - "9595:8080"
    volumes:
      - /path/to/curatarr/config:/config
      - /path/to/series/source:/media/series-source:ro
      - /path/to/series/destination:/media/series-destination
      - /path/to/movies/source:/media/movie-source:ro
      - /path/to/movies/destination:/media/movie-destination
    environment:
      Sonarr__Url: http://sonarr:8989
      Sonarr__ApiKey: your-sonarr-api-key
      Radarr__Url: http://radarr:7878
      Radarr__ApiKey: your-radarr-api-key
      Bazarr__Url: http://bazarr:6767
      Bazarr__ApiKey: your-bazarr-api-key
      SeriesSource__Root: /media/series-source
      SeriesDestination__Root: /media/series-destination
      MovieSource__Root: /media/movie-source
      MovieDestination__Root: /media/movie-destination
    restart: unless-stopped
```

Start it with `docker compose up -d`. Swap `image:` for `build: .` to build from a local checkout instead of pulling from Docker Hub.

## Status

Early development. The schema and integrations are still taking shape — expect things to move around.
