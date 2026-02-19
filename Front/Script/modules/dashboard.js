import { api } from './api.js';
import { getUser } from './auth.js';
import { showToast } from './modals.js';
import { BASE_PATH } from './config.js';

function escapeHtml(str) {
    if (!str) return '';
    return str.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
}

export async function initDashboard() {
    try {
        const data = await api.getDashboard();
        renderHeroWithMotivation(data.stress);
        renderProgress(data);
        renderAlerts(data);
        renderStats(data);
        renderWorkload(data);
        renderRelocationSuggestions(data);
        renderSuggestion(data);
        renderWeeklySuggestions();
        renderMiniCalendar();
        renderReview(data);
        renderOverdue(data);
    } catch (err) {
        showToast('Failed to load dashboard', 'error');
    }
}

/* ---- Section 1: Hero Greeting + Motivation ---- */
function renderHeroWithMotivation(stress) {
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

    const score = stress?.score ?? 0;
    let text;

    if (score <= 25) {
        text = "You're in great shape! Enjoy the calm.";
    } else if (score <= 40) {
        text = "You're balancing things well. Stay focused!";
    } else if (score <= 60) {
        text = 'Things are picking up. Prioritize your top tasks!';
    } else if (score <= 80) {
        text = 'Your workload is heavy. Consider what can wait.';
    } else {
        text = "You're under high pressure. Focus on essentials only!";
    }

    el.innerHTML = `
        <h1 class="dash-hero__title">${greeting}, ${firstName}</h1>
        <p class="dash-hero__date">${dateStr}</p>
        <p class="dash-hero__motivation">${text}</p>
    `;

    const motEl = document.getElementById('dashMotivation');
    if (motEl) motEl.innerHTML = '';
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

    // Overdue warnings
    const overdueCount = (data.overdueTasks || []).length;
    if (overdueCount > 0) {
        pills.push(`<span class="dash-pill dash-pill--danger">${overdueCount} overdue task${overdueCount > 1 ? 's' : ''}</span>`);
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
        const totalH = d.totalHours ?? d.scheduledHours;
        const pct = Math.min(100, (totalH / maxHours) * 100);
        const colorClass = d.isOverloaded ? 'overloaded' : totalH > 6 ? 'heavy' : totalH > 3 ? 'moderate' : 'light';

        // Build tooltip with breakdown
        const studyH = d.studyHours ?? d.scheduledHours;
        const workH = d.workHours ?? 0;
        const classH = d.classHours ?? 0;
        const personalH = d.personalHours ?? 0;
        const breakdown = [];
        if (studyH > 0) breakdown.push(`Study: ${studyH}h`);
        if (classH > 0) breakdown.push(`Class: ${classH}h`);
        if (workH > 0) breakdown.push(`Work: ${workH}h`);
        if (personalH > 0) breakdown.push(`Personal: ${personalH}h`);
        const tooltip = breakdown.join(' | ') || `${totalH}h`;

        html += `
            <div class="dash-workload__bar-group ${isToday ? 'today' : ''}" title="${tooltip}">
                <div class="dash-workload__bar-track">
                    <div class="dash-workload__bar dash-workload__bar--${colorClass}" style="height:${pct}%"></div>
                </div>
                <span class="dash-workload__bar-label">${dayName}</span>
                <span class="dash-workload__bar-value">${totalH}h</span>
            </div>
        `;
    });

    html += '</div>';
    el.innerHTML = html;
}

/* ---- Section: Relocation Suggestions ---- */
function renderRelocationSuggestions(data) {
    const el = document.getElementById('dashRelocationSuggestions');
    if (!el) return;

    const suggestions = data.relocationSuggestions || [];
    if (!suggestions.length) {
        el.innerHTML = '';
        return;
    }

    const cards = suggestions.map((s, i) => {
        const eventDate = new Date(s.currentFrom);
        const dateStr = `${eventDate.getFullYear()}-${String(eventDate.getMonth() + 1).padStart(2, '0')}-${String(eventDate.getDate()).padStart(2, '0')}`;
        return `
        <div class="relocation-card" data-index="${i}">
            <div class="relocation-card__icon">&#128161;</div>
            <div class="relocation-card__body">
                <div class="relocation-card__title">No room for "${escapeHtml(s.blockedTaskTitle)}"</div>
                <div class="relocation-card__message">${escapeHtml(s.message)}</div>
            </div>
            <div class="relocation-card__actions">
                <a href="${BASE_PATH}/Pages/Calendar.html?date=${dateStr}" class="btn btn-sm btn-primary relocation-move" title="Go to calendar to move this event">Move</a>
                <button class="btn btn-sm btn-ghost relocation-dismiss" data-index="${i}" title="Dismiss">&times;</button>
            </div>
        </div>
    `}).join('');

    el.innerHTML = `
        <div class="dash-relocations__card">
            <div class="dash-relocations__header">
                <span class="dash-relocations__icon">&#9888;</span>
                <h3 class="dash-relocations__title">Scheduling Suggestions</h3>
            </div>
            ${cards}
        </div>
    `;

    el.querySelectorAll('.relocation-dismiss').forEach(btn => {
        btn.addEventListener('click', () => {
            btn.closest('.relocation-card').remove();
            if (!el.querySelector('.relocation-card')) el.innerHTML = '';
        });
    });
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

    // Build reason hints
    const reasons = [];
    if (task.dueDate) {
        const daysLeft = Math.ceil((new Date(task.dueDate) - new Date()) / (1000 * 60 * 60 * 24));
        if (daysLeft <= 1) reasons.push('Due very soon');
        else if (daysLeft <= 3) reasons.push(`Due in ${daysLeft} days`);
        else if (daysLeft <= 7) reasons.push('Due this week');
    }
    if (task.estimatedHours && task.estimatedHours >= 4) reasons.push('Large task');
    if (task.isShared) reasons.push('Shared with friend');
    const reasonText = reasons.length ? reasons.join(' · ') : 'Based on deadline, workload & course weight';

    el.innerHTML = `
        <div class="dash-suggestion__card">
            <div class="dash-suggestion__header">
                <span class="dash-suggestion__label">Next task to work on</span>
            </div>
            <div class="dash-suggestion__body">
                <div class="dash-suggestion__title">${escapeHtml(task.title)}</div>
                <div class="dash-suggestion__meta">
                    <span class="dash-suggestion__course">${escapeHtml(task.courseName)}</span>
                    <span class="badge badge-priority-${priorityClass}">${task.priority || 'Medium'}</span>
                    ${dueStr ? `<span class="dash-suggestion__due">Due: ${dueStr}</span>` : ''}
                    ${task.estimatedHours ? `<span class="dash-suggestion__hours">${task.estimatedHours}h</span>` : ''}
                </div>
                <div class="dash-suggestion__reason">${reasonText}</div>
            </div>
            <a href="${BASE_PATH}/Pages/Tasks.html" class="btn btn-sm btn-primary">View Tasks</a>
        </div>
    `;
}

/* ---- Section: Task Review ---- */
function renderReview(data) {
    const el = document.getElementById('dashReview');
    if (!el) return;

    // Filter overdue tasks OUT of review (they have their own section)
    const reviewTasks = data.needsReviewTasks || [];
    const overdueIds = new Set((data.overdueTasks || []).map(t => t.taskId));
    const tasks = reviewTasks.filter(t => !overdueIds.has(t.taskId)).slice(0, 10);

    const header = `
        <div class="dash-review__header">
            <h2 class="dash-review__title">Needs Review</h2>
            <span class="dash-review__badge">${tasks.length}</span>
        </div>
    `;

    if (!tasks.length) {
        el.innerHTML = header + '<p class="dash-review__empty">No tasks to review right now.</p>';
        return;
    }

    const formatSlotTime = (d) => d.toLocaleTimeString('en-US', { hour: '2-digit', minute: '2-digit', hour12: false });
    const formatSlotDate = (d) => d.toLocaleDateString('en-US', { month: 'short', day: 'numeric' });

    const cards = tasks.map(t => {
        const due = t.dueDate ? new Date(t.dueDate) : null;
        const now = new Date();
        const isOverdue = due && due < now;
        const dateStr = due ? due.toLocaleDateString('en-US', { month: 'short', day: 'numeric' }) : '';
        const priorityClass = (t.priority || 'medium').toLowerCase();

        const isNeedReview = t.schedulingStatus === 'NeedReview';
        const isUnscheduled = t.schedulingStatus === 'Unscheduled' || t.schedulingStatus === 'Partial';

        // Build scheduled time slots display — group by day
        let slotsHtml = '';
        const slots = t.scheduledSlots || [];
        if (slots.length) {
            // Group slots by date key, merge into earliest-start / latest-end per day
            const byDay = {};
            slots.forEach(s => {
                const from = new Date(s.from);
                const to = new Date(s.to);
                const key = from.toDateString();
                if (!byDay[key]) byDay[key] = { from, to };
                else {
                    if (from < byDay[key].from) byDay[key].from = from;
                    if (to > byDay[key].to) byDay[key].to = to;
                }
            });
            const slotItems = Object.values(byDay).map(s =>
                `<span class="dash-review-slot">${formatSlotDate(s.from)}, ${formatSlotTime(s.from)}-${formatSlotTime(s.to)}</span>`
            ).join('');
            slotsHtml = `<div class="dash-review-slots">${slotItems}</div>`;
        }

        // Status badge
        let scheduleInfo = '';
        if (isNeedReview) {
            scheduleInfo = `<span class="badge badge-review">Pending Review</span>`;
        } else if (t.schedulingStatus === 'Scheduled') {
            scheduleInfo = `<span class="badge badge-scheduled">Scheduled</span>`;
        } else if (t.schedulingStatus === 'Partial') {
            scheduleInfo = '<span class="badge badge-partial">Partially Scheduled</span>';
        } else if (isUnscheduled) {
            scheduleInfo = '<span class="badge badge-unscheduled">Not Scheduled</span>';
        }

        // Edit goes to calendar (with date param for first slot)
        const firstSlot = slots.length ? new Date(slots[0].from) : null;
        const calDateParam = firstSlot ? `?date=${firstSlot.getFullYear()}-${String(firstSlot.getMonth()+1).padStart(2,'0')}-${String(firstSlot.getDate()).padStart(2,'0')}` : '';
        const editHref = `${BASE_PATH}/Pages/Calendar.html${calDateParam}`;

        return `
            <div class="dash-task-card">
                <div class="dash-task-card__body">
                    <div class="dash-task-card__top">
                        <span class="dash-task-card__title">${escapeHtml(t.title)}</span>
                        <span class="dash-task-card__priority dash-task-card__priority--${priorityClass}">${escapeHtml(t.priority) || 'Medium'}</span>
                    </div>
                    <div class="dash-task-card__bottom">
                        <span class="dash-task-card__status ${isOverdue ? 'dash-task-card__status--overdue' : ''}">
                            ${isOverdue ? 'Overdue - needs rescheduling' : (dateStr ? 'Due: ' + dateStr : '')}
                        </span>
                        ${scheduleInfo}
                        ${isUnscheduled ? '<span class="dash-task-card__reason">Could not fit in schedule</span>' : ''}
                    </div>
                    ${slotsHtml}
                </div>
                <div class="dash-task-card__actions">
                    ${isUnscheduled
                        ? `<a href="${BASE_PATH}/Pages/Calendar.html" class="btn btn-sm btn-primary">Schedule Manually</a>`
                        : `<button class="btn btn-sm btn-primary dash-approve-btn" data-task-id="${t.taskId}">Approve</button>`
                    }
                    <a href="${editHref}" class="btn btn-sm btn-ghost">Edit in Calendar</a>
                </div>
            </div>
        `;
    }).join('');

    el.innerHTML = header + '<div class="dash-review__list">' + cards + '</div>';

    // Bind approve buttons — call approve endpoint (keeps schedule, removes from review)
    el.querySelectorAll('.dash-approve-btn').forEach(btn => {
        btn.addEventListener('click', async () => {
            const taskId = btn.dataset.taskId;
            btn.disabled = true;
            btn.textContent = 'Approving...';
            try {
                await api.approveScheduledTask(taskId);
                showToast('Schedule approved!', 'success');
                const freshData = await api.getDashboard();
                renderProgress(freshData);
                renderAlerts(freshData);
                renderStats(freshData);
                renderWorkload(freshData);
                renderRelocationSuggestions(freshData);
                renderSuggestion(freshData);
                renderReview(freshData);
                renderOverdue(freshData);
            } catch (err) {
                showToast('Failed to approve schedule', 'error');
                btn.disabled = false;
                btn.textContent = 'Approve';
            }
        });
    });
}

/* ---- Completion Modal (used by Review section) ---- */
function showCompletionModal(taskId, taskTitle, estHours) {
    // Remove existing modal if any
    document.getElementById('dashCompleteModal')?.remove();

    const estDisplay = estHours ? `${estHours}h estimated` : 'No estimate';

    const modal = document.createElement('div');
    modal.id = 'dashCompleteModal';
    modal.className = 'modal-overlay';
    modal.innerHTML = `
        <div class="modal" style="max-width:420px">
            <div class="modal-header">
                <h3>Complete Task</h3>
                <button class="modal-close" id="dashCompleteClose">&times;</button>
            </div>
            <div class="modal-body">
                <p style="font-weight:600;margin-bottom:var(--space-2)">${taskTitle}</p>
                <p style="color:var(--text-secondary);margin-bottom:var(--space-4)">${estDisplay}</p>
                <label class="form-label">How long did this actually take? (hours)</label>
                <input type="number" id="dashCompleteHours" class="form-input" min="0" step="0.5" placeholder="e.g. 3.5" style="margin-bottom:var(--space-4)">
                <div style="display:flex;gap:var(--space-3)">
                    <button class="btn btn-primary" id="dashCompleteConfirm" style="flex:1">Mark Complete</button>
                    <button class="btn btn-ghost" id="dashCompleteCancel" style="flex:1">Cancel</button>
                </div>
            </div>
        </div>
    `;
    document.body.appendChild(modal);
    requestAnimationFrame(() => modal.classList.add('open'));

    // Close
    const close = () => modal.remove();
    modal.querySelector('#dashCompleteClose').addEventListener('click', close);
    modal.querySelector('#dashCompleteCancel').addEventListener('click', close);
    modal.addEventListener('click', (e) => { if (e.target === modal) close(); });

    // Complete
    modal.querySelector('#dashCompleteConfirm').addEventListener('click', async () => {
        const actualHours = parseFloat(document.getElementById('dashCompleteHours').value) || null;
        try {
            const result = await api.completeTask(taskId, { actualHours });
            close();
            showToast('Task completed!', 'success');

            // Machine Learning hint: if actual hours deviate >30% from estimate
            if (actualHours && estHours && parseFloat(estHours) > 0) {
                const ratio = actualHours / parseFloat(estHours);
                if (ratio > 1.3 || ratio < 0.7) {
                    const direction = ratio > 1 ? 'longer' : 'shorter';
                    showToast(`Tip: This task took ${direction} than estimated (${ratio.toFixed(1)}x). Future estimates will improve.`, 'info');
                }
            }

            // Show course accuracy feedback from Machine Learning stats
            if (result?.mlStats?.sampleSize >= 3) {
                const bias = result.mlStats.estimationBias;
                if (bias && bias !== 'accurate') {
                    const biasMsg = bias === 'underestimate'
                        ? 'You tend to underestimate tasks in this course.'
                        : 'You tend to overestimate tasks in this course.';
                    setTimeout(() => showToast(`${biasMsg} Avg ratio: ${result.mlStats.courseAvgRatio.toFixed(2)}x (${result.mlStats.sampleSize} tasks)`, 'info'), 1000);
                }
            }

            // Refresh dashboard
            const freshData = await api.getDashboard();
            renderProgress(freshData);
            renderAlerts(freshData);
            renderStats(freshData);
            renderWorkload(freshData);
            renderRelocationSuggestions(freshData);
            renderSuggestion(freshData);
            renderReview(freshData);
            renderOverdue(freshData);
        } catch (err) {
            showToast('Failed to complete task', 'error');
        }
    });
}

/* ---- Section: Overdue Tasks ---- */
function renderOverdue(data) {
    const el = document.getElementById('dashOverdue');
    if (!el) return;

    const tasks = data.overdueTasks || [];
    if (!tasks.length) {
        el.innerHTML = '';
        return;
    }

    const cards = tasks.map(t => {
        const due = t.dueDate ? new Date(t.dueDate) : null;
        const daysOverdue = due ? Math.ceil((Date.now() - due.getTime()) / (1000 * 60 * 60 * 24)) : 0;
        const priorityClass = (t.priority || 'medium').toLowerCase();

        return `
            <div class="dash-task-card dash-task-card--overdue">
                <div class="dash-task-card__body">
                    <div class="dash-task-card__top">
                        <span class="dash-task-card__title">${escapeHtml(t.title)}</span>
                        <span class="dash-task-card__priority dash-task-card__priority--${priorityClass}">${escapeHtml(t.priority) || 'Medium'}</span>
                    </div>
                    <div class="dash-task-card__bottom">
                        <span class="dash-task-card__status dash-task-card__status--overdue">
                            ${daysOverdue} day${daysOverdue !== 1 ? 's' : ''} overdue
                        </span>
                        <span class="dash-task-card__course">${escapeHtml(t.courseName)}</span>
                    </div>
                </div>
                <div class="dash-task-card__actions">
                    <button class="btn btn-sm btn-primary dash-complete-overdue-btn" data-task-id="${t.taskId}" data-title="${escapeHtml(t.title)}" data-est-hours="${t.estimatedHours || ''}">Mark Complete</button>
                    <button class="btn btn-sm btn-ghost dash-reschedule-btn" data-task-id="${t.taskId}">Reschedule</button>
                </div>
            </div>
        `;
    }).join('');

    el.innerHTML = `
        <div class="dash-overdue__header">
            <h2 class="dash-review__title">Overdue Tasks</h2>
            <span class="dash-review__badge dash-review__badge--danger">${tasks.length}</span>
        </div>
        <div class="dash-review__list">${cards}</div>
    `;

    // Bind Mark Complete buttons
    el.querySelectorAll('.dash-complete-overdue-btn').forEach(btn => {
        btn.addEventListener('click', () => {
            const taskId = parseInt(btn.dataset.taskId);
            const title = btn.dataset.title;
            const estHours = btn.dataset.estHours;
            showCompletionModal(taskId, title, estHours);
        });
    });

    // Bind Reschedule buttons
    el.querySelectorAll('.dash-reschedule-btn').forEach(btn => {
        btn.addEventListener('click', async () => {
            const taskId = btn.dataset.taskId;
            try {
                btn.disabled = true;
                btn.textContent = 'Scheduling...';
                await api.runScheduling();
                showToast('Scheduling updated!', 'success');
                const freshData = await api.getDashboard();
                renderProgress(freshData);
                renderAlerts(freshData);
                renderStats(freshData);
                renderWorkload(freshData);
                renderRelocationSuggestions(freshData);
                renderSuggestion(freshData);
                renderReview(freshData);
                renderOverdue(freshData);
            } catch (err) {
                showToast('Failed to reschedule', 'error');
                btn.disabled = false;
                btn.textContent = 'Reschedule';
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
    class:    { bg: '#E3EEF9', border: '#4A90D9', text: '#2D5A8A' },
    task:     { bg: '#FFF3E0', border: '#F28D35', text: '#A06020' },
    work:     { bg: '#F3E5F5', border: '#9B76FF', text: '#5E3DBF' },
    personal: { bg: '#E6F5EE', border: '#43B88C', text: '#2A7A5A' },
    exam:     { bg: '#FCE4EC', border: '#E84B6A', text: '#A03050' },
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

    // Determine time range based on events (default 8-17)
    let startSlot = 8;
    let endSlot = 17;
    events.forEach(e => {
        const fh = new Date(e.from).getHours();
        const th = new Date(e.to).getHours() + (new Date(e.to).getMinutes() > 0 ? 1 : 0);
        if (fh < startSlot) startSlot = Math.max(0, fh);
        if (th > endSlot) endSlot = Math.min(23, th);
    });
    const totalHours = endSlot - startSlot;
    const cellHeight = 40; // px per hour (compact)

    // Header
    const fmt = (d) => d.toLocaleDateString('en', { month: 'short', day: 'numeric' });
    const rangeLabel = `Next 3 Days`;

    let html = '<div class="mini-cal">';
    html += '<div class="mini-cal__header">';
    html += '<div class="mini-cal__header-left">';
    html += `<span class="mini-cal__title">${rangeLabel}</span>`;
    html += '<div class="mini-cal__legend-inline">';
    html += '<span class="legend-dot" style="background:#4A90D9" title="Classes"></span>';
    html += '<span class="legend-dot" style="background:#F28D35" title="Tasks"></span>';
    html += '<span class="legend-dot" style="background:#9B76FF" title="Work"></span>';
    html += '<span class="legend-dot" style="background:#43B88C" title="Personal"></span>';
    html += '<span class="legend-dot" style="background:#E84B6A" title="Exams"></span>';
    html += '</div></div>';
    html += '<div class="mini-cal__header-right">';
    html += '<div class="mini-cal__nav">';
    html += '<button id="miniCalPrev">&larr;</button>';
    html += '<button id="miniCalNext">&rarr;</button>';
    html += '</div>';
    html += `<a href="${BASE_PATH}/Pages/Calendar.html" class="mini-cal__link">Full Calendar</a>`;
    html += '<button id="miniCalAdd" class="mini-cal__add" title="Add event">+</button>';
    html += '</div></div>';

    // Time-grid
    html += '<div class="mini-cal__grid">';

    // Time labels column
    html += '<div class="mini-cal__time-col">';
    html += '<div class="mini-cal__day-header mini-cal__time-header"><span class="mini-cal__day-name" style="visibility:hidden">&nbsp;</span><span class="mini-cal__day-num" style="visibility:hidden">&nbsp;</span></div>'; // invisible placeholder to match day-header height
    for (let h = startSlot; h < endSlot; h++) {
        html += `<div class="mini-cal__time-label" style="height:${cellHeight}px">${h.toString().padStart(2, '0')}:00</div>`;
    }
    html += '</div>';

    // Day columns
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

        html += `<div class="mini-cal__day-col" data-date="${dateStr}">`;
        html += `<div class="mini-cal__day-header ${isToday ? 'mini-cal__day-header--today' : ''}">`;
        html += `<span class="mini-cal__day-name">${dayName}</span>`;
        html += `<span class="mini-cal__day-num ${isToday ? 'today' : ''}">${day.getDate()}</span>`;
        html += '</div>';

        // Day body with hour cells + positioned events
        html += `<div class="mini-cal__day-body" style="height:${totalHours * cellHeight}px">`;

        // Hour grid lines
        for (let h = startSlot; h < endSlot; h++) {
            html += `<div class="mini-cal__cell" style="height:${cellHeight}px"></div>`;
        }

        // Positioned events
        dayEvents.forEach(e => {
            const fromTime = new Date(e.from);
            const toTime = new Date(e.to);
            const startHour = fromTime.getHours() + fromTime.getMinutes() / 60;
            const endHour = toTime.getHours() + toTime.getMinutes() / 60;
            const clampedStart = Math.max(startHour, startSlot);
            const clampedEnd = Math.min(endHour, endSlot);
            if (clampedEnd <= clampedStart) return; // skip invisible events
            const top = (clampedStart - startSlot) * cellHeight;
            const height = Math.max(18, (clampedEnd - clampedStart) * cellHeight);
            const colors = MINI_CAL_COLORS[e.eventType] || MINI_CAL_COLORS.personal;
            const label = e.courseName || e.taskTitle || e.workPlace || e.description || e.type || 'Event';
            const timeStr = `${fromTime.getHours().toString().padStart(2, '0')}:${fromTime.getMinutes().toString().padStart(2, '0')}`;

            html += `<div class="mini-cal__event" style="top:${top}px;height:${height}px;background:${colors.bg};border-left:3px solid ${colors.border};color:${colors.text}">`;
            html += `<span class="mini-cal__event-time">${timeStr}</span> ${label}`;
            html += '</div>';
        });

        html += '</div></div>'; // close day-body, day-col
    });

    html += '</div>'; // close grid
    html += '</div>'; // close mini-cal

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
        window.location.href = `${BASE_PATH}/Pages/Calendar.html?date=${dateStr}&add=1`;
    });

    // Day header click → navigate to calendar page at that date
    el.querySelectorAll('.mini-cal__day-col').forEach(col => {
        col.querySelector('.mini-cal__day-header')?.addEventListener('click', () => {
            const dateStr = col.dataset.date;
            if (dateStr) {
                window.location.href = `${BASE_PATH}/Pages/Calendar.html?date=${dateStr}`;
            }
        });
    });
}
