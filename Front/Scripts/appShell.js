// SmartStudy app shell + auth helpers
//
// Loaded by every page after jQuery + ajaxCall.js. Provides:
//   * BASE_PATH / SERVER / API_BASE globals
//   * Auth helpers (getAuthHeaders, isLoggedIn, getUser, saveAuth, logout, requireAuth)
//   * escapeHtmlGlobal HTML escape helper
//   * SmartStudy.initShell(currentPage)  — sidebar + topbar + notifications bell
//   * Global $.ajaxSetup hook that adds  Authorization: Bearer <token>  to every
//     AJAX call when a JWT is in localStorage. Keeps ajaxCall.js bare.
//   * Global $(document).ajaxError hook that redirects to Login.html on 401
//     for any non-/auth/ endpoint.

// ---- Base path detection (supports IIS sub-app deployment) -----------------
var __pathMatch = window.location.pathname.match(/^(.*?)\/Pages\//);
var BASE_PATH = __pathMatch ? __pathMatch[1] : "";

// API lives on a sibling IIS app in production (tar1 instead of tar2).
// In dev it's the same origin as the frontend.
var __apiBasePath = BASE_PATH.replace(/\/tar2$/, "/tar1");
var SERVER = window.location.origin + __apiBasePath + "/";
var API_BASE = SERVER + "api";

// ---- Auth storage helpers --------------------------------------------------
function getAuthHeaders() {
    var token = localStorage.getItem("smartstudy_token");
    var headers = { "Content-Type": "application/json" };
    if (token) headers["Authorization"] = "Bearer " + token;
    return headers;
}

function isLoggedIn() {
    return !!localStorage.getItem("smartstudy_token");
}

function getUser() {
    var data = localStorage.getItem("smartstudy_user");
    return data ? JSON.parse(data) : null;
}

function saveAuth(res) {
    localStorage.setItem("smartstudy_token", res.token);
    localStorage.setItem("smartstudy_user", JSON.stringify({
        email: res.email,
        firstName: res.firstName,
        lastName: res.lastName,
        onboardingCompleted: res.onboardingCompleted !== false
    }));
}

function logout() {
    localStorage.removeItem("smartstudy_token");
    localStorage.removeItem("smartstudy_user");
    window.location.href = BASE_PATH + "/Pages/Login.html";
}

function requireAuth() {
    if (!isLoggedIn()) {
        window.location.href = BASE_PATH + "/Pages/Login.html";
        return false;
    }
    return true;
}

// ---- HTML escape helper (used across pages) -------------------------------
function escapeHtmlGlobal(s) {
    if (s == null) return "";
    return String(s).replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;");
}

// ---- Global jQuery AJAX hooks: JWT injection + 401 auto-logout ------------
// Lets ajaxCall.js stay a bare HW3-style $.ajax wrapper while every authenticated
// call still gets the Bearer token, and any 401 from a non-auth endpoint logs out.
$.ajaxSetup({
    beforeSend: function (xhr) {
        var token = localStorage.getItem("smartstudy_token");
        if (token) xhr.setRequestHeader("Authorization", "Bearer " + token);
    }
});

$(document).ajaxError(function (event, xhr, settings) {
    var url = (settings && settings.url) || "";
    if (xhr.status === 401 && url.indexOf("/auth/") === -1) {
        localStorage.removeItem("smartstudy_token");
        localStorage.removeItem("smartstudy_user");
        window.location.href = BASE_PATH + "/Pages/Login.html";
    }
});

// ---- App shell: sidebar + topbar + notifications bell ---------------------
// Each app page calls SmartStudy.initShell(currentPage) after DOM ready.
var SmartStudy = (function () {
    var pollingTimer = null;
    var notifications = [];
    var unreadCount = 0;

    var NAV = [
        { page: "dashboard", label: "Dashboard", href: "Dashboard.html" },
        { page: "tasks",     label: "Tasks",     href: "Tasks.html" },
        { page: "calendar",  label: "Calendar",  href: "Calendar.html" },
        { page: "courses",   label: "Courses",   href: "Courses.html" },
        { page: "exams",     label: "Exams",     href: "Exams.html" },
        { page: "analytics", label: "Analytics", href: "Analytics.html" },
        { page: "friends",   label: "Friends",   href: "Friends.html" },
        { page: "settings",  label: "Settings",  href: "Settings.html" }
    ];

    var PAGE_TITLES = {
        dashboard: "",
        tasks: "Tasks",
        calendar: "Calendar",
        courses: "Courses",
        exams: "Exams",
        analytics: "Analytics",
        friends: "Friends",
        settings: "Settings"
    };

    function initShell(currentPage) {
        var body = document.body;
        var pageRoot = document.getElementById("pageRoot");
        if (!pageRoot) return;

        body.classList.add("app-layout");

        var user = getUser() || {};
        var initials = ((user.firstName || " ")[0] || "") + ((user.lastName || " ")[0] || "");
        if (!initials.trim()) initials = "U";

        var navHtml = NAV.map(function (n) {
            var isActive = n.page === currentPage;
            return '<a href="' + BASE_PATH + '/Pages/' + n.href + '" class="sidebar-nav-item ' + (isActive ? 'active' : '') + '" data-page="' + n.page + '">' + n.label + '</a>';
        }).join("");

        var sidebarHtml =
            '<aside class="sidebar" id="ssSidebar">' +
                '<nav class="sidebar-nav" role="navigation" aria-label="Main navigation">' + navHtml + '</nav>' +
                '<button class="sidebar-logout" id="ssLogoutBtn">Logout</button>' +
                '<div class="sidebar-user">' +
                    '<div class="sidebar-user-avatar">' + escapeHtmlGlobal(initials.toUpperCase()) + '</div>' +
                    '<div class="sidebar-user-info">' +
                        '<div class="sidebar-user-name">' + escapeHtmlGlobal((user.firstName || "") + " " + (user.lastName || "")).trim() + '</div>' +
                        '<div class="sidebar-user-email">' + escapeHtmlGlobal(user.email || "") + '</div>' +
                    '</div>' +
                '</div>' +
            '</aside>';

        var topbarHtml =
            '<header class="topbar" id="ssTopbar">' +
                '<button class="sidebar-toggle" id="ssSidebarToggle">&#9776;</button>' +
                '<div class="topbar-logo"><img src="' + BASE_PATH + '/Images/logo.png" alt="SmartStudy"></div>' +
                '<span class="topbar-title">' + escapeHtmlGlobal(PAGE_TITLES[currentPage] || "") + '</span>' +
                '<div class="topbar-actions">' +
                    '<div class="notif-wrapper">' +
                        '<button class="notif-bell" id="ssNotifBell" title="Notifications">' +
                            '&#128276;' +
                            '<span class="notif-badge" id="ssNotifBadge" style="display:none">0</span>' +
                        '</button>' +
                        '<div class="notif-dropdown" id="ssNotifDropdown">' +
                            '<div class="notif-dropdown__header">' +
                                '<span class="notif-dropdown__title">Notifications</span>' +
                                '<button class="btn btn-ghost btn-sm" id="ssNotifMarkAll">Mark all read</button>' +
                            '</div>' +
                            '<div class="notif-dropdown__list" id="ssNotifList">' +
                                '<div class="notif-empty">Loading...</div>' +
                            '</div>' +
                        '</div>' +
                    '</div>' +
                '</div>' +
            '</header>';

        var overlayHtml = '<div class="sidebar-overlay" id="ssSidebarOverlay"></div>';

        // Wrap existing pageRoot content into <main class="main-content">
        var existingContent = pageRoot.innerHTML;
        pageRoot.innerHTML = "";
        pageRoot.outerHTML =
            sidebarHtml +
            topbarHtml +
            overlayHtml +
            '<main class="main-content" id="ssMain">' + existingContent + '</main>';

        // Wire events
        var $sidebar = $("#ssSidebar");
        var $overlay = $("#ssSidebarOverlay");

        $("#ssLogoutBtn").on("click", function () { logout(); });

        $("#ssSidebarToggle").on("click", function () {
            $sidebar.toggleClass("open");
            $overlay.toggleClass("show");
        });
        $overlay.on("click", function () {
            $sidebar.removeClass("open");
            $overlay.removeClass("show");
        });
        $sidebar.find(".sidebar-nav-item").on("click", function () {
            $sidebar.removeClass("open");
            $overlay.removeClass("show");
        });
        $(window).on("resize", function () {
            if (window.innerWidth > 768) {
                $sidebar.removeClass("open");
                $overlay.removeClass("show");
            }
        });

        // Notification bell
        var $bell = $("#ssNotifBell");
        var $dropdown = $("#ssNotifDropdown");
        $bell.on("click", function (e) {
            e.stopPropagation();
            $dropdown.toggleClass("show");
            if ($dropdown.hasClass("show")) fetchAndRenderNotifications();
        });
        $(document).on("click", function (e) {
            if (!$(e.target).closest(".notif-wrapper").length) {
                $dropdown.removeClass("show");
            }
        });
        $("#ssNotifMarkAll").on("click", function () {
            ajaxCall("POST", API_BASE + "/notifications/mark-all-read", null,
                function () {
                    notifications.forEach(function (n) { n.isRead = true; });
                    unreadCount = 0;
                    updateNotifBadge();
                    renderNotifDropdown();
                },
                function () { /* silent */ }
            );
        });

        // Initial fetch + polling
        fetchAndRenderNotifications();
        pollingTimer = setInterval(fetchUnreadCount, 60000);
    }

    function fetchAndRenderNotifications() {
        // Trigger generation then fetch
        ajaxCall("POST", API_BASE + "/notifications/generate", null,
            function () { loadNotifications(); },
            function () { loadNotifications(); }
        );
    }

    function loadNotifications() {
        ajaxCall("GET", API_BASE + "/notifications", null,
            function (data) {
                notifications = (data && data.notifications) || [];
                unreadCount = (data && data.unreadCount) || 0;
                updateNotifBadge();
                renderNotifDropdown();
            },
            function () { /* silent */ }
        );
    }

    function fetchUnreadCount() {
        ajaxCall("GET", API_BASE + "/notifications/unread-count", null,
            function (data) {
                unreadCount = (data && data.count) || 0;
                updateNotifBadge();
            },
            function () { /* silent */ }
        );
    }

    function updateNotifBadge() {
        var $badge = $("#ssNotifBadge");
        if (!$badge.length) return;
        if (unreadCount > 0) {
            $badge.text(unreadCount > 99 ? "99+" : unreadCount);
            $badge.css("display", "flex");
        } else {
            $badge.css("display", "none");
        }
    }

    function renderNotifDropdown() {
        var $list = $("#ssNotifList");
        if (!$list.length) return;

        if (!notifications.length) {
            $list.html('<div class="notif-empty">No notifications yet</div>');
            return;
        }

        var ICONS = {
            deadline: "&#9200;", overload: "&#9888;",
            shared_invite: "&#129309;", shared_response: "&#9989;",
            shared_task_invite: "&#129309;", shared_task_response: "&#9989;",
            shared_task_no_time: "&#128337;",
            daily_summary: "&#128197;", weekly_reminder: "&#128203;"
        };

        var html = notifications.map(function (n) {
            var icon = ICONS[n.type] || "&#128276;";
            var timeAgo = getTimeAgo(new Date(n.createdAt));
            return '<div class="notif-item ' + (n.isRead ? '' : 'notif-item--unread') + '" data-id="' + n.notificationId + '">' +
                '<span class="notif-item__icon">' + icon + '</span>' +
                '<div class="notif-item__body">' +
                    '<div class="notif-item__title">' + escapeHtmlGlobal(n.title) + '</div>' +
                    '<div class="notif-item__message">' + escapeHtmlGlobal(n.message) + '</div>' +
                    '<div class="notif-item__time">' + timeAgo + '</div>' +
                '</div>' +
            '</div>';
        }).join("");

        $list.html(html);

        $list.find(".notif-item--unread").on("click", function () {
            var id = parseInt($(this).data("id"));
            var $item = $(this);
            ajaxCall("POST", API_BASE + "/notifications/mark-read", JSON.stringify({ notificationIds: [id] }),
                function () {
                    $item.removeClass("notif-item--unread");
                    unreadCount = Math.max(0, unreadCount - 1);
                    updateNotifBadge();
                },
                function () { /* silent */ }
            );
        });
    }

    function getTimeAgo(date) {
        var seconds = Math.floor((new Date() - date) / 1000);
        if (seconds < 60) return "just now";
        var minutes = Math.floor(seconds / 60);
        if (minutes < 60) return minutes + "m ago";
        var hours = Math.floor(minutes / 60);
        if (hours < 24) return hours + "h ago";
        var days = Math.floor(hours / 24);
        return days + "d ago";
    }

    return {
        initShell: initShell,
        refreshNotifications: fetchAndRenderNotifications
    };
})();
