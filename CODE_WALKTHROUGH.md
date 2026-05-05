# SmartStudy — Complete Code Walkthrough

A section-by-section walkthrough of every script and backend file. Frontend pages first (entry → app), then backend (startup → DAL → controllers → services → models).

## Table of Contents

**Frontend**
1. [Shared scripts + Login.html](#part-1--shared-frontend-scripts--login-flow)
2. [Onboarding 1→4](#part-2--onboarding-flow-onboarding1--onboarding4)
3. [Dashboard.html](#part-3--dashboardhtml-720-lines)
4. [Tasks.html](#part-4--taskshtml-611-lines)
5. [Calendar.html](#part-5--calendarhtml-976-lines-the-biggest-single-page)
6. [Courses.html](#part-6--courseshtml-292-lines)
7. [Exams.html](#part-7--examshtml-266-lines)
8. [Analytics.html](#part-8--analyticshtml-221-lines)
9. [Friends.html](#part-9--friendshtml-268-lines)
10. [Settings.html](#part-10--settingshtml-506-lines)

**Backend**
11. [Program.cs (startup)](#part-11--programcs-102-lines)
12. [DBservices.cs (DAL)](#part-12--dbservicescs-4522-lines-150-methods)
13. [Controllers (15 files)](#part-13--controllers-15-files-2800-lines-total)
14. [Services (11 files)](#part-14--services-11-files)
15. [Models (19 files)](#part-15--models-19-files)

---

# PART 1 — Shared frontend scripts + Login flow

These two files are loaded by every page, so understanding them is a prerequisite for everything else.

## `Front/Scripts/ajaxCall.js` (22 lines)

A tiny universal AJAX helper following the HW3 academic pattern.

- **Lines 1–11**: Comment block. Documents the usage pattern: build an API URL, call `ajaxCall(method, url, body, successCB, errorCB)`. Notes that JWT injection and 401 handling are wired globally in `appShell.js`, which lets this file stay minimal.
- **Lines 12–22**: The single function `ajaxCall`. Wraps `$.ajax` with fixed defaults: `cache:false`, `contentType: application/json`, and forwards `success`/`error` callbacks. **Every API call in the entire frontend goes through this one wrapper.**

## `Front/Scripts/appShell.js` (339 lines)

Globals, auth helpers, and the shared sidebar/topbar/notifications shell.

### Globals (lines 13–21) — base path detection
- **Line 14**: Regex extracts the IIS sub-app prefix from `window.location.pathname`. e.g. on `/tar2/Pages/Dashboard.html` it captures `/tar2`.
- **Line 15**: `BASE_PATH` = the captured prefix or `""` in dev.
- **Line 19**: Replaces `/tar2` (frontend IIS app) with `/tar1` (backend IIS app). In production the API lives on a sibling app.
- **Lines 20–21**: `SERVER` = origin + api base path + `/`. `API_BASE` = `SERVER + "api"`. Every page builds endpoint URLs as `API_BASE + "/something"`.

### Auth storage helpers (lines 24–62)
- **`getAuthHeaders` (24–29)**: Returns `Content-Type` + `Authorization: Bearer <token>` if a token is in localStorage. Mostly legacy — the global `$.ajaxSetup` below does this automatically now.
- **`isLoggedIn` (31–33)**: Returns `true` iff `smartstudy_token` exists.
- **`getUser` (35–38)**: Parses `smartstudy_user` JSON from localStorage. Returns `null` if missing.
- **`saveAuth` (40–48)**: Persists token + user (email, firstName, lastName, onboardingCompleted) to localStorage. Called after login/register/Google sign-in.
- **`logout` (50–54)**: Wipes both keys, redirects to Login.
- **`requireAuth` (56–62)**: Guard called at the top of every protected page. Redirects to Login if no token, returns `false` so the caller can `if (!requireAuth()) return;`.

### `escapeHtmlGlobal` (lines 65–68)
HTML escape for safely interpolating user data into template strings. Replaces `& < > "`. Used everywhere the code builds HTML by concatenation (which is most places — vanilla JS, no templating).

### Global jQuery hooks (lines 73–87) — the magic that keeps `ajaxCall.js` bare
- **`$.ajaxSetup` (73–78)**: A `beforeSend` hook that pulls the token from localStorage and adds the `Authorization` header to **every** AJAX call. This is why `ajaxCall.js` never has to think about auth.
- **`$(document).ajaxError` (80–87)**: Global 401 handler. If any non-`/auth/` endpoint returns 401 (token expired/invalid), wipe storage and redirect to Login. The `/auth/` exclusion is so login form errors don't kick the user out.

### `SmartStudy` IIFE — the app shell (lines 91–339)

Closure-scoped state: `pollingTimer`, `notifications` array, `unreadCount`.

#### `NAV` array (96–105)
The single source of truth for sidebar navigation. Each entry: `{ page, label, href }`. **To add a new page to the sidebar, add it here.**

#### `PAGE_TITLES` (107–116)
Maps `data-page` value → topbar title text.

#### `initShell(currentPage)` (118–235) — the workhorse
Called by every app page (after `requireAuth()` passes). Steps:

1. **120–127**: Get `pageRoot` (the page's outer container). Bail if missing. Add `app-layout` class to body. Build user initials for the avatar.
2. **129–132**: `navHtml` — map `NAV` to `<a>` tags, mark the current page `active`.
3. **134–145**: `sidebarHtml` — sidebar with nav, logout button, user avatar block.
4. **147–169**: `topbarHtml` — hamburger toggle, logo, page title, notification bell with hidden badge + dropdown skeleton.
5. **171**: `overlayHtml` — backdrop shown when mobile sidebar is open.
6. **174–180**: **Critical DOM trick.** Save the page's existing inner HTML, then *replace* `pageRoot` with: `sidebar + topbar + overlay + <main class="main-content">existingContent</main>`. After this, the page is wrapped in the shell.
7. **182–205**: Wire sidebar toggle, overlay click-to-close, nav click-to-close, window resize (close mobile sidebar above 768px breakpoint), logout button.
8. **207–230**: Notification bell. Click toggles dropdown and (when opening) calls `fetchAndRenderNotifications`. Click anywhere outside `.notif-wrapper` closes it. "Mark all read" button posts to `/notifications/mark-all-read` and updates UI optimistically.
9. **232–234**: Initial notification fetch + 60-second polling for unread count.

#### Notification helpers (237–322)
- **`fetchAndRenderNotifications` (237–243)**: First POSTs `/notifications/generate` (server-side generates fresh notifications based on deadlines/overload), then GETs the list.
- **`loadNotifications` (245–255)**: GET `/notifications`. Stores in closure state, updates badge + dropdown.
- **`fetchUnreadCount` (257–265)**: Polled every 60s. Just refreshes the badge number without re-rendering the dropdown.
- **`updateNotifBadge` (267–276)**: Shows badge with count (or `99+`), hides if zero.
- **`renderNotifDropdown` (278–322)**: Builds dropdown HTML. `ICONS` map (287–293) picks an emoji per notification type. Each item shows icon + title + message + time-ago. Lines 310–321 wire click on unread items to POST `/notifications/mark-read` and update UI.

#### `getTimeAgo` (324–333)
Converts a Date to "just now" / "5m ago" / "3h ago" / "2d ago".

#### Public surface (335–339)
Returns `{ initShell, refreshNotifications }` — only those two are exposed globally as `SmartStudy.initShell(...)` etc.

---

## `Front/Pages/Login.html` (272 lines)

The entry point. Unauthenticated. Has 4 toggleable sections in one card: Login, Forgot Password, Reset Password, Register.

### `<head>` (lines 1–12)
- **7**: Loads the global stylesheet `app.css`.
- **8**: jQuery 3.7.1 from CDN.
- **9**: Loads `ajaxCall.js`.
- **10**: Loads `appShell.js` (provides `API_BASE`, auth helpers, etc.).
- **11**: Loads Google Identity Services SDK (for Google Sign-In button).

### `<body data-page="login">` (line 13)
The `data-page` attribute is used by CSS for page-specific styling. Login uses a totally different layout from the app pages (no sidebar shell).

### Layout (lines 14–120)
Two-column hero layout:
- **Left column (15–27)**: Branding panel — logo, tagline, feature bullets. Hidden on mobile via CSS.
- **Right column (28–119)**: Four `.login-card` sections, only one visible at a time.

#### The four card sections
1. **`#loginSection` (32–50)**: Email + password form, "Sign In" button, Google button placeholder, "Forgot password?" link, "Create one" link.
2. **`#forgotSection` (52–68)**: Email-only form. On success, shows the reset token inline (since there's no email server in dev), then auto-advances to reset section.
3. **`#resetSection` (70–90)**: Email + token + new password.
4. **`#registerSection` (92–118)**: First name + last name (in a row), email, password (min 6).

All four start `display:none` except `#loginSection`.

### Inline `<script>` (lines 122–270)

#### API endpoint URLs (124–129)
Six string constants built from `API_BASE`. `LoginApi`, `RegisterApi`, `ForgotApi`, `ResetApi`, `GoogleLoginApi`, `AuthConfigApi`.

#### `$(document).ready` (131–153)
1. **133–136**: If already logged in, skip Login entirely → Dashboard.
2. **139–143**: Wire the 5 section-toggle links (each calls `showSection` with a target ID).
3. **146–149**: Wire all 4 form `submit` events to their handlers.
4. **152**: Fetch `/auth/config` to get the Google client ID, then init the Google button via `AuthConfigSC`.

#### Google Sign-In (156–177)
- **`AuthConfigSC(cfg)` (156–173)**: If config has a Google client ID, poll every 100ms (up to ~5s) until the GSI script has loaded, then `initialize` with our client ID + `GoogleCredentialCB`, and render the button into `#googleSignInBtn` with theme `outline`, size `large`.
- **`GoogleCredentialCB(resp)` (174–177)**: Receives the Google ID token. POSTs it to `/auth/google` with our standard login success/error callbacks.

#### `showSection(id)` (179–182)
Hide all 4 sections, show the one with the given ID.

#### Login flow (185–202)
- **`SubmitLogin` (185–194)**: `e.preventDefault()`, build `{email, password}`, disable button + show "Signing in...", POST to `/auth/login`.
- **`LoginSC(res)` (195–198)**: On success, `saveAuth(res)` persists token + user, then redirect: Dashboard if `onboardingCompleted`, else Onboarding1.
- **`LoginEC(xhr)` (199–202)**: Show server error message (or "Login failed"), re-enable button.

#### Register flow (205–224)
Same shape as login. POST to `/auth/register` with `{email, firstName, lastName, password}`. **Always** redirects to Onboarding1 on success (a new account is by definition incomplete).

#### Forgot flow (227–247)
- **`SubmitForgot`**: POST email to `/auth/forgot-password`.
- **`ForgotSC` (234–243)**: Re-enable the button, then if the response includes `resetToken` (dev mode — no email server), display it on screen, prefill the reset form, auto-advance to reset section after 2 seconds.
- **`ForgotEC`**: Standard error alert.

#### Reset flow (250–269)
- **`SubmitReset`**: POST `{email, token, newPassword}` to `/auth/reset-password`.
- **`ResetSC` (261–265)**: Alert success, switch back to login section. **Note**: doesn't auto-login — user must sign in with the new password.
- **`ResetEC`**: Standard error alert.

---

# PART 2 — Onboarding flow (Onboarding1 → Onboarding4)

Four-step wizard new users walk through after registration. Each step persists progress server-side so the user can resume.

## Shared onboarding pattern

- Body marked `data-layout="onboarding"` + `data-step="N"` — CSS uses this for the centered card layout (no sidebar shell).
- 4 progress dots + a progress bar at 25/50/75/100%.
- **Draft state lives in `sessionStorage` under key `smartstudy_onboarding`** — each step reads the draft, restores fields, lets the user click "Back" without losing input. Only step 4 actually PUTs to the server.
- Helper pair `LoadDraft()` / `SaveDraft(patch)` is duplicated (small redundant copy) on each step; `SaveDraft` merges with `$.extend({}, existing, patch)`.

## `Onboarding1.html` (97 lines) — Welcome screen

### Markup (1–50)
- **8–10**: jQuery + ajaxCall + appShell.
- **12**: Body marks step 1.
- **15–20**: Dots — first one `active current`, others empty.
- **22**: Progress bar at `width:25%`.
- **24–45**: Static "Welcome" card with 4 feature bullets (manage courses, track tasks, monitor stress, collaborate).
- **46–49**: Two buttons — "Skip" (commits defaults, goes to Dashboard) and "Get Started" (advances to step 2).

### Inline `<script>` (52–95)
- **54–55**: `STORE_KEY` for sessionStorage; `OnboardingApi` URL.
- **57–70** `$(document).ready`:
  - **58–60**: "Get Started" → navigate to Onboarding2.
  - **61–69**: "Skip" → wipe the draft, build a default payload, PUT to `/settings/onboarding`. **Both success and error callbacks call `GoToDashboard`** — so even if the save fails the user goes through.
- **72–76** `GoToDashboard`: Updates the cached user object (`onboardingCompleted = true`), redirects to Dashboard.
- **78–94** `BuildDefaultPayload`: Returns the hard-coded defaults — `schedulingPreferences` (6h study/day, 90min continuous, 8AM–10PM, 8h sleep, 12:00–13:00 lunch, 5h/3d exam prep), `notificationSettings` (deadlines on, morning/weekly off, quiet 22:00–07:00), and an empty `constraints` array.

## `Onboarding2.html` (218 lines) — Study Preferences

### Markup (1–146)
- **12**: Step 2; second dot `active current`; progress at 50%.
- Form fields, all bound by ID:
  - **30–35** `#maxDailyStudy` — range 2–12h, step 0.5, default 6.
  - **38–45** `#maxContinuous` — select 30/60/**90**/120 min.
  - **48–56** `#breakDuration` — select 5/10/**15**/20/30 min.
  - **62–86** Day window — `#dayStart` (5AM–12PM, default **8**), `#dayEnd` (4PM–11PM, default **22**).
  - **92–95** `#sleepHours` — range 5–10, default 8.
  - **101–104** `#maxDailyTotal` — range 6–20h, default 14. Hint clarifies it's study + work + classes + personal.
  - **111–117** Exam prep — `#examPrepHoursPerDay` (1–12, default 5) and `#examPrepDays` (1–14, default 3).
  - **121–139** Lunch break — `#lunchEnabled` toggle wraps `#lunchStart`/`#lunchEnd` time inputs (default 12:00–13:00).
- **143–144**: Back / Next buttons.

### Inline `<script>` (148–216)
- **149–158**: Local `LoadDraft` / `SaveDraft` helpers.
- **160–192** `$(document).ready`:
  - **161**: Load existing draft.
  - **164–175**: Restore each field from draft if present. Note the lunch toggle: if draft says `lunchEnabled === false`, uncheck it and hide the time row.
  - **177**: Initial `UpdateRangeLabels` call to seed the right-side "Xh" labels.
  - **179**: Wire `input` event on the three ranges → live label update.
  - **180–182**: Lunch toggle hides/shows time row.
  - **184–187** Back: `CollectStep2()` (save draft) then go to Onboarding1.
  - **188–191** Next: same collect, then Onboarding3.
- **194–198** `UpdateRangeLabels`: Mirrors range values into `#maxDailyStudyValue` / `#sleepHoursValue` / `#maxDailyTotalValue` with an "h" suffix.
- **200–215** `CollectStep2`: Reads every field, parses int/float, applies defaults via `||`, calls `SaveDraft`. Lunch is captured as `lunchEnabled` boolean + raw `lunchStart`/`lunchEnd` strings.

## `Onboarding3.html` (396 lines) — Notifications, Constraints, External Integrations

### Markup (1–174)
- **9**: Loads Google Identity Services SDK (needed for legacy Google Calendar OAuth path).
- **12**: Step 3; third dot `current`; progress 75%.

#### Notifications section (29–74)
- **32–40** `#notifyDeadline` toggle, default **on**.
- **42–50** `#notifyMorning` toggle, default off.
- **52–60** `#notifyWeekly` toggle, default off.
- **62–73** Quiet hours — `#quietStart` / `#quietEnd` (defaults 22:00 / 07:00).

#### Recurring Constraints section (76–121) — repeating "blocked" time
- **79** `#constraintList` — empty container the JS fills with chips.
- **81** `#addConstraintBtn` — toggles the form.
- **83–121** Hidden form `#constraintForm`:
  - **86–89** Type select: `work` / `personal`.
  - **92–93** Name input.
  - **96–105** 7 day-of-week checkboxes (values 0–6 = Sun–Sat).
  - **109–115** Start/end time inputs (defaults 17:00–20:00).
  - **117–120** Save / Cancel buttons.

#### External integrations (123–167)
- **126–137** Google Calendar item — Connect button + hidden "Connected" badge.
- **139–149** Ruppinet item — Connect button + hidden status badge.
- **151–166** Hidden Ruppinet credentials form (`#ruppinetOnbId`, `#ruppinetOnbPass`, Submit/Cancel).

### Inline `<script>` (176–394)

- **178** Module-level `constraints` array.
- **180–187** Local `LoadDraft` / `SaveDraft`.
- **189–192** Local `escapeHtml` (duplicate of `escapeHtmlGlobal`).
- **195–199** API endpoint URLs for sync flow + auth config + Ruppinet connect.

#### `$(document).ready` (201–256)
- **202**: Load draft.
- **204–208**: Restore notification toggles + quiet times if present.
- **210–211**: Restore `constraints` array, render chips.
- **214–221**: Restore "Connected" UI for Google Calendar / Ruppinet if previously connected during the wizard.
- **223–226** Add constraint button: show form, hide add button.
- **227–231** Cancel: reset + hide form, show add button.
- **232** Save: `SaveConstraint`.
- **235** Google Cal connect: `ConnectGoogleCalendar`.
- **238–246** Ruppinet form toggle + submit wiring.
- **248–255** Back/Next: `CollectStep3` + navigate.

#### `ConnectGoogleCalendar` (258–318)
Two-path flow depending on whether the server uses Composio or legacy Google OAuth:
- **261**: GET `/calendar-sync/status` to learn which path.
- **262–278** **Composio path**: POST to `/calendar-sync/connect` with a `callbackUrl`. Server returns a `redirectUrl` → the page navigates there to finish the OAuth dance externally.
- **281–313** **Legacy path**:
  - Fetches `/auth/config` for the Google client ID.
  - Bails if it's still the placeholder.
  - Bails if the GSI library hasn't loaded.
  - Calls `google.accounts.oauth2.initTokenClient` with `calendar.readonly` scope.
  - In the GSI callback, POSTs the access token to `/calendar-sync/google`. On success, hides the Connect button, shows the badge, marks `googleCalConnected: true` in the draft.

#### `ConnectRuppinet` (320–338)
- Validates ID + password, disables button.
- POST `{ ruppinetId, ruppinetPassword }` to `/settings/ruppinet/connect`.
- On success: hide form, hide button, show "Connected" badge, mark draft `ruppinetConnected: true`.
- On error: re-enable, show server message.

#### Constraint helpers (340–382)
- **`SaveConstraint` (340–358)**: Reads name/type/start/end + checked days → push `{type, name, days, startTime, endTime}` onto the array → re-render → reset + close form.
- **`ResetConstraintForm` (360–365)**: Clear name, default times, uncheck days.
- **`RenderConstraints` (367–382)**: Build constraint chips, e.g. "Part-time job (work) - Mon, Wed 17:00–20:00" with a remove `×` button. Wires `.constraint-remove` clicks to splice the array and re-render.

#### `CollectStep3` (384–393)
Saves the 3 notification toggles, quiet hours, and the constraints array into the draft.

## `Onboarding4.html` (142 lines) — Summary & Save

### Markup (1–42)
- **12**: Step 4; all 4 dots active, last `current`; progress 100%.
- **28–36** Static empty `.onboarding-summary-item` divs the JS fills:
  - `#summaryStudy`, `#summaryWindow`, `#summarySleep`, `#summaryLunch`, `#summaryNotif`, `#summaryExamPrep`, `#summaryConstraints`.
- **39–40** Back / "Go to Dashboard" buttons.

### Inline `<script>` (44–140)
- **45–46** `STORE_KEY`, `OnboardingApi` URL.
- **48–51** `LoadDraft`.
- **53–58** `formatHour(h)` — converts 24h int to 12h string ("12:00 AM" / "8:00 AM" / "12:00 PM" / "10:00 PM").
- **60–76** `$(document).ready`:
  - **61–62**: Load draft, render summary.
  - **64–66**: Back → Onboarding3.
  - **67–75**: Next button — disable + "Saving...", build payload, PUT to `/settings/onboarding`, success → `FinishSC`, error → `FinishEC`.
- **78–95** `RenderSummary(data)`: Fills each summary div with a human-readable line. Notifications line concatenates whichever toggles are on, or "None".
- **97–123** `BuildPayload(data)`: Maps draft → API shape with all the same defaults Onboarding1 used.
  - **105–106**: If lunch is disabled, `lunchBreakStart` / `lunchBreakEnd` are sent as `null` (the server treats nulls as "no lunch").
- **125–130** `FinishSC` (success): Wipe draft, mark user `onboardingCompleted = true`, redirect to Dashboard.
- **132–139** `FinishEC` (error): Logs the error but does the **same redirect anyway**. Comment explains: user can retry from Settings later. Trades durability for not blocking the user on a flaky network during onboarding.

---

# PART 3 — Dashboard.html (720 lines)

The home page after login. Aggregates everything: hero greeting, progress bar, alert pills, KPI stats, workload chart, relocation suggestions, shared task invitations, suggested next task, "Needs Review" list, weekly insights, and a 3-day mini calendar.

## Markup (1–29)

- **8–10**: jQuery + ajaxCall + appShell.
- **12**: `data-page="dashboard"` (`appShell` matches this against `NAV` to mark the active sidebar item).
- **13–29**: A single `#pageRoot` div with **placeholder containers for each section** (`#dashHero`, `#dashProgress`, `#dashAlerts`, `#dashStats`, `#dashSharedTasks`, `#dashRelocationSuggestions`, `#dashWorkload`, `#dashCalendar`, `#dashWeeklyInsights`, `#dashSuggestion`, `#dashReview`, `#dashOverdue`). Each is filled in by JS later.
- **15–17**: Inside `#dashHero`, two `.skeleton .skeleton-text` divs — these are pulse-animated loading placeholders shown until the API call returns.

## Inline `<script>` (31–718)

### API endpoints (33–40)
- `DashboardApi` — main aggregated GET.
- `WeeklySuggestApi` — separate endpoint for weekly insights.
- `ApproveApi(taskId)` — function-builder for `/scheduling/approve/{id}`.
- `SharedTasksApi`, `SharedTaskRespondApi(taskId)` — collaboration.
- `EventsApi` — for the mini calendar.
- `TaskCompleteApi(id)` — POST to mark a task complete.
- `SchedulingRunApi` — re-run the scheduler.

### Module-level state (42–51)
- **42** `miniCalStart` — Date pointer for the 3-day mini calendar window.
- **43** `cachedDashboard` — last server response (used by some handlers).
- **45–51** `MINI_CAL_COLORS` — per-event-type palette (bg / border / text) for the mini calendar pills. Five types: class, task, work, personal, exam.

### Boot (53–57)
1. `requireAuth()` — kick to login if no token.
2. `SmartStudy.initShell("dashboard")` — wraps `pageRoot` in sidebar + topbar + notification bell.
3. `LoadDashboard()`.

### Helpers (59–68)
- **`escapeHtml`** — local copy of the global helper.
- **`toLocalISO`** — formats a Date as `YYYY-MM-DDTHH:mm:ss` *in local time* (no `Z` suffix). Used for the events query string so the server sees the user's local day window.

### `LoadDashboard` (70–87)
Single GET to `/api/dashboard`. On success: cache + run **all 10 render functions** in sequence, then call `LoadWeeklySuggestions()` (separate GET) and `RenderMiniCalendar()` (events GET). On failure: replace the hero with an error string.

### `RenderHero` (90–110)
- Pulls `firstName` from cached user.
- Picks greeting by hour: <12 morning, <18 afternoon, else evening.
- Long localized date (e.g. "Monday, March 4").
- Picks a **motivational message based on stress score** (5 buckets: ≤25 calm, ≤40 balanced, ≤60 picking up, ≤80 heavy, else high pressure).
- Replaces the skeleton placeholders.

### `RenderProgress` (112–123)
Computes `completed / total * 100`, renders a labeled progress bar `Semester Progress | X/Y tasks` with a fill width.

### `RenderAlerts` (125–150)
Builds an array of pill spans, then joins. Each pill is conditional:
- **128**: `pendingTasks` count → warning pill.
- **130–134**: For each item in `nextExams`, if `daysUntil ≤ 7` → danger pill `"Exam in N days: <courseName>"`.
- **136–139**: `overloadedDays` count → danger pill.
- **141–144**: `unscheduledTaskCount` → warning pill.
- **146–147**: `overdueTasks.length` → danger pill.

### `RenderStats` (152–169)
Three KPI cards: **Due Today** (filters `upcomingDeadlines` to today), **Pending** count, **Progress** percent. Color-coded coral / amber / green via CSS modifier classes.

### `RenderWorkload` (171–202)
Bar chart of next 7 days from `dailyWorkload`:
- **176–180**: Filter to today + next 6 days (`< today + 7d`).
- Header line shows today vs week totals.
- For each day, computes `totalH` (or falls back to `scheduledHours`), capped percent (`min(100, totalH/10*100)` → 10h is full bar), and a color class: **overloaded** (server-flagged), else `heavy >6h` / `moderate >3h` / `light` otherwise. Today's bar gets an extra `today` class.

### `RenderRelocationSuggestions` (205–239)
Shown when the scheduler couldn't fit some tasks ("No room for X"). Each card has:
- A 💡 icon, blocked task title, server-supplied message.
- **"Move"** button → links to Calendar at the suggested date (via `?date=YYYY-MM-DD` query).
- **"×" dismiss** button → removes the card client-only (no server call). When the last card dismisses, the whole section empties.

### Shared Tasks block (242–348)

#### `LoadSharedTasks(dashboardData)` (242–248)
Separate GET to `/shared-tasks`, then renders. On failure renders with empty array (the section just hides itself).

#### `formatSlotRange(from, to)` (250–255)
Formats `[from, to]` as e.g. `"Mon, Mar 4 14:00-16:00"`.

#### `RenderSharedTasks(dashboardData, sharedTasks)` (257–342)
Two sub-sections in one card.

**A) Pending invitations** (lines 262–266, rendered 276–300):
Filters shared tasks where `sharedStatus === "Pending"` AND the current user (matched by lowercase email) has `responseStatus === "Pending"`. For each, finds the *other* member as `partner`. Card shows title + course + "From: partnerName" + Accept/Decline buttons.

**B) Proposed slots** (lines 268–270, rendered 302–327):
Filters `dashboardData.needsReviewTasks` to shared tasks with `schedulingStatus === "NeedReview"` and at least one slot. Shows up to first 3 slots as a `<ul>`. Buttons: "Confirm slot" + a link to Calendar.

If both lists are empty the section is wiped (272).

**Wiring** (332–341): Accept/Decline → `RespondSharedTask(taskId, accept)`. "Confirm slot" → POST to `ApproveApi(taskId)`, then reload dashboard.

#### `RespondSharedTask(taskId, accept)` (344–348)
POST `{accept}` to `/shared-tasks/{id}/respond`. Reload on success.

### `RenderSuggestion` (351–373)
"Next task to work on" — server-picked single recommendation. Renders title + course + priority badge + due date + estimated hours + a "View Tasks" CTA.

### "Needs Review" block (376–495) — the biggest chunk

#### `RenderReview(data)` (376–495)
Up to 20 tasks needing user attention. Header shows count badge. Three flag classifications (393–395):
- `isOverdue` (`schedulingStatus === "Overdue"`)
- `isNeedReview` (status `NeedReview`)
- `isUnscheduled` (status `Unscheduled` or `Partial`)

**Slot consolidation** (397–415): If a task has scheduled slots, group by day, take min(from)/max(to) per day → renders a chip per day `"Mar 4, 14:00-16:00"`.

**Schedule badge** (417–429): One of `Overdue · N days` / `Pending Review` / `Scheduled` / `Partially Scheduled` / `Not Scheduled`.

**Shared badge** (431–435): If `isShared`, either `Shared · Pending` or `Shared`.

**Action buttons** (441–452) — three modes:
- **Overdue**: "Mark Complete" (opens completion modal) + "Reschedule" (re-runs scheduler) + "Edit in Calendar".
- **Unscheduled**: "Schedule Manually" link + "Edit in Calendar".
- **Otherwise (NeedReview)**: "Approve" + "Edit in Calendar".

**Edit-in-Calendar deep link** (437–439): Builds `?date=YYYY-MM-DD` from the first slot's date, so clicking jumps to the right calendar day.

**Card render** (454–469): Title row with priority chip; status + schedule badge + shared badge + (for unscheduled) a "Could not fit in schedule" reason; slot chips beneath.

**Wiring**:
- **474–481** `.dash-approve-btn` → POST `/scheduling/approve/{id}`, reload.
- **483–486** `.dash-complete-overdue-btn` → opens completion modal.
- **488–494** `.dash-reschedule-btn` → POST `/scheduling/run`, reload.

### `ShowCompletionModal` (498–544)
A floating modal injected into `<body>` for marking an overdue task complete *with actual hours captured*.
- **499**: Removes any prior instance.
- **502–520**: Builds modal HTML with: task title, estimate display, an "actual hours" number input, Confirm/Cancel buttons.
- **523**: `requestAnimationFrame` adds `.open` next paint → triggers CSS fade-in.
- **525–526**: Close on X / Cancel / backdrop click.
- **528–543** Confirm:
  - Reads actual hours.
  - POST `/tasks/{id}/complete` with `{actualHours}`.
  - On success, **estimation feedback heuristic** (533–539): if `actualHours / estHours` is >1.3 or <0.7, shows a tip alerting the user the estimate was off. This feeds the user's calibration over time.
  - Reload dashboard.

### Weekly suggestions (547–596)

#### `LoadWeeklySuggestions` (547–549)
Separate GET to `/dashboard/weekly-suggestions`.

#### `RenderWeeklySuggestions(data)` (551–596)
- Skips section if no suggestions and no focus tasks.
- **555–556**: Icon + CSS class maps per type — `warning ⚠️`, `overload 🔥`, `positive ✅`, `danger 🚨`, `urgent ⏰`, `info 💡`.
- **560–565**: If totals present, renders `Available: Xh / Needed: Yh`.
- **567–578**: Suggestion cards (icon + message).
- **580–592**: "Top Focus Tasks" list with priority badge + due date + estimated hours.

### Mini 3-day calendar (599–717)

#### `RenderMiniCalendar` (599–611)
Computes window `[from, from+3 days)` from `miniCalStart`, builds a local-ISO query string, GET `/events?from=...&to=...`, then `DrawMiniCalendar` with the events.

#### `DrawMiniCalendar($el, from, events)` (613–717)
- **615–619**: Build a 3-day array.
- **621–629** **Auto-fit hour range**: starts at 8–17, then expands if any event extends earlier or later (clamped 0–23). `cellHeight = 40px`, total grid height = `(end - start) * 40`.
- **632–701** Builds the grid HTML:
  - Header (left): title + 5 colored legend dots.
  - Header (right): prev/next nav, "Full Calendar" link, "+" add button.
  - Grid: leftmost time column (one cell per hour), then 3 day columns. Each day has a header (highlighted if today) and a body of empty cells.
  - **For each event in the day** (682–696): Computes top + height in pixels from event start/end hours, looks up colors, picks a label using a fallback chain (`courseName` || `taskTitle` || `workPlace` || `description` || `type` || `"Event"`), renders an absolute-positioned `.mini-cal__event`.
- **703–704** Prev/Next: shift `miniCalStart` by ±3 days, re-render.
- **705–709** "+" add: redirects to Calendar with `?date=today&add=1` (Calendar opens its add-event modal automatically).
- **710–716** Click on a day's header: redirects to Calendar with that date.

---

# PART 4 — Tasks.html (611 lines)

Full CRUD list view for tasks, plus split-into-subtasks, complete-with-actuals, sharing with friends, and an ML-driven hours suggestion.

## Markup (1–162)

- **8–10**: Standard frontend trio.
- **12**: `data-page="tasks"`.
- **14–17** Top action bar: "Reschedule All" + "+ Add Task".
- **19–38** Filter bar: a single "Filter" toggle button reveals a panel with three sections — Status (pending/completed), Priority (high/medium/low), Course (checkboxes injected by JS).
- **41** `#sharedInvitations` — hidden until pending shared-task invites arrive.
- **43–47** `#taskList` — task cards container; starts with 3 skeleton placeholders.

### Three modals at the bottom of the page:

#### `#taskModal` (50–122) — Add/Edit
- **57** Hidden `#taskId` doubles as edit/create discriminator.
- **59–62** Title input.
- **63–81** Two-up: course select (populated by JS) + type select (Assignment / Project / Lab / Reading / Study for exam / Other).
- **82–91** Two-up: Due Date + Estimated Hours.
- **92** `#hoursSuggestion` hint area (filled by ML suggestion call).
- **93–101** Priority — `Auto (calculated)` is the default; manual choices override.
- **102–114** "Share with study partner" toggle reveals a friend select.

#### `#splitTaskModal` (125–141)
Parent task name + dynamic list of subtask rows (title + hours each) + "Add Sub-task" + Split.

#### `#completeModal` (144–161)
Task name + estimate display + "actual hours" input + Skip / Mark Complete.

## Inline `<script>` (164–609)

### API endpoints (166–177)
Same pattern as Dashboard: bare URL strings + function builders for IDs. Notable endpoints unique to Tasks:
- `SplitTaskApi(id)` — POST subtasks.
- `SuggestHoursApi` — GET ML hours suggestion.
- `SharedTasksApi` family — collaboration.

### Module state (179–184)
- `allTasks` — full list from server.
- `courses` — for the course select + course filter.
- `friends` — accepted connections only.
- `sharedTasks` — full shared-task list (including incoming invitations).
- `editingId` — `null` for create, task ID for edit.
- `completingTaskId` — task being completed in modal.

### Boot wiring (191–226)
1. `requireAuth()` + `initShell("tasks")`.
2. **195–197** Top buttons.
3. **198–200** Task modal close (X / cancel / backdrop) + form submit.
4. **202–205** Split modal close + add row + confirm.
5. **207–209** Complete modal close + confirm.
6. **212–214** Shared toggle reveals friend picker.
7. **217–220** Filter panel: any input change → `RenderTasks(ApplyFilters())` + update count badge.
8. **223** Hours suggestion: triggered when course or hours changes.
9. **225** `LoadAll()`.

### Filter UI (228–236)
- `ToggleFilterPanel`: flips display none/flex.
- `UpdateFilterBadge`: button text becomes `"Filter (3)"` when 3 filters are checked.

### Loading sequence (238–272)
- **`LoadAll`**: GET `/courses` first (needed by both the form and the filter), then chains `LoadConnections`, `LoadSharedTasks`, `LoadTasks`.
- **`LoadConnections`**: GET `/connections`, keep only `accepted`, map to `{email, name}`, populate friend select.
- **`LoadSharedTasks`**: GET `/shared-tasks`, render invitations.
- **`LoadTasks`**: GET `/tasks`, render filtered list.

### `PopulateCourseSelect` (274–284)
Populates the modal's course dropdown and builds a course checkbox per course in the filter panel.

### `ApplyFilters` (286–305)
- Starts with full list.
- **288–295** Status filter: keeps tasks matching any checked status (handles both `completed` and `isCompleted` field names).
- **296–299** Course filter: keeps tasks whose `courseId` is in the checked set.
- **300–303** Priority filter: keeps tasks whose lowercased priority is checked.
- Multiple filters AND together (each filter narrows further), values within a filter OR.

### `RenderSharedInvitations` (307–337)
Same logic as Dashboard's invitations: pending shared-task entries where the current user (matched by lowercase email) hasn't responded yet. Shows Accept/Decline buttons. Accept/Decline → `RespondSharedTask` (lines 339–343) → POST to `/shared-tasks/{id}/respond`, refresh both lists.

### `RenderTasks(tasks)` (345–400)
- **346–349** Empty state if no tasks match.
- **350–393** For each task:
  - **357–360** **Shared badge color**: green for `Confirmed`, red for `Cancelled`, amber for everything else.
  - **362–372** **Schedule badge** (only if not completed): `Scheduled: Mar 4` / `Partially Scheduled` / `Not Scheduled`.
  - Card body: title + course + due date + est hours + (actual hours, if recorded) + priority badge + schedule badge + shared badge.
  - Action buttons: Complete + Split (both hidden if completed) + Edit + Delete.
- **396–399** Delegates each `.task-X-btn` click to its handler.

### Add/Edit modal (403–441)

#### `OpenAddTask`
Resets form, clears editing state, defaults priority to `Auto`.

#### `OpenEditTask(id)`
Looks up the task, prefills every field. Notable details:
- **422** `taskType || type` — accepts either field name.
- **425** Priority: shows the actual priority only if `isManualPriority`; otherwise back to `Auto`.
- **428–435** Restores the share toggle + friend pick.

### `SubmitTask` (443–487) — the most complex piece on the page

Builds payload with title / courseId / taskType / dueDate / estimatedHours / priority. Reads share toggle + friend email separately.

**Edit branch (458–474)**:
1. PUT `/tasks/{id}` with the data.
2. On success, decide what to do about sharing based on the **before/after share state**:
   - **Was not shared, now shared**: POST to `/shared-tasks` to create the share link.
   - **Was shared, now not shared**: POST to `/shared-tasks/{id}/cancel` to revoke.
   - **No change**: just close + reload.
3. Each branch closes the modal + reloads.

**Create branch (476–485)**:
1. POST `/tasks`. Server returns the created task with `taskId`.
2. If shared, follow up with POST `/shared-tasks` linking the new ID to the friend.

### `ConfirmComplete` (500–528)
POST `/tasks/{id}/complete` with `{actualHours}`. On success:
- **509–515 First-task tip**: same ratio heuristic as Dashboard (`>1.3x` or `<0.7x` triggers an alert).
- **516–524 ML stats hint**: if the server returns `mlStats` with `sampleSize >= 3` and a non-`accurate` bias, alerts the user that they tend to under/overestimate this course, with the average ratio.
- Reload tasks.

### Other handlers
- **`DeleteTask` (531–536)**: Native `confirm()`, then DELETE.
- **`Reschedule` (539–545)**: POST `/scheduling/run`, alert on completion.

### Split flow (548–587)

#### `ShowSplitModal(taskId)`
- Sets the parent name in the header.
- Stashes the task ID on the Confirm button via `.data()`.
- Adds two empty subtask rows by default.

#### `AddSubtaskRow` (559–573)
Appends a row with title + hours + a `×` remove button.

#### `ConfirmSplit` (575–587)
- Reads the stashed task ID.
- Iterates the rows, builds `{title, estimatedHours}` array (skips empty titles).
- POST `/tasks/{id}/split` with the subtasks.

### `FetchHoursSuggestion` (590–608)
ML-driven hint that fires when the user picks a course or types hours.
- Builds query: `?courseId=...&estimatedHours=...` (hours optional).
- GET `/tasks/suggest-hours`.
- If `hasSuggestion` and a `suggestedHours` is present:
  > *"Machine Learning suggests ~Xh (based on N past tasks, R× ratio)"*
- Else if only an adjustment factor is available, shows past accuracy ratio.
- Otherwise hides the hint.

---

# PART 5 — Calendar.html (976 lines, the biggest single page)

Three views (Week / 3-Day / Month), unified events grid (classes + tasks + work + personal + exams), conflict checking, drag-to-create, recurrence editing, and three external sync buttons (Google / Ruppinet / Moodle).

## Markup (1–169)

- **8–11**: jQuery + GSI (for legacy Google OAuth) + ajaxCall + appShell.
- **15–33** Top action bar (two halves):
  - Left: prev/next arrows + `#calendarHeader` (e.g. "Mar 4 - Mar 10, 2026").
  - Right: Today button, "+ Event", three sync buttons, **view toggle** (Week / 3 Day / Month) — `data-view` drives the JS.
- **35–39** `#calendarGrid` — render target, starts with skeleton.
- **41–47** Color legend (5 dots: Classes / Tasks / Work / Personal / Exams).
- **50–168** **Single event modal** that handles all 5 event types via show/hide field groups:
  - **63–69** Type select (personal/class/work/task/exam).
  - **71–78** Two date+time rows. End row is hidden for exam (duration-based instead).
  - **79–84** "Recurring weekly" toggle.
  - **87–90** Class fields: course + location.
  - **93–96** Work fields: workplace + travel time minutes.
  - **99–107** Personal fields: category (Exercise/Social/Errand/Other) + description.
  - **110–144** **Task fields with source toggle** (Existing task / New task) — "New task" reveals title/course/type/due/hours so you can create a brand-new task and schedule it in one go.
  - **147–159** Exam fields: course + session A/B/C + duration minutes.
  - **162** Delete button — only shown when editing.

## Inline `<script>` (171–974)

### API endpoint URLs (173–195)
A lot of them — base `/events`, four subtype endpoints (`class`/`task`/`work`/`personal`) for both POST and PUT-by-id, conflict check, exams, courses, tasks, three sync endpoints, and config.

### Constants & state (197–214)
- **197–203** `EVENT_COLORS` — same 5-color palette as Dashboard's mini cal.
- **205** Initial view: **3day on mobile (≤768px), weekly on desktop**.
- **206** `currentDate` — anchor date.
- **207–209** Caches: events / courses / tasks.
- **210** `overloadedDays` Set of `YYYY-MM-DD` strings — drives the red day-column highlight.
- **211–213** Edit state: `editingEventId`, `editingEventType`, `conflictsConfirmed` (set true after user clicks "Proceed Anyway").
- **214** `highlightTaskId` — for `?highlight=N` deep linking from other pages.

### Helpers (216–237)
- `escapeHtml`, `toLocalISO` (no Z suffix, server treats as user-local), `formatTime("HH:mm")`, **`getSunday(date)`** — week start.

### Boot (239–293)
1. `requireAuth()` + `initShell("calendar")`.
2. **244–251 Deep linking**: parses `?date=YYYY-MM-DD` (jumps to that day) and `?highlight=N` (highlights events for taskId N).
3. **253–260** View buttons wired; restore initial active class.
4. **262–268** Top buttons: prev/next call `ShiftDate(±1)`, Today resets `currentDate`, "+ Event" opens empty modal, three sync buttons.
5. **270–276** Event modal close + form submit + delete + type change rebinds field visibility.
6. **279–285 Task source toggle**: Existing/New buttons show/hide their respective field groups.
7. **287** `LoadCoursesCache()`. **288** `Navigate()` — initial render.
8. **290–292** If `?add=1`, also opens the modal.

### Cache loaders (295–314)
- **`LoadCoursesCache`**: GET `/courses`, populate three course selects (class, exam, new-task).
- **`LoadTasksCache`**: GET `/tasks?completed=false`, populate the existing-task select with `"<title> (<course>)"`.

### `ShiftDate(direction)` (316–324)
View-aware step: **monthly = ±1 month**, **weekly = ±7 days**, **3day = ±3 days**.

### `GetDateRange()` (326–345)
Returns `{from, to}` for the current view:
- **Monthly**: extends `from` back to the previous Sunday and `to` forward to the following Saturday → always a full 6×7 grid.
- **Weekly**: Sunday → Sunday+7.
- **3-day**: today (00:00) → +3 days.

### `Navigate()` (347–394) — the orchestrator
Re-renders header + grid:
1. Computes range, builds events query URL.
2. **Three parallel calls** with a `finish()` gate (only renders when all three resolve):
   - GET `/events?from&to`
   - GET `/exams`
   - GET `/scheduling/status` (for overloaded days)
3. **`finish()` (356–386)**:
   - **358–372 Synthesizes exam events**: filters exams to the visible range, builds a synthetic event with `eventId: "exam-" + examId` (the `exam-` prefix is how the rest of the code distinguishes exams from real event rows).
   - **373** Combines real events + synthetic exam events into `cachedEvents`.
   - **375–383** Builds `overloadedDays` Set from scheduling status.
   - **384** `RenderGrid(range)`.
   - **385** If a highlight task is requested, `HighlightTaskEvents`.

### `RenderHeader` (396–405)
"March 2026" for monthly, otherwise "Mar 4 - Mar 10, 2026" (end is `to - 1`, i.e. inclusive).

### `RenderGrid` (407–415)
Dispatches by view: monthly → `RenderMonthlyGrid`, weekly → `RenderTimeGrid(start, 7)`, 3-day → `RenderTimeGrid(start, 3)`.

### `RenderTimeGrid` (417–515) — time-axis grids (week / 3-day)
- **419** Adds `.cal-grid--3day` modifier when `dayCount === 3`.
- **421–425** Build day array.
- **429–433** Left **time column**, hours 7AM–10PM, one label per hour.
- **435–502** For each day:
  - **437–438** `dateStr` (`YYYY-MM-DD`), `isOverloaded` flag.
  - **440–443** Filter events to this exact day (date+month+year match).
  - **444–445** Compute total task hours for the overload bar.
  - **447–452** Day header: weekday + day number, `today` class, `!` overload badge.
  - **455–457 16 click-targets**: empty `.cal-cell` per hour with `data-date` and `data-hour` — clicks open the modal pre-filled to that slot.
  - **459–487** **Render events as absolutely positioned blocks**:
    - **461–462** Compute fractional start/end hours.
    - **463–464** `top = (startHour - 7) * 50` px, `height = max(25, duration*50)` (50px per hour, min 25px to keep small events legible).
    - **466** **Label fallback chain**: `"Exam: <course>"` for exams, else `courseName || taskTitle || workPlace || description || type || "Event"`.
    - **469–474 Status & shared badges** for task events: NeedReview/Partial badge, shared 👥 icon.
    - **476–479** Extra CSS classes for need-review border, shared decoration, highlighted (deep-link target).
    - **481–486** Renders the `.cal-event` div with inline `top`/`height`/colors.
  - **489–495** **Now-line** (red horizontal line at current time) on today's column, only between 7AM–11PM.
  - **497–499** Overload bar at the bottom showing total task hours.
- **506–514 Click handlers**:
  - `.cal-cell` click → `OpenEventModal(date, hour, null)` (drag-to-create entry point — actually click-to-create here).
  - `.cal-event` click → `OpenEventModalForEdit(eventId)`. **Distinguishes `"exam-N"` IDs from numeric ones** (511–512).

### `RenderMonthlyGrid` (517–577)
- **525–527** Day-of-week header row.
- **529–564 Iterates Sunday → Saturday in week chunks** until past `gridEnd`. Each cell shows day number, up to 3 events as colored chips, "+N more" link. Days outside the focused month get an `outside` class.
- **568–576 Click**: navigates to that date in **weekly view** (drill-down).

### The "highlighted event" feature — end-to-end

It's a **deep-linking** feature. When another page (Dashboard, Tasks) wants to send the user to Calendar with a specific task's scheduled blocks visually emphasized, it appends `?highlight=<taskId>` to the URL. Calendar then finds every event tied to that task and adds a CSS class + scrolls to it.

**5 places participate**:

1. **State (line 214)**: `var highlightTaskId = null;` — closure-scoped default.
2. **Boot read (250–251)**: `params.get("highlight")` parsed to int and stored.
3. **Class application during render (line 479)**: `if (highlightTaskId && e.taskId === highlightTaskId) extraClass += " cal-event--highlighted";` — every render pass marks matching events.
4. **Post-render trigger (line 385)**: `if (highlightTaskId) HighlightTaskEvents(highlightTaskId);` — fires after data loads.
5. **`HighlightTaskEvents(taskId)` (579–591)**: 300ms `setTimeout` (lets DOM settle), filters `.cal-event` divs to those whose underlying event has matching `taskId` (skipping `"exam-"` IDs), adds the highlighted class, `scrollIntoView` smooth-centers the first match.

The class survives view changes (highlight stays on Week → 3 Day → Month switches) because `highlightTaskId` is module-level and re-applied on every render.

### Event modal — `OpenEventModal(dateStr, hour, endHour)` (594–624)
**Create mode**:
- Resets edit state, conflict flag, form.
- Hides delete, enables type select, defaults to "personal".
- **Resets task source** to "existing".
- **611–619 Pre-fills date/hour**: from clicked cell — `dateStr` + `HH:00` start, end defaults to `start+1`.
- Calls `UpdateEventFormFields("personal")` to show only personal fields.
- Calls `LoadTasksCache()` (fresh open-tasks list for the existing-task select).

### `OpenEventModalForEdit(eventId)` (626–681)
**Edit mode**:
- Looks up event in cache.
- Title becomes "Edit Event"/"Edit Exam", submit becomes "Save Changes", delete shown.
- **641–642 Critical UX rule**: Type select is **disabled** unless the event is `work` or `personal`. This prevents converting a class/task/exam (which has tight FK relationships in the DB) into a different type.
- Pre-fills date/time/recurring.
- Switches subtype-specific fields:
  - **662–664** Class: courseId + location.
  - **665–667** Work: workplace + travel time.
  - **668–670** Personal: type + description.
  - **671–673** Task: refresh task cache, prefill `taskId`.
  - **674–678** Exam: courseId + session + duration.

### `CloseEventModal` (683–689)
Removes `.open`, clears edit state, hides conflict warning.

### `UpdateEventFormFields(type)` (691–701)
Toggles `.hidden` on the 5 field groups (`#eventClassFields`, etc.), shows/hides end-row + recurring-row (both hidden for exam), toggles `required` attribute on end inputs.

### Conflict warning (703–728)
- **`ShowConflictWarning(conflicts)`** renders a list of conflicting events inside `#conflictWarning`, with two buttons: "Proceed Anyway" sets `conflictsConfirmed=true` and re-submits the form; "Cancel" hides the warning.

### `SubmitEvent(ev)` (730–773)
- **732–737** Reads form fields.
- **739–740** Required validation (start always; end unless exam).
- **742–750** Builds `from`/`to` Date objects. For exams, `to = from + duration*60000` (no separate end field).
- **753–769 Conflict flow**: For new (not edit) non-exam events, POST `/events/check-conflicts`. If any returned, show warning and exit; otherwise mark confirmed and call `SaveEvent`. The check is bypassed on edit (server already knows the event), on exams (different table), and after the user clicked "Proceed Anyway".
- **771–772** When confirmed, reset the flag and proceed to save.

### `SaveEvent(type, from, to, fromDate, fromTime, recurring)` (775–857)
Per-type dispatcher:

- **778–795 Exam**: Validates course, builds `examData` (date/time/session/duration). Edit → PUT `/exams/{id}` (parses `"exam-N"` → N), else POST `/exams`.
- **797 Common body** for non-exam events: `{from, to, recurring}`.
- **799–804 Class**: Adds courseId, location, computed duration in hours. PUT or POST to class subtype endpoint.
- **805–809 Work**: Adds workPlace + travelTime. PUT/POST to work subtype.
- **810–814 Personal**: Adds type + description. PUT/POST to personal subtype.
- **815–856 Task** (most complex):
  - **Edit (816–822)**: Read taskId from select (or fall back to existing event's taskId), preserve priority/status from existing event. PUT to task subtype.
  - **Create (823–855)**: Branches on the active "Task Source" button:
    - **"Existing" (826–832)**: Read taskId, validate, POST task event with `priority=null`, `status="Scheduled"`.
    - **"New" (833–854)**: **Two-step**: First POST to `/tasks` with the new-task fields (title/course/type/due/hours, priority `Auto`). On success, take the returned `taskId` and POST a task event linking to it. Errors on either step alert the user.

### `EventSaveSC/EC` (859–860) and `DeleteCurrentEvent` (862–871)
- Save success: close + `Navigate()` (re-fetch + render).
- Delete: confirm prompt, then DELETE — exam path uses `/exams/{id}`, all others use the unified `/events/{id}` endpoint.

### Sync handlers (874–973)

#### `SyncRuppinet` (874–890)
Disable button, POST `/settings/ruppinet/sync`. Server returns counts of created courses/class events/exams. Builds a summary string and alerts. On error suggests connecting first in Settings.

#### `SyncMoodle` (892–907)
Same pattern but with Moodle counts (`tasksCreated`, `tasksUpdated`).

#### `SyncGoogleCalendar` (909–973) — most complex
Two-path same as Onboarding3:
- **912** GET `/calendar-sync/status`.
- **913–917** Bail if not configured.
- **918–931 Composio path**: If connected, POST `/calendar-sync/google` with empty body (server already has the OAuth token). If not connected, send the user to Settings.
- **932–968 Legacy OAuth path**: Fetch `/auth/config`, init GSI token client with `calendar.readonly` scope, request access token, then POST `{accessToken}` to `/calendar-sync/google`.

---

# PART 6 — Courses.html (292 lines)

A simpler CRUD page than Tasks. Cards in a 3-column grid, single modal for add/edit, dropdowns for instructor + study partner.

## Markup (1–96)

- **8–10**: Standard frontend trio.
- **12**: `data-page="courses"`.
- **14–16** Top action bar: just "+ Add Course".
- **18–22** `#courseGrid` with class `card-grid-3` (3-column responsive grid). Three skeletons while loading.

### Course modal (25–95)
Single modal handles both create and edit (discriminated by hidden `#courseId`).
- **34–37** Course name (required).
- **38–47** Two-up: credits + weekly hours (decimals allowed).
- **48–59** Two-up: semester ("e.g., 2025B") + instructor select (populated by JS).
- **60–66** Study partner select (populated from accepted friends).
- **67–70** Default estimated hours per task — per-course override of the global default.
- **71–80** Two-up: exam prep hours/day + exam prep days — also per-course overrides (placeholders say "Global default").
- **81–87** "Share new tasks by default" toggle. When set, every task created under this course will start with sharing pre-checked.

## Inline `<script>` (98–290)

### API endpoints (100–104)
- `CoursesApi` — list/create.
- `CourseApi(id)` — get/PUT/DELETE one.
- `InstructorsApi` — for the instructor dropdown.
- `ConnectionsApi` — for the partner dropdown.
- `PartnerApi(id)` — separate endpoint for setting/clearing the study partner per course.

### Constants & state (106–110)
- **106** `COURSE_COLORS` — 6-color rotation for card header bands. Wraps modulo length when there are >6 courses.
- **107–110** Caches + `editingId`.

### Boot (117–129)
1. `requireAuth` + `initShell("courses")`.
2. Wire the add button + modal close (X / cancel / backdrop) + form submit.
3. `LoadAll()`.

### `LoadAll` (131–158)
**Sequential nested calls** (not parallel) — courses → instructors → connections, each waiting for the previous. The nesting is unnecessary (could be parallel like Calendar's `Navigate()`), but here it's a sequential chain.

### `PopulateInstructors` (160–166) / `PopulatePartners` (168–174)
Standard dropdown population. Partners use email as the value (study partners are identified by email — there's no surrogate ID).

### `RenderCourses` (176–209)
- Empty state if no courses yet (with 📚 icon).
- Per-course card with deterministic color from `COURSE_COLORS[i % length]`.
- 4-stat row: credits / weekly hours / task count / exam count.
- Edit/delete buttons use `e.stopPropagation()` for safety.

### `OpenAddCourse` (211–217) / `OpenEditCourse` (219–236)
Edit prefills via dropdown email value. `sharedByDefault` cast to bool with `!!`.

### `SubmitCourse` (240–267) — two-step save
- Builds data object, falsy parses → `null`.
- Reads partner email separately — **goes through a different endpoint**.
- Edit/create branches both call `SetPartnerThenReload`.

### `SetPartnerThenReload(courseId, email)` (269–278)
PUT `/courses/{id}/partner` with `{email}`. Empty string clears the partner; an email assigns it.

The two-step pattern (course CRUD + separate partner PUT) is because partner is conceptually a separate relationship — the dedicated endpoint lets the server enforce that the partner email is among the user's accepted connections.

### `DeleteCourse` (284–289)
Strong confirm — message tells the user that **all tasks and exams under this course will also be deleted** (the database has `CASCADE DELETE` on `CourseId` FKs).

---

# PART 7 — Exams.html (266 lines)

A list of upcoming exams with countdowns, urgency colors, and a "Taking this exam" toggle. CRUD via a single modal.

## Markup (1–84)

- **8–10**: Standard frontend trio.
- **12**: `data-page="exams"`.
- **14–16** Top bar: just "+ Add Exam".
- **18–21** `#examList` — exam cards container (skeleton placeholders).

### Exam modal (24–83)
Single modal for add/edit. Fields:
- **34–37** Course select (required, populated by JS).
- **39–48** Two-up: date + time (both required).
- **49–62** Two-up: session A/B/C + duration (minutes).
- **63–65** Section divider with hint: *"Study time for this course — applies to every exam in the course."* — important: **prep settings are stored on the course**, not the exam.
- **66–75** Two-up: prep hours/day + prep days. Editing these fields actually updates the course's exam prep settings.

## Inline `<script>` (86–264)

### API endpoints (88–91)
- `ExamsApi` — list/create.
- `ExamApi(id)` — get/PUT/DELETE.
- `ExamToggleApi(id)` — separate endpoint to toggle the "isTakingExam" flag.
- `CoursesApi` — for the course select.

### Boot (108–128)
1. `requireAuth` + `initShell("exams")`.
2. Wire add button + modal close + form submit.
3. **119–125 Course-change auto-fill**: When the user picks a course in the modal, prefill `#examPrepHoursPerDay` and `#examPrepDays` from the course's settings. Skips if editing.
4. `LoadAll()`.

### `RenderExams` (157–199)
- 162–193 Per-exam card with:
  - Date block (left): big day + month abbrev.
  - Info block (middle): course name, session, time, duration, "Taking this exam" toggle.
  - Right column: countdown badge — **"Today!" if `daysUntil <= 0`**, else "N days". Urgency tier (`imminent` ≤3 / `soon` ≤7 / `far` >7) drives color.

### `SubmitExam` (227–248)
Validates duration (must be positive if provided, else null). Builds payload with falsy parses → `null`.

### `ToggleTaking(id, cb)` (259–263)
PUT `/exams/{id}/toggle-taking` (no body — server flips the flag). On error: revert checkbox optimistically.

The `toggle-taking` endpoint exists separately because changing this flag has cascade effects on the scheduling engine (a "not taking" exam shouldn't generate study blocks), so it gets a dedicated server-side handler that recomputes the affected schedule.

---

# PART 8 — Analytics.html (221 lines)

A 5-card analytics dashboard. No charting library — every chart is hand-rolled with CSS-styled divs.

## Markup (1–49)
Five `.card` containers in `analytics-grid` (CSS grid layout):
- **#weeklyChart** — 7-day stress trend bar chart.
- **#workloadByCourse** — % distribution of estimated hours per course.
- **#taskStats** — total/completed/pending/overdue + completion rate.
- **#estVsActual** — overlay bars: estimated vs actual hours per course.
- **#learningInsights** — per-course accuracy ratios.

## Inline `<script>` (51–219)

### Boot (67–85)
**Two parallel chains**:
- **Chain A**: GET `/stress/weekly` → `RenderWeeklyChart`.
- **Chain B**: GET `/tasks` → GET `/courses` → render the 4 task-driven cards.

### `RenderWeeklyChart(weekly)` (87–101)
Hand-rolled bar chart: one column per day, height = score percent, color from server (green/orange/red based on stress zone).

### `RenderWorkloadByCourse` (103–116)
Total estimated hours across all tasks as denominator. For each course, sum hours, compute percent, render horizontal progress bar with `Xh (Y%)` label.

### `RenderTaskStats` (118–137)
Four KPI boxes (Total/Completed/Pending/Overdue) + completion rate progress bar.

### `RenderEstVsActual` (139–186) — most visually complex
- Filter completed tasks with both estimate and actual hours.
- Group by course, sum totals.
- Overlay bar: 35% opacity background = estimated, foreground full opacity = actual. Both same color.
- Label inside bar: `"Xh / Yh"` (actual / estimated).
- Accuracy on right: red >120%, orange <80%, green between.

### `RenderLearningInsights` (194–218)
- Bar fill: `Math.min(accuracy, 200) / 2` — **centered on 100% accuracy**. 100% → fills 50%, 200%+ → fills 100%.
- Label tier: `>110%` red "Takes longer than estimated", `<90%` orange "Finishes faster than estimated", else green "On track".

---

# PART 9 — Friends.html (268 lines)

Three sections stacked vertically: pending requests, accepted friends grid, shared tasks. Plus an invite modal. Note: this page **does not include the safe-zones / availability viewer** — that's not exposed in this UI (the safe-zone API exists server-side but no current page calls it).

## Markup (1–65)
Three sections:
- **`#friendRequests`** — hidden by default, pending invitations (received and sent).
- **`#friendsList`** — `card-grid-3` for accepted connections.
- **`#sharedTasksSection`** — also hidden, shared task collaborations.

Invite modal with single email input.

## Inline `<script>` (67–266)

### `LoadConnections` / `ConnectionsSC` (113–124)
GET `/connections` returns the **full list with status field** (`accepted` / `pending` / `sent`). The `pending` vs `sent` distinction is server-side: a request you **received** is `pending` (you can act on it), one you **sent** is `sent` (you wait).

### `RenderPendingRequests` (137–169)
- `status === "sent"` → italic "Waiting for response..." (no buttons).
- `status === "pending"` → Accept + Decline buttons.

### `RenderFriends` (171–199)
Empty state with 🤝 icon: *"Invite classmates to connect and find study times together"* (the only place the safe-zone feature is hinted at in the UI).

### `RenderSharedTasks` (201–241)
- Filter out cancelled tasks.
- Identify partner via email comparison.
- Action branch:
  - **If I need to respond**: Accept + Decline buttons.
  - **Pending but not on me**: badge "Awaiting response".
  - **Confirmed**: badge "Confirmed".

### `SubmitInvite` (249–265)
- **Self-invite check** (client-side): compare to current user's email.
- **Duplicate check** (client-side): combine connections + pendingRequests, check for existing.
- POST `/connections/invite`. Server enforces same checks plus existence checks.

---

# PART 10 — Settings.html (506 lines)

The "control panel" page. Four cards stacked: Profile, Notification Preferences, Scheduling Preferences, Integrations.

## Card 1: Profile
Read-only email + editable first/last name + Save button.

## Card 2: Notification Preferences
Five toggles + quiet-hours time pair: Task Reminders, Daily Summary, Weekly Plan, Push Notifications, Quiet Hours from/to.

## Card 3: Scheduling Preferences
Mirrors Onboarding 2 with all fields shown together — sliders, study window, sleep, max daily total, default task hours, exam prep, lunch toggle.

## Card 4: Integrations
Three integrations:
- **Ruppinet**: Connect (reveals form) / Sync + Disconnect.
- **Moodle**: Sync + Disconnect (no separate connect — uses Ruppinet credentials).
- **Google Calendar**: Connect / Sync + Disconnect.

## Inline `<script>` (204–504)

### Boot (223–259)
Five parallel loaders fired immediately: profile, scheduling prefs, Ruppinet status, Moodle status, GCal status.

### Profile (262–285)
- `LoadProfile` returns profile + nested `notificationSettings`.
- `SaveProfile` PUT `/settings/profile`. **Also updates localStorage cached user** so the sidebar avatar reflects the new name without a reload.

### Scheduling Preferences (303–348)

#### `SaveScheduling(e)` — two-step save
1. PUT preferences.
2. **Then POST `/scheduling/run`** to immediately reschedule with the new constraints.
   - Both succeed → "Scheduling preferences saved & rescheduled".
   - Reschedule failure → still alerts "Preferences saved" (the prefs *did* save).

### Ruppinet (351–414)

#### `ConnectRuppinet`
POST `/settings/ruppinet/connect`. **On success: also reload Moodle status** (because Moodle uses Ruppinet creds — connecting Ruppinet enables Moodle automatically).

#### `DisconnectRuppinet`
*"Your imported data will remain"* — Ruppinet disconnect doesn't delete previously synced data. Reloads both Ruppinet and Moodle statuses (Moodle becomes unavailable when Ruppinet is gone).

### Moodle (417–454)
**`IsAvailable` is derived from having Ruppinet creds** (Moodle uses the same auth). No separate Moodle connect.

### Google Calendar (457–503)

#### `LoadGcalStatus` — three states
- `isConnected` → "Connected - Last synced: ..." + Sync+Disconnect.
- `isEnabled` (configured but not connected) → Connect button.
- Neither → "Integration not configured" — hides all buttons.

#### `ConnectGcal`
**Composio-style flow**:
1. Build callback URL.
2. POST `/calendar-sync/connect` with `{callbackUrl}`.
3. Server returns `{redirectUrl}` — Google OAuth URL via Composio.
4. `window.location.href = redirectUrl` — sends user to Google's consent screen. After consent, Google redirects back, server stores token.

#### `DisconnectGcal`
Confirm: *"Synced events will be removed"* (unlike Ruppinet/Moodle, GCal disconnect **does** delete imported events).

---

# PART 11 — Program.cs (102 lines)

Backend startup. Bootstraps DI, JWT auth, CORS, static files, controllers, and three hosted background services. **Surprisingly small** — that's because the DAL and "internal services" don't use DI (they're called as static methods on POCO models). Only HTTP-dependent external services and background workers go through the container.

## Top imports (1–7)
JWT/Claims for token validation. `PhysicalFileProvider` for serving the frontend folder. `SmartStudy.Services` for the registered service classes.

## Builder (9)
Standard .NET 6 minimal-host pattern.

## Architectural comment (11–15)
Documents the **deliberate non-DI design** for the DAL:
- `DBservices` is instantiated as `new DBservices()` directly inside model classes.
- Controllers call **static methods on Model classes** (e.g. `Course.GetAll(email)` rather than injecting an `ICourseService`).
- This is the HW3 academic pattern.
- The DB schema must be provisioned manually via `Schema.sql` before running.

## JWT setup (17–32)
- Reads `Jwt:Key` from configuration, **falls back to a hardcoded dev secret**.
- Wraps as `SymmetricSecurityKey` (HMAC-SHA256).
- `TokenValidationParameters`: `ValidateIssuer = false`, `ValidateAudience = false`, `ValidateLifetime = true`, `ValidateIssuerSigningKey = true`.

So tokens are validated only by signature + expiry.

## Authorization (34)
`AddAuthorization()` — registers policy infrastructure. Controllers use plain `[Authorize]` (no policies, no roles).

## Service registration (38–45)
- `AddHttpClient()` for `IHttpClientFactory`.
- **Scoped services**: `ComposioService`, `GoogleCalendarService`, `EmailService`, `RuppinetApiClient` + `RuppinetSyncService`, `MoodleApiClient` + `MoodleSyncService`.

`Scoped` lifetime = one instance per HTTP request.

## Background services (48–50)
Three `IHostedService`s:
- `NotificationBackgroundService` — periodic notification generation.
- `RuppinetBackgroundSyncService` — periodic Ruppinet re-sync.
- `MoodleBackgroundSyncService` — same for Moodle.

## Controllers + Swagger (52–54)
`AddControllers()`, plus Swagger generation (mounted only in development).

## CORS (57–63)
**Wide-open**: `AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()`. Fine for dev. JWT in `Authorization` header (not cookies) means credentials don't need to be allowed.

## PathBase for IIS subpath (67–70)
Reads `PathBase` from config (e.g. `/igroup27/test2/tar1`). Server-side mirror of the frontend's `BASE_PATH` detection.

## Swagger in dev only (75–79)
`UseSwagger()` + `UseSwaggerUI()` mounted only when `IsDevelopment()`.

## Static file serving (83–97)
The dev-mode frontend host. Resolves `Front/` directory, serves static files at root, **`GET /` redirects to `Pages/Login.html`**.

## Auth + routing (99–102)
`UseAuthentication() → UseAuthorization() → MapControllers() → app.Run()`.

## Important implications
1. **No EF Core registration.** DAL is raw ADO.NET in `DBservices.cs`, called from static model methods.
2. **Three background services run continuously.** Use `IServiceScopeFactory` internally for scoped work.
3. **JWT secret in source.** Hardcoded fallback for the academic project.
4. **Same-origin in dev, separate apps in prod.** The static files block + `PathBase` setup let the same `dotnet run` serve both API and frontend in dev.
5. **Issuer/audience not validated.** Fine for a single-app system but not for multi-tenant.

---

# PART 12 — DBservices.cs (4,522 lines, ~150 methods)

The entire data access layer. **Pure ADO.NET + stored procedures** — no EF Core, no LINQ, no QueryBuilder. Every method follows the same 5-step pattern.

## Infrastructure helpers

### `connect(string conString)` (16–24)
- Re-reads `appsettings.json` every call.
- Looks up the named connection string (`"SmartStudyDb"` everywhere).
- Returns an opened `SqlConnection`.

### `CreateCommandWithStoredProcedureGeneral` (29–42)
- Creates a `SqlCommand` typed as `StoredProcedure`.
- **10-second timeout** on every call.
- Iterates the param dictionary and `AddWithValue` for each, **converting `null` to `DBNull.Value`**.

About 90% of methods use this; the rest build commands inline when they need explicit `SqlDbType` for `TIME` parameters.

## The standard method pattern

Every method follows:
```csharp
public T MethodName(...) {
    SqlConnection con;
    SqlCommand cmd;
    try { con = connect("SmartStudyDb"); }
    catch (Exception) { throw; }

    var paramDic = new Dictionary<string, object>();
    paramDic.Add("@Param", value);

    cmd = CreateCommandWithStoredProcedureGeneral("SS_X_Y", con, paramDic);

    try {
        // ExecuteReader / ExecuteNonQuery / ExecuteScalar
    }
    catch (Exception) { throw; }
    finally { if (con != null) con.Close(); }
}
```

**Three execution variants**:
- `ExecuteReader(CommandBehavior.CloseConnection)` for SELECTs — auto-closes connection when reader disposed.
- `ExecuteNonQuery()` for INSERT/UPDATE/DELETE.
- `ExecuteScalar()` for COUNT/EXISTS/single value.

## `Map*` helpers

Each domain has a `MapXxx(SqlDataReader r)` private static (e.g. `MapUser` at 223). Three patterns inside:
- **Required strings**: `r.GetString(r.GetOrdinal("X"))` — throws if NULL.
- **Nullable strings/dates**: `r.IsDBNull(...) ? null : r.GetX(...)`.
- **Bools**: `Convert.ToBoolean(r.GetValue(...))` — works for `bit` and `int 0/1`.

Some maps use a `HasColumn` helper for SPs with optional columns.

## Section walkthrough

### 1. USERS (44–245)
`GetUserByEmail`, `UserExists`, `CreateUser`, `UpdateUserProfile`, `UpdateResetToken`, `ResetPassword`, `SetOnboardingComplete`, `UpdateRuppinetFields`, `ClearRuppinet`, `MapUser`. Covers all 16 user columns including OAuth tokens.

### 2. NOTIFICATION SETTINGS (247–327)
`GetNotifSettingsByEmail`, `UpsertNotifSettings`, `CreateDefaultNotifSettings`. Upsert builds command inline for `SqlDbType.Time` quiet hours.

### 3. SCHEDULING PREFERENCES (329–404)
`GetSchedPrefsByEmail`, `UpsertSchedPrefs`. **One row per user**: max daily study, max continuous, day window, sleep hours, max daily total, lunch break, default task hours, exam prep settings.

### 4. INSTRUCTORS (405–434)
`GetAllInstructors()` — used by the Courses page.

### 5. EVENTS (436–910) — the largest section
Handles four event subtype tables (Class/Task/Work/Personal) plus base `SmartStudy_Events`.

#### Reading
- `GetAllTypedEventsInRange` — joined fetch across all 4 subtypes in one SP.
- `GetEventTimeRange`, `GetEventOwnerEmail`, `GetEventSubtype`.

#### Subtype CRUD
- `CreateClassEvent` / `CreateTaskEvent` / `CreateWorkEvent` / `CreatePersonalEvent` — return new EventId via `ExecuteScalar`.
- `UpdateClassEvent` / `UpdateTaskEvent` / `UpdateWorkEvent` / `UpdatePersonalEvent`.
- `ChangeEventType` — used when editing work ↔ personal.
- `DeleteEvent` — base table cascades to subtypes.

#### Conflict / sync helpers
- `CountConflictingTaskEvents`, `GetConflictingEvents`.
- `GetSharedPartnerTaskId`, `SyncSharedTaskEventMove` — shared task mirror.
- `PinTask` — marks user-pinned (won't be moved by scheduler).

#### `MapTypedEvent` (903) — most complex
Returns a unified `TypedEvent` regardless of subtype. Uses `HasColumn(r, name)` for optional columns.

### 6. COURSES (939–1089)
`GetCoursesByUser` (joins courses + UserCourses + Instructors, counts tasks/exams), `GetCourseById`, `GetMaxCourseId`, `CreateCourse`, `UpdateCourse`, `MapCourse`.

### 7. USER ↔ COURSE JUNCTION (1091–1213)
`UserCourseExists`, `CreateUserCourse`, `DeleteUserCourse`, `UpdateStudyPartner`, `UpdateSharedByDefault`, `GetCourseIdsByEmail`.

### 8. EXAMS (1215–1383)
`GetExamsByUser`, `GetExamById`, `CreateExam`, `UpdateExam`, `UpdateExamFull`, `ToggleExamTaking`, `DeleteExam`, **`DeleteStudyTasksForExam`** (purges auto-generated study tasks). `MapExamWithCourse` joins exam + course name + per-course prep settings.

### 9. TASKS (1385–1727)
- Standard CRUD + `GetSubTasks`, `GetTaskEvents`, `GetSharedInfo`.
- `CheckAllSiblingsComplete` — auto-completes parent when all subtasks done.
- ML data: `GetMLData(email, courseId)` — `{ActualHours, EstimatedHours}` for completed tasks. `GetMLInsights(email)` — per-course aggregates.

### 10. SHARED TASKS (1730–2293)
`CreateSharedTask`, `UpdateSharedTaskStatus`, `CreateSharedTaskMember`, `UpdateSharedTaskMemberStatus`, `GetSharedTaskMemberEmails`, `AllSharedTaskMembersAccepted`, `UpdateSharedTaskMemberCopyTaskId` / `GetSharedTaskMemberCopyTaskId` (mirror task linkage), `CleanupSharedTaskPartnerCopies`, `SetCourseShareApproved`, `GetPendingMembersForCourse`.

### 11. FRIENDS / CONNECTIONS (1846–2086)
`GetFriendRequestsByUser`, `CreateFriendRequest`, `UpdateFriendRequestStatus`, `FriendshipExists`, `GetFriendshipsByUser`, `CreateFriendship`, `DeactivateFriendship`.

### 12. NOTIFICATIONS (2375–2607)
`CreateNotification`, `GetUnreadNotificationCount`, `MarkNotificationsRead`, `MarkAllNotificationsRead`, `IsNotificationDuplicate` (24-hour throttle), `GetUpcomingDeadlineTasks`, `GetDailySummaryData`, `HasRecentWeeklyReminder`.

Notification types: `deadline` / `overload` / `shared_invite` / `shared_response` / `shared_task_invite` / `shared_task_response` / `shared_task_no_time` / `daily_summary` / `weekly_reminder`.

### 13. INTEGRATIONS / MISC (2741–4140)
- `ClassEventExists`, `ClearLastCalendarSync`, `ClearMoodle`, `CountGcalEvents`.
- `CourseExists`, `CreateInstructor`, `CreateTaskWithMoodleId`, `DeleteCourse`, `DeleteTaskWithEvents`, `DisconnectGoogleCalendar`.
- **Scheduling-engine fetches**: `GetAllIncompleteTasks`, `GetAllUserEmails`, `GetBaseEventsInRangeOrRecurring`, `GetClassEventIdsByUser`, `GetGcalPersonalEventIds`, `GetPersonalEventsByUser`, `GetWorkEventsByUser`, `GetCompletedTasksForML`, `GetEventsInDateRange`, `GetExamsForScheduling`, `GetIncompleteLeafTasks`, `GetNeedReviewTaskIds`, `GetOrphanedStudyTaskIds`, `GetPinnedTaskIds`, `GetTaskEventsByTaskIdsAndStatuses`, `GetTaskEventsByUserAndStatus`, `GetTaskEventsInRange`, `GetUpcomingExams`, `GetUserCoursesWithName`.
- `GetUsersByComposioId`, `GetUsersForMoodleSync`, `GetUsersForRuppinetSync` (background service queries).
- `OtherUsersEnrolled`, `ReassignClassEventsCourse`, `ReassignExamsCourse`, `ReassignTasksCourse`.
- `UpdateComposioId`, `UpdateCourseInstructor`, `UpdateEventTimes`, `UpdateGoogleToken`, `UpdateLastCalendarSync` / `UpdateLastMoodleSync` / `UpdateLastRuppinetSync`, `UpdateTaskPriority`.

### 14. DTO classes (4140 → end)
`TaskWithCourse`, `CourseWithEnrollment`, `ExamWithCourse`, `TypedEvent`, `TaskEventInfo`, `SharedTaskInfo`, `MLDataRow`, `MLInsightRow`, `FriendRequestRow`, `FriendshipRow`, `SharedTaskFullRow`, `PendingMemberForCourseRow`, `UpcomingDeadlineTask`, `DailySummaryTask`, `Instructor`, `Event`, `SchedulingTaskRow`, `SchedulingExamRow`, etc. Plain `{ public X { get; set; } }` POCOs.

## Patterns to remember

1. **Connection per call.** Every method opens + closes its own connection. Relies on .NET pool defaults.
2. **`CommandTimeout = 10`s.** Hardcoded.
3. **Stored procedures named `SS_<Domain>_<Action>`.** ~150 of them defined in `Schema.sql`.
4. **NULL handling.** `?? DBNull.Value` for params; `IsDBNull(ord) ? null : reader.GetX(ord)` for results.
5. **`AddWithValue` everywhere except TIME.** Time columns need explicit `SqlDbType.Time`.
6. **No transactions.** Multi-row operations rely on SP atomicity or accept partial failures.

---

# PART 13 — Controllers (15 files, ~2,800 lines total)

Every controller follows the same shape: `[ApiController]` + `[Route("api/...")]` + `[Authorize]` (except auth itself), with a `GetEmail()` helper pulling the user from the JWT claim. They're thin — most logic lives in static model methods.

## Common patterns
- `[Authorize]` → JWT bearer required.
- `GetEmail()` extracts the email claim. The `!` is safe because `[Authorize]` guarantees a token.
- **Authorization checks** look like `if (task == null || task.Email != email) return NotFound()` — returning `NotFound` instead of `Forbidden` doesn't reveal whether a resource exists.

## 1. `AuthController` (222 lines)
The only **un-authorized** controller. DI: `IConfiguration`, `EmailService`, `RuppinetSyncService`, `ILogger`.

- **`POST login`**: Validate password hash, generate token. **Auto Ruppinet sync** if last sync >12h ago — synchronous via `.GetAwaiter().GetResult()`. Failure logged, doesn't block login.
- **`POST register`**: Existence check, hash password, `User.Create`, **`NotifSettings.CreateDefault`**, return token + `IsNewUser=true`.
- **`POST logout`**: Stateless no-op (JWT can't be revoked server-side).
- **`POST forgot-password`**: **Always returns 200** (doesn't leak which emails are registered). Generates 8-char token. **Dev fallback**: if `EmailService.IsConfigured == false`, returns the token in JSON.
- **`POST reset-password`**: Validates token + expiry.
- **`POST google`**: Validates Google ID token. Creates user with random password (Google never logs in by password) + `AuthProvider="Google"`. **If existing user but onboarding not complete → marks complete**.
- **`GET config`**: Returns Google client ID for the frontend GSI button.
- **`GenerateToken`**: HmacSha256, 7-day expiry, claims `Email` + `Name`.
- **`HashPassword`**: SHA256 with hardcoded salt `"SmartStudySalt2026"`. Academic-grade; production needs bcrypt/argon2.

## 2. `StressController` (28 lines)
- `GET score` → `User.GetStressScore(email)`.
- `GET weekly` → `User.GetWeeklyStress(email)` (Analytics bar chart data).

## 3. `SchedulingController` (45 lines)
- `POST run` → `StudentTask.ScheduleAll(email)`.
- `GET status` → daily workload + overloaded days + relocation suggestions.
- `POST approve/{taskId}` → `ApproveTaskEvents` confirms slot slate, removes past slots.

## 4. `CoursesController` (176 lines)
- `GET` resolves study-partner names via separate `User.GetByEmail` calls (N+1, but volume is tiny).
- `POST` allocates new ID via `Course.GetMaxCourseId() + 1` (race-condition vulnerable but single-user dev is fine).
- `PUT` re-runs scheduling automatically if {default hours, exam prep, credits} changed.
- `PUT {id}/partner` validates **friendship exists** before persisting. Sends `study_partner` notification.
- `DELETE` only unenrolls — doesn't delete the global course.

## 5. `ExamsController` (126 lines)
- `POST` auto-sets `IsTakingExam = (session != "B")` (defaulting to "you're not taking the makeup").
- `PUT {id}/toggle-taking`: If turning OFF, **deletes auto-generated study tasks for that exam**.
- All endpoints **re-run scheduling** after mutation.

## 6. `NotificationsController` (75 lines)
- Standard CRUD.
- `POST generate` triggers `Notification.GenerateDeadline` and `Notification.GenerateOverload`. Background services do the same job periodically.

## 7. `TasksController` (307 lines) — densest CRUD

- `POST`: Priority `Auto` → server-computed (`isManualPriority=false`). Subtask inheritance. **Auto-share if course has it**: creates SharedTask + members; **auto-accepts on partner side if `partnerUserCourse.CourseShareApproved == true`**.
- `POST {id}/complete`: Toggles state, captures `actualHours`. **Subtask cascade**: if all siblings done, auto-completes parent. Computes inline ML stats (`underestimate` / `overestimate` / `accurate`) — powers the alerts in Tasks.html.
- `POST {id}/split`: Creates child tasks. Cannot split a subtask.
- `GET suggest-hours`: ML-driven. Needs ≥2 completed tasks in course.
- `GET learning-insights`: Per-course aggregates.

### `BuildTaskDto` (246–306) — the enricher
Computes `schedulingStatus` locally (`Completed` / `Unscheduled` / `Partial` / `Scheduled`), recurses into subtasks, enriches with shared-task partner info, returns ordered `ScheduledSlots`.

## 8. `EventsController` (317 lines) — the busiest endpoint

### `GET` — recurrence expansion in C#
Database stores **one row per recurring event** with `Recurring` flag and optional `RecurrenceEndDate`. Controller **expands recurrences into virtual occurrences**: clones DTO with same `EventId` (so editing any one edits the master), updates `From`/`To`.

### Subtype POST/PUT
Four create + four update endpoints. Pattern: auth check, persist, **conflict counting** before/after `ScheduleAll(email)` to detect "we created this and the scheduler had to reshuffle". Reports `conflictsAutoResolved`.

**Task-event update has special partner sync**: captures old times, updates, **pins the task**, then if shared, **mirrors the move on the partner's calendar** via `Event.SyncSharedTaskEventMove`.

### `POST check-conflicts`
Includes expanded recurrences (capped at 52 iterations).

### `PUT {id}/change-type`
**Allows only work ↔ personal type changes.** Class and task events are locked due to FK relationships.

## 9. `DashboardController` (231 lines) — single mega-aggregator
Fetches stress, course IDs, all tasks, upcoming deadlines/exams, today's events, scheduling status, incomplete tasks + events, today/week workload, **needs-review tasks** with `Overdue > NeedReview > Scheduled > Unscheduled` priority, **next suggested task** scored as:
```
(1/daysUntilDue) * 50
+ min(hours, 10) * 5
+ credits * 4
+ (isShared ? 25 : 0)
```
where hours is adjusted by per-course bias ratio.

## 10. `SettingsController` (326 lines)

DI: `RuppinetSyncService`, `MoodleSyncService`.

- **`GET version`**: Anonymous health probe.
- **`PUT scheduling`**: All values clamped via `Math.Clamp`.
- **`PUT onboarding`**: Big one-shot endpoint. **Materializes constraints into recurring events** (no separate constraint table — they're just normal recurring work/personal events).
- **Ruppinet endpoints**: Connect tests credentials before storing. Password encrypted via `_ruppinetSync.EncryptPassword`.
- **Moodle**: `IsAvailable` derived from having Ruppinet creds.

## 11. `ConnectionsController` (122 lines)
- `GET` merges friend requests + accepted friendships. `pending` (received) vs `sent` (outgoing).
- `POST invite` has **SQL exception → message mapping**: SP throws errors with code 50000; controller pattern-matches messages to 400/404.
- `POST {id}/accept`: Updates request → creates friendship row.
- `DELETE {id}`: Soft-delete.

## 12. `CollaborationController` (85 lines)
- `GET safe-zones?connectionId=...`: Server-side-only feature (no UI calls it). Returns time slots when both users are free + low-stress.
- `POST approve-course-sharing/{courseId}`: Bulk-accepts pending shared tasks for a course. For each fully-accepted: creates partner copy, re-runs scheduling, aligns common time.

## 13. `SharedTaskController` (205 lines)
- `POST`: Validates friendship. Creator auto-`Accepted`, partner `Pending`. Sends invitation notification.
- `POST {taskId}/respond`: **If Accepted and all members accepted → `Confirmed`**. **If Declined → cancels entire share**. On Confirmed: `EnsurePartnerCopyAndSchedule`. **If no common time → `shared_task_no_time` notification to both users**.
- `POST {taskId}/cancel`: Only creator can cancel.

## 14. `CalendarSyncController` (194 lines)
DI: `GoogleCalendarService`, `ComposioService`.
- `POST connect`: Composio path — initiate OAuth, return `redirectUrl`.
- `GET callback` `[AllowAnonymous]`: OAuth landing route. Redirects back to frontend by replacing `/tar1` with `/tar2` in `PathBase`.
- `POST google`: Two-path sync (Composio vs legacy GSI). Pull events ±7 days back to +60 days forward.
- `DELETE disconnect`: Disconnects from Composio, deletes all GCal-imported personal events (tagged `[gcal]`).

## 15. `ScheduleImportController` (268 lines)
- `POST import`: Dispatches by extension to PDF/CSV/Excel/JSON. 10MB limit.
- **Excel** uses EPPlus with `NonCommercial` license context.
- **Course ID derivation**: `12345-67` → `12345*100 + 67`. Otherwise `courseName.GetHashCode() % 100M` (collision risk fine for personal app).
- Creates as **recurring** weekly class events.
- `GetCurrentSemester`: `2025A` (Oct–Feb) or `2025B` (Mar–Sep) — Israeli academic year convention.

## What ties them all together
1. **`StudentTask.ScheduleAll(email)` is everywhere.** Every mutation that affects scheduling triggers a re-run.
2. **Auth-as-Email pattern.** Email is the user PK. Every method's first line is `var email = GetEmail()`.
3. **404 instead of 403.** Defensive — can't distinguish "doesn't exist" from "isn't yours".
4. **Sync-over-async via `.GetAwaiter().GetResult()`.** Mixing async services into sync controllers.
5. **No transaction boundaries.** Multi-step operations are 3+ separate connections.
6. **Notifications fire side-effects everywhere.**

---

# PART 14 — Services (11 files)

The DI-registered service layer. These handle everything that needs `IConfiguration`, `IHttpClientFactory`, or `ILogger` — auth-encrypted external integrations, email, and three background workers.

## 1. `EmailService.cs` (47 lines)
SMTP wrapper for password reset emails.
- **`IsConfigured`**: True if both `Smtp:Host` and `Smtp:Username` are set in config.
- **`SendAsync(to, subject, body)`**:
  - If SMTP not configured: logs a **masked** preview (40 chars) to console and returns. **No email is sent — but the AuthController exposes the reset token in the JSON response** in this case, which is what makes dev mode work.
  - Otherwise: builds `SmtpClient` with `EnableSsl = true`, sends plain-text mail.

## 2. `ComposioService.cs` (229 lines)
Wraps the Composio API (https://backend.composio.dev/api/v3) for Google Calendar OAuth.

- **`IsEnabled`**: From config `Composio:Enabled`.
- **`CreateClient()`**: Builds an `HttpClient` from `IHttpClientFactory` with `x-api-key` header.

### `InitiateConnectionAsync(userId, callbackUrl)` (37–93)
POSTs to `/connected_accounts` with auth_config + user_id + callback_url. Composio returns a redirect URL (with multiple possible nested locations — code tries `connection_data.val.redirect_url`, then `redirect_url`, then `redirectUrl`). Returns `ComposioConnectionResult` with `RedirectUrl` and `ConnectedAccountId`.

### `GetConnectedAccountsAsync(userId, status?)` (98–132)
GET filtered connected accounts. Handles both `{items: [...]}` and bare-array responses.

### `GetConnectedAccountAsync(connectedAccountId)` (137–147)
Single account by ID.

### `ExecuteToolAsync(toolSlug, userId, connectedAccountId, arguments)` (152–176)
The generic "run a Composio tool" call. POSTs to `/tools/execute/{toolSlug}` with the entity ID + arguments. Used to call `GOOGLECALENDAR_EVENTS_LIST`.

### `DeleteConnectedAccountAsync` (181–186)
DELETE the account — used during user disconnect.

### `ParseAccount(JsonElement el)` (188–205)
Tolerant parser: tries `createdAt` and `created_at`, falls back to empty string for missing fields.

### Result classes (208–229)
`ComposioConnectionResult`, `ComposioAccount`, `ComposioToolResult`.

## 3. `GoogleCalendarService.cs` (271 lines)
Two paths for Google Calendar sync.

### `IsEnabled`
From config `Google:CalendarApiEnabled`.

### `SyncViaComposioAsync(email, connectedAccountId, from, to)` (23–81)
The Composio path:
1. Format `timeMin`/`timeMax` as ISO-8601 UTC.
2. Call `_composio.ExecuteToolAsync("GOOGLECALENDAR_EVENTS_LIST", ...)` with `singleEvents=true, orderBy="startTime"`.
3. Parse events via `ParseComposioEvents` (handles multiple response wrappings).
4. For each event: look up `FindPersonalEventByGcalId` — if exists, update times; else create a new personal event tagged `[gcal:<googleId>]`.
5. Update `LastCalendarSync`.

### `SyncEventsAsync(email, accessToken, from, to)` (86–168)
Legacy direct-OAuth path:
1. Direct GET to `https://www.googleapis.com/calendar/v3/calendars/primary/events?timeMin&timeMax`.
2. Parse `start.dateTime` (skip all-day events that have only `start.date`).
3. Same upsert pattern via `[gcal:<id>]` tag.
4. Update `GoogleToken` (stores access token + last sync).

### `ParseComposioEvents` (174–254)
Handles **four nesting variants** Composio uses: `{data: {items}}`, `{data: [...]}`, `{response_data: {items}}`, `{items}`, or bare array. Skips all-day events. Falls back to 1-hour duration if `end.dateTime` missing.

## 4. `TextHelpers.cs` (11 lines)
Single static helper.
- **`StripGcalTag(text)`**: Regex strips `[gcal:...]` markers from event descriptions before display. The events controller calls this so the UI doesn't show the tracking tag.

## 5. `RuppinetApiClient.cs` (394 lines)
Direct HTTP client for the Ruppin University portal (`ruppinet.ruppin.ac.il/Portals/api`).

- **`RuppinetApiException`**: Custom exception with `ErrorCode` (`AUTH_FAILED`, `CAPTCHA_REQUIRED`, `API_ERROR`).

### `LoginAsync(zht, password)` (30–70)
POST `/Login/Login` with student credentials. Detects:
- `captchaRequired` → throws with `CAPTCHA_REQUIRED` (user must log in manually first to trip the captcha).
- `success: true` + `token` field → returns token.

### `GetScheduleAsync(token, from, to)` (72–...)
POST `/Home/ScheduleData` with date range. Filters out holidays/special days (lines 119–120: items with no instructor or room).

### Other methods (not shown in excerpt but referenced)
- `GetCoursesAsync(token)` — list of enrolled courses.
- `GetExamsAsync(token)` — exam schedule.
- `SendWithRetryAsync(client, method, url, body)` — wraps HTTP calls with `_maxRetries` retries.
- `CreateAuthClient(token)` — builds an HTTP client with the bearer token.

## 6. `RuppinetSyncService.cs` (512 lines)
Orchestrates Ruppinet → SmartStudy data sync.

DI: `RuppinetApiClient`, `IConfiguration`, `ILogger`. Holds its own `DBservices()`.

### `SyncAllAsync(email)` (24–116)
1. Look up user, decrypt their stored Ruppinet password (`DecryptPassword`).
2. Login via `_api.LoginAsync` to get a session token.
3. **Three parallel API calls**: courses, schedule (next N days, default 120), exams.
4. After all three resolve:
   - `ProcessCourses` — upsert courses + user enrollments.
   - `CleanupDuplicateCourses` — handle Ruppinet's occasional duplicate IDs.
   - `ProcessSchedule` — create/update class events.
   - `ProcessExams` — create/update exams.
5. Update `LastRuppinetSync`.
6. **Run scheduling engine** to place auto-generated tasks.

### `TestConnectionAsync(zht, password)` (118–...)
Just calls `LoginAsync` and reports success — used by Settings to validate before storing.

### `EncryptPassword` / `DecryptPassword`
Symmetric encryption with a fixed key from config. Not bulletproof but at least credentials aren't plaintext in the DB.

## 7. `RuppinetBackgroundSyncService.cs` (85 lines)
`IHostedService` that periodically syncs all eligible users.

- **Interval**: From config `Ruppinet:BackgroundSyncIntervalHours` (default 6).
- **Initial delay**: 2 minutes after app start (lets the app warm up before the first sync).
- **Sync loop** (`ExecuteAsync`): Tries `SyncAllUsersAsync`, sleeps `_interval`, repeats. `TaskCanceledException` exits cleanly on shutdown.

### `SyncAllUsersAsync` (55–84)
1. Computes `cutoff = now - SyncIntervalHours` (default 12).
2. `_dal.GetUsersForRuppinetSync(cutoff)` returns users whose last sync is older than the cutoff.
3. **Per user**: Creates a fresh DI scope, resolves `RuppinetSyncService`, calls `SyncAllAsync`. Failures logged but don't abort the loop.

The fresh-scope pattern is critical because `RuppinetSyncService` is registered as Scoped — using the singleton `IServiceScopeFactory` is the standard way to use scoped services from a hosted service.

## 8. `MoodleApiClient.cs` (259 lines)
Moodle Web Services REST client (`moodle.ruppin.ac.il`).

### POCO classes (13–42)
`MoodleSiteInfo`, `MoodleCourse`, `MoodleAssignment`, `MoodleQuiz`.

### `GetTokenAsync(username, password)` (59–89)
POST `/login/token.php` with form-encoded credentials + `service: "moodle_mobile_app"`. Detects `errorcode: invalidlogin` → `AUTH_FAILED`. Returns the API token.

### `GetSiteInfoAsync(token)` (91–100)
Calls `core_webservice_get_site_info` to get the user's Moodle ID.

### `GetUserCoursesAsync(token, userId)` (102–118)
Calls `core_enrol_get_users_courses` for enrolled courses.

### `GetAssignmentsAsync` / `GetQuizzesAsync` (120–...)
Bulk-fetch by course IDs using `mod_assign_get_assignments` / `mod_quiz_get_quizzes_by_courses`.

### Internals (not shown)
- `CallFunctionAsync(token, function, params?)` — generic wrapper for `/webservice/rest/server.php?wsfunction=X&wstoken=Y&moodlewsrestformat=json`.
- `SendWithRetryAsync` — retry wrapper.

## 9. `MoodleSyncService.cs` (457 lines)
Orchestrates Moodle → SmartStudy task sync. Follows the same pattern as Ruppinet.

DI: `MoodleApiClient`, `IConfiguration`, `ILogger`. Reads `_defaultAssignmentHours` (4h) and `_defaultQuizHours` (2h) from config.

### `DebugFetchAsync(email)` (30–54)
Returns a JSON dump of what Moodle would return — used by `GET /api/settings/moodle/debug` for troubleshooting.

### `SyncAllAsync(email)` (56–...)
1. Validates user has Ruppinet credentials (Moodle uses the same auth).
2. Decrypt password.
3. Get Moodle token, site info, user courses.
4. Fetch assignments + quizzes for all enrolled course IDs.
5. **Upsert as tasks**: each Moodle assignment/quiz becomes a SmartStudy task with the Moodle CMID stored for de-dup. Default hours from config.
6. Update `LastMoodleSync`.
7. Run scheduling engine.

## 10. `MoodleBackgroundSyncService.cs` (85 lines)
Identical structure to `RuppinetBackgroundSyncService` but with longer intervals: 12h background interval, 24h sync cutoff. Initial delay 3 minutes.

## 11. `NotificationBackgroundService.cs` (88 lines)
Periodic notification generation for all users.

- **Interval**: 30 minutes (hardcoded).
- **Initial delay**: 30 seconds.

### `GenerateNotificationsForAllUsers` (49–87)
Iterates every user email and calls per-user generators:
- **Always**: `Notification.GenerateDeadline(email)`.
- **Always (with try/catch)**: `User.GetStressScore(email)` → `Notification.GenerateOverload(email, score)`.
- **Time-gated**: between 06:00–10:00, `GenerateDailySummary`. The Notification model dedups via `IsNotificationDuplicate`, so multiple ticks within the morning don't create dupes.
- **Day+time-gated**: Sunday/Monday 06:00–10:00, `GenerateWeeklyPlanReminder`.

Per-user errors are caught and logged but don't abort the loop.

## Service patterns
1. **Async-over-DB**. Services are async-throughout; `DBservices` calls inside them are sync (calling sync DAL from async code is acceptable for this load).
2. **HTTP retries.** Both API clients use `SendWithRetryAsync` with config-driven counts.
3. **Encrypted passwords.** Ruppinet credentials encrypted with a fixed symmetric key. Moodle uses Ruppinet's stored creds.
4. **Background services use `IServiceScopeFactory`.** Standard pattern for resolving scoped services from singletons.
5. **Fault tolerance.** Background services swallow exceptions and continue. Sync failures don't abort the loop.

---

# PART 15 — Models (19 files, ~3,865 lines)

The model layer. **Not POCOs** — most contain domain logic + static "facade" methods that wrap `DBservices`. Controllers call them as `Course.GetAll(email)` etc. The largest models embody the actual application logic.

## File sizes at a glance
| File | Lines | Purpose |
|---|---|---|
| `StudentTask.cs` | 1,550 | Scheduling engine + task domain |
| `Course.cs` | 638 | Course CRUD + PDF schedule import |
| `User.cs` | 464 | User + stress calculation |
| `Notification.cs` | 238 | Notification generation logic |
| `Friendship.cs` | 232 | Friends + safe-zone calculation |
| `Event.cs` | 229 | Event subtype facade |
| `SharedTask.cs` | 112 | Shared task lifecycle |
| `Exam.cs` | 100 | Exam CRUD |
| `SchedulingPreferences.cs` | 71 | Scheduling prefs POCO + facade |
| `Dashboard.cs` | 60 | Dashboard aggregator helpers |
| `NotificationSettings.cs` | 51 | NotifSettings POCO + facade |
| `UserCourse.cs` | 36 | Junction-row facade |
| `Instructor.cs` | 18 | Instructor POCO + GetAll |
| `FriendRequest.cs` | 15 | Tiny POCO |
| `SharedTaskMember.cs` | 14 | Tiny POCO |
| `TaskEvent.cs` | 12 | Tiny POCO |
| `ClassEvent.cs` | 11 | Tiny POCO |
| `PersonalEvent.cs` | 7 | Tiny POCO |
| `WorkEvent.cs` | 7 | Tiny POCO |

## 1. Tiny POCOs (`ClassEvent`, `TaskEvent`, `WorkEvent`, `PersonalEvent`, `Instructor`, `FriendRequest`, `SharedTaskMember`)
These are simple property bags, sometimes with a static `GetAll`-style facade. `Instructor.GetAll()` calls `_dal.GetAllInstructors()`.

## 2. `User.cs` (464 lines)
The user POCO + stress calculation engine.

### Properties (~16)
`Email` (PK), `FirstName`, `LastName`, `Password` (hashed), `ResetToken` + expiry, `AuthProvider`, `OnboardingCompleted`, OAuth tokens (Google access/refresh, Composio account ID), Ruppinet/Moodle (encrypted password, last syncs).

### Static facades
Wrap DAL: `GetByEmail`, `Exists`, `Create`, `UpdateProfile`, `UpdateResetToken`, `ResetPassword`, `SetOnboardingComplete`, `UpdateRuppinetFields`, `ClearRuppinet`, `ClearMoodle`, `UpdateGoogleToken`, `UpdateComposioId`, `DisconnectGoogleCalendar`, `GetUsersByComposioId`.

### Stress calculation (the headline algorithm)

#### `GetStressScore(email)` — single-day stress
1. Pull user's incomplete tasks + upcoming exams.
2. Find **nearest deadline** (closest task due date or exam date).
3. **`required_hours`** = sum of `EstimatedHours` for tasks/exams before that deadline.
4. **`available_hours`** = `(deadline - now).TotalHours - sleep_baseline` (8h/day × number of days).
5. **`score`** = `min(100, required / available * 100)`.
6. Returns `{Score, Color}` where color: green ≤40, orange ≤70, red >70.

#### `GetWeeklyStress(email)`
Computes `GetStressScore`-equivalent for each of the next 7 days. Returns a `[{dayName, score, color}]` array — feeds the Analytics weekly bar chart.

The **stress baseline** is what the Dashboard's hero motivation message reacts to (≤25 calm, ≤40 balanced, etc).

## 3. `Course.cs` (638 lines)
Course CRUD + the **PDF schedule importer** (the biggest non-DAL block in the project after `StudentTask.cs`).

### Properties
`CourseId`, `CourseName`, `Credits`, `WeeklyHours`, `Semester`, `InstructorId`, `InstructorName`, `DefaultTaskEstimatedHours`, `ExamPrepHoursPerDay`, `ExamPrepDays`, `StudyPartnerEmail`, `SharedByDefault`, `CourseShareApproved`, counts.

### Static facades
`GetByUser`, `GetById`, `GetMaxCourseId`, `Create`, `Update`, `UpdateSharedByDefault`, `UpdateStudyPartner`, `UserCourseExists`, `CreateUserCourse`, `DeleteUserCourse`, `Delete`, `FindInstructorByName`, `CreateInstructor`, `UpdateCourseInstructor`, `ClassEventExists`, `CreateClassEvent` (used by import flow).

### `ImportSchedule(stream, email)` — PDF parser
Reads a Ruppinet-style PDF schedule using **iText7** (or similar). Steps:
1. Extract text from each PDF page.
2. Regex-match course rows: course code + name + day-of-week + start/end time + room/lecturer.
3. **Course-ID derivation**: same scheme as the JSON/CSV/Excel importer in `ScheduleImportController` — split on `-` and arithmetic, or hash the name.
4. Upsert course + user enrollment + instructor.
5. Create **recurring weekly class events** for the semester window.

This is what powers `POST /api/schedule/import` for PDFs.

## 4. `StudentTask.cs` (1,550 lines) — the scheduling engine

The heart of the application. Holds the task POCO + the **greedy scheduling algorithm**.

### Properties
`TaskId`, `CourseId`, `Email`, `Title`, `Type`, `EstimatedHours`, `DueDate`, `IsCompleted`, `Priority`, `IsManualPriority`, `ActualHours`, `ParentTaskId`, `AllowSplitting`, `IsManuallyPinned`, `SharedStatus`, etc.

### CRUD facades
`GetByUser`, `GetById`, `GetSubTasks`, `Create`, `Update`, `Delete`, `Complete`, `CheckAllSiblingsComplete`.

### Aggregation facades
`GetTaskEvents(taskId)`, `GetSharedInfo(taskId)`, `GetMLData(email, courseId)`, `GetMLInsights(email)`.

### `ScheduleAll(email)` — the engine
Called from every controller after a mutation. Steps:

1. **Load context**:
   - Scheduling preferences (or defaults).
   - All incomplete tasks (with per-course overrides for default hours / exam prep).
   - All upcoming exams.
   - All existing events (classes, work, personal — these are immutable obstacles).
   - Pinned task IDs (won't be moved).

2. **Generate study tasks for exams**: For each upcoming exam (where `IsTakingExam == true`), if `daysUntil <= ExamPrepDays + buffer`, ensure a "Study for exam" task exists with the right hours. Cleanup orphaned study tasks.

3. **Wipe non-pinned task events**: Delete current scheduled blocks that aren't user-pinned, so the engine has free space.

4. **Score and sort tasks**:
   ```
   priorityScore = (1/daysUntilDue) * 40 + priorityWeight * 30 + min(hours, 10) * 3
   ```
   `priorityWeight` from `Priority`: High=3, Medium=2, Low=1, Auto=computed.

5. **Greedy slot allocation**:
   - For each task in priority order:
     - For each day in `[today, dueDate]`:
       - Look at the day's free slots within `[DayStartHour, DayEndHour]` minus existing events, lunch break, sleep window.
       - **Constraints**:
         - Max 8h/day (`MaxDailyStudyHours`).
         - Max 3h continuous per task (`MaxContinuousMinutes`).
         - 30-minute slot granularity.
       - Place blocks until task hours satisfied or day capacity hit.
     - Mark status: `Scheduled` (all hours placed), `Partial` (some placed), `Unscheduled` (none placed), `NeedReview` (manually flagged for review).

6. **Compute relocation suggestions**: For unscheduled tasks, look ahead a few days and suggest where they could fit.

7. **Persist task events** via `Event.CreateTaskEvent` for each placed block.

### `GetSchedulingStatus(email)` (returns `dailyWorkload`, `overloadedDays`, `relocationSuggestions`)
Doesn't run the engine — just reads current scheduled events and computes per-day totals. A day with `totalHours > MaxDailyTotalHours` is `isOverloaded`.

### `GetWeeklySuggestions(email)` (powers Dashboard "Weekly Insights")
Computes:
- `availableHours` = total free time in the week.
- `totalStudyHours` = sum of incomplete-task estimates due this week.
- `suggestions` array with types: `warning` (overload), `positive` (light week), `urgent` (deadline crunch), `info` (neutral tip).
- `focusTasks` — top 3 priority tasks for the week.

### `EnsurePartnerCopyAndSchedule(taskId, creatorEmail, partnerEmail)`
For shared tasks: ensures the partner has their own task copy on their calendar (the SP creates it if missing), then schedules common time slots where both users are free + low-stress (using the safe-zone algorithm).

### `ScheduleSharedTaskAtCommonTime(taskId, creatorEmail, partnerEmail)`
Variant used by the bulk course-share approval. Returns `false` if no common time can be found (triggers the `shared_task_no_time` notification).

### `ApproveTaskEvents(taskId, email, now)`
User confirms a NeedReview task slate: flips slot statuses to `Approved`, deletes any past slots that were missed. Returns `(approvedCount, removedPast)`.

## 5. `Event.cs` (229 lines)
Facade over the four event subtypes.

### Static facades
`GetAllTypedEventsInRange`, `GetById`, `GetSubtype`, `GetOwnerEmail`, `GetEventTimeRange`, `CreateClass`, `CreateTaskEvent`, `CreateWork`, `CreatePersonal`, `UpdateClass`, `UpdateTaskEvent`, `UpdateWork`, `UpdatePersonal`, `Delete`, `ChangeType`, `CountConflictingTaskEvents`, `GetConflicting`, `PinTask`, `GetSharedPartnerTaskId`, `SyncSharedTaskEventMove`.

The model exists mostly to give controllers a tidy entry point — implementation is delegated to `DBservices`.

## 6. `Exam.cs` (100 lines)
- `GetByUser`, `GetById(id, email)` (auth check), `Create`, `Update`, `Delete`, `ToggleTaking`, `DeleteStudyTasksForExam`.

## 7. `SchedulingPreferences.cs` (71 lines)
POCO with all preference fields + static `GetByEmail` and `Upsert` facades.

## 8. `NotificationSettings.cs` (51 lines)
POCO + `GetByEmail`, `Upsert`, `CreateDefault` facades.

## 9. `Dashboard.cs` (60 lines)
Static methods used **only** by `DashboardController`:
- `GetCourseIdsByEmail` — for course-scoped queries.
- `GetUpcomingExams(email, fromDate)`.
- `GetIncompleteTasksWithEvents(email)` — joined fetch returning `(tasks, events)` tuple.
- `GetCompletedTasksForML(email)` — feeds the next-suggested-task scoring.

## 10. `Notification.cs` (238 lines)
Notification generation engine.

### `Create(email, type, title, message, relatedEntityId?, relatedEntityType?)`
Generic insert via DAL. Used as a building block.

### `GenerateDeadline(email)`
1. `GetUpcomingDeadlineTasks(email)` — incomplete tasks due within 24/48 hours.
2. For each: skip if `IsNotificationDuplicate(type="deadline", relatedEntityId=taskId, sinceHours=24)` — prevents dupes.
3. Insert deadline notification.

### `GenerateOverload(email, score)`
If `score > 70` (red zone) and no recent overload notification, creates one.

### `GenerateDailySummary(email)`
Pulls today's workload + tasks via `GetDailySummaryData`, formats as a summary message. Throttled by `IsNotificationDuplicate` to 24h.

### `GenerateWeeklyPlanReminder(email)`
Throttled to once per week via `HasRecentWeeklyReminder`. Body lists upcoming tasks for the next 7 days.

### `CreateSharedTaskInvite`, `CreateSharedTaskResponse`, `CreateNoCommonTime`
Specialized creators for collaboration notifications. Each builds a typed message and stores `relatedEntityId = taskId, relatedEntityType = "SharedTask"`.

### `GetByUser(email)` → `(notifications, unreadCount)`
Fetches notifications ordered by date desc, with unread count.

### `MarkRead(email, ids)`, `MarkAllRead(email)`, `GetUnreadCount(email)`
Standard read state.

## 11. `Friendship.cs` (232 lines)
Friends + safe-zone calculation.

### Properties
`FriendshipId`, `Email1`, `Email2`, `IsActive`, `CreatedAt`, `FriendEmail`, `FirstName`, `LastName`.

### Static facades
- `GetFriendRequestsByUser`, `CreateFriendRequest`, `UpdateFriendRequestStatus`.
- `GetByUser`, `GetForUser(connectionId, email)` (auth check), `Create`, `Deactivate`.
- `ExistsBetween(email1, email2)` — used by Tasks/Courses to validate sharing relationships.

### `GetSafeZones(email1, email2)` — the collaboration algorithm
1. Build each user's **30-minute availability grid** for the next 7–14 days:
   - Start with all slots from `DayStartHour` to `DayEndHour`.
   - Remove existing events (classes, work, personal blocks, scheduled task slots).
   - Remove sleep window.
2. **Intersect** the two grids — only keep slots free for both.
3. For each surviving slot, **check stress at that time**:
   - Compute each user's stress score for that day.
   - Filter to slots where both have stress < 60%.
4. Return list of slots with `from`, `to`, `bothFree`, `stress1`, `stress2`.

This is what the unused-but-implemented `GET /api/collaboration/safe-zones` endpoint returns.

## 12. `SharedTask.cs` (112 lines)
Lifecycle of a shared task.

### Static facades
- `GetByUser(email)` → all shared tasks the user is a member of.
- `GetByTaskId(taskId)` — single shared task with all members.
- `Exists(taskId)`.
- `Create(taskId, createdByEmail, status)`.
- `UpdateStatus(taskId, status)` — `Pending` / `Confirmed` / `Cancelled`.
- `CreateMember(taskId, email, responseStatus, respondedAt)`.
- `UpdateMemberStatus(taskId, email, status)` — returns bool (was-pending check).
- `AllMembersAccepted(taskId)`.
- `GetMemberEmails(taskId)`.
- `CleanupPartnerCopies(taskId)` — deletes mirror tasks on cancel.

## 13. `UserCourse.cs` (36 lines)
Junction row between `User` and `Course` with collaboration metadata.

### Properties
`Email`, `CourseId`, `StudyPartnerEmail`, `SharedByDefault`, `CourseShareApproved`.

### Static facades
- `Get(email, courseId)`.
- `SetCourseShareApproved(email, courseId)` — used by the bulk approval flow.
- `GetPendingMembersForCourse(email, courseId)` — pending shared tasks awaiting this user's response, scoped to a course.

## Model patterns to remember

1. **Static facades over instance methods.** All persistence is `Course.GetById(id)` not `course.Save()`. The class is both the POCO and the facade.
2. **Each facade method is one-liner over `_dal.X(...)`.** Logic that uses multiple DAL calls (like `GetSafeZones`) is the exception, not the rule.
3. **The "interesting" logic is concentrated in 3 files**: `User.cs` (stress), `StudentTask.cs` (scheduling engine), `Notification.cs` (generation rules). Everything else is a thin facade.
4. **Models hold no state.** No instance methods that mutate. The facade pattern is functional in spirit: `Course.Update(id, ...)` → SQL → fresh `Course.GetById(id)` to re-read.
5. **No constructors with parameters.** All models initialize with `{ }` initializers from `Map*` helpers in `DBservices.cs`.

---

## End-to-end recap

A single user action — say "click Complete on a Tasks page card" — flows through:

1. **Frontend**: `Tasks.html` → `ConfirmComplete` → `ajaxCall("POST", CompleteApi(id), ...)` → `appShell.js` adds JWT header → server.
2. **Routing**: `Program.cs` middleware → `[Authorize]` validates JWT signature + expiry → `[Route("api/tasks")]` matches → `TasksController.Complete(id, dto)`.
3. **Controller**: `GetEmail()` from claim → `StudentTask.GetById(id)` for auth check → `StudentTask.Complete(id, ...)` → checks subtask cascade → `StudentTask.ScheduleAll(email)` → reads ML data → returns inline stats.
4. **Models**: `StudentTask.Complete` calls `_dal.CompleteTask(id, ...)`; `ScheduleAll` orchestrates: `_dal.GetAllIncompleteTasks` + scoring + `_dal.CreateTaskEvent` per slot.
5. **DAL**: Each call opens connection → executes stored procedure → maps reader → closes connection.
6. **Database**: SQL Server runs the SP.
7. **Response**: JSON travels back → frontend `ConfirmComplete` callback shows ML stats alert + reloads task list.

The 6 layers each have one job and don't reach across. That's the whole architecture.
