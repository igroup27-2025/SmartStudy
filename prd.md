# SmartStudy Stage 1 PRD

## Product Summary
SmartStudy is a standalone web application that centralizes a student's academic life, reduces stress through intelligent workload analysis, and enables safe peer collaboration. Stage 1 focuses on manual data entry and core academic management features without external system integrations.

## Stage 1 Goals
- Provide full CRUD for courses, tasks, and exams.
- Calculate and visualize a real-time stress/load score (0-100).
- Offer a dashboard that reflects workload distribution and high-risk days.
- Enable controlled peer collaboration with privacy-preserving availability sharing.
- Deliver a fast, AJAX-based UX without page reloads.

## Target Users
- University and college students managing multiple courses and deadlines.
- Students balancing academics with work and personal commitments.
- Peer study partners seeking shared availability without revealing personal details.

## User Pain Points
- Scattered deadlines and lack of a unified academic plan.
- Difficulty estimating workload and stress level.
- Inefficient coordination with study partners.

## In-Scope Modules and Requirements

### Module A: Academic Inventory (Courses & Exams)
**Courses**
- Create, view, update, and delete courses with fields: CourseName, Credits.
- Each course is owned by a user.

**Exams**
- Create, view, update, and delete exams with fields: ExamDate, Course, Weight (if needed), Location.
- Exams are treated as "hard deadlines" that heavily influence the stress algorithm.

**Business Rules**
- Exams must be associated with a valid course.
- Exam dates are immutable once the exam has started (future enhancement; for Stage 1, basic validation only).

### Module B: Smart Task Management
**Tasks**
- Create, view, update, and delete tasks with fields: Title, Deadline, EstimatedDuration (minutes), Priority, Course (optional), IsCompleted.
- Mark tasks as completed; completion updates the stress calculation immediately.

**Business Rules**
- EstimatedDuration is required and must be >= 0.
- Deadlines must be in the future at creation time (soft validation).

### Module C: Stress & Load Analysis (The Brain)
**Score**
- Real-time score from 0 to 100 based on total estimated work vs. available hours until the nearest major deadline (exam or task deadline).
- Completed tasks are excluded from the workload.

**Visualization**
- Dashboard stress meter with a clear numeric score and color thresholds.
- Color-coded days in weekly view:
  - Green: low load
  - Orange: moderate load
  - Red: high load

**Business Rules**
- Exams increase the weight of nearby workload.
- Stress score recalculates when tasks or exams change.

### Module D: Controlled Collaborative Learning
**Connections**
- Users can invite another user and accept/decline requests.
- Connections have status: Pending or Accepted.

**Safe Zone Finder**
- Computes overlapping free time where both users are:
  - Available (no events/tasks in the slot)
  - Below 60% stress for that slot
- Only availability is shared; never expose task names or personal details.

## Core User Flows
- **Onboarding**: create account, enter basic info, add first course/task/exam.
- **Dashboard**: view stress meter, weekly load, and upcoming deadlines.
- **Course management**: add/edit/remove courses and link exams/tasks.
- **Task management**: add/edit/remove tasks and mark complete.
- **Exam management**: add/edit/remove exams and reflect in stress score.
- **Collaboration**: send invite, accept, find safe times, schedule session.

## Data Model Overview (SQL Server / EF Core)
**Entities**
- Users: UserId, Username, Email, PasswordHash
- Courses: CourseId, UserId (FK), CourseName, Credits
- Tasks: TaskId, UserId (FK), CourseId (FK), Title, Deadline, EstimatedDuration, IsCompleted, Priority
- Exams: ExamId, CourseId (FK), ExamDate, Location
- StudyConnections: ConnectionId, UserAId (FK), UserBId (FK), Status
- CollaborativeSessions: SessionId, HostTaskId (FK), GuestUserId (FK), StartTime, EndTime, IsConfirmed

**Relationships**
- Users 1..N Courses
- Users 1..N Tasks
- Courses 1..N Exams
- Courses 1..N Tasks (optional relationship)
- Users N..N Users through StudyConnections
- Tasks 1..N CollaborativeSessions (hosted sessions tied to a task)

## UX & Visual Identity
- Primary: #2C3E50 (Deep Blue)
- Accent: #F39C12 (Orange/Yellow)
- Status: Green (#27AE60), Orange (#E67E22), Red (#E74C3C)

**Dashboard Layout**
- Stress header with a prominent load gauge.
- Weekly view grid with color-coded days and task distribution.
- Collaboration panel ("Study Circle") with quick "Find Time" actions.

## Non-Functional Requirements
- **Performance**: stress score should update within 300ms after data changes.
- **Privacy**: collaboration only exposes availability, not task details.
- **Security**: password hashing, authenticated API endpoints for user data.
- **Usability**: SPA-like behavior with AJAX and no full-page reloads.

## Constraints
- No external APIs (no Moodle, Google, etc.).
- All data entry is manual for Stage 1.
- Frontend must use jQuery with AJAX.
- Backend is ASP.NET Core Web API with EF Core and SQL Server.

## Out of Scope (Stage 1)
- Calendar sync or external data imports.
- Advanced AI recommendations beyond the defined stress algorithm.
- Mobile native apps (web only).
- Real-time collaboration or messaging.

## Success Metrics
- Users can complete full CRUD flows without page reloads.
- Stress score recalculates correctly on task/exam changes.
- Collaboration safe-zone search completes under 2 seconds.
- 0 critical security issues in auth and data access paths.

## Implementation Roadmap
- **Week 1**: DB schema, authentication, basic CRUD for tasks/courses.
- **Week 2**: Stress algorithm and dashboard visualization.
- **Week 3**: Collaboration logic, invitations, safe zone picker.
- **Week 4**: UI/UX polish and end-to-end AJAX testing.

## Assumptions / Open Questions
- Users manage their own courses; no shared course catalogs.
- Exam weight field may be optional for Stage 1 unless required for stress scoring.
- Stress algorithm definition can be refined in Stage 2 if needed.
