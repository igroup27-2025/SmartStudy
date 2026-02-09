# SmartStudy Project Context

## Overview
SmartStudy is a student planning and productivity app concept. The current repo is a static HTML/CSS/JS front end with a minimal .NET backend skeleton and a SQL schema. The UI covers onboarding, tasks, calendar, courses, exams, analytics, and settings. Backend logic is not yet implemented beyond a sample endpoint.

## Primary Goals (from existing UI and schema)
- Help students plan weekly schedules with a calendar view and event types.
- Track tasks, deadlines, and workload with prioritization and progress.
- Organize courses and exams for the semester.
- Offer analytics for study habits and task completion.
- Provide profile and notification preferences.

## Current Scope by Layer
### Frontend (static prototype)
- Multi-page HTML UI in `Front/Pages`.
- Single CSS theme in `Front/CSS/app.css`.
- Small JS modules in `Front/Script` to handle layout, nav, modals, and calendar logic.
- Pages reference `logo.png` and `icon.png` assets (not present in repo).

### Backend (placeholder)
- ASP.NET minimal API in `Server/SmartStudy/Program.cs`.
- Uses `Microsoft.AspNetCore.OpenApi` and exposes default `/weatherforecast`.
- No domain endpoints yet.

### Data Model (SQL schema)
- Users and notification settings.
- Courses, instructors, enrollment.
- Exams and tasks.
- Events with subtypes (class, task, work, personal).

## Key User Flows in UI
- **Onboarding (4 steps)**: welcome, user info, study preferences, quick start actions.
- **Dashboard**: workload banner, progress bar, KPI tiles, weekly calendar preview, and tasks summary.
- **Tasks**: create task modal, lists by status (overdue, this week, upcoming, completed).
- **Calendar**: 3‑day/week/month views with event types and add-event modal.
- **Courses**: list of courses and upcoming deadlines.
- **Exams**: list of upcoming exams.
- **Analytics**: KPI tiles and chart placeholders.
- **Settings**: profile data and notification toggles.

## Frontend Structure
### Pages (`Front/Pages`)
- `Login.html`: sign-in UI (no auth wiring).
- `Onboarding.html` + `Onboarding-2/3/4.html`: onboarding steps.
- `Dashboard.html`: overview, calendar snapshot, tasks list.
- `Tasks.html`: task lists + "New Task" modal.
- `Calendar.html`: calendar views + "Add Event" modal and inline JS for view switching.
- `Courses.html`: course cards + deadlines list.
- `Exams.html`: upcoming exams list.
- `Analytics.html`: KPI cards and chart placeholders.
- `Settings.html`: profile form and notifications settings.

### JS Modules (`Front/Script`)
- `main.js`: bootstraps layout, sidebar, modals, onboarding.
- `modules/layout.js`: injects sidebar/topbar shell and sets active nav.
- `modules/sidebar.js`: hamburger open/close for mobile.
- `modules/modals.js`: generic open/close for modal backdrops.
- `modules/event-modal.js`: event type toggling in modal; demo submit.
- `modules/task-modal.js`: task modal submit; demo submit.
- `modules/onboarding.js`: step progress bar fill.

### Styling (`Front/CSS/app.css`)
- Defines global theme, typography, cards, buttons, inputs, layout shell, sidebar, and calendar styles.
- Uses CSS variables for brand palette and layout sizing.

## Backend Structure
### .NET Project
- `Server/SmartStudy/SmartStudy.csproj`: targets `net10.0`, OpenAPI package.
- `Program.cs`: minimal API with example `/weatherforecast` endpoint.
- `appsettings.json`: logging configuration and hosts.

## Data Model Summary (from `SQL`)
### Core Tables
- `SmartStudy_Users`: user profile and credentials.
- `SmartStudy_NotificationSettings`: per-user notification preferences.
- `SmartStudy_Instructors`: instructors list.
- `SmartStudy_Courses`: course metadata (hours, credits, semester).
- `SmartStudy_UserCourses`: enrollment (user-to-course).
- `SmartStudy_Exams`: exams linked to courses.
- `SmartStudy_Tasks`: tasks linked to courses.
- `SmartStudy_Events`: base events with time range and recurrence.

### Event Subtypes
- `SmartStudy_ClassEvents`: class events linked to courses.
- `SmartStudy_TaskEvents`: task events linked to tasks (priority/status).
- `SmartStudy_WorkEvents`: work events.
- `SmartStudy_PersonalEvents`: personal events with type/description.

### Indexes
- Events by user and time, tasks by course and due date, exams by course and date.

## Current Gaps / Next Steps (based on repo state)
- Backend APIs for auth, tasks, courses, exams, calendar events, analytics.
- Integration between UI and backend (currently static/demo behavior).
- Missing assets referenced in HTML/CSS (`logo.png`, `icon.png`).
- Any formal project definition/spec documents are not in this repo.

## Expected Artifacts (not found in this folder)
- Project definition/specification documents referenced by the user (Hebrew project definition and product spec). If these are available elsewhere, add them to the repo to refine this context file.
