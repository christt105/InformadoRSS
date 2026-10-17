# InformadoRSS

Lightweight single-container RSS/Atom to Discord forwarder, built as a replacement for [MonitoRSS](https://github.com/synzen/MonitoRSS).

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

# Backend
dotnet run --project src/InformadoRSS
```

The app listens on <http://localhost:8420>. `GET /health` returns `{"status":"ok","version":"..."}`.

For frontend work, run the backend and `npm run dev` in `web/`; the Vite dev server proxies `/api` and `/health` to port 8420.

Tests:

```sh
dotnet test
```

Native AOT binary:

```sh
dotnet publish src/InformadoRSS -c Release -r linux-x64 -o out
```

## Docker

```sh
docker build -t informadorss .
docker run -d --name informadorss -p 8420:8420 -v informadorss-data:/data informadorss
```

Images are published to `ghcr.io/christt105/informadorss`: `X.Y.Z` and `latest` for releases, `pr-N` for each pull request.

The image is based on a chiseled (distroless) runtime and runs as the non-root `app` user (UID 1654). When bind-mounting a host directory to `/data`, make sure that user can write to it. The container health check runs `InformadoRSS --healthcheck`, which requests `/health` and exits with 0 or 1; an optional URL argument overrides the default `http://localhost:8420/health`.

## License

[MIT](LICENSE)
