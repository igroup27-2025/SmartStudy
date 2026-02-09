import { api } from './api.js';
import { showToast } from './modals.js';

let currentView = window.innerWidth <= 768 ? '3day' : 'weekly'; // 'monthly' | 'weekly' | '3day'
let currentDate = new Date();

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
    await navigate();
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
        const events = await api.getEvents(from, to);

        if (currentView === 'monthly') {
            renderMonthlyGrid(events, from, to);
        } else if (currentView === 'weekly') {
            renderTimeGrid(events, from, 7);
        } else {
            renderTimeGrid(events, from, 3);
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
        html += `<div class="cal-day-col ${isToday ? 'today' : ''}">`;
        html += `<div class="cal-day-header ${isToday ? 'today' : ''}">
            <span class="cal-day-name">${dayNames[day.getDay()]}</span>
            <span class="cal-day-num">${day.getDate()}</span>
        </div>`;
        html += '<div class="cal-day-body">';

        // Hour cells
        for (let h = 7; h <= 22; h++) {
            html += '<div class="cal-cell"></div>';
        }

        // Events for this day
        const dayEvents = events.filter(e => {
            const eDate = new Date(e.from);
            return eDate.getDate() === day.getDate() &&
                   eDate.getMonth() === day.getMonth() &&
                   eDate.getFullYear() === day.getFullYear();
        });

        dayEvents.forEach(e => {
            const from = new Date(e.from);
            const to = new Date(e.to);
            const startHour = from.getHours() + from.getMinutes() / 60;
            const endHour = to.getHours() + to.getMinutes() / 60;
            const top = (startHour - 7) * 50;
            const height = Math.max(25, (endHour - startHour) * 50);
            const colors = EVENT_COLORS[e.eventType] || EVENT_COLORS.personal;
            const label = e.courseName || e.workPlace || e.description || e.type || 'Event';

            html += `<div class="cal-event" style="top:${top}px;height:${height}px;background:${colors.bg};border-left:3px solid ${colors.border};color:${colors.text}">
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

        html += '</div></div>';
    });

    grid.innerHTML = html;
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

            const classes = ['cal-month-day'];
            if (isOutside) classes.push('outside');
            if (isToday) classes.push('today');

            html += `<div class="${classes.join(' ')}" data-date="${dateStr}">`;
            html += `<div class="cal-month-day__num">${day.getDate()}</div>`;

            // Show up to 3 event labels
            const maxShow = 3;
            dayEvents.slice(0, maxShow).forEach(e => {
                const colors = EVENT_COLORS[e.eventType] || EVENT_COLORS.personal;
                const label = e.courseName || e.workPlace || e.description || e.type || 'Event';
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
