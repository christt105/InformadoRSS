# InformadoRSS

Lightweight single-container RSS/Atom to Discord forwarder.

It polls feeds, filters items per route and posts them to Discord channels through the bot REST API (no gateway connection, no bot commands). Configuration lives in SQLite and is edited from a small web UI served by the same binary.

## Status

In use since October 2026 as a replacement for MonitoRSS, running 19 feeds and about 30 routes on a home server in a single container (about 75 MiB of RAM and a 37 MB image). Feeds are polled, filtered per route and delivered to Discord or ntfy, with alerts for broken feeds and destinations. The web UI manages feeds, routes (filter builder, template editor, Discord server and channel picker) and settings; it has no login and is meant for a trusted LAN.

The UI talks to a JSON API under `/api`: `/api/feeds` (list with health, newest item and routes; create, read, replace, delete; `POST /api/feeds/validate` to check a URL without saving), `/api/feeds/{feedId}/routes` and `/api/routes/{id}` (plus `POST /api/routes/validate`), `/api/settings` (secrets such as the bot token are write-only), `/api/meta`, `/api/discord/...` and `/api/import/...`.

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

## Feeds that rewrite entries

Items are deduplicated by their id (`guid` or Atom `id`). Some feeds keep one entry per thing and rewrite it in place: a MyAnimeList list feed (`rss.php?type=rw&u=<user>`) has one entry per anime whose description goes from `Watching - 1 of 12 episodes` to `Completed - 12 of 12 episodes`, so the change is never sent. A feed can set an identity pattern, a regular expression over the plain-text description whose first match is appended to the item id. With `^[^-]+` on a MyAnimeList feed, each status (`Watching`, `Completed`, `Dropped`, ...) is a new item while episode progress inside a status is not. Changing the pattern marks the items currently in the feed as seen without sending them.

## Old items and manual sends

Items published more than `Scheduler:MaxItemAge` ago (default `7.00:00:00`, set it with the `Scheduler__MaxItemAge` environment variable) are marked as seen without being sent, so a long outage does not flood the channels when the service comes back. Items without a publication date are not affected.

The feed page of the web UI has an Items section that fetches the feed and shows what each route did with every item. From there an item can be sent right away, to a chosen Discord channel or to the route destinations, without changing any state, or reset so that routes forget it and the next check treats it as new (for example after fixing a wrong publication date; an item still older than the maximum age is skipped again). The same actions are available at `GET /api/feeds/{id}/items`, `POST /api/feeds/{id}/items/send` and `POST /api/feeds/{id}/items/reset`.

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

## Backups

InformadoRSS can copy its database to a folder on a schedule. Backups are off until `backups.directory` is set, usually to a mounted volume such as `/backups`. A backup is taken when the newest one in the folder is older than the interval, so restarts do not add extra copies; after each one the oldest files beyond the configured count are deleted. Copies are written with SQLite's `VACUUM INTO`, which is consistent while the app keeps running, and named `informadorss-yyyyMMddTHHmmssZ.db` after their UTC time. Other files in the folder are left alone.

| Key | Meaning |
| --- | --- |
| `backups.directory` | Folder inside the container that receives the backups; empty disables them |
| `backups.intervalHours` | Hours between two backups, default `24` |
| `backups.keep` | Number of backups to keep, default `14` |

A failed backup is retried every 15 minutes and raises one alert through the alert target above, plus one more when it works again.

To restore, stop the container, replace `informadorss.db` in the data folder with the chosen backup (and delete any `informadorss.db-wal` and `informadorss.db-shm` next to it), then start the container again.

## Docker

```sh
docker build -t informadorss .
docker run -d --name informadorss -p 8420:8420 -v informadorss-data:/data -v informadorss-backups:/backups informadorss
```

Images are published to `ghcr.io/christt105/informadorss`: `X.Y.Z` and `latest` for releases, `pr-N` for each pull request.

The image is based on a chiseled (distroless) runtime and runs as the non-root `app` user (UID 1654). When bind-mounting a host directory to `/data`, make sure that user can write to it. The container health check runs `InformadoRSS --healthcheck`, which requests `/health` and exits with 0 or 1; an optional URL argument overrides the default `http://localhost:8420/health`.

## Importing from MonitoRSS

Export MonitoRSS's `userfeeds` collection from its Mongo container (database `rss` in the default MonitoRSS stack):

```sh
docker exec <mongo-container> mongoexport --db rss --collection userfeeds --jsonArray --out /tmp/userfeeds.json
docker cp <mongo-container>:/tmp/userfeeds.json .
```

Then run the importer against the same database the server uses, or post the file to `POST /api/import/monitorss`:

```sh
InformadoRSS import-monitorss userfeeds.json --dry-run
InformadoRSS import-monitorss userfeeds.json --test-channel <channel id> [--test-guild <guild id>]
InformadoRSS send-latest [--channel <channel id>] [--feed <feed id>] [--include-disabled]
InformadoRSS import-monitorss userfeeds.json
```

Each MonitoRSS feed becomes a feed (feeds with the same URL are merged) and each Discord channel connection a route with one Discord destination. Templates, `CONTAINS`/`EQ`/`MATCHES` filters with `not`, and AND/OR groups are translated. Anything else is listed in the report: options that only change details (formatter flags, placeholder limits, mentions, date checks) are dropped with a note, while connections with embeds, buttons, webhooks, threads or an untranslatable filter are imported disabled, with their text content and without the filter, for review. Connections without a channel are skipped.

Imported routes start unseeded, so the first fetch marks every current item as seen without posting it. `--test-channel` redirects every destination to one channel (and `--test-guild` sets its server). `send-latest` posts the newest item that passes each route filter, without touching the seen items, to check the result. Re-running the import is safe: routes are matched to their MonitoRSS connection, so running it again without `--test-channel` moves the test routes to their real channels and keeps their seen items.

In the container, run the commands with `docker exec informadorss /app/InformadoRSS ...` after copying the file into `/data`.

## License

[MIT](LICENSE)
