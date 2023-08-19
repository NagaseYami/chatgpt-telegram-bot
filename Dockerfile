FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0-preview AS publish
ARG TARGETARCH
WORKDIR /app

RUN if [ "$TARGETARCH" = "amd64" ]; then \
    RID="linux-musl-x64"; \
    elif [ "$TARGETARCH" = "arm64" ]; then \
    RID="linux-arm64"; \
    fi

COPY . .
RUN dotnet publish "chatgpt-telegram-bot.csproj" -a $RID -c Release -o ./publish --self-contained true

FROM mcr.microsoft.com/dotnet/runtime-deps:7.0-alpine AS runtime
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["./chatgpt-telegram-bot"]
