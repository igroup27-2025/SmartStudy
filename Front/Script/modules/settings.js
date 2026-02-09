import { api } from './api.js';
import { showToast } from './modals.js';
import { getUser, saveAuth, logout } from './auth.js';

export async function initSettings() {
    try {
        const profile = await api.getProfile();
        renderProfile(profile);
        renderNotifications(profile.notificationSettings);
        setupSave();
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
            };
            await api.updateNotifications(data);
            showToast('Notifications updated');
        } catch (err) {
            showToast('Failed to update notifications', 'error');
        }
    });
}
