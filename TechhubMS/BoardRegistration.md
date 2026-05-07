# Board Session Recording System - Registration Guide

## 1. Add NuGet Packages

Add the following packages to your projects:

### TechHub.Core.csproj
```xml
<PackageReference Include="MongoDB.Bson" Version="2.28.0" />
```

### TechHub.Service.csproj
```xml
<PackageReference Include="MongoDB.Driver" Version="2.28.0" />
<PackageReference Include="RabbitMQ.Client" Version="6.8.1" />
```

### TechHub.Background.csproj
```xml
<PackageReference Include="RabbitMQ.Client" Version="6.8.1" />
```

---

## 2. Update appsettings.json

Add the following configuration sections to your `appsettings.json`:

```json
{
  "MongoDB": {
    "ConnectionString": "mongodb://localhost:27017",
    "DatabaseName": "TechHubBoard"
  },
  "RabbitMQ": {
    "Host": "localhost",
    "Port": 5672,
    "Username": "guest",
    "Password": "guest",
    "BoardBatchQueue": "board.batch.queue"
  }
}
```

---

## 3. Update Program.cs

Add the following lines to your `Program.cs`:

```csharp
// Add these using statements at the top
using TechHub.Service.Extensions;
using TechHub.Background.Extensions;

// Add after other service registrations (before var app = builder.Build())
builder.Services.AddBoardServices(builder.Configuration);
builder.Services.AddBoardWorkers();
```

### Full Program.cs Example

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using TechHub.Background.Extensions;
using TechHub.Core.Configuration;
using TechHub.Core.Profiles;
using TechHub.QuestionBank.Controllers;
using TechHub.Service.Extensions;
using TechhubMS;
using TechhubMS.Middleware;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    // ... existing Serilog config
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog();

    // Add services to the container
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
    builder.Services.AddAutoMapper(typeof(AutoMapperProfile));
    builder.Services.AddHttpContextAccessor();
    
    // ... existing service registrations

    // Multi-tenant services
    builder.Services.AddMultiTenantServices(builder.Configuration);

    // ========== ADD BOARD SERVICES HERE ==========
    builder.Services.AddBoardServices(builder.Configuration);
    builder.Services.AddBoardWorkers();
    // =============================================

    var app = builder.Build();

    // ... rest of the pipeline configuration
    
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application failed to start");
}
finally
{
    Log.CloseAndFlush();
}
```

---

## 4. ServiceCollectionExtensions.cs Alternative

If you prefer to keep all registrations in `ServiceCollectionExtensions.cs`, add:

```csharp
using TechHub.Background.Workers;
using TechHub.Core.Configuration;
using TechHub.Service.Interface;
using TechHub.Service.Repository;
using TechHub.Service.Service;

// Inside AddMultiTenantServices method, add:

// MongoDB Configuration
services.Configure<MongoDbSettings>(
    configuration.GetSection(MongoDbSettings.SectionName));

// RabbitMQ Configuration
services.Configure<RabbitMQSettings>(
    configuration.GetSection(RabbitMQSettings.SectionName));

// Board Services
services.AddSingleton<IBoardSessionRepository, BoardSessionRepository>();
services.AddSingleton<IBoardPublisherService, BoardPublisherService>();
services.AddScoped<IBoardSessionService, BoardSessionService>();

// Board Background Worker
services.AddHostedService<BoardSyncWorker>();
```

---

## 5. API Endpoints

After registration, the following endpoints will be available:

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| POST | `/api/board/session/{sessionId}/batch` | SubjectTeacher, HeadTeacher | Submit board batch during live class |
| POST | `/api/board/session/{sessionId}/manifest` | SubjectTeacher, HeadTeacher | Submit session manifest when class ends |
| GET | `/api/board/session/{sessionId}` | SubjectTeacher, HeadTeacher, Admin | Get session details |

---

## 6. File Structure Created

```
TechHub.Core/
├── Configuration/
│   ├── MongoDbSettings.cs
│   └── RabbitMQSettings.cs
├── Entities/Board/
│   ├── AudioChunk.cs
│   ├── BoardBatch.cs
│   ├── BoardDimensions.cs
│   ├── BoardInfo.cs
│   ├── BoardSession.cs
│   ├── BoardStroke.cs
│   ├── Chapter.cs
│   ├── ClassroomInfo.cs
│   ├── LessonInfo.cs
│   ├── MediaAsset.cs
│   ├── SessionChunk.cs
│   ├── SessionStats.cs
│   ├── SessionStatus.cs
│   ├── StrokeSummary.cs
│   ├── SubjectInfo.cs
│   └── TeacherInfo.cs
├── Messages/
│   └── BoardBatchMessage.cs
└── ViewModels/Board/
    ├── BoardBatchViewModel.cs
    ├── StrokeViewModel.cs
    └── Manifest/
        ├── AudioChunkViewModel.cs
        ├── BoardDimensionsViewModel.cs
        ├── BoardInfoViewModel.cs
        ├── ChapterViewModel.cs
        ├── ChunkViewModel.cs
        ├── ClassroomInfoViewModel.cs
        ├── LessonInfoViewModel.cs
        ├── MediaAssetViewModel.cs
        ├── SessionInfoViewModel.cs
        ├── SessionManifestViewModel.cs
        ├── SessionStatsViewModel.cs
        ├── StrokeSummaryViewModel.cs
        ├── SubjectInfoViewModel.cs
        └── TeacherInfoViewModel.cs

TechHub.Service/
├── Extensions/
│   └── BoardServiceExtensions.cs
├── Interface/
│   ├── IBoardPublisherService.cs
│   ├── IBoardSessionRepository.cs
│   └── IBoardSessionService.cs
├── Repository/
│   └── BoardSessionRepository.cs
└── Service/
    ├── BoardPublisherService.cs
    └── BoardSessionService.cs

TechHub.Background/
├── Extensions/
│   └── BoardWorkerExtensions.cs
└── Workers/
    └── BoardSyncWorker.cs

TechhubMS/
└── Controllers/
    └── BoardSessionController.cs
```

---

## 7. Testing with Postman/curl

### Submit Batch
```bash
curl -X POST "https://localhost:7001/api/board/session/550e8400-e29b-41d4-a716-446655440000/batch" \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "sessionId": "550e8400-e29b-41d4-a716-446655440000",
    "lessonId": "lesson_abc123",
    "batchIndex": 0,
    "startMs": 0,
    "endMs": 60000,
    "strokeCount": 25,
    "sizeBytes": 12800,
    "strokes": []
  }'
```

### Submit Manifest
```bash
curl -X POST "https://localhost:7001/api/board/session/550e8400-e29b-41d4-a716-446655440000/manifest" \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "version": "2.0",
    "session": {
      "id": "550e8400-e29b-41d4-a716-446655440000",
      "lessonId": "lesson_abc123",
      "schoolId": "YOUR_SCHOOL_ID",
      "recordedAt": "2026-05-08T09:00:00.000Z",
      "publishedAt": "2026-05-08T09:45:30.000Z",
      "teacher": { "id": "teacher_001", "name": "Mr. Sunday Omoyele" }
    },
    "lesson": { ... },
    "stats": { ... },
    "chunks": [],
    "mediaAssets": [],
    "boards": [],
    "chapters": []
  }'
```
