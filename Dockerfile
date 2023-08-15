FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0-preview AS publish
ARG TARGETARCH
WORKDIR /app

RUN arch=$TARGETARCH \
    && if [ "$arch" = "amd64" ]; then arch="x64"; fi \
    && echo $TARGETOS-$arch > /tmp/rid

COPY . .
RUN dotnet publish "chatgpt-telegram-bot.csproj" -a $TARGETARCH -c Release -o ./publish --self-contained true

FROM mcr.microsoft.com/dotnet/runtime-deps:7.0-alpine AS runtime
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["./chatgpt-telegram-bot"]
