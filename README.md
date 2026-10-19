# InformadoRSS

Lightweight single-container RSS/Atom to Discord forwarder.

It polls feeds, filters items per route and posts them to Discord channels through the bot REST API (no gateway connection, no bot commands). Configuration lives in SQLite and is edited from a small web UI served by the same binary.

## Status

Early development. Only the project skeleton exists so far: the server, the `/health` endpoint and an empty web page. It does not deliver anything yet.

## Stack

- .NET 10 ASP.NET Core Minimal API, compiled with NativeAOT
- Vue 3 + Vite + TypeScript frontend, served as static files by the binary
- SQLite for configuration and state (in `/data` inside the container)

## Running locally

Requirements: .NET 10 SDK and Node.js 22. Native AOT publishing additionally needs `clang` and `zlib1g-dev`.

```sh
# Frontend: writes the build into src/InformadoRSS/wwwroot
cd web
npm ci
npm run build
cd ..

# Backend, with the database in the working directory instead of /data
Database__Path=./informadorss.db dotnet run --project src/InformadoRSS
```

The SQLite database path comes from the `Database:Path` setting (default `/data/informadorss.db`); the schema is created or upgraded on startup. The app listens on <http://localhost:8420>. `GET /health` returns `{"status":"ok","version":"..."}`.

For frontend work, run the backend and `npm run dev` in `web/`; the Vite dev server proxies `/api` and `/health` to port 8420.

Tests:

```sh
dotnet test
```

Native AOT binary:

```sh
dotnet publish src/InformadoRSS -c Release -r linux-x64 -o out
```

## Alerts and ntfy

InformadoRSS sends one alert when a feed or destination breaks and one when it recovers. A feed counts as broken after it has failed continuously for a while (6 hours by default, so short daily outages such as YouTube feeds answering 404 for a few hours stay quiet); a destination counts as broken when its sink rejects it permanently (for example a deleted Discord channel). What was alerted is stored in the database, so restarts do not repeat alerts. Without a configured target, alerts are only logged.

Settings (in the `settings` table):

| Key | Meaning |
|---|---|
| `alerts.target` | `discord` (default) or `ntfy` |
| `alerts.discordChannelId` | Discord channel that receives alerts |
| `alerts.discordUserId` | Discord user that receives alerts by direct message, used when no channel is set |
| `alerts.ntfyTopic` | ntfy topic that receives alerts |
| `alerts.feedBrokenAfterMinutes` | Minutes without a successful fetch before a feed is reported, default `360` |
| `ntfy.url` | Base URL of the ntfy server, e.g. `http://192.168.1.15:8094` |
| `ntfy.token` | Optional access token (`tk_...`), or `user:password` for basic auth |

ntfy also works as a route destination: sink `ntfy` with a `topic` parameter.

## Docker

```sh
docker build -t informadorss .
docker run -d --name informadorss -p 8420:8420 -v informadorss-data:/data informadorss
```

Images are published to `ghcr.io/christt105/informadorss`: `X.Y.Z` and `latest` for releases, `pr-N` for each pull request.

The image is based on a chiseled (distroless) runtime and runs as the non-root `app` user (UID 1654). When bind-mounting a host directory to `/data`, make sure that user can write to it. The container health check runs `InformadoRSS --healthcheck`, which requests `/health` and exits with 0 or 1; an optional URL argument overrides the default `http://localhost:8420/health`.

## License

[MIT](LICENSE)
