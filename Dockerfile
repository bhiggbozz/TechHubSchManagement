FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80

# ── Install SkiaSharp native dependencies ────────────────────────────────
RUN apt-get update && apt-get install -y \
    libfontconfig1 \
    libfreetype6 \
    libpng16-16 \
    libgdiplus \
    && rm -rf /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore "TechhubMS/TechhubMS.csproj"
RUN dotnet build "TechhubMS/TechhubMS.csproj" \
    -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "TechhubMS/TechhubMS.csproj" \
    -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TechhubMS.dll"]