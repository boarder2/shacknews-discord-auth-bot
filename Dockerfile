FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build
WORKDIR /build

ARG BUILD_VERSION="1.0.0.0"

COPY . .

RUN dotnet publish -c Release src/shacknews-discord-auth-bot.csproj -r alpine-x64 /p:BuildVersion=${BUILD_VERSION}

FROM mcr.microsoft.com/dotnet/runtime:5.0-alpine

WORKDIR /dotnetapp

COPY --from=build /build/src/bin/Release/net5.0/alpine-x64/publish/ .

ENTRYPOINT ["./shacknews-discord-auth-bot"]