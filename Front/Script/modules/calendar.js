import { api } from './api.js';
import { openModal, closeModal, showToast } from './modals.js';

let currentView = window.innerWidth <= 768 ? '3day' : 'weekly'; // 'monthly' | 'weekly' | '3day'
let currentDate = new Date();
let cachedEvents = [];

const EVENT_COLORS = {
    class: { bg: '#E0F7FA', border: '#00BCD4', text: '#006064' },
    task: { bg: '#FFF3E0', border: '#F28D35', text: '#E65100' },
    work: { bg: '#F3E5F5', border: '#9B76FF', text: '#4A148C' },
    personal: { bg: '#FFF8E1', border: '#F2C777', text: '#F57F17' },
};

export async function initCalendar() {
    // Support deep-linking via ?date=YYYY-MM-DD
    const params = new URLSearchParams(window.location.search);
    const dateParam = params.get('date');
    if (dateParam) {
        const parsed = new Date(dateParam + 'T00:00:00');
        if (!isNaN(parsed)) currentDate = parsed;
    }

    setupViewToggle();
    setupNavigation();
    setupEventCreation();
    await navigate();

    // Auto-open event creation modal if ?add=1
    if (params.get('add') === '1') {
        openEventModal(dateParam || null);
    }
}

/* ---- View Toggle ---- */
function setupViewToggle() {
    // Set active button to match currentView (may differ on mobile)
    document.querySelectorAll('.cal-view-btn').forEach(btn => {
        btn.classList.toggle('active', btn.dataset.view === currentView);
        btn.addEventListener('click', () => {
            document.querySelectorAll('.cal-view-btn').forEach(b => b.classList.remove('active'));
            btn.classList.add('active');
            currentView = btn.dataset.view;
            navigate();
        });
    });
}

/* ---- Navigation ---- */
function setupNavigation() {
    document.getElementById('calPrev')?.addEventListener('click', () => {
        shiftDate(-1);
        navigate();
    });
    document.getElementById('calNext')?.addEventListener('click', () => {
        shiftDate(1);
        navigate();
    });
    document.getElementById('calToday')?.addEventListener('click', () => {
        currentDate = new Date();
        navigate();
    });

    // Add event button
    document.getElementById('calAddEvent')?.addEventListener('click', () => {
        openEventModal(null);
    });

    // Add constraint button
    document.getElementById('calAddConstraint')?.addEventListener('click', () => {
        openConstraintModal();
    });

    // Import schedule button
    const importBtn = document.getElementById('calImportSchedule');
    const fileInput = document.getElementById('scheduleFileInput');
    importBtn?.addEventListener('click', () => fileInput?.click());
    fileInput?.addEventListener('change', async () => {
        const file = fileInput.files[0];
        if (!file) return;
        try {
            importBtn.disabled = true;
            importBtn.textContent = 'Importing...';
            const result = await api.importSchedule(file);
            const msg = `Imported ${result.eventsCreated} event(s) from ${result.courses.length} course(s)` +
                (result.entriesSkipped ? ` (${result.entriesSkipped} skipped)` : '');
            showToast(msg);
            await navigate();
        } catch (err) {
            showToast(err.message || 'Failed to import schedule', 'error');
        } finally {
            importBtn.disabled = false;
            importBtn.textContent = 'Import Schedule';
            fileInput.value = '';
        }
    });
}

function shiftDate(direction) {
    if (currentView === 'monthly') {
        currentDate.setMonth(currentDate.getMonth() + direction);
    } else if (currentView === 'weekly') {
        currentDate.setDate(currentDate.getDate() + 7 * direction);
    } else {
        currentDate.setDate(currentDate.getDate() + 3 * direction);
    }
}

/* ---- Date Range Calculation ---- */
function getDateRange() {
    if (currentView === 'monthly') {
        const first = new Date(currentDate.getFullYear(), currentDate.getMonth(), 1);
        const last = new Date(currentDate.getFullYear(), currentDate.getMonth() + 1, 0);
        // Extend to fill grid rows — start from Monday
        const gridStart = getMonday(first);
        const gridEnd = new Date(last);
        // Extend to Sunday
        const dayOfWeek = gridEnd.getDay();
        const daysToSunday = dayOfWeek === 0 ? 0 : 7 - dayOfWeek;
        gridEnd.setDate(gridEnd.getDate() + daysToSunday);
        gridEnd.setHours(23, 59, 59, 999);
        return { from: gridStart, to: gridEnd };
    } else if (currentView === 'weekly') {
        const from = getMonday(currentDate);
        const to = new Date(from);
        to.setDate(to.getDate() + 7);
        return { from, to };
    } else {
        // 3-day: start from currentDate
        const from = new Date(currentDate);
        from.setHours(0, 0, 0, 0);
        const to = new Date(from);
        to.setDate(to.getDate() + 3);
        return { from, to };
    }
}

/* ---- Main Render Orchestrator ---- */
async function navigate() {
    renderHeader();
    const { from, to } = getDateRange();

    try {
        cachedEvents = await api.getEvents(from, to);

        if (currentView === 'monthly') {
            renderMonthlyGrid(cachedEvents, from, to);
        } else if (currentView === 'weekly') {
            renderTimeGrid(cachedEvents, from, 7);
        } else {
            renderTimeGrid(cachedEvents, from, 3);
        }
    } catch (err) {
        showToast('Failed to load events', 'error');
    }
}

/* ---- Header ---- */
function renderHeader() {
    const el = document.getElementById('calendarHeader');
    if (!el) return;

    if (currentView === 'monthly') {
        el.textContent = currentDate.toLocaleDateString('en', { month: 'long', year: 'numeric' });
    } else {
        const { from, to } = getDateRange();
        const end = new Date(to);
        end.setDate(end.getDate() - 1); // to is exclusive
        const fmt = (d) => d.toLocaleDateString('en', { month: 'short', day: 'numeric' });
        el.textContent = `${fmt(from)} - ${fmt(end)}, ${end.getFullYear()}`;
    }
}

/* ---- Weekly / 3-Day Time Grid ---- */
function renderTimeGrid(events, startDate, dayCount) {
    const grid = document.getElementById('calendarGrid');
    if (!grid) return;

    grid.className = `calendar-grid${dayCount === 3 ? ' cal-grid--3day' : ''}`;

    const days = [];
    for (let i = 0; i < dayCount; i++) {
        const d = new Date(startDate);
        d.setDate(d.getDate() + i);
        days.push(d);
    }

    const today = new Date();
    today.setHours(0, 0, 0, 0);

    const dayNames = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];

    // Time column
    let html = '<div class="cal-time-col"><div class="cal-day-header"></div>';
    for (let h = 7; h <= 22; h++) {
        html += `<div class="cal-time-label">${h.toString().padStart(2, '0')}:00</div>`;
    }
    html += '</div>';

    days.forEach((day) => {
        const isToday = day.getTime() === today.getTime();
        const dateStr = `${day.getFullYear()}-${String(day.getMonth() + 1).padStart(2, '0')}-${String(day.getDate()).padStart(2, '0')}`;

        // Calculate daily workload for overload indicator
        const dayEvents = events.filter(e => {
            const eDate = new Date(e.from);
            return eDate.getDate() === day.getDate() &&
                   eDate.getMonth() === day.getMonth() &&
                   eDate.getFullYear() === day.getFullYear();
        });

        const taskHours = dayEvents
            .filter(e => e.eventType === 'task')
            .reduce((sum, e) => sum + (new Date(e.to) - new Date(e.from)) / 3600000, 0);
        const isOverloaded = taskHours > 8;

        html += `<div class="cal-day-col ${isToday ? 'today' : ''} ${isOverloaded ? 'cal-day-col--overloaded' : ''}">`;
        html += `<div class="cal-day-header ${isToday ? 'today' : ''}" data-date="${dateStr}">
            <span class="cal-day-name">${dayNames[day.getDay()]}</span>
            <span class="cal-day-num">${day.getDate()}</span>
            ${isOverloaded ? '<span class="cal-overload-badge">!</span>' : ''}
        </div>`;
        html += '<div class="cal-day-body">';

        // Hour cells (clickable for event creation)
        for (let h = 7; h <= 22; h++) {
            html += `<div class="cal-cell" data-date="${dateStr}" data-hour="${h}"></div>`;
        }

        // Events for this day
        dayEvents.forEach(e => {
            const from = new Date(e.from);
            const to = new Date(e.to);
            const startHour = from.getHours() + from.getMinutes() / 60;
            const endHour = to.getHours() + to.getMinutes() / 60;
            const top = (startHour - 7) * 50;
            const height = Math.max(25, (endHour - startHour) * 50);
            const colors = EVENT_COLORS[e.eventType] || EVENT_COLORS.personal;
            const label = e.courseName || e.taskTitle || e.workPlace || e.description || e.type || 'Event';
            const isAutoScheduled = e.eventType === 'task' && e.status === 'Scheduled';

            html += `<div class="cal-event ${!isAutoScheduled ? 'cal-event--draggable' : ''}"
                data-event-id="${e.eventId}" data-event-type="${e.eventType}"
                ${!isAutoScheduled ? 'draggable="true"' : ''}
                style="top:${top}px;height:${height}px;background:${colors.bg};border-left:3px solid ${colors.border};color:${colors.text}">
                <div class="cal-event-title">${label}</div>
                <div class="cal-event-time">${formatTime(from)} - ${formatTime(to)}</div>
            </div>`;
        });

        // Current time line
        if (isToday) {
            const now = new Date();
            const nowHour = now.getHours() + now.getMinutes() / 60;
            if (nowHour >= 7 && nowHour <= 23) {
                const lineTop = (nowHour - 7) * 50;
                html += `<div class="cal-now-line" style="top:${lineTop}px"></div>`;
            }
        }

        // Overload indicator bar at bottom
        if (isOverloaded) {
            html += `<div class="cal-overload-bar">Overloaded: ${taskHours.toFixed(1)}h scheduled</div>`;
        }

        html += '</div></div>';
    });

    grid.innerHTML = html;

    // Click on empty cell to create event
    grid.querySelectorAll('.cal-cell').forEach(cell => {
        cell.addEventListener('click', () => {
            const date = cell.dataset.date;
            const hour = cell.dataset.hour;
            openEventModal(date, parseInt(hour));
        });
    });

    // Click on event for details popup
    grid.querySelectorAll('.cal-event').forEach(el => {
        el.addEventListener('click', (e) => {
            e.stopPropagation();
            const eventId = parseInt(el.dataset.eventId);
            showEventDetails(eventId, el);
        });
    });

    // Setup drag-and-drop for moveable events
    setupDragAndDrop(grid);

    // Setup drag-to-create (mousedown on cells)
    setupDragToCreate(grid);
}

/* ---- Drag-and-Drop (move events) ---- */
function setupDragAndDrop(grid) {
    grid.querySelectorAll('.cal-event--draggable').forEach(el => {
        el.addEventListener('dragstart', (e) => {
            e.dataTransfer.setData('text/plain', el.dataset.eventId);
            e.dataTransfer.effectAllowed = 'move';
            el.classList.add('dragging');
        });
        el.addEventListener('dragend', () => {
            el.classList.remove('dragging');
            grid.querySelectorAll('.cal-cell').forEach(c => c.classList.remove('drag-over'));
        });
    });

    grid.querySelectorAll('.cal-cell').forEach(cell => {
        cell.addEventListener('dragover', (e) => {
            e.preventDefault();
            e.dataTransfer.dropEffect = 'move';
            cell.classList.add('drag-over');
        });
        cell.addEventListener('dragleave', () => {
            cell.classList.remove('drag-over');
        });
        cell.addEventListener('drop', async (e) => {
            e.preventDefault();
            cell.classList.remove('drag-over');
            const eventId = parseInt(e.dataTransfer.getData('text/plain'));
            const event = cachedEvents.find(ev => ev.eventId === eventId);
            if (!event) return;

            const newDate = cell.dataset.date;
            const newHour = parseInt(cell.dataset.hour);
            const oldFrom = new Date(event.from);
            const oldTo = new Date(event.to);
            const duration = oldTo - oldFrom;

            const newFrom = new Date(`${newDate}T${String(newHour).padStart(2, '0')}:00`);
            const newTo = new Date(newFrom.getTime() + duration);

            try {
                const data = {
                    from: newFrom.toISOString(),
                    to: newTo.toISOString(),
                    recurring: event.recurring,
                };

                if (event.eventType === 'class') {
                    data.courseId = event.courseId;
                    data.location = event.location;
                    data.duration = duration / 3600000;
                    await api.updateClassEvent(eventId, data);
                } else if (event.eventType === 'work') {
                    data.workPlace = event.workPlace;
                    await api.updateWorkEvent(eventId, data);
                } else if (event.eventType === 'personal') {
                    data.type = event.type;
                    data.description = event.description;
                    await api.updatePersonalEvent(eventId, data);
                }

                showToast('Event moved');
                await navigate();
            } catch {
                showToast('Failed to move event', 'error');
            }
        });
    });
}

/* ---- Drag-to-Create (select time range) ---- */
let dragCreateState = null;

function setupDragToCreate(grid) {
    grid.querySelectorAll('.cal-cell').forEach(cell => {
        cell.addEventListener('mousedown', (e) => {
            if (e.button !== 0) return;
            // Don't start drag-to-create if clicking on an event
            if (e.target.closest('.cal-event')) return;

            dragCreateState = {
                startDate: cell.dataset.date,
                startHour: parseInt(cell.dataset.hour),
                endHour: parseInt(cell.dataset.hour),
            };
            cell.classList.add('cal-cell-selecting');
        });

        cell.addEventListener('mouseenter', () => {
            if (!dragCreateState) return;
            if (cell.dataset.date !== dragCreateState.startDate) return;
            const hour = parseInt(cell.dataset.hour);
            dragCreateState.endHour = hour;

            // Highlight range
            grid.querySelectorAll('.cal-cell').forEach(c => {
                if (c.dataset.date !== dragCreateState.startDate) return;
                const h = parseInt(c.dataset.hour);
                const minH = Math.min(dragCreateState.startHour, dragCreateState.endHour);
                const maxH = Math.max(dragCreateState.startHour, dragCreateState.endHour);
                c.classList.toggle('cal-cell-selecting', h >= minH && h <= maxH);
            });
        });
    });

    document.addEventListener('mouseup', () => {
        if (!dragCreateState) return;

        const { startDate, startHour, endHour } = dragCreateState;
        const minH = Math.min(startHour, endHour);
        const maxH = Math.max(startHour, endHour) + 1;

        // Clear selection highlight
        grid.querySelectorAll('.cal-cell-selecting').forEach(c => c.classList.remove('cal-cell-selecting'));
        dragCreateState = null;

        // Only trigger if range is > 0 cells
        if (maxH > minH + 1 || maxH === minH + 1) {
            openEventModal(startDate, minH, maxH);
        }
    });
}

/* ---- Monthly Grid ---- */
function renderMonthlyGrid(events, gridStart, gridEnd) {
    const grid = document.getElementById('calendarGrid');
    if (!grid) return;

    grid.className = 'calendar-grid cal-grid--month';

    const today = new Date();
    today.setHours(0, 0, 0, 0);
    const thisMonth = currentDate.getMonth();

    const dayNames = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'];

    let html = '<div class="cal-month">';

    // Header row
    html += '<div class="cal-month-header">';
    dayNames.forEach(d => { html += `<span>${d}</span>`; });
    html += '</div>';

    // Build weeks
    const cursor = new Date(gridStart);
    while (cursor <= gridEnd) {
        html += '<div class="cal-month-week">';

        for (let i = 0; i < 7; i++) {
            const day = new Date(cursor);
            const isOutside = day.getMonth() !== thisMonth;
            const isToday = day.getTime() === today.getTime();
            const dateStr = `${day.getFullYear()}-${String(day.getMonth() + 1).padStart(2, '0')}-${String(day.getDate()).padStart(2, '0')}`;

            const dayEvents = events.filter(e => {
                const eDate = new Date(e.from);
                return eDate.getDate() === day.getDate() &&
                       eDate.getMonth() === day.getMonth() &&
                       eDate.getFullYear() === day.getFullYear();
            });

            // Calculate workload for color coding
            const taskHours = dayEvents
                .filter(e => e.eventType === 'task')
                .reduce((sum, e) => sum + (new Date(e.to) - new Date(e.from)) / 3600000, 0);

            const classes = ['cal-month-day'];
            if (isOutside) classes.push('outside');
            if (isToday) classes.push('today');
            if (taskHours > 8) classes.push('cal-month-day--overloaded');
            else if (taskHours > 5) classes.push('cal-month-day--heavy');
            else if (taskHours > 2) classes.push('cal-month-day--moderate');

            html += `<div class="${classes.join(' ')}" data-date="${dateStr}">`;
            html += `<div class="cal-month-day__num">${day.getDate()}</div>`;

            // Show up to 3 event labels
            const maxShow = 3;
            dayEvents.slice(0, maxShow).forEach(e => {
                const colors = EVENT_COLORS[e.eventType] || EVENT_COLORS.personal;
                const label = e.courseName || e.taskTitle || e.workPlace || e.description || e.type || 'Event';
                html += `<div class="cal-month-event" style="background:${colors.bg};border-left-color:${colors.border};color:${colors.text}">${label}</div>`;
            });

            if (dayEvents.length > maxShow) {
                html += `<div class="cal-month-more">+${dayEvents.length - maxShow} more</div>`;
            }

            // Mobile dots (hidden on desktop via CSS)
            if (dayEvents.length > 0) {
                const types = [...new Set(dayEvents.map(e => e.eventType))];
                html += '<div class="cal-month-dots">';
                types.slice(0, 3).forEach(t => {
                    const colors = EVENT_COLORS[t] || EVENT_COLORS.personal;
                    html += `<span class="cal-month-dot" style="background:${colors.border}"></span>`;
                });
                html += '</div>';
            }

            html += '</div>';
            cursor.setDate(cursor.getDate() + 1);
        }

        html += '</div>';
    }

    html += '</div>';
    grid.innerHTML = html;

    // Day click → switch to weekly view at that date
    grid.querySelectorAll('.cal-month-day').forEach(cell => {
        cell.addEventListener('click', () => {
            const dateStr = cell.dataset.date;
            if (dateStr) {
                currentDate = new Date(dateStr + 'T00:00:00');
                currentView = 'weekly';
                document.querySelectorAll('.cal-view-btn').forEach(b => b.classList.remove('active'));
                document.querySelector('.cal-view-btn[data-view="weekly"]')?.classList.add('active');
                navigate();
            }
        });
    });
}

/* ---- Event Creation / Editing ---- */
let editingEventId = null;
let editingEventType = null;
let conflictsConfirmed = false;

function setupEventCreation() {
    const form = document.getElementById('eventForm');
    if (!form) return;

    // Type switcher
    const typeSelect = document.getElementById('eventTypeSelect');
    typeSelect?.addEventListener('change', () => {
        updateEventFormFields(typeSelect.value);
    });

    form.addEventListener('submit', async (e) => {
        e.preventDefault();

        const type = document.getElementById('eventTypeSelect').value;
        const fromDate = document.getElementById('eventFromDate').value;
        const fromTime = document.getElementById('eventFromTime').value;
        const toDate = document.getElementById('eventToDate').value;
        const toTime = document.getElementById('eventToTime').value;
        const recurring = document.getElementById('eventRecurring')?.checked || false;

        if (!fromDate || !fromTime || !toDate || !toTime) {
            showToast('Please fill in date and time fields', 'error');
            return;
        }

        const from = new Date(`${fromDate}T${fromTime}`);
        const to = new Date(`${toDate}T${toTime}`);

        if (to <= from) {
            showToast('End time must be after start time', 'error');
            return;
        }

        // Check for conflicts before creating (if not already confirmed)
        if (!conflictsConfirmed && !editingEventId) {
            try {
                const conflicts = await api.checkConflicts({
                    from: from.toISOString(),
                    to: to.toISOString(),
                });
                if (conflicts && conflicts.length > 0) {
                    showConflictWarning(conflicts);
                    return;
                }
            } catch { /* proceed if check fails */ }
        }
        conflictsConfirmed = false;

        try {
            const isEditing = editingEventId !== null;

            if (type === 'class') {
                const data = {
                    from: from.toISOString(),
                    to: to.toISOString(),
                    recurring,
                    courseId: parseInt(document.getElementById('eventCourseId').value),
                    location: document.getElementById('eventLocation')?.value || null,
                    duration: (to - from) / 3600000
                };
                if (isEditing) await api.updateClassEvent(editingEventId, data);
                else await api.createClassEvent(data);
            } else if (type === 'work') {
                const data = {
                    from: from.toISOString(),
                    to: to.toISOString(),
                    recurring,
                    workPlace: document.getElementById('eventWorkPlace')?.value || null,
                    travelTime: parseInt(document.getElementById('eventTravelTime')?.value) || null
                };
                if (isEditing) await api.updateWorkEvent(editingEventId, data);
                else await api.createWorkEvent(data);
            } else if (type === 'task') {
                const activeSource = document.querySelector('.task-source-btn.active')?.dataset.source || 'existing';
                let taskId;

                if (activeSource === 'existing') {
                    taskId = parseInt(document.getElementById('eventTaskId').value);
                    if (!taskId) {
                        showToast('Please select a task', 'error');
                        return;
                    }
                } else {
                    // Create a new task first
                    const title = document.getElementById('eventTaskTitle').value?.trim();
                    const courseId = parseInt(document.getElementById('eventTaskCourseId').value);
                    if (!title || !courseId) {
                        showToast('Task title and course are required', 'error');
                        return;
                    }
                    const newTask = await api.createTask({
                        title,
                        courseId,
                        taskType: document.getElementById('eventTaskType')?.value || 'Other',
                        dueDate: document.getElementById('eventTaskDueDate')?.value || null,
                        estimatedHours: parseFloat(document.getElementById('eventTaskHours')?.value) || null,
                        priority: document.getElementById('eventTaskPriority')?.value || null
                    });
                    taskId = newTask.taskId;
                }

                const data = {
                    from: from.toISOString(),
                    to: to.toISOString(),
                    recurring,
                    taskId,
                    priority: null,
                    status: 'Scheduled'
                };
                await api.createTaskEvent(data);
            } else {
                const data = {
                    from: from.toISOString(),
                    to: to.toISOString(),
                    recurring,
                    type: document.getElementById('eventPersonalType')?.value || null,
                    description: document.getElementById('eventDescription')?.value || null
                };
                if (isEditing) await api.updatePersonalEvent(editingEventId, data);
                else await api.createPersonalEvent(data);
            }

            showToast(isEditing ? 'Event updated' : 'Event created');
            editingEventId = null;
            editingEventType = null;
            closeModal('eventModal');
            await navigate();
        } catch (err) {
            showToast(err.message || 'Failed to save event', 'error');
        }
    });
}

/* ---- Conflict Warning ---- */
function showConflictWarning(conflicts) {
    const el = document.getElementById('conflictWarning');
    if (!el) return;

    const list = conflicts.map(c => {
        const from = new Date(c.from);
        const to = new Date(c.to);
        const label = c.courseName || c.taskTitle || c.workPlace || c.description || c.type || 'Event';
        return `<li>${label} (${formatTime(from)} - ${formatTime(to)})</li>`;
    }).join('');

    el.innerHTML = `
        <div class="conflict-warning__icon">&#9888;</div>
        <div class="conflict-warning__body">
            <strong>Time conflict detected!</strong>
            <ul>${list}</ul>
            <div class="conflict-warning__actions">
                <button type="button" class="btn btn-sm btn-primary" id="conflictCreateAnyway">Create Anyway</button>
                <button type="button" class="btn btn-sm btn-secondary" id="conflictCancel">Cancel</button>
            </div>
        </div>
    `;
    el.style.display = 'flex';

    document.getElementById('conflictCreateAnyway')?.addEventListener('click', () => {
        conflictsConfirmed = true;
        el.style.display = 'none';
        // Re-trigger form submit
        document.getElementById('eventForm')?.requestSubmit();
    });

    document.getElementById('conflictCancel')?.addEventListener('click', () => {
        el.style.display = 'none';
    });
}

/* ---- Constraint Modal ---- */
function openConstraintModal() {
    const form = document.getElementById('eventForm');
    if (!form) return;
    form.reset();

    editingEventId = null;
    editingEventType = null;

    const title = document.querySelector('#eventModal .modal-header h3');
    const submitBtn = document.getElementById('eventSubmitBtn');
    if (title) title.textContent = 'Add Fixed Constraint';
    if (submitBtn) submitBtn.textContent = 'Create Constraint';

    // Pre-set type to work and recurring
    const typeSelect = document.getElementById('eventTypeSelect');
    if (typeSelect) {
        typeSelect.value = 'work';
        typeSelect.disabled = false;
    }
    updateEventFormFields('work');
    document.getElementById('eventRecurring').checked = true;

    // Hide conflict warning
    const cw = document.getElementById('conflictWarning');
    if (cw) cw.style.display = 'none';

    openModal('eventModal');
}

function openEventModal(dateStr, hour, endHour) {
    const form = document.getElementById('eventForm');
    if (!form) return;
    form.reset();

    // Reset editing state
    editingEventId = null;
    editingEventType = null;
    conflictsConfirmed = false;

    // Update modal title and button
    const title = document.querySelector('#eventModal .modal-header h3');
    const submitBtn = document.getElementById('eventSubmitBtn');
    if (title) title.textContent = 'Add Event';
    if (submitBtn) submitBtn.textContent = 'Create Event';

    // Hide conflict warning
    const cw = document.getElementById('conflictWarning');
    if (cw) cw.style.display = 'none';

    // Enable type selector for new events
    const typeSelect = document.getElementById('eventTypeSelect');
    if (typeSelect) typeSelect.disabled = false;

    // Pre-fill date/time if provided
    if (dateStr) {
        document.getElementById('eventFromDate').value = dateStr;
        document.getElementById('eventToDate').value = dateStr;
    }
    if (hour !== undefined) {
        document.getElementById('eventFromTime').value = `${String(hour).padStart(2, '0')}:00`;
        const end = endHour !== undefined ? endHour : hour + 1;
        document.getElementById('eventToTime').value = `${String(end).padStart(2, '0')}:00`;
    }

    updateEventFormFields('personal');
    document.getElementById('eventTypeSelect').value = 'personal';

    // Reset task source toggle to "existing"
    document.querySelectorAll('.task-source-btn').forEach(b => b.classList.remove('active'));
    document.querySelector('.task-source-btn[data-source="existing"]')?.classList.add('active');
    document.getElementById('taskExistingFields')?.classList.remove('hidden');
    document.getElementById('taskNewFields')?.classList.add('hidden');

    // Populate course select
    populateEventCourses();

    openModal('eventModal');
}

function openEventModalForEdit(event) {
    const form = document.getElementById('eventForm');
    if (!form) return;
    form.reset();

    editingEventId = event.eventId;
    editingEventType = event.eventType;
    conflictsConfirmed = false;

    // Update modal title and button
    const title = document.querySelector('#eventModal .modal-header h3');
    const submitBtn = document.getElementById('eventSubmitBtn');
    if (title) title.textContent = 'Edit Event';
    if (submitBtn) submitBtn.textContent = 'Save Changes';

    // Hide conflict warning
    const cw = document.getElementById('conflictWarning');
    if (cw) cw.style.display = 'none';

    // Set type (disable changing type on edit)
    const typeSelect = document.getElementById('eventTypeSelect');
    if (typeSelect) {
        typeSelect.value = event.eventType;
        typeSelect.disabled = true;
    }
    updateEventFormFields(event.eventType);

    // Fill date/time
    const from = new Date(event.from);
    const to = new Date(event.to);
    const pad = (n) => String(n).padStart(2, '0');
    const dateStr = (d) => `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
    const timeStr = (d) => `${pad(d.getHours())}:${pad(d.getMinutes())}`;

    document.getElementById('eventFromDate').value = dateStr(from);
    document.getElementById('eventFromTime').value = timeStr(from);
    document.getElementById('eventToDate').value = dateStr(to);
    document.getElementById('eventToTime').value = timeStr(to);
    document.getElementById('eventRecurring').checked = event.recurring;

    // Fill type-specific fields
    if (event.eventType === 'class') {
        populateEventCourses().then(() => {
            const courseSelect = document.getElementById('eventCourseId');
            if (courseSelect) courseSelect.value = event.courseId || '';
        });
        const locInput = document.getElementById('eventLocation');
        if (locInput) locInput.value = event.location || '';
    } else if (event.eventType === 'work') {
        const wpInput = document.getElementById('eventWorkPlace');
        if (wpInput) wpInput.value = event.workPlace || '';
        const ttInput = document.getElementById('eventTravelTime');
        if (ttInput) ttInput.value = event.travelTime || '';
    } else if (event.eventType === 'task') {
        populateEventTasks().then(() => {
            const taskSelect = document.getElementById('eventTaskId');
            if (taskSelect) taskSelect.value = event.taskId || '';
        });
        document.querySelectorAll('.task-source-btn').forEach(b => b.classList.remove('active'));
        document.querySelector('.task-source-btn[data-source="existing"]')?.classList.add('active');
        document.getElementById('taskExistingFields')?.classList.remove('hidden');
        document.getElementById('taskNewFields')?.classList.add('hidden');
    } else if (event.eventType === 'personal') {
        const ptSelect = document.getElementById('eventPersonalType');
        if (ptSelect) ptSelect.value = event.type || 'Other';
        const descInput = document.getElementById('eventDescription');
        if (descInput) descInput.value = event.description || '';
    }

    // Populate courses (needed for class events)
    populateEventCourses();

    openModal('eventModal');
}

function updateEventFormFields(type) {
    document.getElementById('eventClassFields')?.classList.toggle('hidden', type !== 'class');
    document.getElementById('eventWorkFields')?.classList.toggle('hidden', type !== 'work');
    document.getElementById('eventPersonalFields')?.classList.toggle('hidden', type !== 'personal');
    document.getElementById('eventTaskFields')?.classList.toggle('hidden', type !== 'task');

    if (type === 'task') {
        populateEventTasks();
        populateTaskCourses();
        setupTaskSourceToggle();
    }
}

async function populateEventCourses() {
    const select = document.getElementById('eventCourseId');
    if (!select || select.options.length > 1) return;
    try {
        const courses = await api.getCourses();
        select.innerHTML = '<option value="">Select course...</option>' +
            courses.map(c => `<option value="${c.courseId}">${c.courseName}</option>`).join('');
    } catch { /* silent */ }
}

let cachedTaskList = [];

async function populateEventTasks() {
    const select = document.getElementById('eventTaskId');
    if (!select) return;
    try {
        cachedTaskList = await api.getTasks({ completed: false });
        select.innerHTML = '<option value="">Select a task...</option>' +
            cachedTaskList.map(t => `<option value="${t.taskId}">${t.title}${t.courseName ? ' (' + t.courseName + ')' : ''}</option>`).join('');

        // Attach change listener (remove old one first to avoid duplicates)
        select.removeEventListener('change', onTaskSelectChange);
        select.addEventListener('change', onTaskSelectChange);
        // Reset info card
        onTaskSelectChange();
    } catch { /* silent */ }
}

function onTaskSelectChange() {
    const select = document.getElementById('eventTaskId');
    const card = document.getElementById('taskInfoCard');
    if (!select || !card) return;

    const taskId = parseInt(select.value);
    const task = cachedTaskList.find(t => t.taskId === taskId);

    if (!task) {
        card.classList.add('hidden');
        return;
    }

    card.classList.remove('hidden');

    const hoursEl = document.getElementById('taskInfoHours');
    const dueEl = document.getElementById('taskInfoDue');
    const priorityEl = document.getElementById('taskInfoPriority');

    if (hoursEl) {
        hoursEl.textContent = task.estimatedHours
            ? `Est. ${task.estimatedHours}h`
            : 'No estimate';
    }
    if (dueEl) {
        dueEl.textContent = task.dueDate
            ? `Due ${new Date(task.dueDate).toLocaleDateString('en', { month: 'short', day: 'numeric' })}`
            : 'No due date';
    }
    if (priorityEl) {
        priorityEl.textContent = task.priority || '';
        priorityEl.className = task.priority ? `task-info-priority priority-${task.priority.toLowerCase()}` : '';
    }
}

async function populateTaskCourses() {
    const select = document.getElementById('eventTaskCourseId');
    if (!select || select.options.length > 1) return;
    try {
        const courses = await api.getCourses();
        select.innerHTML = '<option value="">Select course...</option>' +
            courses.map(c => `<option value="${c.courseId}">${c.courseName}</option>`).join('');
    } catch { /* silent */ }
}

let taskSourceToggleSetup = false;
function setupTaskSourceToggle() {
    if (taskSourceToggleSetup) return;
    taskSourceToggleSetup = true;

    document.querySelectorAll('.task-source-btn').forEach(btn => {
        btn.addEventListener('click', () => {
            document.querySelectorAll('.task-source-btn').forEach(b => b.classList.remove('active'));
            btn.classList.add('active');
            const source = btn.dataset.source;
            document.getElementById('taskExistingFields')?.classList.toggle('hidden', source !== 'existing');
            document.getElementById('taskNewFields')?.classList.toggle('hidden', source !== 'new');
        });
    });
}

/* ---- Event Details Popup ---- */
function showEventDetails(eventId, targetEl) {
    // Remove existing popup
    document.querySelector('.cal-event-popup')?.remove();

    const event = cachedEvents.find(e => e.eventId === eventId);
    if (!event) return;

    const from = new Date(event.from);
    const to = new Date(event.to);
    const colors = EVENT_COLORS[event.eventType] || EVENT_COLORS.personal;
    const label = event.courseName || event.taskTitle || event.workPlace || event.description || event.type || 'Event';

    const popup = document.createElement('div');
    popup.className = 'cal-event-popup';
    popup.innerHTML = `
        <div class="cal-event-popup__header" style="border-left: 4px solid ${colors.border}">
            <strong>${label}</strong>
            <button class="cal-event-popup__close">&times;</button>
        </div>
        <div class="cal-event-popup__body">
            <div><strong>Type:</strong> ${event.eventType}</div>
            <div><strong>Time:</strong> ${formatTime(from)} - ${formatTime(to)}</div>
            <div><strong>Date:</strong> ${from.toLocaleDateString('en-US', { weekday: 'short', month: 'short', day: 'numeric' })}</div>
            ${event.location ? `<div><strong>Location:</strong> ${event.location}</div>` : ''}
            ${event.workPlace ? `<div><strong>Workplace:</strong> ${event.workPlace}</div>` : ''}
            ${event.description ? `<div><strong>Description:</strong> ${event.description}</div>` : ''}
            ${event.status ? `<div><strong>Status:</strong> ${event.status}</div>` : ''}
        </div>
        <div class="cal-event-popup__actions">
            <button class="btn btn-sm btn-secondary cal-event-edit" data-event-id="${eventId}">Edit</button>
            <button class="btn btn-sm btn-ghost cal-event-delete" data-event-id="${eventId}">Delete</button>
        </div>
    `;

    targetEl.style.position = 'relative';
    targetEl.appendChild(popup);

    popup.querySelector('.cal-event-popup__close').addEventListener('click', (e) => {
        e.stopPropagation();
        popup.remove();
    });

    popup.querySelector('.cal-event-edit')?.addEventListener('click', (e) => {
        e.stopPropagation();
        popup.remove();
        openEventModalForEdit(event);
    });

    popup.querySelector('.cal-event-delete')?.addEventListener('click', async (e) => {
        e.stopPropagation();
        try {
            await api.deleteEvent(eventId);
            showToast('Event deleted');
            popup.remove();
            await navigate();
        } catch {
            showToast('Failed to delete event', 'error');
        }
    });

    // Close on outside click
    const closeOnOutside = (e) => {
        if (!popup.contains(e.target) && e.target !== targetEl) {
            popup.remove();
            document.removeEventListener('click', closeOnOutside);
        }
    };
    setTimeout(() => document.addEventListener('click', closeOnOutside), 0);
}

/* ---- Utilities ---- */
function getMonday(date) {
    const d = new Date(date);
    const day = d.getDay();
    const diff = d.getDate() - day + (day === 0 ? -6 : 1);
    d.setDate(diff);
    d.setHours(0, 0, 0, 0);
    return d;
}

function formatTime(date) {
    return `${date.getHours().toString().padStart(2, '0')}:${date.getMinutes().toString().padStart(2, '0')}`;
}
