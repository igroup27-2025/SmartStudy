// Layout module - injects sidebar + topbar shell around page content
import { getUser, logout } from './auth.js';
import { BASE_PATH } from './config.js';

const NAV = [
    { page: 'dashboard', label: 'Dashboard', href: BASE_PATH + '/Pages/Dashboard.html' },
    { page: 'tasks', label: 'Tasks', href: BASE_PATH + '/Pages/Tasks.html' },
    { page: 'calendar', label: 'Calendar', href: BASE_PATH + '/Pages/Calendar.html' },
    { page: 'courses', label: 'Courses', href: BASE_PATH + '/Pages/Courses.html' },
    { page: 'exams', label: 'Exams', href: BASE_PATH + '/Pages/Exams.html' },
    { page: 'analytics', label: 'Analytics', href: BASE_PATH + '/Pages/Analytics.html' },
    { page: 'friends', label: 'Friends', href: BASE_PATH + '/Pages/Friends.html' },
    { page: 'settings', label: 'Settings', href: BASE_PATH + '/Pages/Settings.html' },
];

export function initLayout() {
    const body = document.body;
    const currentPage = body.dataset.page;
    const user = getUser();

    const pageRoot = document.getElementById('pageRoot');
    if (!pageRoot) return;

    const pageContent = pageRoot.innerHTML;
    pageRoot.innerHTML = '';

    const pageTitles = {
        dashboard: '',
        tasks: 'Tasks',
        calendar: 'Calendar',
        courses: 'Courses',
        exams: 'Exams',
        analytics: 'Analytics',
        friends: 'Friends',
        settings: 'Settings'
    };

    body.classList.add('app-layout');

    // Sidebar
    const sidebar = document.createElement('aside');
    sidebar.className = 'sidebar';
    sidebar.innerHTML = `
        <nav class="sidebar-nav" role="navigation" aria-label="Main navigation">
            ${NAV.map(n => `
                <a href="${n.href}" class="sidebar-nav-item ${currentPage === n.page ? 'active' : ''}" data-page="${n.page}">
                    ${n.label}
                </a>
            `).join('')}
        </nav>
        <button class="sidebar-logout" id="sidebarLogoutBtn">Logout</button>
        <div class="sidebar-user">
            <div class="sidebar-user-avatar">${user ? (user.firstName?.[0] || '') + (user.lastName?.[0] || '') || 'U' : 'U'}</div>
            <div class="sidebar-user-info">
                <div class="sidebar-user-name">${user ? user.firstName + ' ' + user.lastName : 'User'}</div>
                <div class="sidebar-user-email">${user ? user.email : ''}</div>
            </div>
        </div>
    `;

    // Topbar
    const topbar = document.createElement('header');
    topbar.className = 'topbar';
    topbar.innerHTML = `
        <button class="sidebar-toggle" id="sidebarToggle">&#9776;</button>
        <div class="topbar-logo">
            <img src="${BASE_PATH}/Images/logo.png" alt="SmartStudy">
        </div>
        <span class="topbar-title">${pageTitles[currentPage] || ''}</span>
        <div class="topbar-actions">
            <div class="notif-wrapper">
                <button class="notif-bell" id="notifBell" title="Notifications">
                    &#128276;
                    <span class="notif-badge" id="notifBadge" style="display:none">0</span>
                </button>
                <div class="notif-dropdown" id="notifDropdown">
                    <div class="notif-dropdown__header">
                        <span class="notif-dropdown__title">Notifications</span>
                        <button class="btn btn-ghost btn-sm" id="notifMarkAll">Mark all read</button>
                    </div>
                    <div class="notif-dropdown__list" id="notifList">
                        <div class="notif-empty">Loading...</div>
                    </div>
                </div>
            </div>
        </div>
    `;

    // Main content
    const main = document.createElement('main');
    main.className = 'main-content';
    main.innerHTML = pageContent;

    // Sidebar overlay for mobile
    const overlay = document.createElement('div');
    overlay.className = 'sidebar-overlay';
    overlay.id = 'sidebarOverlay';

    body.prepend(overlay);
    body.prepend(main);
    body.prepend(topbar);
    body.prepend(sidebar);

    // Remove the original pageRoot since content moved to main
    pageRoot.remove();

    // Event listeners
    document.getElementById('sidebarLogoutBtn')?.addEventListener('click', () => logout());

    document.getElementById('sidebarToggle')?.addEventListener('click', () => {
        sidebar.classList.toggle('open');
        overlay.classList.toggle('show');
    });

    overlay.addEventListener('click', () => {
        sidebar.classList.remove('open');
        overlay.classList.remove('show');
    });

    // Close sidebar when a nav link is clicked (mobile)
    sidebar.querySelectorAll('.sidebar-nav-item').forEach(link => {
        link.addEventListener('click', () => {
            sidebar.classList.remove('open');
            overlay.classList.remove('show');
        });
    });

    // Close sidebar on window resize to desktop
    window.addEventListener('resize', () => {
        if (window.innerWidth > 768) {
            sidebar.classList.remove('open');
            overlay.classList.remove('show');
        }
    });

}
