FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
ENV ASPNETCORE_HTTP_PORTS=80
ENV DOTNET_USE_POLLING_FILE_WATCHER=1

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore "TechSchPlatform.Api/TechSchPlatform.Api.csproj"
RUN dotnet build "TechSchPlatform.Api/TechSchPlatform.Api.csproj" \
    -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "TechSchPlatform.Api/TechSchPlatform.Api.csproj" \
    -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TechSchPlatform.Api.dll"]