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
    package Serilog.AspNetCore \
    --version 8.0.0

RUN dotnet add TechhubMS.csproj \
    package Serilog.Settings.Configuration \
    --version 8.0.0

# ✅ Remove Microsoft.Extensions.Logging from main project
# It flows down and conflicts with Serilog.ILogger
# in TechHub.Service files
RUN dotnet remove TechhubMS.csproj \
    package Microsoft.Extensions.Logging

# ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
# FIX TechHub.Core
# ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
RUN dotnet add TechHub.Core/TechHub.Core.csproj \
    package Microsoft.Data.SqlClient \
    --version 5.2.0

# ✅ AutoMapperProfile.cs uses Profile from AutoMapper
# TechHub.Core has AutoMapper but Profile class
# needs explicit package reference
RUN dotnet add TechHub.Core/TechHub.Core.csproj \
    package AutoMapper \
    --version 14.0.0

# ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
# FIX TechHub.Entity.Migration
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
# ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

# ✅ Add AutoMapper — fixes IMapper in SchoolService/UserService
RUN dotnet add TechHub.Service/TechHub.Service.csproj \
    package AutoMapper \
    --version 14.0.0

# ✅ Add Dapper
RUN dotnet add TechHub.Service/TechHub.Service.csproj \
    package Dapper \
    --version 2.1.28

# ✅ Add Azure — SchoolService imports Azure namespace
RUN dotnet add TechHub.Service/TechHub.Service.csproj \
    package Azure.Storage.Blobs \
    --version 12.19.1

# ✅ Add JWT — UserService generates tokens
RUN dotnet add TechHub.Service/TechHub.Service.csproj \
    package Microsoft.IdentityModel.Tokens \
    --version 8.0.0

RUN dotnet add TechHub.Service/TechHub.Service.csproj \
    package System.IdentityModel.Tokens.Jwt \
    --version 8.0.0

# ✅ Remove Microsoft.Extensions.Logging from Service
# It conflicts with Serilog.ILogger
# TeacherTrustScoreService, TenantService,
# SchoolService, UserService all use Serilog.ILogger
# Having both causes ambiguous reference error
RUN dotnet remove TechHub.Service/TechHub.Service.csproj \
    package Microsoft.Extensions.Logging

# ✅ Downgrade Configuration packages
RUN dotnet add TechHub.Service/TechHub.Service.csproj \
    package Microsoft.Extensions.Configuration.Abstractions \
    --version 8.0.0

# ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
# FIX TechHub.Background
# AutomaticRetry and Queue attributes
# are in Hangfire.Core
# TechHub.Background already has Hangfire 1.8.23
# which includes Hangfire.Core
# But the attributes may not be resolving
# ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

# ✅ Explicitly add Hangfire.Core separately
RUN dotnet add TechHub.Background/TechHub.Background.csproj \
    package Hangfire.Core \
    --version 1.8.23

# ✅ Downgrade Serilog.Extensions.Hosting
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
# RESTORE AFTER ALL PACKAGE CHANGES
# Must restore AFTER all dotnet add/remove commands
# so build uses updated packages
# ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
RUN dotnet restore "TechhubMS.csproj"

# Build WITHOUT --no-restore so it
# uses the fresh restore above
RUN dotnet build "TechhubMS.csproj" \
    -c Release \
    -o /app/build

FROM build AS publish
RUN dotnet publish "TechhubMS.csproj" \
    -c Release \
    -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TechhubMS.dll"]