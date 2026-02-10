import { api } from './api.js';
import { showToast } from './modals.js';
import { getUser, saveAuth, logout } from './auth.js';

export async function initSettings() {
    try {
        const profile = await api.getProfile();
        renderProfile(profile);
        renderNotifications(profile.notificationSettings);
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
