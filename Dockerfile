FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0-preview AS publish
ARG TARGETARCH
WORKDIR /app

RUN if [ "$TARGETARCH" = "amd64" ]; then \
    echo "linux-musl-x64" > /tmp/rid; \
    elif [ "$TARGETARCH" = "arm64" ]; then \
    echo "linux-arm64" > /tmp/rid; \
    fi

COPY . .
RUN dotnet publish "chatgpt-telegram-bot.csproj" -a $(cat /tmp/rid) -c Release -o ./publish --self-contained true

FROM mcr.microsoft.com/dotnet/runtime-deps:7.0-alpine AS runtime
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["./chatgpt-telegram-bot"]
