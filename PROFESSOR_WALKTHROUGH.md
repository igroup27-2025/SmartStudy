# SmartStudy — Code Walkthrough for Professor Review

A structured guide to presenting the SmartStudy codebase. Covers architecture, key algorithms, and likely questions.

---

## Table of Contents
1. [High-Level Architecture](#1-high-level-architecture)
2. [Auth Flow](#2-auth-flow--authcontrollercs)
3. [Task Create End-to-End](#3-task-create-end-to-end--the-full-stack-in-one-flow)
4. [Stress Score Algorithm](#4-stress-score-algorithm--stressservicecs)
5. [Scheduling Engine](#5-scheduling-engine--schedulingservicescheduleAlltasksasync)
6. [Collaboration Safe-Zone](#6-collaboration-safe-zone--safezoneservicecs)
7. [Database Design](#7-database-design--schemasql)
8. [Presentation Order](#presentation-order)

---

## 1. High-Level Architecture

**Three-tier**: Browser → ASP.NET Core API → SQL Server (via stored procedures).

```
Front/ (static HTML/CSS/JS)  →  Server/SmartStudy/ (ASP.NET Core)  →  Schema.sql (SQL Server)
```

**Key decisions to defend:**
- No build step on the frontend (vanilla JS ES6 modules)
- No ORM on the backend (stored procs only via ADO.NET)
- Full control over SQL; keeps the stack simple

### Frontend layout
- `Front/Pages/` — 13 standalone HTML pages. Each has `data-page` on `<body>`; onboarding pages also have `data-layout="onboarding"`.
- `Front/Script/main.js` — entry point; detects page type and bootstraps modules.
- `Front/Script/modules/layout.js` — dynamically injects sidebar + topbar around `#pageRoot`. `NAV` array registers pages.
- `Front/Script/modules/api.js` — all 40+ API calls; JWT attached via `Authorization` header; 401 auto-redirects to login.
- `Front/Script/modules/modals.js` — generic modal system (ID-based open, ESC/backdrop close).
- `Front/CSS/app.css` — single ~5600-line file, CSS custom properties for theming (primary `#F28D35`, accent `#54BFB5`).

### Backend layout
1. `Program.cs` — startup, JWT config, CORS, static file serving from `Front/`.
2. `Controllers/` — thin HTTP layer.
3. `Services/` — business logic (StressService, SchedulingService, SafeZoneService, NotificationService, Ruppinet/Moodle sync).
4. `DAL/DBservices.cs` — every DB call invokes a stored procedure via `SqlHelper.cs`.
5. `DTOs/` — request/response shapes, separate from internal models.

**Critical point**: No EF Core, no LINQ-to-SQL. Every query is a named stored procedure in `Schema.sql`. This gives SQL-level control and performance predictability but means schema changes touch two places.

---

## 2. Auth Flow — `AuthController.cs`

**What to say:** "Authentication is JWT-based. The controller exposes login, register, Google OAuth, forgot/reset password. Tokens are HMAC-SHA256 signed and live for 7 days."

### Line-by-line (login, lines 33–62)

```csharp
var user = await _db.GetUserByEmailAsync(dto.Email);           // line 36 — stored proc lookup
if (user == null || user.Password != HashPassword(dto.Password))
    return Unauthorized(...);                                   // line 37–38 — compare hashes
var token = GenerateToken(user);                                // line 40
```

`HashPassword` (lines 221–226): SHA-256 with static salt `"SmartStudySalt2026"`. **Be honest if asked**: a real system should use per-user salts (bcrypt/argon2). This is a student project trade-off.

`GenerateToken` (lines 200–219): JWT claims = `Email` + `Name`. Key from `appsettings.json` → `Jwt:Key`, fallback hardcoded. Expires in 7 days.

### Backend wiring (`Program.cs` lines 18–32)

```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => {
        options.TokenValidationParameters = new TokenValidationParameters {
            ValidateIssuer = false,           // we don't check issuer
            ValidateAudience = false,         // we don't check audience
            ValidateLifetime = true,          // we DO check expiry
            ValidateIssuerSigningKey = true,  // we DO check signature
            IssuerSigningKey = key
        };
    });
```

**Middleware order** (lines 99–101): `UseAuthentication()` → `UseAuthorization()` → `MapControllers()`. Order matters — auth must run before authorization can read the ClaimsPrincipal.

### Frontend side (`Front/Script/modules/api.js` lines 9–40)

- Token stored in `localStorage` under key `smartstudy_token`.
- Every request attaches `Authorization: Bearer <token>`.
- **401 handler (line 31)**: clears token + user from localStorage and redirects to Login page, but skips `/auth/*` paths so login errors don't trigger infinite redirects.

### Google OAuth (lines 138–189)
Uses `Google.Apis.Auth` to validate the Google-issued ID token, auto-registers new users, issues our own JWT.

### Likely professor questions
- **"Why Email as PK instead of a user ID?"** — Natural key, enforced unique, simpler FK joins, no surrogate needed.
- **"What if someone steals the JWT?"** — They'd have 7 days of access. Mitigations would be shorter expiry + refresh tokens; out of scope for MVP.
- **"Why no `ValidateIssuer/Audience`?"** — Single-audience app, not federated. Reasonable for MVP; in production would add these.

---

## 3. Task Create End-to-End — The Full Stack in One Flow

**Trace a task from button click to row insert.**

### Step 1: Browser (`Front/Script/modules/api.js` line 73)
```js
createTask: (data) => request('POST', '/tasks', data),
```
`request()` attaches JWT + `Content-Type: application/json`, sends to `http://localhost:5071/api/tasks`.

### Step 2: Controller (`TasksController.cs` lines 50–120)

```csharp
[HttpPost]
public async Task<IActionResult> Create([FromBody] CreateTaskDto dto)
{
    var email = GetEmail();                    // line 53 — pulled from JWT claims
```

`GetEmail()` (line 24) — `User.FindFirst(ClaimTypes.Email)!.Value`. The `[Authorize]` attribute on line 12 + JWT middleware means `User` is populated from the validated token by the time you're inside the action.

- Lines 55–61: Auto vs. manual priority resolution.
- Lines 67–74: If this is a sub-task, inherit courseId + dueDate from parent.
- Line 76: The DB insert.

```csharp
var taskId = await _db.CreateTaskAsync(courseId, email, dto.Title, dto.Type,
    dto.EstimatedHours, dueDate, dto.ParentTaskId, dto.AllowSplitting, priority, isManualPriority);
```

- Lines 82–102: If the course has a study partner set (`SharedByDefault`), auto-create shared-task rows so both users collaborate by default.
- Lines 109–116: **Re-run the scheduler** after task creation. Every write triggers a full reschedule.

### Step 3: DAL (`DBservices.cs` lines 696–713)

```csharp
public async Task<int> CreateTaskAsync(int courseId, string email, string title, string type, ...)
{
    var result = await _sql.ScalarAsync("SS_Tasks_Create",
        SqlHelper.Param("@CourseId", courseId),
        SqlHelper.Param("@Email", email),
        ...);
    return Convert.ToInt32(result);
}
```

**Every DAL method follows this exact pattern**: call a stored proc by name, pass named parameters, return scalar/reader result. No SQL strings in C#, no injection risk.

### Step 4: Stored proc (`Schema.sql`)
`SS_Tasks_Create` — inserts into `SmartStudy_Tasks`, returns `SCOPE_IDENTITY()` as the new `TaskId`.

### Key architectural point
Every domain operation goes through this same four-layer path:

**JS api module → ASP.NET controller → DBservices method → stored procedure**

The controller owns HTTP and authz. The DAL owns SQL. The service layer owns business logic. Nothing crosses layers — the controller never touches SQL directly.

---

## 4. Stress Score Algorithm — `StressService.cs`

**One-line pitch:** "It answers the question: 'given everything you have to do and everything already on your calendar, can you realistically fit it in before your next deadline?'"

### The formula (lines 40–87)

```
requiredHours  = Σ(estimated_hours × ML_ratio_per_course) + (upcoming_exams × 10h)
availableHours = hours_to_nearest_deadline − sleep − existing_events
score          = min(100, requiredHours / availableHours × 100)
```

### Line-by-line

**Lines 25–38 — ML adjustment.** Look at this user's completed tasks, group by course, compute average `actual/estimated` ratio. Only trust courses with ≥2 completed samples.

```csharp
var courseRatios = completedForML
    .GroupBy(t => t.CourseId)
    .Where(g => g.Count() >= 2)
    .ToDictionary(g => g.Key, g => g.Average(t => t.ActualHours / t.EstimatedHours));
```

This is a lightweight "ML" feature: if a student historically takes 1.5× their estimate for Math courses, multiply new Math estimates by 1.5 when computing stress.

**Lines 40–48 — Required hours.** Sum incomplete tasks' estimates, multiplied by the per-course ratio if we have one.

**Lines 50–52 — Exam weight.** Each exam in the next 14 days adds a flat 10 hours to the workload.

**Lines 55–83 — Available hours.** Find the nearest deadline (task or exam). Between "now" and that deadline:
- Subtract `sleepHours × days` (user's preference, default 8h/day).
- Subtract hours already booked with existing non-recurring events.
- Floor at 1 hour to avoid divide-by-zero.

**Lines 85–87 — Score.** `min(100, ratio × 100)`. Capped at 100.

**Lines 184–196 — Zones.**
| Range | Color | Level |
|-------|-------|-------|
| ≤ 40  | `#27AE60` (green)  | Low |
| ≤ 70  | `#F28D35` (orange) | Moderate |
| > 70  | `#E74C3C` (red)    | High |

### Worked example
"A student has 12 hours of incomplete tasks due in 3 days, no exams. 3 days × 24h = 72h. Subtract 3×8h sleep = 48h available. 12/48 = 25% → score 25 → Green."

### Weekly view (lines 122–182)
For each of 7 days, compute `max(studyLoad%, totalLoad%)` where loads are `actualHours / userPreferredMax × 100`. **This is a different formula** than the daily score — it's a backward-looking "how packed is this day" metric, not a forward-looking "can I finish" metric. Be ready for this question.

---

## 5. Scheduling Engine — `SchedulingService.ScheduleAllTasksAsync`

**This is the crown-jewel algorithm. Know it cold.**

### Big picture (lines 18–500+)
A greedy algorithm that, for each user's task list, places 30-minute study blocks into free calendar slots in priority order, respecting daily caps and continuous-work limits.

### The 7 phases

**Phase 1: Load preferences (lines 24–32)**
User settings: day start/end hours, max daily study, max continuous minutes, break duration, exam prep hours/day, exam prep days. All have sensible defaults.

**Phase 2: Define scheduling window (lines 40–53)**
- Start = today.
- End = max(latest task due date, latest exam date) + 1 day.
- Floor at today+7 so we always plan at least a week.

**Phase 3: Clear old auto-scheduled events (lines 63–75)**
Delete everything this user auto-scheduled before — **except pinned tasks** (manually placed by the user, treated as immovable). This is why the algorithm is idempotent: every run rebuilds from scratch.

**Phase 4: Auto-create "Study for exam" tasks (lines 132–190)**
For each upcoming exam, ensure there's a corresponding study task with `totalHours = prepHoursPerDay × prepDays`. Clean up orphans if an exam was deleted.

**Phase 5: Priority scoring (lines 223–261)** — **what the professor will ask about**

```csharp
var score = (1.0 / daysUntilDue) * 50      // deadline pressure (1–∞)
          + Math.Min(hours, 10) * 5         // workload weight, capped
          + credits * 4                      // course importance
          + (isShared ? 25 : 0);            // shared tasks get a boost
```

Then bucket into High (>70) / Medium (>35) / Low. Sort descending by score.

**Reasoning to explain:**
- `1/daysUntilDue × 50`: non-linear — a task due tomorrow scores 50, due in 10 days scores 5. Aggressively prioritizes imminent deadlines.
- `min(hours, 10) × 5`: cap prevents a 40-hour project from dominating forever.
- `credits × 4`: a 6-credit course outranks a 3-credit course all else equal.
- `isShared × 25`: bias toward shared work so partners stay aligned.

**Phase 6a: Schedule exam study on designated prep days (lines 280+)**
Exam tasks are placed first because they have hard "must be before exam date" windows.

**Phase 6b: Greedy placement of regular tasks (line 341+)**
For each task in priority order:
- Walk days from today → due date.
- Find free 30-min slots (compute by subtracting busy intervals from 8AM–10PM window).
- Place blocks respecting: `maxDailyStudy`, `maxDailyTotal`, `maxContinuousMinutes` (force break after), `breakMinutes`.
- Stop when task's remaining hours = 0 or we run out of slots before deadline.

### Constraints defense (what the professor will probe)

- **Why greedy and not optimal (e.g., ILP)?** Greedy is O(n·m), runs in tens of milliseconds per reschedule. Tasks re-plan on every mutation — latency matters more than 2% worse packing.
- **What if the deadline is infeasible?** Task stays partially scheduled, flagged as `Partial` status, surfaced in UI.
- **Pinned tasks?** User-placed blocks aren't cleared in Phase 3; they're included as fixed busy intervals in Phase 6b.

---

## 6. Collaboration Safe-Zone — `SafeZoneService.cs`

**One-line pitch:** "Find time windows in the next 7 days when both friends are free AND both are below 60% stress."

### Algorithm (lines 18–81)

**Step 1 (lines 25–30):** Compute both users' current stress scores. Fetch both users' events (including recurring-expanded) for the next 7 days.

**Step 2 (lines 35–48):** For each day, work a 30-min grid from 8AM–10PM. Skip past slots for today.

**Step 3 — Busy intervals (`GetBusyIntervals`, lines 83–113):**
- Clip each event to the day window.
- Sort by start time.
- **Merge overlapping intervals** — classic interval-merging algorithm. Walk sorted list; if next starts before current ends, extend current; else start new.

**Step 4 — Mutual free slots (`FindMutualFreeSlots`, lines 115–147):**
- Union both users' busy lists, merge overlaps again.
- Free = gaps between merged busy intervals.
- **Set-theoretic framing: `free(A) ∩ free(B) = ¬(busy(A) ∪ busy(B))`**. Know this framing — professors love it.

**Step 5 — Filter + merge (lines 149–163):**
- Drop slots shorter than 30 minutes.
- Keep longer contiguous blocks intact.

**Step 6 — Stress gate (line 66):**
```csharp
if (myStress.Score >= 60 || friendStress.Score >= 60) continue;
```
If either person is stressed, no safe zones are returned at all. **Defend this:** the whole point is that collaboration shouldn't happen when anyone is burning out.

### Recurring event expansion (lines 165–203)
Weekly recurring events (classes) are expanded into individual copies within the 7-day window. `evt.From.AddDays(7)` until past `RecurrenceEndDate` or the window end.

---

## 7. Database Design — `Schema.sql`

### The 13 tables (all prefixed `SmartStudy_`)

```
Users ──┬── NotificationSettings (1:1)
        ├── UserCourses ── Courses ── Instructors
        │                      ├── Tasks
        │                      └── Exams
        ├── Events ──┬── ClassEvents    (TPT subtype)
        │            ├── TaskEvents     (TPT subtype)
        │            ├── WorkEvents     (TPT subtype)
        │            └── PersonalEvents (TPT subtype)
        └── StudyConnections (self-referential N:N)
```

### Key design decisions to defend

**1. Email as the User PK (not a surrogate int)**
- *Pro:* Natural key, already unique, simplifies FKs (every table that references a user stores Email).
- *Con:* Email changes would cascade to many rows. Not supported — enforced at app level.
- *Alternative:* UserId INT. Decision was taste + MVP simplicity.

**2. Events use Table-Per-Type (TPT) inheritance**
Base `SmartStudy_Events` holds `EventId`, `Email`, `From`, `To`, `Recurring`, `RecurrenceEndDate`. Four subtype tables (`ClassEvents`, `TaskEvents`, `WorkEvents`, `PersonalEvents`) each have `EventId` FK back to base, plus type-specific columns.

**Why TPT over alternatives?**
- *vs. single table + nullable columns:* Keeps each subtype's schema clean.
- *vs. Table-Per-Concrete-Class:* Enables polymorphic queries — "show me all events on this day" hits one base table.
- *Cost:* Joins required to get full subtype data. Acceptable given event volumes.

**3. UserCourses junction (N:N)**
Composite PK (`Email` + `CourseId`). A course has many enrolled users; a user takes many courses. Enrollment-specific fields (study partner, share settings) live on the junction.

**4. CASCADE delete on `Tasks.CourseId` and `Exams.CourseId`**
Delete a course → its tasks and exams disappear automatically. Defensible because a task without a course has no meaning. Strong choice that trades safety for simplicity.

**5. ~150 stored procedures**
Every CRUD operation has a named SP: `SS_Tasks_Create`, `SS_Tasks_Update`, `SS_Tasks_GetByUser`, etc.
- *Pro:* SQL logic lives with the data; DBA-reviewable.
- *Pro:* Parameterization enforced by interface — injection-safe by construction.
- *Pro:* Execution plans are cached.
- *Con:* Schema changes touch two files.
- *vs. EF Core:* Traded ORM ergonomics for explicit SQL control.

### The DAL pattern (`DBservices.cs` lines 696–713 as canonical example)

```csharp
public async Task<int> CreateTaskAsync(int courseId, string email, string title, ...)
{
    var result = await _sql.ScalarAsync("SS_Tasks_Create",      // SP name
        SqlHelper.Param("@CourseId", courseId),                 // parameters
        SqlHelper.Param("@Email", email),
        ...);
    return Convert.ToInt32(result);
}
```

Every method in `DBservices.cs` looks like this. `SqlHelper.cs` wraps ADO.NET boilerplate (connection open, `SqlCommand`, dispose). Three verbs: `ScalarAsync`, `ExecuteAsync`, `ReaderAsync`.

---

## Presentation Order

1. **Architecture diagram** (or describe verbally) — the three tiers.
2. **`Program.cs`** — show DI wiring, JWT setup, middleware order.
3. **`AuthController.cs` login** → trace JWT issuance → **`api.js`** → show header attachment. Establishes the full request path.
4. **`TasksController.cs::Create`** → **`DBservices::CreateTaskAsync`** → **`Schema.sql::SS_Tasks_Create`**. One-flow proof that the layering is consistent.
5. **`StressService.cs`** — walk the formula with the worked example.
6. **`SchedulingService.cs`** — explain the 7 phases, spend most time on the priority scoring formula.
7. **`SafeZoneService.cs`** — set-intersection framing impresses.
8. **`Schema.sql`** — close on TPT inheritance + Email-as-PK, the two design decisions to own.

---

## Live Demo Script

1. Register a user → show JWT in DevTools network tab.
2. Add a course, then tasks with deadlines → show stress score change on dashboard.
3. Run scheduling → show blocks appear on calendar.
4. Add a friend connection → show safe-zone intersection.
5. Open `Schema.sql` and `DBservices.cs` side-by-side to show end-to-end task create.

---

## Things to Be Honest About If Asked

- **Password hashing**: SHA-256 with static salt. Real system should use bcrypt/argon2 with per-user salts.
- **JWT validation**: Issuer/Audience checks disabled — fine for single-tenant MVP.
- **No automated tests** — manual testing through the UI.
- **CASCADE deletes** on Course → Tasks/Exams is an opinionated choice.
- **Greedy scheduling** is not optimal; it's fast and good-enough.
