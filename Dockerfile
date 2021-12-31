FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /build

ARG BUILD_VERSION="1.0.0.0"

COPY . .

RUN dotnet publish -c Release src/shacknews-discord-auth-bot.csproj -r linux-musl-x64 --no-self-contained /p:BuildVersion=${BUILD_VERSION}

FROM mcr.microsoft.com/dotnet/runtime:6.0-alpine

RUN apk --no-cache add icu-libs
WORKDIR /dotnetapp

COPY --from=build /build/src/bin/Release/net6.0/linux-musl-x64/publish/ .

ENTRYPOINT ["./shacknews-discord-auth-bot"]