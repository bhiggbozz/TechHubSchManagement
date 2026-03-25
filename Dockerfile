FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY . .

# ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
# FIX MAIN PROJECT
# ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
RUN dotnet add TechhubMS.csproj \
    package Microsoft.AspNetCore.SignalR.Common \
    --version 8.0.0

RUN dotnet add TechhubMS.csproj \
    package Microsoft.EntityFrameworkCore.Design \
    --version 8.0.0

RUN dotnet add TechhubMS.csproj \
    package Microsoft.EntityFrameworkCore.SqlServer \
    --version 8.0.0

RUN dotnet add TechhubMS.csproj \
    package Microsoft.Extensions.Logging \
    --version 8.0.0

RUN dotnet add TechhubMS.csproj \
    package Serilog.AspNetCore \
    --version 8.0.0

RUN dotnet add TechhubMS.csproj \
    package Serilog.Settings.Configuration \
    --version 8.0.0

# ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
# FIX TechHub.Core
# QueryBuilder.cs uses DynamicParameters from Dapper
# Already has Dapper and AutoMapper but
# missing Microsoft.Data.SqlClient
# ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
RUN dotnet add TechHub.Core/TechHub.Core.csproj \
    package Microsoft.Data.SqlClient \
    --version 5.2.0

# ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
# FIX TechHub.Entity.Migration
# Downgrade EF Core 9 to 8
# ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
RUN dotnet add TechHub.Entity.Migration/TechHub.Entity.Migration.csproj \
    package Microsoft.EntityFrameworkCore \
    --version 8.0.0

RUN dotnet add TechHub.Entity.Migration/TechHub.Entity.Migration.csproj \
    package Microsoft.EntityFrameworkCore.Design \
    --version 8.0.0

RUN dotnet add TechHub.Entity.Migration/TechHub.Entity.Migration.csproj \
    package Microsoft.EntityFrameworkCore.SqlServer \
    --version 8.0.0

RUN dotnet add TechHub.Entity.Migration/TechHub.Entity.Migration.csproj \
    package Microsoft.EntityFrameworkCore.Tools \
    --version 8.0.0

# ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
# FIX TechHub.Service
# SchoolService.cs uses: AutoMapper, Dapper, Azure
# UserService.cs uses: AutoMapper, Dapper
# These are confirmed missing from TechHub.Service.csproj
# ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
RUN dotnet add TechHub.Service/TechHub.Service.csproj \
    package AutoMapper \
    --version 14.0.0
# ✅ Matches version in TechHub.Core
# Fixes IMapper not found in SchoolService and UserService

RUN dotnet add TechHub.Service/TechHub.Service.csproj \
    package Dapper \
    --version 2.1.28
# ✅ SchoolService and UserService use Dapper directly

RUN dotnet add TechHub.Service/TechHub.Service.csproj \
    package Azure.Storage.Blobs \
    --version 12.19.1
# ✅ SchoolService imports Azure namespace

RUN dotnet add TechHub.Service/TechHub.Service.csproj \
    package Microsoft.Extensions.Configuration \
    --version 8.0.0
# ✅ Both services use IConfiguration

RUN dotnet add TechHub.Service/TechHub.Service.csproj \
    package Microsoft.Extensions.Configuration.Abstractions \
    --version 8.0.0

RUN dotnet add TechHub.Service/TechHub.Service.csproj \
    package Microsoft.IdentityModel.Tokens \
    --version 8.0.0
# ✅ UserService uses JWT token generation

RUN dotnet add TechHub.Service/TechHub.Service.csproj \
    package System.IdentityModel.Tokens.Jwt \
    --version 8.0.0
# ✅ UserService uses JwtSecurityToken

# ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
# FIX TechHub.Background
# Downgrade Serilog.Extensions.Hosting 10.0.0 → 8.0.0
# ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
RUN dotnet add TechHub.Background/TechHub.Background.csproj \
    package Serilog.Extensions.Hosting \
    --version 8.0.0

# ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
# FIX TeachHub.QuestionBank
# ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
RUN dotnet add TeachHub.QuestionBank/TechHub.QuestionBank.csproj \
    package Serilog \
    --version 4.3.1

RUN dotnet add TeachHub.QuestionBank/TechHub.QuestionBank.csproj \
    package Dapper \
    --version 2.1.28

RUN dotnet add TeachHub.QuestionBank/TechHub.QuestionBank.csproj \
    package Microsoft.Data.SqlClient \
    --version 5.2.0

RUN dotnet add TeachHub.QuestionBank/TechHub.QuestionBank.csproj \
    package CloudinaryDotNet \
    --version 1.28.0

RUN dotnet add TeachHub.QuestionBank/TechHub.QuestionBank.csproj \
    package Microsoft.AspNetCore.Authorization \
    --version 8.0.0

RUN dotnet add TeachHub.QuestionBank/TechHub.QuestionBank.csproj \
    package AutoMapper \
    --version 14.0.0

# ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
# RESTORE AND BUILD
# ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
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