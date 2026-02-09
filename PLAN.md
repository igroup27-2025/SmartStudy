# SmartStudy Build Plan

## Phase 1: Backend Foundation
- [x] Upgrade .csproj to net10.0
- [x] Switch from SQL Server to SQLite (development mode)
- [x] Add JWT authentication package
- [x] Update Program.cs: SQLite, CORS, JWT, static files, seed data
- [x] Update StudentTask model: add Title, IsCompleted, Email, Priority
- [x] Update Course model: auto-increment CourseId
- [x] Update DbContext for model changes
- [x] Create DTO classes for all entities
- [x] Create SeedDataService with comprehensive mock data
- [x] Create StressService for stress score calculation

## Phase 2: Backend APIs
- [x] AuthController: POST /api/auth/register, POST /api/auth/login
- [x] CoursesController: full CRUD at /api/courses
- [x] TasksController: CRUD + POST /api/tasks/{id}/complete
- [x] ExamsController: full CRUD at /api/exams
- [x] EventsController: CRUD for all event types
- [x] StressController: GET /api/stress/score, GET /api/stress/weekly
- [x] DashboardController: GET /api/dashboard (aggregated view)

## Phase 3: Frontend Foundation
- [x] app.css: Full design system (3000+ lines)
- [x] main.js: Entry point with page detection
- [x] layout.js: Dynamic sidebar + topbar injection
- [x] sidebar.js: Sidebar navigation and state
- [x] modals.js: Generic modal open/close system
- [x] api.js: API client with auth token handling
- [x] auth.js: Authentication state management

## Phase 4: Frontend Pages (12 pages)
- [x] Login.html: Login form + registration
- [x] Dashboard.html: Stress meter, upcoming deadlines, task summary
- [x] Tasks.html: Task list with CRUD, filtering, completion
- [x] Courses.html: Course cards with CRUD
- [x] Exams.html: Exam list with CRUD
- [x] Calendar.html: Weekly calendar view with events
- [x] Analytics.html: Stress trends, workload charts
- [x] Settings.html: User profile, notification preferences
- [x] Onboarding1-4.html: Welcome, courses, tasks, setup complete

## Phase 5: Mock Data
- [x] 2 demo users (demo@smartstudy.com / Demo123, yuval@smartstudy.com / Test123)
- [x] 3 instructors
- [x] 5 courses with enrollment
- [x] 13 tasks (mix of completed/pending, various priorities)
- [x] 6 exams (upcoming dates across Feb-Mar 2026)
- [x] 16+ events (classes, work, personal)

## Phase 6: Testing
- [x] Build backend successfully
- [x] Test all API endpoints with curl (auth, courses, tasks, exams, events, stress, dashboard)
- [x] Open frontend in browser and verify all 12 pages render correctly
- [x] Test CRUD operations end-to-end (create task, delete task, toggle completion)
- [x] Verify stress score calculation (100/High with 68h required, 67.7h available)
- [x] Test filtering on Tasks page (by status, course, priority)
- [x] Test calendar week navigation (prev/next/today)
- [x] Test onboarding flow (steps 1-4, redirects to Dashboard)
- [x] Test logout flow (Settings → Login redirect)
- [x] Fix CSS issues (onboarding centering, missing classes, layout adjustments)

## Architecture Decisions
- SQLite for zero-config development (data stored in smartstudy.db)
- JWT tokens for stateless authentication
- Frontend served by ASP.NET Core static file middleware
- Single server on port 5071 serves both API and frontend
- EnsureCreated() for schema generation (no migration dependency)
