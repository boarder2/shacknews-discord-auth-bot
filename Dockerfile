FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /build

ARG BUILD_VERSION="1.0.0.0"

COPY . .

RUN dotnet publish -c Release src/shacknews-discord-auth-bot.csproj -r linux-musl-x64 /p:BuildVersion=${BUILD_VERSION}

FROM mcr.microsoft.com/dotnet/runtime:8.0-alpine

RUN apk --no-cache add icu-libs
WORKDIR /dotnetapp

COPY --from=build /build/src/bin/Release/net8.0/linux-musl-x64/publish/ .

ENTRYPOINT ["dotnet", "shacknews-discord-auth-bot.dll"]