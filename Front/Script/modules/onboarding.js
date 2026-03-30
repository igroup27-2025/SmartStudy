import { api } from './api.js';
import { BASE_PATH, API_BASE_PATH } from './config.js';

const STORE_KEY = 'smartstudy_onboarding';

function loadData() {
    try { return JSON.parse(sessionStorage.getItem(STORE_KEY)) || {}; }
    catch { return {}; }
}

function saveData(data) {
    const existing = loadData();
    sessionStorage.setItem(STORE_KEY, JSON.stringify({ ...existing, ...data }));
}

export function initOnboarding() {
    const body = document.body;
    const step = parseInt(body.dataset.step || '1');

    // Update step indicator
    document.querySelectorAll('.onboarding-dot').forEach((dot, i) => {
        if (i < step) dot.classList.add('active');
        if (i === step - 1) dot.classList.add('current');
    });

    // Update progress bar
    const progress = document.getElementById('onboardingProgress');
    if (progress) {
        progress.style.width = `${(step / 4) * 100}%`;
    }

    // Init step-specific logic
    if (step === 2) initStep2();
    if (step === 3) initStep3();
    if (step === 4) initStep4();

    // Navigation buttons
    document.getElementById('onboardingNext')?.addEventListener('click', async () => {
        if (step === 2) collectStep2();
        if (step === 3) collectStep3();

        if (step < 4) {
            window.location.href = `${BASE_PATH}/Pages/Onboarding${step + 1}.html`;
        } else {
            await finishOnboarding();
        }
    });

    document.getElementById('onboardingSkip')?.addEventListener('click', async () => {
        // Save defaults
        sessionStorage.removeItem(STORE_KEY);
        await finishOnboarding();
    });

    document.getElementById('onboardingBack')?.addEventListener('click', () => {
        // Save current step data before going back
        if (step === 2) collectStep2();
        if (step === 3) collectStep3();

        if (step > 1) {
            window.location.href = `${BASE_PATH}/Pages/Onboarding${step - 1}.html`;
        }
    });
}

/* ---- Step 2: Study Preferences ---- */
function initStep2() {
    const data = loadData();

    // Range sliders - live display
    const maxDaily = document.getElementById('maxDailyStudy');
    const maxDailyVal = document.getElementById('maxDailyStudyValue');
    const sleep = document.getElementById('sleepHours');
    const sleepVal = document.getElementById('sleepHoursValue');

    if (maxDaily && maxDailyVal) {
        if (data.maxDailyStudyHours) maxDaily.value = data.maxDailyStudyHours;
        maxDailyVal.textContent = `${maxDaily.value}h`;
        maxDaily.addEventListener('input', () => {
            maxDailyVal.textContent = `${maxDaily.value}h`;
        });
    }

    if (sleep && sleepVal) {
        if (data.sleepHoursPerDay) sleep.value = data.sleepHoursPerDay;
        sleepVal.textContent = `${sleep.value}h`;
        sleep.addEventListener('input', () => {
            sleepVal.textContent = `${sleep.value}h`;
        });
    }

    // Max daily total slider
    const maxTotal = document.getElementById('maxDailyTotal');
    const maxTotalVal = document.getElementById('maxDailyTotalValue');
    if (maxTotal && maxTotalVal) {
        if (data.maxDailyTotalHours) maxTotal.value = data.maxDailyTotalHours;
        maxTotalVal.textContent = `${maxTotal.value}h`;
        maxTotal.addEventListener('input', () => {
            maxTotalVal.textContent = `${maxTotal.value}h`;
        });
    }

    // Restore saved values
    if (data.maxContinuousMinutes) setSelect('maxContinuous', data.maxContinuousMinutes);
    if (data.dayStartHour) setSelect('dayStart', data.dayStartHour);
    if (data.dayEndHour) setSelect('dayEnd', data.dayEndHour);
    if (data.breakDurationMinutes) setSelect('breakDuration', data.breakDurationMinutes);
    if (data.examPrepHoursPerDay) document.getElementById('examPrepHoursPerDay').value = data.examPrepHoursPerDay;
    if (data.examPrepDays) document.getElementById('examPrepDays').value = data.examPrepDays;

    // Lunch break toggle
    const lunchEnabled = document.getElementById('lunchEnabled');
    const lunchRow = document.getElementById('lunchTimeRow');
    if (data.lunchEnabled === false) {
        if (lunchEnabled) lunchEnabled.checked = false;
        if (lunchRow) lunchRow.style.display = 'none';
    }
    if (data.lunchStart) document.getElementById('lunchStart').value = data.lunchStart;
    if (data.lunchEnd) document.getElementById('lunchEnd').value = data.lunchEnd;

    lunchEnabled?.addEventListener('change', () => {
        if (lunchRow) lunchRow.style.display = lunchEnabled.checked ? '' : 'none';
    });
}

function collectStep2() {
    saveData({
        maxDailyStudyHours: parseFloat(document.getElementById('maxDailyStudy')?.value) || 6,
        maxContinuousMinutes: parseInt(document.getElementById('maxContinuous')?.value) || 90,
        dayStartHour: parseInt(document.getElementById('dayStart')?.value) || 8,
        dayEndHour: parseInt(document.getElementById('dayEnd')?.value) || 22,
        sleepHoursPerDay: parseFloat(document.getElementById('sleepHours')?.value) || 8,
        lunchEnabled: document.getElementById('lunchEnabled')?.checked ?? true,
        lunchStart: document.getElementById('lunchStart')?.value || '12:00',
        lunchEnd: document.getElementById('lunchEnd')?.value || '13:00',
        breakDurationMinutes: parseInt(document.getElementById('breakDuration')?.value) || 15,
        maxDailyTotalHours: parseFloat(document.getElementById('maxDailyTotal')?.value) || 14,
        examPrepHoursPerDay: parseFloat(document.getElementById('examPrepHoursPerDay')?.value) || 5,
        examPrepDays: parseInt(document.getElementById('examPrepDays')?.value) || 3,
    });
}

/* ---- Step 3: Notifications & Constraints ---- */
let constraints = [];

function initStep3() {
    const data = loadData();

    // Restore notification toggles
    if (data.notifyDeadline !== undefined) document.getElementById('notifyDeadline').checked = data.notifyDeadline;
    if (data.notifyMorning !== undefined) document.getElementById('notifyMorning').checked = data.notifyMorning;
    if (data.notifyWeekly !== undefined) document.getElementById('notifyWeekly').checked = data.notifyWeekly;
    if (data.quietStart) document.getElementById('quietStart').value = data.quietStart;
    if (data.quietEnd) document.getElementById('quietEnd').value = data.quietEnd;

    // Restore constraints
    constraints = data.constraints || [];
    renderConstraints();

    // Google Calendar connect
    if (data.googleCalConnected) {
        const btn = document.getElementById('connectGoogleCal');
        const status = document.getElementById('googleCalStatus');
        if (btn) btn.classList.add('hidden');
        if (status) status.classList.remove('hidden');
    }
    document.getElementById('connectGoogleCal')?.addEventListener('click', async () => {
        try {
            const syncStatus = await api.getCalendarSyncStatus();

            if (syncStatus.useComposio) {
                const callbackUrl = window.location.origin + API_BASE_PATH + '/api/calendar-sync/callback';
                const result = await api.connectGoogleCalendar(callbackUrl);
                if (result.redirectUrl) {
                    window.location.href = result.redirectUrl;
                    return;
                }
                alert('Failed to connect Google Calendar.');
                return;
            }

            const config = await api.getAuthConfig();
            if (!config.googleClientId || config.googleClientId === 'PLACEHOLDER_GOOGLE_CLIENT_ID') {
                alert('Google Calendar sync is not configured yet.');
                return;
            }
            const tokenClient = google.accounts.oauth2.initTokenClient({
                client_id: config.googleClientId,
                scope: 'https://www.googleapis.com/auth/calendar.readonly',
                callback: async (resp) => {
                    if (resp.error) return;
                    try {
                        await api.syncGoogleCalendar(resp.access_token);
                        const btn = document.getElementById('connectGoogleCal');
                        const status = document.getElementById('googleCalStatus');
                        if (btn) btn.classList.add('hidden');
                        if (status) status.classList.remove('hidden');
                        saveData({ googleCalConnected: true });
                    } catch (err) {
                        console.error('Google Calendar sync failed:', err);
                    }
                }
            });
            tokenClient.requestAccessToken();
        } catch (err) {
            console.error('Google Calendar connect error:', err);
        }
    });

    // Ruppinet connect (onboarding)
    document.getElementById('connectRuppinetOnboarding')?.addEventListener('click', () => {
        const form = document.getElementById('ruppinetOnboardingForm');
        const btn = document.getElementById('connectRuppinetOnboarding');
        if (form) form.style.display = '';
        if (btn) btn.classList.add('hidden');
    });
    document.getElementById('ruppinetOnbCancel')?.addEventListener('click', () => {
        const form = document.getElementById('ruppinetOnboardingForm');
        const btn = document.getElementById('connectRuppinetOnboarding');
        if (form) form.style.display = 'none';
        if (btn) btn.classList.remove('hidden');
    });
    document.getElementById('ruppinetOnbSubmit')?.addEventListener('click', async () => {
        const idVal = document.getElementById('ruppinetOnbId')?.value?.trim();
        const passVal = document.getElementById('ruppinetOnbPass')?.value;
        if (!idVal || !passVal) { alert('Please fill in both fields'); return; }
        const submitBtn = document.getElementById('ruppinetOnbSubmit');
        submitBtn.disabled = true;
        submitBtn.textContent = 'Connecting...';
        try {
            await api.connectRuppinet(idVal, passVal);
            const form = document.getElementById('ruppinetOnboardingForm');
            const connectBtn = document.getElementById('connectRuppinetOnboarding');
            const status = document.getElementById('ruppinetOnboardingStatus');
            if (form) form.style.display = 'none';
            if (connectBtn) connectBtn.classList.add('hidden');
            if (status) status.classList.remove('hidden');
        } catch (err) {
            alert(err.message || 'Connection failed');
        } finally {
            submitBtn.disabled = false;
            submitBtn.textContent = 'Connect & Sync';
        }
    });

    // Add constraint button
    document.getElementById('addConstraintBtn')?.addEventListener('click', () => {
        document.getElementById('constraintForm').style.display = 'block';
        document.getElementById('addConstraintBtn').style.display = 'none';
    });

    document.getElementById('cancelConstraintBtn')?.addEventListener('click', () => {
        document.getElementById('constraintForm').style.display = 'none';
        document.getElementById('addConstraintBtn').style.display = '';
        resetConstraintForm();
    });

    document.getElementById('saveConstraintBtn')?.addEventListener('click', () => {
        const name = document.getElementById('constraintName')?.value?.trim();
        const type = document.getElementById('constraintType')?.value || 'work';
        const start = document.getElementById('constraintStart')?.value;
        const end = document.getElementById('constraintEnd')?.value;
        const days = [];
        document.querySelectorAll('.onboarding-day-picker input:checked').forEach(cb => {
            days.push(parseInt(cb.value));
        });

        if (!name || !start || !end || !days.length) return;

        constraints.push({ type, name, days, startTime: start, endTime: end });
        renderConstraints();

        document.getElementById('constraintForm').style.display = 'none';
        document.getElementById('addConstraintBtn').style.display = '';
        resetConstraintForm();
    });
}

function resetConstraintForm() {
    document.getElementById('constraintName').value = '';
    document.getElementById('constraintStart').value = '17:00';
    document.getElementById('constraintEnd').value = '20:00';
    document.querySelectorAll('.onboarding-day-picker input').forEach(cb => cb.checked = false);
}

function renderConstraints() {
    const el = document.getElementById('constraintList');
    if (!el) return;

    const dayNames = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];

    el.innerHTML = constraints.map((c, i) => {
        const daysStr = c.days.map(d => dayNames[d]).join(', ');
        return `<div class="onboarding-constraint-item">
            <span>${c.name} (${c.type}) - ${daysStr} ${c.startTime}-${c.endTime}</span>
            <button type="button" class="btn btn-ghost btn-sm constraint-remove" data-index="${i}">&times;</button>
        </div>`;
    }).join('');

    el.querySelectorAll('.constraint-remove').forEach(btn => {
        btn.addEventListener('click', () => {
            constraints.splice(parseInt(btn.dataset.index), 1);
            renderConstraints();
        });
    });
}

function collectStep3() {
    saveData({
        notifyDeadline: document.getElementById('notifyDeadline')?.checked ?? true,
        notifyMorning: document.getElementById('notifyMorning')?.checked ?? false,
        notifyWeekly: document.getElementById('notifyWeekly')?.checked ?? false,
        quietStart: document.getElementById('quietStart')?.value || '22:00',
        quietEnd: document.getElementById('quietEnd')?.value || '07:00',
        constraints: constraints,
    });
}

/* ---- Step 4: Summary ---- */
function initStep4() {
    const data = loadData();

    setText('summaryStudy', `Max ${data.maxDailyStudyHours || 6}h study/day, ${data.maxDailyTotalHours || 14}h total/day, ${data.maxContinuousMinutes || 90}min continuous, ${data.breakDurationMinutes || 15}min breaks`);

    const startHour = data.dayStartHour || 8;
    const endHour = data.dayEndHour || 22;
    setText('summaryWindow', `Study window: ${formatHour(startHour)} - ${formatHour(endHour)}`);

    setText('summarySleep', `Sleep: ${data.sleepHoursPerDay || 8}h per day`);

    if (data.lunchEnabled !== false) {
        setText('summaryLunch', `Lunch break: ${data.lunchStart || '12:00'} - ${data.lunchEnd || '13:00'}`);
    } else {
        setText('summaryLunch', 'Lunch break: Off');
    }

    const notifs = [];
    if (data.notifyDeadline !== false) notifs.push('Deadlines');
    if (data.notifyMorning) notifs.push('Morning summary');
    if (data.notifyWeekly) notifs.push('Weekly plan');
    setText('summaryNotif', `Notifications: ${notifs.length ? notifs.join(', ') : 'None'}`);

    setText('summaryExamPrep', `Exam prep: ${data.examPrepHoursPerDay || 5}h/day, ${data.examPrepDays || 3} days before exam`);

    const cCount = (data.constraints || []).length;
    setText('summaryConstraints', `Recurring constraints: ${cCount || 'None'}`);

    setText('summaryExternal', `External systems: ${data.googleCalConnected ? 'Google Calendar connected' : 'None connected'}`);
}

/* ---- Finish & Save ---- */
async function finishOnboarding() {
    const data = loadData();

    const payload = {
        schedulingPreferences: {
            maxDailyStudyHours: data.maxDailyStudyHours || 6,
            maxContinuousMinutes: data.maxContinuousMinutes || 90,
            dayStartHour: data.dayStartHour || 8,
            dayEndHour: data.dayEndHour || 22,
            sleepHoursPerDay: data.sleepHoursPerDay || 8,
            lunchBreakStart: data.lunchEnabled !== false ? (data.lunchStart || '12:00') : null,
            lunchBreakEnd: data.lunchEnabled !== false ? (data.lunchEnd || '13:00') : null,
            breakDurationMinutes: data.breakDurationMinutes || 15,
            defaultTaskEstimatedHours: 4.0,
            maxDailyTotalHours: data.maxDailyTotalHours || 14,
            examPrepHoursPerDay: data.examPrepHoursPerDay || 5,
            examPrepDays: data.examPrepDays || 3,
        },
        notificationSettings: {
            notifyBeforeTask: data.notifyDeadline !== false,
            dailyMorningSummary: data.notifyMorning || false,
            weeklyPlanReminder: data.notifyWeekly || false,
            enablePushNotification: false,
            quietHoursStart: data.quietStart || '22:00',
            quietHoursEnd: data.quietEnd || '07:00',
        },
        constraints: data.constraints || [],
    };

    try {
        await api.saveOnboarding(payload);
    } catch (err) {
        console.error('Onboarding save failed:', err);
    }

    sessionStorage.removeItem(STORE_KEY);
    window.location.href = BASE_PATH + '/Pages/Dashboard.html';
}

/* ---- Helpers ---- */
function setSelect(id, value) {
    const el = document.getElementById(id);
    if (el) el.value = value;
}

function setText(id, text) {
    const el = document.getElementById(id);
    if (el) el.textContent = text;
}

function formatHour(h) {
    if (h === 0) return '12:00 AM';
    if (h < 12) return `${h}:00 AM`;
    if (h === 12) return '12:00 PM';
    return `${h - 12}:00 PM`;
}
