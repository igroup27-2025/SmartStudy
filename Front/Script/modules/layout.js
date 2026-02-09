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
        <h1 class="topbar-title">${pageTitles[currentPage] || 'SmartStudy'}</h1>
        <div class="topbar-actions">
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

    // Bottom nav bar (mobile)
    const BOTTOM_TABS = [
        { page: 'dashboard', label: 'Dashboard', href: '/Pages/Dashboard.html', icon: '&#9632;' },
        { page: 'tasks',     label: 'Tasks',     href: '/Pages/Tasks.html',     icon: '&#9745;' },
        { page: 'calendar',  label: 'Calendar',  href: '/Pages/Calendar.html',  icon: '&#128197;' },
        { page: 'courses',   label: 'Courses',   href: '/Pages/Courses.html',   icon: '&#128218;' },
    ];
    const MORE_ITEMS = [
        { page: 'exams',     label: 'Exams',     href: '/Pages/Exams.html',     icon: '&#128203;' },
        { page: 'analytics', label: 'Analytics',  href: '/Pages/Analytics.html', icon: '&#128200;' },
        { page: 'friends',   label: 'Friends',   href: '/Pages/Friends.html',   icon: '&#129309;' },
        { page: 'settings',  label: 'Settings',  href: '/Pages/Settings.html',  icon: '&#9881;' },
    ];

    const isMoreActive = MORE_ITEMS.some(m => m.page === currentPage);

    const bottomNav = document.createElement('nav');
    bottomNav.className = 'bottom-nav';
    bottomNav.innerHTML = `
        ${BOTTOM_TABS.map(t => `
            <a href="${t.href}" class="bottom-nav__item ${currentPage === t.page ? 'active' : ''}" data-page="${t.page}">
                <span class="bottom-nav__icon">${t.icon}</span>
                <span class="bottom-nav__label">${t.label}</span>
            </a>
        `).join('')}
        <button class="bottom-nav__item ${isMoreActive ? 'active' : ''}" id="bottomNavMore">
            <span class="bottom-nav__icon">&#8943;</span>
            <span class="bottom-nav__label">More</span>
        </button>
    `;

    // More sheet overlay
    const moreSheet = document.createElement('div');
    moreSheet.className = 'bottom-nav__more-sheet';
    moreSheet.id = 'moreSheet';
    moreSheet.innerHTML = `
        <div class="bottom-nav__more-backdrop" id="moreBackdrop"></div>
        <div class="bottom-nav__more-panel">
            <div class="bottom-nav__more-handle"></div>
            ${MORE_ITEMS.map(m => `
                <a href="${m.href}" class="bottom-nav__more-item ${currentPage === m.page ? 'active' : ''}">
                    <span class="bottom-nav__more-icon">${m.icon}</span>
                    <span>${m.label}</span>
                </a>
            `).join('')}
        </div>
    `;

    body.prepend(overlay);
    body.prepend(main);
    body.prepend(topbar);
    body.prepend(sidebar);
    body.appendChild(bottomNav);
    body.appendChild(moreSheet);

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

    // More sheet toggle
    document.getElementById('bottomNavMore')?.addEventListener('click', () => {
        moreSheet.classList.toggle('open');
    });

    document.getElementById('moreBackdrop')?.addEventListener('click', () => {
        moreSheet.classList.remove('open');
    });

    // Close dropdown on outside click
    document.addEventListener('click', (e) => {
        if (!e.target.closest('.topbar-user')) {
            document.getElementById('userDropdown')?.classList.remove('show');
        }
    });
}
