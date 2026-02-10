// Notifications module - bell icon dropdown + polling
import { api } from './api.js';

let polling = null;
let notifications = [];
let unreadCount = 0;

export function initNotifications() {
    setupBellClick();
    setupMarkAllRead();
    fetchAndRender();
    // Poll every 60 seconds
    polling = setInterval(fetchUnreadCount, 60000);
}

async function fetchAndRender() {
    try {
        // Trigger generation then fetch
        await api.generateNotifications();
        const data = await api.getNotifications();
        notifications = data.notifications || [];
        unreadCount = data.unreadCount || 0;
        updateBadge();
        renderDropdown();
    } catch { /* silent */ }
}

async function fetchUnreadCount() {
    try {
        const data = await api.getUnreadCount();
        unreadCount = data.count || 0;
        updateBadge();
    } catch { /* silent */ }
}

function updateBadge() {
    const badge = document.getElementById('notifBadge');
    if (!badge) return;
    if (unreadCount > 0) {
        badge.textContent = unreadCount > 99 ? '99+' : unreadCount;
        badge.style.display = 'flex';
    } else {
        badge.style.display = 'none';
    }
}

function renderDropdown() {
    const list = document.getElementById('notifList');
    if (!list) return;

    if (!notifications.length) {
        list.innerHTML = '<div class="notif-empty">No notifications yet</div>';
        return;
    }

    list.innerHTML = notifications.map(n => {
        const timeAgo = getTimeAgo(new Date(n.createdAt));
        const iconMap = {
            deadline: '&#9200;',
            overload: '&#9888;',
            shared_invite: '&#129309;',
            shared_response: '&#9989;',
        };
        const icon = iconMap[n.type] || '&#128276;';
        return `
            <div class="notif-item ${n.isRead ? '' : 'notif-item--unread'}" data-id="${n.notificationId}">
                <span class="notif-item__icon">${icon}</span>
                <div class="notif-item__body">
                    <div class="notif-item__title">${n.title}</div>
                    <div class="notif-item__message">${n.message}</div>
                    <div class="notif-item__time">${timeAgo}</div>
                </div>
            </div>
        `;
    }).join('');

    // Click to mark read
    list.querySelectorAll('.notif-item--unread').forEach(item => {
        item.addEventListener('click', async () => {
            const id = parseInt(item.dataset.id);
            try {
                await api.markNotificationsRead([id]);
                item.classList.remove('notif-item--unread');
                unreadCount = Math.max(0, unreadCount - 1);
                updateBadge();
            } catch { /* silent */ }
        });
    });
}

function setupBellClick() {
    const bell = document.getElementById('notifBell');
    const dropdown = document.getElementById('notifDropdown');
    if (!bell || !dropdown) return;

    bell.addEventListener('click', (e) => {
        e.stopPropagation();
        dropdown.classList.toggle('show');
        // Refresh when opening
        if (dropdown.classList.contains('show')) {
            fetchAndRender();
        }
    });

    // Close on outside click
    document.addEventListener('click', (e) => {
        if (!e.target.closest('.notif-wrapper')) {
            dropdown.classList.remove('show');
        }
    });
}

function setupMarkAllRead() {
    document.getElementById('notifMarkAll')?.addEventListener('click', async () => {
        try {
            await api.markAllNotificationsRead();
            notifications.forEach(n => n.isRead = true);
            unreadCount = 0;
            updateBadge();
            renderDropdown();
        } catch { /* silent */ }
    });
}

function getTimeAgo(date) {
    const seconds = Math.floor((new Date() - date) / 1000);
    if (seconds < 60) return 'just now';
    const minutes = Math.floor(seconds / 60);
    if (minutes < 60) return `${minutes}m ago`;
    const hours = Math.floor(minutes / 60);
    if (hours < 24) return `${hours}h ago`;
    const days = Math.floor(hours / 24);
    return `${days}d ago`;
}
