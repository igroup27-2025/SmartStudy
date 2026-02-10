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
        renderWorkload(data);
        renderUnscheduled(data);
        renderSuggestion(data);
        renderWeeklySuggestions();
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

    // Overload warnings
    const overloaded = data.overloadedDays || [];
    if (overloaded.length > 0) {
        pills.push(`<span class="dash-pill dash-pill--danger">${overloaded.length} overloaded day${overloaded.length > 1 ? 's' : ''} this week</span>`);
    }

    // Unscheduled warnings
    const unscheduled = data.unscheduledTaskCount ?? 0;
    if (unscheduled > 0) {
        pills.push(`<span class="dash-pill dash-pill--warning">${unscheduled} task${unscheduled > 1 ? 's' : ''} not scheduled</span>`);
    }

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

/* ---- Section: Workload Overview ---- */
function renderWorkload(data) {
    const el = document.getElementById('dashWorkload');
    if (!el) return;

    const workload = data.dailyWorkload || [];
    if (!workload.length) {
        el.innerHTML = '';
        return;
    }

    // Show next 7 days
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    const week = workload.filter(d => {
        const date = new Date(d.date);
        return date >= today && date < new Date(today.getTime() + 7 * 24 * 60 * 60 * 1000);
    }).slice(0, 7);

    if (!week.length) {
        el.innerHTML = '';
        return;
    }

    const maxHours = 10;
    const todayHours = data.todayWorkloadHours ?? 0;
    const weeklyHours = data.weeklyWorkloadHours ?? 0;

    let html = `
        <div class="dash-workload__header">
            <h3 class="dash-workload__title">Workload Overview</h3>
            <div class="dash-workload__summary">
                <span class="dash-workload__today">Today: ${todayHours}h</span>
                <span class="dash-workload__weekly">This week: ${weeklyHours}h</span>
            </div>
        </div>
        <div class="dash-workload__chart">
    `;

    week.forEach(d => {
        const date = new Date(d.date);
        const isToday = date.getTime() === today.getTime();
        const dayName = date.toLocaleDateString('en', { weekday: 'short' });
        const pct = Math.min(100, (d.scheduledHours / maxHours) * 100);
        const colorClass = d.isOverloaded ? 'overloaded' : d.scheduledHours > 6 ? 'heavy' : d.scheduledHours > 3 ? 'moderate' : 'light';

        html += `
            <div class="dash-workload__bar-group ${isToday ? 'today' : ''}">
                <div class="dash-workload__bar-track">
                    <div class="dash-workload__bar dash-workload__bar--${colorClass}" style="height:${pct}%"></div>
                </div>
                <span class="dash-workload__bar-label">${dayName}</span>
                <span class="dash-workload__bar-value">${d.scheduledHours}h</span>
            </div>
        `;
    });

    html += '</div>';
    el.innerHTML = html;
}

/* ---- Section: Unscheduled Tasks Alert ---- */
function renderUnscheduled(data) {
    const el = document.getElementById('dashUnscheduled');
    if (!el) return;

    const count = data.unscheduledTaskCount ?? 0;
    if (count === 0) {
        el.innerHTML = '';
        return;
    }

    // Find unscheduled tasks from upcoming deadlines
    const unscheduled = (data.upcomingDeadlines || [])
        .filter(t => t.schedulingStatus === 'Unscheduled' || t.schedulingStatus === 'Partial');

    el.innerHTML = `
        <div class="dash-unscheduled__card">
            <div class="dash-unscheduled__header">
                <span class="dash-unscheduled__icon">&#9888;</span>
                <h3 class="dash-unscheduled__title">${count} Unscheduled Task${count > 1 ? 's' : ''}</h3>
            </div>
            ${unscheduled.length ? `
                <ul class="dash-unscheduled__list">
                    ${unscheduled.map(t => `
                        <li>
                            <span class="dash-unscheduled__task-name">${t.title}</span>
                            ${t.dueDate ? `<span class="dash-unscheduled__due">Due: ${new Date(t.dueDate).toLocaleDateString('en-US', { month: 'short', day: 'numeric' })}</span>` : ''}
                        </li>
                    `).join('')}
                </ul>
            ` : ''}
            <p class="dash-unscheduled__hint">These tasks couldn't fit in your schedule. Consider extending deadlines or reducing workload.</p>
        </div>
    `;
}

/* ---- Section: Suggestion Card ---- */
function renderSuggestion(data) {
    const el = document.getElementById('dashSuggestion');
    if (!el) return;

    const task = data.nextSuggestedTask;
    if (!task) {
        el.innerHTML = '';
        return;
    }

    const priorityClass = (task.priority || 'medium').toLowerCase();
    const dueStr = task.dueDate
        ? new Date(task.dueDate).toLocaleDateString('en-US', { month: 'short', day: 'numeric' })
        : '';

    el.innerHTML = `
        <div class="dash-suggestion__card">
            <div class="dash-suggestion__header">
                <span class="dash-suggestion__label">Next task to work on</span>
            </div>
            <div class="dash-suggestion__body">
                <div class="dash-suggestion__title">${task.title}</div>
                <div class="dash-suggestion__meta">
                    <span class="dash-suggestion__course">${task.courseName}</span>
                    <span class="badge badge-priority-${priorityClass}">${task.priority || 'Medium'}</span>
                    ${dueStr ? `<span class="dash-suggestion__due">Due: ${dueStr}</span>` : ''}
                    ${task.estimatedHours ? `<span class="dash-suggestion__hours">${task.estimatedHours}h</span>` : ''}
                </div>
            </div>
            <a href="/Pages/Tasks.html" class="btn btn-sm btn-primary">View Tasks</a>
        </div>
    `;
}

/* ---- Section: Task Review ---- */
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

        // Scheduling info
        let scheduleInfo = '';
        if (t.schedulingStatus === 'Scheduled' && t.scheduledDate) {
            const sd = new Date(t.scheduledDate);
            scheduleInfo = `<span class="badge badge-scheduled">Scheduled: ${sd.toLocaleDateString('en-US', { month: 'short', day: 'numeric' })}</span>`;
        } else if (t.schedulingStatus === 'Partial') {
            scheduleInfo = '<span class="badge badge-partial">Partially Scheduled</span>';
        } else if (t.schedulingStatus === 'Unscheduled') {
            scheduleInfo = '<span class="badge badge-unscheduled">Not Scheduled</span>';
        }

        return `
            <div class="dash-task-card">
                <div class="dash-task-card__body">
                    <div class="dash-task-card__top">
                        <span class="dash-task-card__title">${t.title}</span>
                        <span class="dash-task-card__priority dash-task-card__priority--${priorityClass}">${t.priority || 'Medium'}</span>
                    </div>
                    <div class="dash-task-card__bottom">
                        <span class="dash-task-card__status ${isOverdue ? 'dash-task-card__status--overdue' : ''}">
                            ${isOverdue ? 'Overdue - needs rescheduling' : 'Due: ' + dateStr}
                        </span>
                        ${scheduleInfo}
                    </div>
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
                renderWorkload(freshData);
                renderUnscheduled(freshData);
                renderSuggestion(freshData);
                renderReview(freshData);
            } catch (err) {
                showToast('Failed to approve task', 'error');
                btn.disabled = false;
                btn.textContent = 'Approve';
            }
        });
    });
}

/* ---- Section: Weekly Insights ---- */
async function renderWeeklySuggestions() {
    const el = document.getElementById('dashWeeklyInsights');
    if (!el) return;

    try {
        const data = await api.getWeeklySuggestions();
        if (!data || (!data.suggestions?.length && !data.focusTasks?.length)) {
            el.innerHTML = '';
            return;
        }

        const typeIcons = {
            warning: '&#9888;',
            overload: '&#128293;',
            positive: '&#9989;',
            danger: '&#128680;',
            urgent: '&#9200;',
            info: '&#128161;',
        };

        const typeClasses = {
            warning: 'suggestion--warning',
            overload: 'suggestion--danger',
            positive: 'suggestion--success',
            danger: 'suggestion--danger',
            urgent: 'suggestion--warning',
            info: 'suggestion--info',
        };

        let html = '<div class="card"><div class="card-header"><h3 class="card-title">Weekly Insights</h3></div><div class="card-body">';

        // Summary
        if (data.totalStudyHours !== undefined) {
            html += `<div class="weekly-summary">
                <span>Available study time: <strong>${data.availableHours || 0}h</strong></span>
                <span>Needed: <strong>${data.totalStudyHours || 0}h</strong></span>
            </div>`;
        }

        // Suggestion cards
        if (data.suggestions?.length) {
            html += '<div class="suggestion-list">';
            data.suggestions.forEach(s => {
                const icon = typeIcons[s.type] || typeIcons.info;
                const cls = typeClasses[s.type] || 'suggestion--info';
                html += `<div class="suggestion-card ${cls}">
                    <span class="suggestion-card__icon">${icon}</span>
                    <span class="suggestion-card__text">${s.message}</span>
                </div>`;
            });
            html += '</div>';
        }

        // Focus tasks
        if (data.focusTasks?.length) {
            html += '<h4 class="weekly-focus-title">Top Focus Tasks</h4>';
            html += '<div class="focus-task-list">';
            data.focusTasks.forEach(t => {
                const priorityClass = (t.priority || 'medium').toLowerCase();
                html += `<div class="focus-task-item">
                    <span class="focus-task-item__title">${t.title}</span>
                    <span class="badge badge-priority-${priorityClass}">${t.priority || 'Medium'}</span>
                    ${t.dueDate ? `<span class="focus-task-item__due">Due ${new Date(t.dueDate).toLocaleDateString('en', { month: 'short', day: 'numeric' })}</span>` : ''}
                    ${t.estimatedHours ? `<span class="focus-task-item__hours">${t.estimatedHours}h</span>` : ''}
                </div>`;
            });
            html += '</div>';
        }

        html += '</div></div>';
        el.innerHTML = html;
    } catch {
        el.innerHTML = '';
    }
}

/* ---- Section: Dashboard 3-Day Calendar ---- */
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
                const label = e.courseName || e.taskTitle || e.workPlace || e.description || e.type || 'Event';
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
