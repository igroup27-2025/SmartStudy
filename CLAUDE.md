# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

SmartStudy is a student planning web app (Stage 1 MVP). Students manage courses, tasks, exams, and events with stress/workload analysis and peer collaboration features. All data entry is manual — no external integrations.

## Tech Stack

- **Frontend**: Static HTML5/CSS3/Vanilla JS (ES6 modules) — no build step, no bundler
- **Backend**: ASP.NET Core Minimal API, C#, .NET 10.0
- **Database**: SQL Server with planned EF Core integration (not yet configured)

## Running the Project

### Backend
```bash
cd "Server/SmartStudy"
dotnet run                          # HTTP on localhost:5193
dotnet run --launch-profile https   # HTTPS on localhost:7211
```

### Frontend
Open any HTML file in `Front/Pages/` directly in a browser. No build step required. Pages load JS modules from `Front/Script/main.js`.

### Database
Execute the `SQL` file against a SQL Server instance to create all 12 tables (prefixed `SmartStudy_`).

## Architecture

### Three-Tier Design
```
Browser (HTML/CSS/JS)  →  AJAX (JSON)  →  ASP.NET Core API  →  EF Core  →  SQL Server
```

### Frontend Architecture
- **Pages** (`Front/Pages/`): 12 standalone HTML files. Each sets `data-page` and optionally `data-layout="onboarding"` on `<body>`.
- **Entry point** (`Front/Script/main.js`): Detects onboarding vs app pages, initializes layout shell, sidebar, and modals.
- **Layout shell** (`Front/Script/modules/layout.js`): Dynamically injects sidebar + topbar around `#pageRoot` content. Navigation defined in `NAV` array — add new pages there.
- **Modals** (`Front/Script/modules/modals.js`): Generic open/close by element ID. Supports backdrop click and ESC.
- **Styling** (`Front/CSS/app.css`): Single file, ~3000 lines. Uses CSS custom properties for theming. All component styles are here.

### Backend Architecture (Planned — not yet implemented)
The backend currently has only a demo `/weatherforecast` endpoint. The planned structure per `technical_design.md`:
- `Controllers/` — API controllers
- `Models/` — EF Core entity classes
- `Data/` — DbContext and configuration
- `Services/` — Business logic (StressService, CollaborationService)

### Database Schema
12 tables with `SmartStudy_` prefix. Key design decisions:
- **Users PK is Email** (NVARCHAR), not a surrogate integer key
- **Events use TPT inheritance**: base `SmartStudy_Events` table with four subtype tables (ClassEvents, TaskEvents, WorkEvents, PersonalEvents) joined on EventId FK
- **Tasks and Exams link to Courses** via CourseId FK with CASCADE delete
- **UserCourses** is a junction table for N:N enrollment (Email + CourseId composite PK)

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

## Design System
- **Primary**: #F28D35 (orange), **Accent**: #54BFB5 (teal)
- **Event colors**: Cyan (classes), Orange (tasks), Purple #9B76FF (work), Yellow #F2C777 (personal), Pink #FF607E (exams)
- **Border radius**: 18px cards, 14px buttons
- **Mobile**: Hamburger sidebar, responsive calendar

## API Endpoints (Planned)
All under `/api`, JSON in/out, authenticated:
- **Auth**: register, login, logout
- **Courses**: CRUD at `/api/courses`
- **Tasks**: CRUD at `/api/tasks` + `/api/tasks/{id}/complete`
- **Exams**: CRUD at `/api/exams`
- **Stress**: `/api/stress/score`, `/api/stress/weekly`
- **Collaboration**: connections invite/accept/decline, `/api/collaboration/safe-zones`

## Implementation Status
- **Complete**: All 12 frontend pages, CSS design system, JS module system, SQL schema, project documentation
- **Not started**: Backend API endpoints, EF Core entities/DbContext, authentication, services, frontend-backend AJAX wiring
