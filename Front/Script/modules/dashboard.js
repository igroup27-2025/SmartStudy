import { api } from './api.js';
import { getUser } from './auth.js';
import { showToast } from './modals.js';

export async function initDashboard() {
    try {
        const data = await api.getDashboard();
        renderHero();
        renderMotivation(data.stress);
        renderProgress(data);
        renderAlerts(data);
        renderStats(data);
        renderMiniCalendar();
        renderReview(data);
    } catch (err) {
        showToast('Failed to load dashboard', 'error');
    }
}

/* ---- Section 1: Hero Greeting ---- */
function renderHero() {
    const el = document.getElementById('dashHero');
    if (!el) return;

    const user = getUser();
    const firstName = user?.firstName || 'Student';
    const hour = new Date().getHours();
    const greeting = hour < 12 ? 'Good morning' : hour < 18 ? 'Good afternoon' : 'Good evening';

    const now = new Date();
    const dateStr = now.toLocaleDateString('en-US', {
        weekday: 'long', month: 'long', day: 'numeric'
    });

    el.innerHTML = `
        <h1 class="dash-hero__title">${greeting}, ${firstName}</h1>
        <p class="dash-hero__date">${dateStr}</p>
    `;
}

/* ---- Section 2: Motivational Card ---- */
function renderMotivation(stress) {
    const el = document.getElementById('dashMotivation');
    if (!el) return;

    const score = stress?.score ?? 0;
    let icon, text;

    if (score <= 25) {
        icon = '\u2728'; // sparkle
        text = "You're in great shape! Enjoy the calm.";
    } else if (score <= 40) {
        icon = '\u2728';
        text = "You're balancing things well. Stay focused!";
    } else if (score <= 60) {
        icon = '\u26A0\uFE0F'; // warning
        text = 'Things are picking up. Prioritize your top tasks!';
    } else if (score <= 80) {
        icon = '\u26A0\uFE0F';
        text = 'Your workload is heavy. Consider what can wait.';
    } else {
        icon = '\uD83D\uDD25'; // fire
        text = "You're under high pressure. Focus on essentials only!";
    }

    el.innerHTML = `
        <div class="dash-motivation__card">
            <span class="dash-motivation__icon">${icon}</span>
            <p class="dash-motivation__text">${text}</p>
        </div>
    `;
}

/* ---- Section 3: Semester Progress ---- */
function renderProgress(data) {
    const el = document.getElementById('dashProgress');
    if (!el) return;

    const completed = data.completedTasks ?? 0;
    const total = data.totalTasks ?? 0;
    const pct = total > 0 ? Math.round((completed / total) * 100) : 0;

    el.innerHTML = `
        <div class="dash-progress__header">
            <span class="dash-progress__label">Semester Progress</span>
            <span class="dash-progress__count">${completed}/${total} tasks</span>
        </div>
        <div class="dash-progress__track">
            <div class="dash-progress__fill" style="width:${pct}%"></div>
        </div>
    `;
}

/* ---- Section 4: Alert Pills ---- */
function renderAlerts(data) {
    const el = document.getElementById('dashAlerts');
    if (!el) return;

    const pills = [];
    const pending = data.pendingTasks ?? 0;
    const completed = data.completedTasks ?? 0;
    const total = data.totalTasks ?? 0;
    const pct = total > 0 ? Math.round((completed / total) * 100) : 0;

    if (pending > 0) {
        pills.push(`<span class="dash-pill dash-pill--warning">${pending} task${pending > 1 ? 's' : ''} need${pending === 1 ? 's' : ''} review</span>`);
    }
    if (pct >= 60) {
        pills.push(`<span class="dash-pill dash-pill--success">Great job! ${pct}% semester progress</span>`);
    }

    const exams = data.nextExams || [];
    exams.forEach(exam => {
        if (exam.daysUntil <= 7) {
            pills.push(`<span class="dash-pill dash-pill--danger">Exam in ${exam.daysUntil} day${exam.daysUntil !== 1 ? 's' : ''}: ${exam.courseName}</span>`);
        }
    });

    el.innerHTML = pills.length ? pills.join('') : '';
}

/* ---- Section 5: Three Stat Cards ---- */
function renderStats(data) {
    const el = document.getElementById('dashStats');
    if (!el) return;

    const today = new Date();
    today.setHours(0, 0, 0, 0);
    const tomorrow = new Date(today);
    tomorrow.setDate(tomorrow.getDate() + 1);

    const deadlines = data.upcomingDeadlines || [];
    const dueToday = deadlines.filter(t => {
        const d = new Date(t.dueDate);
        return d >= today && d < tomorrow;
    }).length;

    const pending = data.pendingTasks ?? 0;
    const completed = data.completedTasks ?? 0;
    const total = data.totalTasks ?? 0;
    const pct = total > 0 ? Math.round((completed / total) * 100) : 0;

    el.innerHTML = `
        <div class="dash-stat dash-stat--coral">
            <span class="dash-stat__value">${dueToday}</span>
            <span class="dash-stat__label">Due Today</span>
        </div>
        <div class="dash-stat dash-stat--amber">
            <span class="dash-stat__value">${pending}</span>
            <span class="dash-stat__label">Pending</span>
        </div>
        <div class="dash-stat dash-stat--green">
            <span class="dash-stat__value">${pct}%</span>
            <span class="dash-stat__label">Progress</span>
        </div>
    `;
}

/* ---- Section 6: Task Review ---- */
function renderReview(data) {
    const el = document.getElementById('dashReview');
    if (!el) return;

    const tasks = (data.upcomingDeadlines || []).slice(0, 5);

    const header = `
        <div class="dash-review__header">
            <h2 class="dash-review__title">New &amp; Needs Review</h2>
            <span class="dash-review__badge">${tasks.length}</span>
        </div>
    `;

    if (!tasks.length) {
        el.innerHTML = header + '<p class="dash-review__empty">No tasks to review right now.</p>';
        return;
    }

    const cards = tasks.map(t => {
        const due = new Date(t.dueDate);
        const now = new Date();
        const isOverdue = due < now;
        const dateStr = due.toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
        const priorityClass = (t.priority || 'medium').toLowerCase();

        return `
            <div class="dash-task-card">
                <div class="dash-task-card__body">
                    <div class="dash-task-card__top">
                        <span class="dash-task-card__title">${t.title}</span>
                        <span class="dash-task-card__priority dash-task-card__priority--${priorityClass}">${t.priority || 'Medium'}</span>
                    </div>
                    <span class="dash-task-card__status ${isOverdue ? 'dash-task-card__status--overdue' : ''}">
                        ${isOverdue ? 'Overdue - needs rescheduling' : 'Due: ' + dateStr}
                    </span>
                </div>
                <div class="dash-task-card__actions">
                    <button class="btn btn-sm btn-primary dash-approve-btn" data-task-id="${t.taskId}">Approve</button>
                    <a href="/Pages/Tasks.html" class="btn btn-sm btn-ghost">Edit</a>
                </div>
            </div>
        `;
    }).join('');

    el.innerHTML = header + '<div class="dash-review__list">' + cards + '</div>';

    // Bind approve buttons
    el.querySelectorAll('.dash-approve-btn').forEach(btn => {
        btn.addEventListener('click', async () => {
            const taskId = btn.dataset.taskId;
            try {
                btn.disabled = true;
                btn.textContent = '...';
                await api.completeTask(taskId);
                showToast('Task approved!', 'success');
                // Refresh the dashboard
                const freshData = await api.getDashboard();
                renderProgress(freshData);
                renderAlerts(freshData);
                renderStats(freshData);
                renderReview(freshData);
            } catch (err) {
                showToast('Failed to approve task', 'error');
                btn.disabled = false;
                btn.textContent = 'Approve';
            }
        });
    });
}

/* ---- Section 7: Dashboard 3-Day Calendar ---- */
const MINI_CAL_COLORS = {
    class: { bg: '#E0F7FA', border: '#00BCD4', text: '#006064' },
    task: { bg: '#FFF3E0', border: '#F28D35', text: '#E65100' },
    work: { bg: '#F3E5F5', border: '#9B76FF', text: '#4A148C' },
    personal: { bg: '#FFF8E1', border: '#F2C777', text: '#F57F17' },
    exam: { bg: '#FCE4EC', border: '#FF607E', text: '#880E4F' },
};

let miniCalStart = new Date();

async function renderMiniCalendar() {
    const el = document.getElementById('dashCalendar');
    if (!el) return;

    const from = new Date(miniCalStart);
    from.setHours(0, 0, 0, 0);
    const to = new Date(from);
    to.setDate(to.getDate() + 3);

    // Fetch events for the 3-day range
    let events = [];
    try {
        events = await api.getEvents(from, to);
    } catch (err) {
        // Silent fail — show calendar without events
    }

    const today = new Date();
    today.setHours(0, 0, 0, 0);

    const days = [];
    for (let i = 0; i < 3; i++) {
        const d = new Date(from);
        d.setDate(d.getDate() + i);
        days.push(d);
    }

    // Header with date range
    const fmt = (d) => d.toLocaleDateString('en', { month: 'short', day: 'numeric' });
    const rangeLabel = `${fmt(days[0])} - ${fmt(days[2])}`;

    let html = '<div class="mini-cal">';
    html += '<div class="mini-cal__header">';
    html += `<span class="mini-cal__title">${rangeLabel}</span>`;
    html += '<div class="mini-cal__nav">';
    html += '<button id="miniCalPrev">&larr;</button>';
    html += '<button id="miniCalNext">&rarr;</button>';
    html += '<button id="miniCalAdd" class="mini-cal__add" title="Add event">+</button>';
    html += '</div></div>';

    // 3-day columns
    html += '<div class="mini-cal__days">';
    days.forEach(day => {
        const isToday = day.getTime() === today.getTime();
        const dayName = day.toLocaleDateString('en', { weekday: 'short' });
        const dateStr = `${day.getFullYear()}-${String(day.getMonth() + 1).padStart(2, '0')}-${String(day.getDate()).padStart(2, '0')}`;

        const dayEvents = events.filter(e => {
            const eDate = new Date(e.from);
            return eDate.getDate() === day.getDate() &&
                   eDate.getMonth() === day.getMonth() &&
                   eDate.getFullYear() === day.getFullYear();
        });

        html += `<div class="mini-cal__day-col ${isToday ? 'mini-cal__day-col--today' : ''}" data-date="${dateStr}">`;
        html += `<div class="mini-cal__day-header">`;
        html += `<span class="mini-cal__day-name">${dayName}</span>`;
        html += `<span class="mini-cal__day-num ${isToday ? 'today' : ''}">${day.getDate()}</span>`;
        html += '</div>';

        html += '<div class="mini-cal__day-events">';
        if (dayEvents.length === 0) {
            html += '<span class="mini-cal__no-events">No events</span>';
        } else {
            dayEvents.slice(0, 4).forEach(e => {
                const colors = MINI_CAL_COLORS[e.eventType] || MINI_CAL_COLORS.personal;
                const label = e.courseName || e.workPlace || e.description || e.type || 'Event';
                const time = new Date(e.from);
                const timeStr = `${time.getHours().toString().padStart(2, '0')}:${time.getMinutes().toString().padStart(2, '0')}`;
                html += `<div class="mini-cal__event" style="background:${colors.bg};border-left:3px solid ${colors.border};color:${colors.text}">`;
                html += `<span class="mini-cal__event-time">${timeStr}</span> ${label}`;
                html += '</div>';
            });
            if (dayEvents.length > 4) {
                html += `<span class="mini-cal__more">+${dayEvents.length - 4} more</span>`;
            }
        }
        html += '</div></div>';
    });
    html += '</div>'; // close .mini-cal__days

    // Event color legend
    html += '<div class="calendar-legend" style="margin-top:var(--space-3);gap:var(--space-4);flex-wrap:wrap">';
    html += '<span class="legend-item"><span class="legend-dot" style="background:#00BCD4"></span> Classes</span>';
    html += '<span class="legend-item"><span class="legend-dot" style="background:#F28D35"></span> Tasks</span>';
    html += '<span class="legend-item"><span class="legend-dot" style="background:#9B76FF"></span> Work</span>';
    html += '<span class="legend-item"><span class="legend-dot" style="background:#F2C777"></span> Personal</span>';
    html += '<span class="legend-item"><span class="legend-dot" style="background:#FF607E"></span> Exams</span>';
    html += '</div>';

    html += '</div>'; // close .mini-cal

    el.innerHTML = html;

    // Navigation: shift by 3 days
    document.getElementById('miniCalPrev')?.addEventListener('click', () => {
        miniCalStart.setDate(miniCalStart.getDate() - 3);
        renderMiniCalendar();
    });
    document.getElementById('miniCalNext')?.addEventListener('click', () => {
        miniCalStart.setDate(miniCalStart.getDate() + 3);
        renderMiniCalendar();
    });

    // Add event → navigate to calendar page
    document.getElementById('miniCalAdd')?.addEventListener('click', () => {
        const dateStr = `${today.getFullYear()}-${String(today.getMonth() + 1).padStart(2, '0')}-${String(today.getDate()).padStart(2, '0')}`;
        window.location.href = `/Pages/Calendar.html?date=${dateStr}&add=1`;
    });

    // Day column click → navigate to calendar page at that date
    el.querySelectorAll('.mini-cal__day-col').forEach(col => {
        col.addEventListener('click', (e) => {
            if (e.target.closest('#miniCalPrev, #miniCalNext, #miniCalAdd')) return;
            const dateStr = col.dataset.date;
            if (dateStr) {
                window.location.href = `/Pages/Calendar.html?date=${dateStr}`;
            }
        });
    });
}
