// Layout module - injects sidebar + topbar shell around page content
import { getUser, logout } from './auth.js';

const NAV = [
    { page: 'dashboard', label: 'Dashboard', href: '/Pages/Dashboard.html' },
    { page: 'tasks', label: 'Tasks', href: '/Pages/Tasks.html' },
    { page: 'calendar', label: 'Calendar', href: '/Pages/Calendar.html' },
    { page: 'courses', label: 'Courses', href: '/Pages/Courses.html' },
    { page: 'exams', label: 'Exams', href: '/Pages/Exams.html' },
    { page: 'analytics', label: 'Analytics', href: '/Pages/Analytics.html' },
    { page: 'friends', label: 'Friends', href: '/Pages/Friends.html' },
    { page: 'settings', label: 'Settings', href: '/Pages/Settings.html' },
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
        dashboard: 'Dashboard',
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
        <div class="sidebar-logo">
            <img src="/Images/logo.png" alt="SmartStudy" class="sidebar-logo-img">
            <span class="sidebar-logo-text">SmartStudy</span>
        </div>
        <nav class="sidebar-nav">
            ${NAV.map(n => `
                <a href="${n.href}" class="sidebar-nav-item ${currentPage === n.page ? 'active' : ''}" data-page="${n.page}">
                    ${n.label}
                </a>
            `).join('')}
        </nav>
        <div class="sidebar-user">
            <div class="sidebar-user-avatar">${user ? user.firstName[0] + user.lastName[0] : 'U'}</div>
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
            <img src="/Images/logo.png" alt="SmartStudy">
        </div>
        <h1 class="topbar-title">${pageTitles[currentPage] || 'SmartStudy'}</h1>
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
            <div class="topbar-user">
                <div class="topbar-avatar" id="userMenuBtn">${user ? user.firstName[0] : 'U'}</div>
                <div class="topbar-dropdown" id="userDropdown">
                    <a href="/Pages/Settings.html" class="topbar-dropdown-item">Settings</a>
                    <div class="topbar-dropdown-divider"></div>
                    <a href="#" class="topbar-dropdown-item" id="logoutBtn">Logout</a>
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
    document.getElementById('logoutBtn')?.addEventListener('click', (e) => {
        e.preventDefault();
        logout();
    });

    document.getElementById('userMenuBtn')?.addEventListener('click', () => {
        document.getElementById('userDropdown')?.classList.toggle('show');
    });

    document.getElementById('sidebarToggle')?.addEventListener('click', () => {
        sidebar.classList.toggle('open');
        overlay.classList.toggle('show');
    });

    overlay.addEventListener('click', () => {
        sidebar.classList.remove('open');
        overlay.classList.remove('show');
    });

    // Close dropdown on outside click
    document.addEventListener('click', (e) => {
        if (!e.target.closest('.topbar-user')) {
            document.getElementById('userDropdown')?.classList.remove('show');
        }
    });
}
