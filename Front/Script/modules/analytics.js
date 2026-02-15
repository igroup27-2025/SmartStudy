import { api } from './api.js';
import { showToast } from './modals.js';

export async function initAnalytics() {
    try {
        const [stress, weekly, tasks, courses] = await Promise.all([
            api.getStressScore(),
            api.getWeeklyStress(),
            api.getTasks(),
            api.getCourses()
        ]);
        renderStressOverview(stress);
        renderWeeklyChart(weekly);
        renderWorkloadByCourse(tasks, courses);
        renderTaskStats(tasks);
        renderEstimatedVsActual(tasks, courses);
        renderLearningInsights();
    } catch (err) {
        showToast('Failed to load analytics', 'error');
    }
}

function renderStressOverview(stress) {
    const el = document.getElementById('stressOverview');
    if (!el) return;

    const circumference = 2 * Math.PI * 54;
    const offset = circumference - (stress.score / 100) * circumference;

    el.innerHTML = `
        <div class="analytics-stress">
            <svg viewBox="0 0 120 120" width="160" height="160">
                <circle cx="60" cy="60" r="54" fill="none" stroke="#E2E8F0" stroke-width="8"/>
                <circle cx="60" cy="60" r="54" fill="none" stroke="${stress.color}" stroke-width="8"
                    stroke-dasharray="${circumference}" stroke-dashoffset="${offset}"
                    stroke-linecap="round" transform="rotate(-90 60 60)"/>
            </svg>
            <div class="stress-meter-value">
                <span class="stress-meter-number" style="color:${stress.color}">${Math.round(stress.score)}</span>
                <span class="stress-meter-label">${stress.level}</span>
            </div>
        </div>
        <div class="analytics-stress-info">
            <div class="stat-box"><span class="stat-box-value">${stress.requiredHours}h</span><span class="stat-box-label">Required Work</span></div>
            <div class="stat-box"><span class="stat-box-value">${stress.availableHours}h</span><span class="stat-box-label">Available Time</span></div>
        </div>
    `;
}

function renderWeeklyChart(weekly) {
    const el = document.getElementById('weeklyChart');
    if (!el) return;

    el.innerHTML = `
        <div class="bar-chart">
            ${weekly.map(d => `
                <div class="bar-chart-col">
                    <div class="bar-chart-bar-container">
                        <div class="bar-chart-bar" style="height:${(d.score / 100) * 100}%;background:${d.color}" title="${d.dayName}: ${d.score}%"></div>
                    </div>
                    <div class="bar-chart-label">${d.dayName}</div>
                    <div class="bar-chart-value">${Math.round(d.score)}%</div>
                </div>
            `).join('')}
        </div>
    `;
}

function renderWorkloadByCourse(tasks, courses) {
    const el = document.getElementById('workloadByCourse');
    if (!el) return;

    const courseColors = ['#4A90D9', '#F28D35', '#9B76FF', '#43B88C', '#E84B6A', '#54BFB5'];
    const totalHours = tasks.reduce((sum, t) => sum + (t.estimatedHours || 0), 0) || 1;

    el.innerHTML = courses.map((c, i) => {
        const courseHours = tasks
            .filter(t => t.courseId === c.courseId)
            .reduce((sum, t) => sum + (t.estimatedHours || 0), 0);
        const pct = Math.round((courseHours / totalHours) * 100);
        return `
            <div class="progress-item">
                <div class="progress-label">
                    <span>${c.courseName}</span>
                    <span>${courseHours}h (${pct}%)</span>
                </div>
                <div class="progress-bar-track">
                    <div class="progress-bar-fill" style="width:${pct}%;background:${courseColors[i % courseColors.length]}"></div>
                </div>
            </div>
        `;
    }).join('');
}

function renderTaskStats(tasks) {
    const el = document.getElementById('taskStats');
    if (!el) return;

    const total = tasks.length;
    const completed = tasks.filter(t => t.isCompleted).length;
    const pending = total - completed;
    const overdue = tasks.filter(t => !t.isCompleted && t.dueDate && new Date(t.dueDate) < new Date()).length;
    const completionRate = total > 0 ? Math.round((completed / total) * 100) : 0;

    el.innerHTML = `
        <div class="stat-boxes">
            <div class="stat-box"><span class="stat-box-value">${total}</span><span class="stat-box-label">Total Tasks</span></div>
            <div class="stat-box"><span class="stat-box-value" style="color:#27AE60">${completed}</span><span class="stat-box-label">Completed</span></div>
            <div class="stat-box"><span class="stat-box-value" style="color:#F28D35">${pending}</span><span class="stat-box-label">Pending</span></div>
            <div class="stat-box"><span class="stat-box-value" style="color:#E74C3C">${overdue}</span><span class="stat-box-label">Overdue</span></div>
        </div>
        <div class="progress-item" style="margin-top:1rem">
            <div class="progress-label">
                <span>Completion Rate</span>
                <span>${completionRate}%</span>
            </div>
            <div class="progress-bar-track">
                <div class="progress-bar-fill progress-bar-success" style="width:${completionRate}%"></div>
            </div>
        </div>
    `;
}

function renderEstimatedVsActual(tasks, courses) {
    const el = document.getElementById('estimatedVsActual');
    if (!el) return;

    // Only show completed tasks with both estimated and actual hours
    const completedWithHours = tasks.filter(t => t.isCompleted && t.estimatedHours && t.actualHours);

    if (!completedWithHours.length) {
        el.innerHTML = '<p class="text-muted">Complete tasks with actual hours to see comparison data.</p>';
        return;
    }

    // Group by course
    const courseColors = ['#4A90D9', '#F28D35', '#9B76FF', '#43B88C', '#E84B6A', '#54BFB5'];
    const courseMap = {};
    completedWithHours.forEach(t => {
        const key = t.courseId;
        if (!courseMap[key]) {
            courseMap[key] = { courseName: t.courseName || 'Unknown', estimated: 0, actual: 0 };
        }
        courseMap[key].estimated += t.estimatedHours;
        courseMap[key].actual += t.actualHours;
    });

    const entries = Object.values(courseMap);
    const maxHours = Math.max(...entries.flatMap(e => [e.estimated, e.actual]), 1);

    el.innerHTML = `
        <div class="est-vs-actual-chart">
            ${entries.map((e, i) => {
                const estPct = Math.round((e.estimated / maxHours) * 100);
                const actPct = Math.round((e.actual / maxHours) * 100);
                const color = courseColors[i % courseColors.length];
                const accuracy = e.estimated > 0 ? Math.round((e.actual / e.estimated) * 100) : 0;
                return `
                    <div class="est-vs-actual-row">
                        <span class="est-vs-actual-label">${e.courseName}</span>
                        <div class="est-vs-actual-bars">
                            <div class="est-vs-actual-bar">
                                <div class="est-vs-actual-fill est-vs-actual-fill--est" style="width:${estPct}%;background:${color};opacity:0.5"></div>
                                <span class="est-vs-actual-value">${e.estimated.toFixed(1)}h est</span>
                            </div>
                            <div class="est-vs-actual-bar">
                                <div class="est-vs-actual-fill est-vs-actual-fill--act" style="width:${actPct}%;background:${color}"></div>
                                <span class="est-vs-actual-value">${e.actual.toFixed(1)}h actual</span>
                            </div>
                        </div>
                        <span class="est-vs-actual-accuracy ${accuracy > 120 ? 'text-danger' : accuracy < 80 ? 'text-warning' : 'text-success'}">${accuracy}%</span>
                    </div>
                `;
            }).join('')}
        </div>
        <div class="est-vs-actual-legend">
            <span class="legend-item"><span class="legend-dot" style="background:#ccc;opacity:0.5"></span> Estimated</span>
            <span class="legend-item"><span class="legend-dot" style="background:#ccc"></span> Actual</span>
        </div>
    `;
}

async function renderLearningInsights() {
    const el = document.getElementById('learningInsights');
    if (!el) return;

    try {
        const insights = await api.getLearningInsights();

        if (!insights || !insights.length) {
            el.innerHTML = '<p class="text-muted">Complete more tasks with actual hours to see learning insights.</p>';
            return;
        }

        el.innerHTML = insights.map(i => {
            const accuracy = i.adjustmentFactor ? Math.round(i.adjustmentFactor * 100) : 100;
            const isOver = accuracy > 110;
            const isUnder = accuracy < 90;
            const barColor = isOver ? '#E74C3C' : isUnder ? '#F28D35' : '#27AE60';
            const label = isOver ? 'Takes longer than estimated' : isUnder ? 'Finishes faster than estimated' : 'On track';

            return `
                <div class="learning-insight-item">
                    <div class="learning-insight-header">
                        <span class="learning-insight-course">${i.courseName}</span>
                        <span class="learning-insight-factor" style="color:${barColor}">${accuracy}% ${label}</span>
                    </div>
                    <div class="progress-bar-track">
                        <div class="progress-bar-fill" style="width:${Math.min(accuracy, 200) / 2}%;background:${barColor}"></div>
                    </div>
                    <div class="learning-insight-meta">
                        <span>${i.completedTasks} tasks completed</span>
                        <span>Avg: ${i.avgEstimated?.toFixed(1) || 0}h est / ${i.avgActual?.toFixed(1) || 0}h actual</span>
                    </div>
                </div>
            `;
        }).join('');
    } catch {
        el.innerHTML = '<p class="text-muted">Failed to load learning insights.</p>';
    }
}
