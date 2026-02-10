// SmartStudy main.js - Entry point
import { requireAuth } from './modules/auth.js';
import { initLayout } from './modules/layout.js';
import { initModals } from './modules/modals.js';

document.addEventListener('DOMContentLoaded', async () => {
    const body = document.body;
    const page = body.dataset.page;
    const layout = body.dataset.layout;

    // Onboarding pages don't need auth or layout
    if (layout === 'onboarding') {
        const { initOnboarding } = await import('./modules/onboarding.js');
        initOnboarding();
        return;
    }

    // Login page doesn't need layout
    if (page === 'login') {
        const { initLogin } = await import('./modules/login.js');
        initLogin();
        return;
    }

    // All other pages require auth
    if (!requireAuth()) return;

    // Initialize layout shell (sidebar + topbar)
    initLayout();
    initModals();

    // Initialize notifications (bell icon in topbar)
    const { initNotifications } = await import('./modules/notifications.js');
    initNotifications();

    // Initialize page-specific module
    switch (page) {
        case 'dashboard': {
            const { initDashboard } = await import('./modules/dashboard.js');
            initDashboard();
            break;
        }
        case 'tasks': {
            const { initTasks } = await import('./modules/tasks.js');
            initTasks();
            break;
        }
        case 'courses': {
            const { initCourses } = await import('./modules/courses.js');
            initCourses();
            break;
        }
        case 'exams': {
            const { initExams } = await import('./modules/exams.js');
            initExams();
            break;
        }
        case 'calendar': {
            const { initCalendar } = await import('./modules/calendar.js');
            initCalendar();
            break;
        }
        case 'analytics': {
            const { initAnalytics } = await import('./modules/analytics.js');
            initAnalytics();
            break;
        }
        case 'friends': {
            const { initFriends } = await import('./modules/friends.js');
            initFriends();
            break;
        }
        case 'settings': {
            const { initSettings } = await import('./modules/settings.js');
            initSettings();
            break;
        }
    }
});
