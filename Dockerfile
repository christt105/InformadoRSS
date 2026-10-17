# syntax=docker/dockerfile:1

FROM node:22-alpine AS web
WORKDIR /web
COPY web/package.json web/package-lock.json ./
RUN npm ci
COPY web/ ./
RUN npm run build -- --outDir /web/dist

FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS build
RUN apt-get update \
    && apt-get install -y --no-install-recommends clang zlib1g-dev \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /src
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/InformadoRSS/InformadoRSS.csproj src/InformadoRSS/
RUN dotnet restore src/InformadoRSS/InformadoRSS.csproj -r linux-x64
COPY src/ src/
COPY --from=web /web/dist src/InformadoRSS/wwwroot/
ARG VERSION=0.0.0-dev
RUN dotnet publish src/InformadoRSS/InformadoRSS.csproj -c Release -r linux-x64 --no-restore \
        -p:InformationalVersion=${VERSION} -o /app \
    && rm -f /app/*.dbg \
    && mkdir /data

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-noble-chiseled
WORKDIR /app
COPY --from=build /app ./
COPY --from=build --chown=$APP_UID:$APP_UID /data /data
ENV ASPNETCORE_URLS=http://+:8420 \
    ASPNETCORE_HTTP_PORTS=
EXPOSE 8420
VOLUME /data
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD ["/app/InformadoRSS", "--healthcheck"]
ENTRYPOINT ["/app/InformadoRSS"]
