import { api } from './api.js';
import { showToast } from './modals.js';
import { getUser, saveAuth } from './auth.js';
import { API_BASE_PATH } from './config.js';

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
        loadIntegrations();
    } catch (err) {
        showToast('Failed to load settings', 'error');
    }
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

/* ── Integrations ── */

let integrationListenersSetup = false;

async function loadIntegrations() {
    const statusEl = document.getElementById('gcalStatus');
    const connectBtn = document.getElementById('gcalConnectBtn');
    const connectedActions = document.getElementById('gcalConnectedActions');
    if (!statusEl) return;

    // Handle callback from Composio OAuth redirect
    const urlParams = new URLSearchParams(window.location.search);
    const gcalParam = urlParams.get('gcal');
    if (gcalParam) {
        if (gcalParam === 'success') {
            showToast('Google Calendar connected successfully!');
        } else if (gcalParam === 'failed') {
            showToast('Google Calendar connection failed. Please try again.', 'error');
        }
        const cleanUrl = window.location.pathname + window.location.hash;
        window.history.replaceState({}, '', cleanUrl);
    }

    try {
        const syncStatus = await api.getCalendarSyncStatus();

        if (syncStatus && syncStatus.isConnected) {
            const lastSyncText = syncStatus.lastSync
                ? new Date(syncStatus.lastSync).toLocaleString()
                : 'Never';
            statusEl.textContent = `Connected — Last synced: ${lastSyncText} (${syncStatus.syncedEventCount || 0} events)`;
            if (connectBtn) connectBtn.style.display = 'none';
            if (connectedActions) connectedActions.style.display = 'flex';
        } else if (syncStatus && syncStatus.isEnabled) {
            statusEl.textContent = 'Not connected';
            if (connectBtn) connectBtn.style.display = '';
            if (connectedActions) connectedActions.style.display = 'none';
        } else {
            statusEl.textContent = 'Integration not configured';
            if (connectBtn) connectBtn.style.display = 'none';
            if (connectedActions) connectedActions.style.display = 'none';
        }
    } catch {
        statusEl.textContent = 'Not connected';
        if (connectBtn) connectBtn.style.display = '';
        if (connectedActions) connectedActions.style.display = 'none';
    }

    if (integrationListenersSetup) return;
    integrationListenersSetup = true;

    // Connect button — initiate Composio OAuth flow
    connectBtn?.addEventListener('click', async () => {
        try {
            const syncStatus = await api.getCalendarSyncStatus();

            if (syncStatus.useComposio) {
                connectBtn.disabled = true;
                connectBtn.textContent = 'Connecting...';

                const callbackUrl = window.location.origin + API_BASE_PATH + '/api/calendar-sync/callback';
                const result = await api.connectGoogleCalendar(callbackUrl);

                if (result.redirectUrl) {
                    window.location.href = result.redirectUrl;
                } else {
                    showToast('Failed to get authentication URL', 'error');
                    connectBtn.disabled = false;
                    connectBtn.textContent = 'Connect Google Calendar';
                }
            } else {
                await connectViaGoogleIdentity();
            }
        } catch (err) {
            showToast(err.message || 'Failed to connect Google Calendar', 'error');
            connectBtn.disabled = false;
            connectBtn.textContent = 'Connect Google Calendar';
        }
    });

    // Sync Now button
    document.getElementById('gcalSyncBtn')?.addEventListener('click', async () => {
        const syncBtn = document.getElementById('gcalSyncBtn');
        try {
            const syncStatus = await api.getCalendarSyncStatus();

            if (syncStatus.useComposio) {
                syncBtn.disabled = true;
                syncBtn.textContent = 'Syncing...';

                await api.syncGoogleCalendar(null);
                showToast('Calendar synced!');
                loadIntegrations();

                syncBtn.disabled = false;
                syncBtn.textContent = 'Sync Now';
            } else {
                await syncViaGoogleIdentity();
            }
        } catch (err) {
            showToast(err.message || 'Sync failed', 'error');
            if (syncBtn) {
                syncBtn.disabled = false;
                syncBtn.textContent = 'Sync Now';
            }
        }
    });

    // Disconnect button
    document.getElementById('gcalDisconnectBtn')?.addEventListener('click', async () => {
        if (!confirm('Disconnect Google Calendar? All synced events will be removed.')) return;

        try {
            await api.disconnectGoogleCalendar();
            showToast('Google Calendar disconnected');
            loadIntegrations();
        } catch (err) {
            showToast(err.message || 'Failed to disconnect', 'error');
        }
    });
}

// Legacy: connect via Google Identity Services (when Composio is disabled)
async function connectViaGoogleIdentity() {
    const config = await api.getAuthConfig();
    if (!config.googleClientId || config.googleClientId === 'PLACEHOLDER_GOOGLE_CLIENT_ID') {
        showToast('Google Calendar integration is not configured yet', 'error');
        return;
    }

    if (!window.google?.accounts?.oauth2) {
        await new Promise((resolve, reject) => {
            const script = document.createElement('script');
            script.src = 'https://accounts.google.com/gsi/client';
            script.onload = resolve;
            script.onerror = reject;
            document.head.appendChild(script);
        });
    }

    const client = google.accounts.oauth2.initTokenClient({
        client_id: config.googleClientId,
        scope: 'https://www.googleapis.com/auth/calendar.readonly',
        callback: async (tokenResponse) => {
            if (tokenResponse.access_token) {
                try {
                    await api.syncGoogleCalendar(tokenResponse.access_token);
                    showToast('Google Calendar connected!');
                    loadIntegrations();
                } catch (err) {
                    showToast(err.message || 'Failed to sync', 'error');
                }
            }
        },
    });
    client.requestAccessToken();
}

// Legacy: sync via Google Identity Services
async function syncViaGoogleIdentity() {
    const config = await api.getAuthConfig();
    if (!config.googleClientId || config.googleClientId === 'PLACEHOLDER_GOOGLE_CLIENT_ID') {
        showToast('Google Calendar integration is not configured', 'error');
        return;
    }

    if (!window.google?.accounts?.oauth2) {
        await new Promise((resolve, reject) => {
            const script = document.createElement('script');
            script.src = 'https://accounts.google.com/gsi/client';
            script.onload = resolve;
            script.onerror = reject;
            document.head.appendChild(script);
        });
    }

    const client = google.accounts.oauth2.initTokenClient({
        client_id: config.googleClientId,
        scope: 'https://www.googleapis.com/auth/calendar.readonly',
        callback: async (tokenResponse) => {
            if (tokenResponse.access_token) {
                try {
                    await api.syncGoogleCalendar(tokenResponse.access_token);
                    showToast('Calendar synced!');
                    loadIntegrations();
                } catch (err) {
                    showToast(err.message || 'Sync failed', 'error');
                }
            }
        },
    });
    client.requestAccessToken();
}

