# SmartStudy — Changes & Fixes Implementation Prompt

> **Purpose:** This is the master prompt for implementing all pending UI fixes, feature changes, and enhancements across the SmartStudy app. Each change is numbered, scoped to specific files, and grouped by page. Implement in the order listed within each phase.

---

## Phase 1: Quick UI/CSS Fixes (Frontend only — no backend changes)

---

### 1. Dashboard — Mobile stat cards in one row

**What:** The 3 stat cards ("Due Today", "Pending", "Progress") in `#dashStats` stack vertically on mobile. Make them fit in a **single horizontal row** on mobile.

**Files:**
- `Front/CSS/app.css` — find the `.dash-stats` / `.dash-stat` mobile media query (max-width ~768px)

**How:**
- In the mobile breakpoint, set `.dash-stats` to `display:flex; flex-wrap:nowrap; gap:8px`
- Set `.dash-stat` to `flex:1; min-width:0; padding:10px 6px; font-size smaller`
- Reduce `.dash-stat__value` font-size to ~1.2rem and `.dash-stat__label` to ~0.65rem on mobile

---

### 2. Dashboard — Remove Study Load & Total Load from top section

**What:** In `renderMotivation()` inside `dashboard.js`, the motivational card also renders Study Load and Total Load progress bars. **Remove the load indicators entirely.** Keep only the icon + motivational text.

**Files:**
- `Front/Script/modules/dashboard.js` — function `renderMotivation()`

**How:**
- Delete the entire `${showLoad ? \`...\` : ''}` block (lines with `.dash-load-indicators`, `.dash-load-row`, `.dash-load-fill`, `.dash-load-value`, `.dash-load-scheduled`)
- Remove the variables: `studyLoad`, `totalLoad`, `scheduledH`, `showLoad`
- The result should be just the icon + text paragraph inside `.dash-motivation__card`

---

### 3. Dashboard — Remove the Unscheduled Tasks card

**What:** The `#dashUnscheduled` section and the `renderUnscheduled()` function display an "Unscheduled Tasks" alert card. **Remove it entirely** from both the HTML and JS.

**Files:**
- `Front/Pages/Dashboard.html` — delete `<div id="dashUnscheduled" class="dash-unscheduled"></div>`
- `Front/Script/modules/dashboard.js` — delete the entire `renderUnscheduled()` function and remove its call from `initDashboard()`; also remove the call in `showCompletionModal()`'s refresh logic and in `renderOverdue()`'s reschedule refresh logic

---

### 4. Dashboard — Remove `#userMenuBtn` avatar button from topbar

**What:** In `layout.js`, the topbar renders a `.topbar-user` div with a `.topbar-avatar` button (`#userMenuBtn`) and a dropdown with Settings + Logout links. **Remove the entire `.topbar-user` div** (avatar + dropdown). The logout moves to the sidebar (see item 11 below).

**Files:**
- `Front/Script/modules/layout.js` — in the topbar HTML template:
  - Delete the entire `<div class="topbar-user">...</div>` block (the avatar button + dropdown)
  - Delete the `userMenuBtn` click listener
  - Delete the outside-click listener for `.topbar-user`
  - Keep the `#logoutBtn` listener BUT move it to the sidebar (see item 11)

---

### 5. Tasks — Consolidate 3 filter lists into one "Filter" panel

**What:** The Tasks page currently shows 3 separate scrollable filter dropdowns (status, course, priority). **Merge them into a single "Filter" button** that opens one unified dropdown/panel containing all 3 filter categories as collapsible sections.

**Files:**
- `Front/Script/modules/tasks.js` — refactor the filter rendering
- `Front/Pages/Tasks.html` — update the filter container
- `Front/CSS/app.css` — style the unified filter panel

**How:**
- Replace the 3 separate `<select>` / filter lists with a single `<button class="filter-toggle">Filter</button>` 
- On click, toggle a `.filter-panel` dropdown below it
- Inside the panel, show 3 sections: **Status** (checkboxes), **Course** (checkboxes), **Priority** (checkboxes)
- Active filter count shown as a badge on the button: `Filter (3)`
- Apply filtering logic when any checkbox changes

---

### 6. Analytics — Remove Stress Score section

**What:** The Analytics page has a "Stress Score" card/section. **Remove it entirely.**

**Files:**
- `Front/Script/modules/analytics.js` — find and remove the stress score rendering function and its call
- `Front/Pages/Analytics.html` — remove the stress score container element
- `Front/CSS/app.css` — clean up orphaned `.stress-score` / `.analytics-stress` styles

---

### 7. Analytics — Fix Weekly Trends bar heights

**What:** In the "Weekly Trends" chart, the bar visual heights don't match the percentage values displayed on them.

**Files:**
- `Front/Script/modules/analytics.js` — find the weekly trends bar rendering

**How:**
- Ensure the bar height CSS is set from the **same value** as the displayed percentage label
- `bar.style.height = percentage + '%'` where the container has `height:100%` with `align-items:flex-end`
- Both the label text and the bar height must use the identical `percentage` variable

---

### 8. Analytics — Fix Task Statistics for mobile

**What:** The "Task Statistics" section doesn't render properly on mobile (overflows, elements overlap).

**Files:**
- `Front/CSS/app.css` — add/update mobile media queries for the task statistics section

**How:**
- At `@media (max-width: 768px)`: stack any horizontal layouts vertically, reduce font sizes, make charts width:100%, add `overflow-x:auto` if needed
- Ensure pie charts / bar charts have `max-width:100%` and maintain aspect ratio

---

### 9. Analytics — Merge "Estimated vs Actual" into "Learning Insights"

**What:** There are two separate sections: "Estimated vs Actual" and "Learning Insights". **Merge them into a single "Learning Insights"** section that contains both.

**Files:**
- `Front/Script/modules/analytics.js` — combine the two rendering functions into one
- `Front/Pages/Analytics.html` — remove the separate "Estimated vs Actual" container
- `Front/CSS/app.css` — update styles

**How:**
- Keep the "Learning Insights" section title
- Move the estimated vs. actual chart/data inside the Learning Insights card
- Delete the standalone "Estimated vs Actual" section and function

---

### 10. Friends — Smaller friend cards on mobile

**What:** Each friend card is too large on mobile, taking up too much vertical space.

**Files:**
- `Front/CSS/app.css` — mobile media queries for friend cards

**How:**
- At `@media (max-width: 768px)`: reduce card padding (12px → 8px), avatar size (48px → 36px), font sizes
- Consider a horizontal compact layout: avatar on the left, name + status on the right, actions below
- Ensure the card width is 100% but height is minimized

---

### 11. Settings/Layout — Move Logout button to sidebar

**What:** The Logout button is currently inside the Settings page (and in the topbar dropdown being removed in item 4). **Move it to the sidebar** as a dedicated button at the bottom.

**Files:**
- `Front/Script/modules/layout.js` — add a logout button inside the sidebar HTML, between `.sidebar-nav` and `.sidebar-user`
- `Front/Script/modules/settings.js` — remove the logout button/section from the settings page
- `Front/CSS/app.css` — style `.sidebar-logout` at the bottom of the sidebar

**How:**
- In `layout.js`, add after the `</nav>` and before `.sidebar-user`:
```html
<button class="sidebar-logout" id="sidebarLogoutBtn">Logout</button>
```
- Add click listener: `document.getElementById('sidebarLogoutBtn').addEventListener('click', () => logout())`
- Style: full-width button at the bottom of sidebar, muted red color, with a logout icon

---

### 12. Settings — Reorder: Break between sessions below Max continuous time

**What:** The "Break between sessions" field currently appears above or separate from "Max continuous study time". **Move it directly below** Max continuous time.

**Files:**
- `Front/Script/modules/settings.js` — find where the scheduling preferences form fields are rendered and reorder so:
  1. Max continuous study time
  2. Break between sessions (immediately below)

---

### 13. Settings — Replace "ML" with "Machine Learning" (למידת מכונה)

**What:** Wherever the text "ML" appears in settings UI, replace with the clear label **"Machine Learning (למידת מכונה)"**.

**Files:**
- `Front/Script/modules/settings.js` — search for "ML" string literals
- `Front/Pages/Settings.html` — search for "ML" text

**How:**
- Replace all display instances of `"ML"` or `"ML-based"` with `"Machine Learning"` or `"Machine Learning (למידת מכונה)"`

---

### 14. Settings — Remove "Fix Constraints" section

**What:** There's a "Fix Constraints" button/section in Settings. This is redundant since the same functionality exists on the Calendar page. **Remove it.**

**Files:**
- `Front/Script/modules/settings.js` — remove the Fix Constraints rendering/logic
- `Front/Pages/Settings.html` — remove the container if it exists in static HTML
- `Front/CSS/app.css` — clean up styles

---

## Phase 2: Frontend Logic Fixes (JS changes, no backend)

---

### 15. Dashboard — Fix calendar event time positioning

**What:** In the dashboard's 3-day mini calendar (`renderMiniCalendar()`), event blocks' visual positions don't match their actual times on the grid.

**Files:**
- `Front/Script/modules/dashboard.js` — function `renderMiniCalendar()`

**How:**
- Review the calculation at line ~843: `const top = (startHour - startSlot) * cellHeight;`
- Ensure `startHour` correctly accounts for minutes: `startHour = fromTime.getHours() + fromTime.getMinutes() / 60`
- Ensure `cellHeight` matches the actual rendered cell height (currently 40px per hour)
- Verify the `.mini-cal__day-body` container uses `position:relative` and events use `position:absolute`
- Verify `startSlot` is the same value used to render the time label column

---

### 16. Dashboard — Overdue tasks should NOT appear under "Needs Review"

**What:** The `renderReview()` function currently merges overdue tasks into the review list. If a task is overdue, it should **only** appear in the "Overdue" section, **not** in "Needs Review".

**Files:**
- `Front/Script/modules/dashboard.js` — function `renderReview()`

**How:**
- Currently (line ~407-410):
```js
const overdueTasks = data.overdueTasks || [];
const reviewIds = new Set(reviewTasks.map(t => t.taskId));
const merged = [...reviewTasks, ...overdueTasks.filter(t => !reviewIds.has(t.taskId))];
```
- Change to filter OUT overdue tasks from the review list instead of merging them IN:
```js
const overdueIds = new Set((data.overdueTasks || []).map(t => t.taskId));
const tasks = reviewTasks.filter(t => !overdueIds.has(t.taskId)).slice(0, 10);
```

---

### 17. Dashboard — Unscheduled tasks: show reason + "Schedule Manually" button

**What:** In the "Needs Review" section, tasks that are **unscheduled** currently show Approve + Edit buttons. Instead, for unscheduled tasks:
1. Show the **reason** why the task wasn't scheduled
2. Show a **"Schedule Manually"** button that navigates to the Calendar page
3. **Don't** show Approve or Edit buttons for unscheduled tasks

**Files:**
- `Front/Script/modules/dashboard.js` — function `renderReview()`, inside the task card template

**How:**
- Check `t.schedulingStatus`: if `'Unscheduled'` or `'Partial'`, render a different actions block:
```js
const isUnscheduled = t.schedulingStatus === 'Unscheduled' || t.schedulingStatus === 'Partial';
```
- For unscheduled tasks, the card actions should be:
```html
<div class="dash-task-card__actions">
    <span class="dash-task-card__reason">Reason: No available slot</span>
    <a href="/Pages/Calendar.html" class="btn btn-sm btn-primary">Schedule Manually</a>
</div>
```
- For scheduled tasks, keep the current Approve + Edit buttons

---

### 18. Dashboard — Edit button on scheduled tasks navigates to task edit

**What:** When a task IS scheduled, the Edit button should navigate to `Tasks.html?edit={taskId}` to open the task edit form — **not** to the Calendar.

**Files:**
- `Front/Script/modules/dashboard.js` — function `renderReview()`, the `editHref` variable

**How:**
- Change the editHref logic. Currently it sends scheduled tasks to Calendar. Change to always go to Tasks:
```js
const editHref = `${BASE_PATH}/Pages/Tasks.html?edit=${t.taskId}`;
```

---

### 19. Calendar — Add manual Sync button

**What:** Add a "Sync" button at the top of the Calendar page that triggers `POST /api/scheduling/run` and reloads the calendar.

**Files:**
- `Front/Pages/Calendar.html` — add a sync button in the page header area
- `Front/Script/modules/calendar.js` — add click handler
- `Front/CSS/app.css` — style the sync button

**How:**
- Add a button with class `.cal-sync-btn` next to the calendar navigation controls
- On click: show spinner, call `await api.runScheduling()`, then reload events, hide spinner
- Button text: "⟳ Sync" (or use a sync/refresh icon)

---

## Phase 3: Feature Development (Backend + Frontend)

---

### 20. Calendar — Drag-and-drop to reschedule events

**What:** Allow dragging event blocks on the calendar to change their date/time.

**Files:**
- `Front/Script/modules/calendar.js` — implement drag logic
- `Front/CSS/app.css` — drag visual styles

**How:**
- Add `mousedown`/`touchstart` on `.cal-event` blocks
- Track mouse/touch movement, update block position
- On drop: calculate new `from`/`to` from grid position
- Call `PUT /api/events/{type}/{id}` with updated times
- Re-render calendar
- Add visual feedback: shadow, opacity:0.7, cursor:grabbing during drag

---

### 21. Calendar — Manual edits lock events from auto-scheduling

**What:** When a user manually edits a task's time (via drag-drop or edit form), mark it as **"manually pinned"** so the scheduling engine never moves it.

**Files:**
- `Server/SmartStudy/Models/StudentTask.cs` — add `public bool IsManuallyPinned { get; set; } = false;`
- `Server/SmartStudy/DTOs/TaskDtos.cs` — add `IsManuallyPinned` to `TaskDto`
- `Server/SmartStudy/Controllers/EventsController.cs` — when updating a TaskEvent's time manually, set `IsManuallyPinned = true` on the parent task
- `Server/SmartStudy/Services/SchedulingService.cs` — in `ScheduleAllTasksAsync()`, skip tasks where `IsManuallyPinned == true` (treat their existing TaskEvents as fixed)
- `Server/SmartStudy/Data/SmartStudyDbContext.cs` — ensure the new column is included in migration
- `Front/Script/modules/calendar.js` — when calling the update API after drag/edit, include `isManualEdit: true` in the request body

**DB:**
```sql
ALTER TABLE SmartStudy_Tasks ADD IsManuallyPinned BIT NOT NULL DEFAULT 0;
```

---

### 22. Calendar — Allow editing event type

**What:** In the event edit modal, allow changing the event type (e.g., Work → Personal).

**Files:**
- `Front/Script/modules/calendar.js` — add a "Type" dropdown to the edit modal (options: Class, Work, Personal)
- `Server/SmartStudy/Controllers/EventsController.cs` — new endpoint `PUT /api/events/{id}/change-type`

**Backend logic:**
Since events use TPT inheritance, changing type requires:
1. Load the base Event by ID
2. Delete the old subtype row (e.g., from `SmartStudy_WorkEvents`)
3. Create a new subtype row (e.g., in `SmartStudy_PersonalEvents`)
4. Update the discriminator on the base Event row

**Limitation:** Only allow type changes between Work ↔ Personal (Class events have unique fields like instructor, room).

---

### 23. Courses — Shared course: choose partner from friends list

**What:** When a user toggles a course as "Shared", show a dropdown to select a partner from their friends list.

**Files:**
- `Front/Script/modules/courses.js` — in the course create/edit form, when "Shared" is toggled on:
  1. Fetch friends via `GET /api/connections` (accepted only)
  2. Show a searchable dropdown with friend names
  3. Include `partnerEmails` in the create/update API call
- `Server/SmartStudy/DTOs/CourseDtos.cs` — add `List<string>? PartnerEmails` to `CreateCourseDto` and `UpdateCourseDto`
- `Server/SmartStudy/Controllers/CoursesController.cs` — when partners are specified:
  - Add them to `UserCourses`
  - Auto-create `SharedTask` entries for existing tasks in that course

---

### 24. Exams — "Taking this exam" checkbox with Session A/B logic

**What:** Add a checkbox "Taking this exam" (ניגש למבחן) to each exam card:
- **Session A (מועד א):** checkbox is **checked by default**. User can uncheck → removes exam + study time from calendar.
- **Session B (מועד ב):** checkbox is **unchecked by default**. User can check → adds exam + study time to calendar.

**Files:**
- `Server/SmartStudy/Models/Exam.cs` — add `public bool IsTakingExam { get; set; } = true;`
- `Server/SmartStudy/DTOs/ExamDtos.cs` — add `IsTakingExam` to response DTO
- `Server/SmartStudy/Controllers/ExamsController.cs`:
  - On create: set `IsTakingExam = (session == "A")`
  - New endpoint `PUT /api/exams/{id}/toggle-taking`
  - When toggled OFF: delete exam's TaskEvents + exam calendar event
  - When toggled ON: trigger scheduling to create study blocks
- `Server/SmartStudy/Services/SchedulingService.cs` — only create exam prep tasks for exams where `IsTakingExam == true`
- `Front/Script/modules/exams.js` — add checkbox to each exam card, call toggle API on change

**DB:**
```sql
ALTER TABLE SmartStudy_Exams ADD IsTakingExam BIT NOT NULL DEFAULT 1;
```

---

### 25. Friends — One-time course sharing approval

**What:** When a course is shared, instead of approving each task individually, show a **single course-level approval** in the Pending section. Once approved, all current and future tasks in that course are auto-shared.

**Files:**
- `Server/SmartStudy/Models/SharedTask.cs` or new model — add course-level sharing approval flag
- `Server/SmartStudy/Controllers/CollaborationController.cs` — new endpoint `POST /api/collaboration/approve-course-sharing/{courseId}`
  - On approval: auto-create `SharedTask` + `SharedTaskMember` for all existing tasks in the course
- `Server/SmartStudy/Controllers/TasksController.cs` — on task creation, if course has approved sharing → auto-create shared entries
- `Front/Script/modules/friends.js` — in pending section, show course-level requests instead of per-task

---

### 26. Settings — Add integration system connections

**What:** Add an "Integrations" section to Settings where users can connect/update/disconnect external systems (same functionality as onboarding step).

**Files:**
- `Front/Script/modules/settings.js` — add Integrations section
- `Front/Pages/Settings.html` — add container
- Reuse onboarding integration logic/UI from `Front/Script/modules/onboarding.js`

**How:**
- Show cards for each integration (Google Calendar, university system)
- Status: Connected ✓ / Not connected
- Buttons: Connect / Update / Disconnect
- Connect triggers OAuth flow or API key input

---

## Phase 4: Infrastructure

---

### 27. Authentication — Forgot Password actually sends email

**What:** The forgot-password flow generates a token but doesn't send it via email. **Implement real email sending.**

**Files:**
- `Server/SmartStudy/Services/EmailService.cs` — **NEW file**, SMTP email service
- `Server/SmartStudy/Controllers/AuthController.cs` — call EmailService in the forgot-password endpoint
- `Server/SmartStudy/appsettings.json` — add SMTP configuration section

**How:**
- Create `EmailService` with `SendAsync(string to, string subject, string body)` method
- Use `System.Net.Mail.SmtpClient` or a library like `MailKit`
- Configure SMTP settings (host, port, username, password) in `appsettings.json`
- In the forgot-password endpoint, after generating the token, call:
```csharp
await _emailService.SendAsync(email, "SmartStudy — Password Reset",
    $"Click here to reset your password: {baseUrl}/Pages/Login.html?resetToken={token}\nThis link expires in 1 hour.");
```

---

## Summary Table

| # | Page | Change | Phase | Complexity |
|---|------|--------|-------|------------|
| 1 | Dashboard | Mobile stat cards in one row | 1 | Low |
| 2 | Dashboard | Remove Study/Total Load from top | 1 | Low |
| 3 | Dashboard | Remove Unscheduled Tasks card | 1 | Low |
| 4 | Layout | Remove topbar avatar button | 1 | Low |
| 5 | Tasks | Consolidate filters into one panel | 1 | Medium |
| 6 | Analytics | Remove Stress Score section | 1 | Low |
| 7 | Analytics | Fix Weekly Trends bar heights | 1 | Low |
| 8 | Analytics | Fix Task Statistics for mobile | 1 | Low |
| 9 | Analytics | Merge Estimated vs Actual → Learning Insights | 1 | Medium |
| 10 | Friends | Smaller mobile friend cards | 1 | Low |
| 11 | Layout/Settings | Move Logout to sidebar | 1 | Low |
| 12 | Settings | Reorder break/continuous fields | 1 | Low |
| 13 | Settings | Replace "ML" → "Machine Learning" | 1 | Low |
| 14 | Settings | Remove "Fix Constraints" | 1 | Low |
| 15 | Dashboard | Fix calendar event positioning | 2 | Medium |
| 16 | Dashboard | Overdue ≠ Needs Review (no overlap) | 2 | Low |
| 17 | Dashboard | Unscheduled: reason + manual schedule btn | 2 | Medium |
| 18 | Dashboard | Edit button → task edit form | 2 | Low |
| 19 | Calendar | Add Sync button | 2 | Low |
| 20 | Calendar | Drag-and-drop events | 3 | High |
| 21 | Calendar | Manual edit locks from auto-scheduling | 3 | Medium |
| 22 | Calendar | Allow editing event type | 3 | High |
| 23 | Courses | Shared course → choose partner | 3 | Medium |
| 24 | Exams | "Taking this exam" checkbox (A/B) | 3 | Medium |
| 25 | Friends | One-time course sharing approval | 3 | High |
| 26 | Settings | Integration connections | 3 | Medium |
| 27 | Auth | Forgot password sends real email | 4 | Medium |
