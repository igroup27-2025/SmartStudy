# SmartStudy Testing Document

## Testing Strategy

### 1. API Endpoint Testing
Each API endpoint is tested with curl commands verifying:
- **Happy path**: Correct request returns expected response (200/201)
- **Validation**: Invalid input returns 400 with meaningful error
- **Authentication**: Unauthenticated requests return 401
- **Not found**: Missing resources return 404
- **Data integrity**: Responses match expected schema

### 2. Frontend Testing
Manual testing checklist for each page:
- Page loads without JavaScript errors
- Layout renders correctly (sidebar, topbar, content)
- Data displays correctly from API
- Forms submit and validate properly
- Modals open/close correctly
- Navigation between pages works
- Responsive design at mobile breakpoints

### 3. Integration Testing
End-to-end flows tested:
- Register -> Login -> Dashboard
- Add Course -> Add Task -> Complete Task -> Verify stress update
- Add Exam -> View on Calendar -> View in Analytics
- CRUD operations on all entities

---

## Test Best Practices Followed

### Backend
1. **Input validation**: All DTOs validate required fields
2. **Error handling**: Controllers return appropriate HTTP status codes
3. **Data isolation**: Each user only sees their own data
4. **Seed data**: Comprehensive mock data for realistic testing
5. **CORS**: Properly configured for frontend origin

### Frontend
1. **Graceful degradation**: Show loading states while fetching
2. **Error display**: Show user-friendly error messages
3. **Form validation**: Client-side validation before API calls
4. **Token management**: Secure storage and automatic inclusion in requests
5. **State consistency**: UI updates after CRUD operations

---

## API Test Cases

### Auth Endpoints
| Test | Method | URL | Expected |
|------|--------|-----|----------|
| Register new user | POST | /api/auth/register | 200 + token |
| Register duplicate email | POST | /api/auth/register | 400 |
| Login valid credentials | POST | /api/auth/login | 200 + token |
| Login wrong password | POST | /api/auth/login | 401 |
| Login nonexistent user | POST | /api/auth/login | 401 |

### Courses Endpoints
| Test | Method | URL | Expected |
|------|--------|-----|----------|
| Get all courses | GET | /api/courses | 200 + array |
| Get single course | GET | /api/courses/{id} | 200 + object |
| Create course | POST | /api/courses | 201 + object |
| Update course | PUT | /api/courses/{id} | 200 + object |
| Delete course | DELETE | /api/courses/{id} | 204 |
| Get nonexistent | GET | /api/courses/999 | 404 |

### Tasks Endpoints
| Test | Method | URL | Expected |
|------|--------|-----|----------|
| Get all tasks | GET | /api/tasks | 200 + array |
| Create task | POST | /api/tasks | 201 + object |
| Update task | PUT | /api/tasks/{id} | 200 + object |
| Delete task | DELETE | /api/tasks/{id} | 204 |
| Complete task | POST | /api/tasks/{id}/complete | 200 |
| Get nonexistent | GET | /api/tasks/999 | 404 |

### Exams Endpoints
| Test | Method | URL | Expected |
|------|--------|-----|----------|
| Get all exams | GET | /api/exams | 200 + array |
| Create exam | POST | /api/exams | 201 + object |
| Update exam | PUT | /api/exams/{id} | 200 + object |
| Delete exam | DELETE | /api/exams/{id} | 204 |

### Events Endpoints
| Test | Method | URL | Expected |
|------|--------|-----|----------|
| Get all events | GET | /api/events | 200 + array |
| Create class event | POST | /api/events/class | 201 |
| Create task event | POST | /api/events/task | 201 |
| Create work event | POST | /api/events/work | 201 |
| Create personal event | POST | /api/events/personal | 201 |
| Delete event | DELETE | /api/events/{id} | 204 |

### Stress Endpoints
| Test | Method | URL | Expected |
|------|--------|-----|----------|
| Get stress score | GET | /api/stress/score | 200 + score |
| Get weekly stress | GET | /api/stress/weekly | 200 + array |

### Dashboard Endpoint
| Test | Method | URL | Expected |
|------|--------|-----|----------|
| Get dashboard data | GET | /api/dashboard | 200 + object |

---

## Frontend Test Cases

### Login Page
- [x] Login form displays correctly
- [x] Valid credentials redirect to Dashboard
- [ ] Invalid credentials show error message
- [ ] Registration form works
- [x] Token is stored after login

### Dashboard
- [x] Stress meter displays with correct color (red ring, score 100)
- [x] Upcoming deadlines list shows correctly (5 upcoming tasks)
- [x] Task summary counts are accurate (5/13 done, 8 pending, 3 exams)
- [x] Today's schedule shows (3 events)
- [x] Navigation to other pages works

### Tasks Page
- [x] Task list loads from API (13 tasks)
- [x] Add task modal opens and submits (created "Test Task - Integration Check")
- [ ] Edit task works
- [x] Delete task works (deleted test task)
- [x] Complete task updates UI and stress (toggled "Exercise 1" checkbox)
- [x] Filter by status works (filtered to "Completed" showing 5 tasks)
- [x] Filter by course works
- [x] Filter by priority works

### Courses Page
- [x] Course cards display correctly (5 cards in grid, color-coded)
- [ ] Add course modal works
- [ ] Edit course works
- [ ] Delete course works
- [x] Course details show instructor, credits, task/exam counts

### Exams Page
- [x] Exam list loads correctly (6 exams)
- [ ] Add exam modal works
- [ ] Edit exam works
- [ ] Delete exam works
- [x] Days until exam shows correctly (11d to 39d)

### Calendar Page
- [x] Weekly view renders correctly (Mon-Sun with time grid)
- [x] Events display in correct time slots
- [x] Event colors match type (cyan=class, orange=personal, purple=work, yellow=personal)
- [x] Week navigation works (prev/next/today buttons)
- [ ] Click event shows details

### Analytics Page
- [x] Stress score displays (100/High with red ring)
- [x] Weekly stress chart renders (bar chart with daily percentages)
- [x] Workload by course breakdown shows (5 courses with progress bars)
- [x] Task completion stats display (13 total, 5 completed, 8 pending, 0 overdue)

### Settings Page
- [x] User profile info displays (email, first/last name)
- [x] Notification toggles work (4 toggle switches)
- [ ] Save changes persists

### Onboarding Flow
- [x] Step 1 (Welcome) renders centered with features list
- [x] Step 2 (Courses) navigation works
- [x] Step 3 (Tasks) navigation works
- [x] Step 4 (Ready) "Go to Dashboard" redirects correctly

### Logout
- [x] Logout button on Settings page clears session and redirects to Login

---

## Running Tests

### Start the backend:
```bash
cd "Server/SmartStudy"
dotnet run
```

### Test API with curl:
```bash
# Login
curl -X POST http://localhost:5071/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"demo@smartstudy.com","password":"Demo123"}'

# Get courses (with token)
curl http://localhost:5071/api/courses \
  -H "Authorization: Bearer <token>"

# Get stress score
curl http://localhost:5071/api/stress/score \
  -H "Authorization: Bearer <token>"
```

### Test frontend:
Open http://localhost:5071/Pages/Login.html in browser.
Login with demo@smartstudy.com / Demo123

---

## Known Test Data (Seed)
- **Demo User**: demo@smartstudy.com / Demo123
- **5 courses**: CS Intro, Data Structures, Linear Algebra, Probability, Web Dev
- **15 tasks**: Mix of completed and pending with various due dates
- **6 exams**: Spread across Feb-Mar 2026
- **20+ events**: Classes, work shifts, personal, task study sessions
