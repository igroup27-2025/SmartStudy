import { api } from './api.js';
import { showToast } from './modals.js';
import { getUser, saveAuth, logout } from './auth.js';

export async function initSettings() {
    try {
        const [profile, schedPrefs] = await Promise.all([
            api.getProfile(),
            api.getSchedulingPrefs().catch(() => null)
        ]);
        renderProfile(profile);
        renderNotifications(profile.notificationSettings);
        if (schedPrefs) renderSchedulingPrefs(schedPrefs);
        setupSave();
        loadConstraints();
    } catch (err) {
        showToast('Failed to load settings', 'error');
    }

    document.getElementById('logoutSettingsBtn')?.addEventListener('click', logout);
}

function renderProfile(profile) {
    document.getElementById('profileEmail').textContent = profile.email;
    document.getElementById('profileFirstName').value = profile.firstName;
    document.getElementById('profileLastName').value = profile.lastName;
}

function renderNotifications(settings) {
    if (!settings) return;
    document.getElementById('notifyBeforeTask').checked = settings.notifyBeforeTask;
    document.getElementById('dailyMorningSummary').checked = settings.dailyMorningSummary;
    document.getElementById('weeklyPlanReminder').checked = settings.weeklyPlanReminder;
    document.getElementById('enablePushNotification').checked = settings.enablePushNotification;

    // Quiet hours
    if (settings.quietHoursStart) {
        document.getElementById('quietHoursStart').value = settings.quietHoursStart;
    }
    if (settings.quietHoursEnd) {
        document.getElementById('quietHoursEnd').value = settings.quietHoursEnd;
    }
}

function renderSchedulingPrefs(prefs) {
    const maxDaily = document.getElementById('settMaxDaily');
    const maxDailyVal = document.getElementById('settMaxDailyValue');
    const sleep = document.getElementById('settSleepHours');
    const sleepVal = document.getElementById('settSleepHoursValue');

    if (maxDaily) {
        maxDaily.value = prefs.maxDailyStudyHours;
        if (maxDailyVal) maxDailyVal.textContent = `${prefs.maxDailyStudyHours}h`;
        maxDaily.addEventListener('input', () => {
            if (maxDailyVal) maxDailyVal.textContent = `${maxDaily.value}h`;
        });
    }

    if (sleep) {
        sleep.value = prefs.sleepHoursPerDay;
        if (sleepVal) sleepVal.textContent = `${prefs.sleepHoursPerDay}h`;
        sleep.addEventListener('input', () => {
            if (sleepVal) sleepVal.textContent = `${sleep.value}h`;
        });
    }

    const maxCont = document.getElementById('settMaxContinuous');
    if (maxCont) maxCont.value = prefs.maxContinuousMinutes;

    const dayStart = document.getElementById('settDayStart');
    if (dayStart) dayStart.value = prefs.dayStartHour;

    const dayEnd = document.getElementById('settDayEnd');
    if (dayEnd) dayEnd.value = prefs.dayEndHour;

    // Break duration
    const breakDur = document.getElementById('settBreakDuration');
    if (breakDur) breakDur.value = prefs.breakDurationMinutes ?? 15;

    // Max daily total
    const maxTotal = document.getElementById('settMaxDailyTotal');
    const maxTotalVal = document.getElementById('settMaxDailyTotalValue');
    if (maxTotal) {
        maxTotal.value = prefs.maxDailyTotalHours ?? 14;
        if (maxTotalVal) maxTotalVal.textContent = `${maxTotal.value}h`;
        maxTotal.addEventListener('input', () => {
            if (maxTotalVal) maxTotalVal.textContent = `${maxTotal.value}h`;
        });
    }

    // Default task hours
    const defaultHours = document.getElementById('settDefaultTaskHours');
    if (defaultHours) defaultHours.value = prefs.defaultTaskEstimatedHours ?? 4;

    // Exam prep
    const examPrepH = document.getElementById('settExamPrepHours');
    if (examPrepH) examPrepH.value = prefs.examPrepHoursPerDay ?? 5;
    const examPrepD = document.getElementById('settExamPrepDays');
    if (examPrepD) examPrepD.value = prefs.examPrepDays ?? 3;

    // Lunch break
    const lunchEnabled = document.getElementById('settLunchEnabled');
    const lunchRow = document.getElementById('settLunchTimeRow');
    const hasLunch = !!prefs.lunchBreakStart;
    if (lunchEnabled) lunchEnabled.checked = hasLunch;
    if (lunchRow) lunchRow.style.display = hasLunch ? '' : 'none';

    if (prefs.lunchBreakStart) document.getElementById('settLunchStart').value = prefs.lunchBreakStart;
    if (prefs.lunchBreakEnd) document.getElementById('settLunchEnd').value = prefs.lunchBreakEnd;

    lunchEnabled?.addEventListener('change', () => {
        if (lunchRow) lunchRow.style.display = lunchEnabled.checked ? '' : 'none';
    });
}

function setupSave() {
    document.getElementById('profileForm')?.addEventListener('submit', async (e) => {
        e.preventDefault();
        try {
            const data = {
                firstName: document.getElementById('profileFirstName').value,
                lastName: document.getElementById('profileLastName').value,
            };
            await api.updateProfile(data);

            // Update local storage
            const user = getUser();
            if (user) {
                user.firstName = data.firstName;
                user.lastName = data.lastName;
                localStorage.setItem('smartstudy_user', JSON.stringify(user));
            }

            showToast('Profile updated');
        } catch (err) {
            showToast('Failed to update profile', 'error');
        }
    });

    document.getElementById('notificationForm')?.addEventListener('submit', async (e) => {
        e.preventDefault();
        try {
            const data = {
                notifyBeforeTask: document.getElementById('notifyBeforeTask').checked,
                dailyMorningSummary: document.getElementById('dailyMorningSummary').checked,
                weeklyPlanReminder: document.getElementById('weeklyPlanReminder').checked,
                enablePushNotification: document.getElementById('enablePushNotification').checked,
                quietHoursStart: document.getElementById('quietHoursStart').value || null,
                quietHoursEnd: document.getElementById('quietHoursEnd').value || null,
            };
            await api.updateNotifications(data);
            showToast('Notifications updated');
        } catch (err) {
            showToast('Failed to update notifications', 'error');
        }
    });

    document.getElementById('schedulingForm')?.addEventListener('submit', async (e) => {
        e.preventDefault();
        try {
            const lunchEnabled = document.getElementById('settLunchEnabled')?.checked;
            const data = {
                maxDailyStudyHours: parseFloat(document.getElementById('settMaxDaily').value),
                maxContinuousMinutes: parseInt(document.getElementById('settMaxContinuous').value),
                dayStartHour: parseInt(document.getElementById('settDayStart').value),
                dayEndHour: parseInt(document.getElementById('settDayEnd').value),
                sleepHoursPerDay: parseFloat(document.getElementById('settSleepHours').value),
                breakDurationMinutes: parseInt(document.getElementById('settBreakDuration')?.value) || 15,
                maxDailyTotalHours: parseFloat(document.getElementById('settMaxDailyTotal')?.value) || 14,
                defaultTaskEstimatedHours: parseFloat(document.getElementById('settDefaultTaskHours')?.value) || 4,
                examPrepHoursPerDay: parseFloat(document.getElementById('settExamPrepHours')?.value) || 5,
                examPrepDays: parseInt(document.getElementById('settExamPrepDays')?.value) || 3,
                lunchBreakStart: lunchEnabled ? (document.getElementById('settLunchStart').value || null) : null,
                lunchBreakEnd: lunchEnabled ? (document.getElementById('settLunchEnd').value || null) : null,
            };
            await api.updateSchedulingPrefs(data);

            // Trigger rescheduling
            await api.runScheduling();

            showToast('Scheduling preferences updated');
        } catch (err) {
            showToast('Failed to update scheduling preferences', 'error');
        }
    });
}

async function loadConstraints() {
    const el = document.getElementById('constraintsList');
    if (!el) return;

    try {
        const now = new Date();
        const from = new Date(now);
        from.setDate(from.getDate() - 7);
        const to = new Date(now);
        to.setDate(to.getDate() + 30);

        const events = await api.getEvents(from, to);
        // Filter to recurring work/personal events (fixed constraints)
        const seen = new Set();
        const constraints = events.filter(e => {
            if (!e.recurring) return false;
            if (e.eventType !== 'work' && e.eventType !== 'personal') return false;
            // Deduplicate by eventId
            if (seen.has(e.eventId)) return false;
            seen.add(e.eventId);
            return true;
        });

        if (!constraints.length) {
            el.innerHTML = '<p class="text-muted">No fixed constraints set. Add recurring work or personal events from the Calendar.</p>';
            return;
        }

        el.innerHTML = constraints.map(c => {
            const from = new Date(c.from);
            const to = new Date(c.to);
            const dayName = from.toLocaleDateString('en', { weekday: 'long' });
            const timeStr = `${pad(from.getHours())}:${pad(from.getMinutes())} - ${pad(to.getHours())}:${pad(to.getMinutes())}`;
            const label = c.workPlace || c.description || c.type || 'Constraint';

            return `
                <div class="constraint-item">
                    <div class="constraint-item__info">
                        <span class="constraint-item__label">${label}</span>
                        <span class="constraint-item__time">${dayName} ${timeStr}</span>
                    </div>
                    <button class="btn btn-ghost btn-sm constraint-delete" data-id="${c.eventId}" title="Remove">&#128465;</button>
                </div>
            `;
        }).join('');

        el.querySelectorAll('.constraint-delete').forEach(btn => {
            btn.addEventListener('click', async () => {
                try {
                    await api.deleteEvent(parseInt(btn.dataset.id));
                    showToast('Constraint removed');
                    loadConstraints();
                } catch {
                    showToast('Failed to remove constraint', 'error');
                }
            });
        });
    } catch {
        el.innerHTML = '<p class="text-muted">Failed to load constraints.</p>';
    }
}

function pad(n) { return String(n).padStart(2, '0'); }
