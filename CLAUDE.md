# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

SmartStudy is a student planning web app (Stage 1 MVP). Students manage courses, tasks, exams, and events with stress/workload analysis and peer collaboration features. All data entry is manual — no external integrations.

## Tech Stack

- **Frontend**: Static HTML5/CSS3/Vanilla JS (ES6 modules) — no build step, no bundler
- **Backend**: ASP.NET Core, C#, .NET 6.0
- **Database**: SQL Server with EF Core 6.0, auto-creates tables on startup

## Running the Project

### Backend
```bash
cd "Server/SmartStudy"
dotnet run                          # HTTP on localhost:5071
```

### Frontend
Served automatically by the backend from `Front/` directory. Navigate to `http://localhost:5071` after starting the server.

### Database
Tables are auto-created and seeded on first run via `SeedDataService`. Target SQL Server configured in `appsettings.json`.

## Architecture

### Three-Tier Design
```
Browser (HTML/CSS/JS)  →  AJAX (JSON)  →  ASP.NET Core API  →  EF Core  →  SQL Server
```

### Frontend Architecture
- **Pages** (`Front/Pages/`): 13 standalone HTML files (Login, Onboarding x4, Dashboard, Tasks, Calendar, Courses, Exams, Analytics, Friends, Settings). Each sets `data-page` and optionally `data-layout="onboarding"` on `<body>`.
- **Entry point** (`Front/Script/main.js`): Detects onboarding vs app pages, initializes layout shell, sidebar, and modals.
- **Layout shell** (`Front/Script/modules/layout.js`): Dynamically injects sidebar + topbar around `#pageRoot` content. Navigation defined in `NAV` array — add new pages there.
- **Modals** (`Front/Script/modules/modals.js`): Generic open/close by element ID. Supports backdrop click and ESC.
- **Styling** (`Front/CSS/app.css`): Single file, ~5600 lines. Uses CSS custom properties for theming. All component styles are here.
- **API client** (`Front/Script/modules/api.js`): 40+ endpoint mappings, JWT auth headers, 401 redirect handling.

### Backend Architecture
- `Controllers/` — 11 API controllers (Auth, Courses, Tasks, Exams, Events, Stress, Dashboard, Settings, Scheduling, ScheduleImport, Connections, Collaboration)
- `Models/` — 13 EF Core entity classes with navigation properties
- `DTOs/` — Request/response objects (separate files per domain)
- `Services/` — Business logic (StressService, SchedulingService, ScheduleImportService, SafeZoneService, SeedDataService)
- `Data/` — SmartStudyDbContext with TPT event inheritance

### Database Schema
13 tables with `SmartStudy_` prefix. Key design decisions:
- **Users PK is Email** (NVARCHAR), not a surrogate integer key
- **Events use TPT inheritance**: base `SmartStudy_Events` table with four subtype tables (ClassEvents, TaskEvents, WorkEvents, PersonalEvents) joined on EventId FK
- **Tasks and Exams link to Courses** via CourseId FK with CASCADE delete
- **UserCourses** is a junction table for N:N enrollment (Email + CourseId composite PK)
- **StudyConnections** tracks peer collaboration requests (Pending/Accepted status)

## Key Domain Algorithms

### Stress Score (0–100)
```
required_hours = sum of EstimatedHours for incomplete tasks/exams before nearest deadline
available_hours = hours until deadline minus sleep baseline (8h/day)
score = min(100, required_hours / available_hours * 100)
```
Thresholds: Green (0–40), Orange (40–70), Red (70–100).

### Collaboration Safe-Zone
Intersect two users' 30-minute availability grids, then filter slots where both users have stress < 60%.

### Automatic Scheduling Engine
Greedy algorithm that assigns task study blocks into free calendar slots:
- Priority scoring: (1/daysUntilDue)*40 + priorityWeight*30 + min(hours,10)*3
- Constraints: Max 8h/day, max 3h continuous per task, 30-min slots, 8AM-10PM
- Re-runs after task create/update/delete/complete

## Design System
- **Primary**: #F28D35 (orange), **Accent**: #54BFB5 (teal)
- **Event colors**: Cyan (classes), Orange (tasks), Purple #9B76FF (work), Yellow #F2C777 (personal), Pink #FF607E (exams)
- **Border radius**: 18px cards, 14px buttons
- **Mobile**: Hamburger sidebar, responsive calendar

## API Endpoints
All under `/api`, JSON in/out, JWT authenticated (except auth endpoints):
- **Auth**: `POST /api/auth/login`, `/register`, `/logout`
- **Courses**: CRUD at `/api/courses`
- **Tasks**: CRUD at `/api/tasks` + `POST /api/tasks/{id}/complete`
- **Exams**: CRUD at `/api/exams`
- **Events**: CRUD with subtype endpoints (`/events/class`, `/events/task`, `/events/work`, `/events/personal`)
- **Stress**: `GET /api/stress/score`, `/api/stress/weekly`
- **Dashboard**: `GET /api/dashboard` (aggregated view)
- **Scheduling**: `POST /api/scheduling/run`, `GET /api/scheduling/status`
- **Settings**: `GET/PUT /api/settings/profile`, `PUT /api/settings/notifications`
- **Connections**: `GET /api/connections`, `POST /api/connections/invite`, `POST /api/connections/{id}/accept`, `POST /api/connections/{id}/decline`, `DELETE /api/connections/{id}`
- **Collaboration**: `GET /api/collaboration/safe-zones?connectionId=...`
- **Import**: `POST /api/schedule/import` (PDF parsing)

## Implementation Status
- **Complete**: All 13 frontend pages, CSS design system, JS module system, 40+ API endpoints, EF Core entities/DbContext, JWT authentication, stress service, scheduling engine, collaboration module, PDF import, auto-seeding
