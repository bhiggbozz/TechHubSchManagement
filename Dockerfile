FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["TechhubMS.csproj", "."]
COPY ["TechHub.Core/TechHub.Core.csproj", "TechHub.Core/"]
COPY ["TechHub.Service/TechHub.Service.csproj", "TechHub.Service/"]
COPY ["TechHub.Background/TechHub.Background.csproj", "TechHub.Background/"]
COPY ["TeachHub.QuestionBank/TechHub.QuestionBank.csproj", "TechHub.QuestionBank/"]

RUN dotnet restore "TechhubMS.csproj"

COPY . .

# csproj is at /src level not /src/TechhubMS
RUN dotnet build "TechhubMS.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "TechhubMS.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TechhubMS.dll"]