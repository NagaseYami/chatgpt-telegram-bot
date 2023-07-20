FROM mcr.microsoft.com/dotnet/sdk:7.0-alpine AS publish
WORKDIR /app
COPY . .
RUN dotnet restore "chatgpt-telegram-bot.csproj"
RUN dotnet publish "chatgpt-telegram-bot.csproj" -c Release -o ./publish

FROM mcr.microsoft.com/dotnet/runtime:7.0-alpine AS runtime
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "chatgpt-telegram-bot.dll"]
