FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY . .

# Restore via main project not solution file
# ProjectReferences pull in all dependencies
RUN dotnet restore "TechhubMS.csproj"

RUN dotnet build "TechhubMS.csproj" \
    -c Release \
    --no-restore \
    -o /app/build

FROM build AS publish
RUN dotnet publish "TechhubMS.csproj" \
    -c Release \
    --no-restore \
    -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TechhubMS.dll"]