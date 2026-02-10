import { api } from './api.js';
import { openModal, closeModal, showToast } from './modals.js';
import { getUser } from './auth.js';

let connections = [];
let pendingRequests = [];

// Demo data fallback (used when API is unavailable)
const DEMO_CONNECTIONS = [
    { connectionId: 1, friendEmail: 'sarah.cohen@uni.ac.il', friendName: 'Sarah Cohen', status: 'accepted', connectedDate: '2026-01-15' },
    { connectionId: 2, friendEmail: 'david.levi@uni.ac.il', friendName: 'David Levi', status: 'accepted', connectedDate: '2026-01-22' },
];

const DEMO_PENDING = [
    { connectionId: 3, friendEmail: 'maya.alon@uni.ac.il', friendName: 'Maya Alon', status: 'pending', connectedDate: '2026-02-05' },
];

const DEMO_SAFE_ZONES = [
    { date: '2026-02-10', day: 'Tuesday', startTime: '10:00', endTime: '12:00', myStress: 25, friendStress: 30 },
    { date: '2026-02-10', day: 'Tuesday', startTime: '14:00', endTime: '16:00', myStress: 20, friendStress: 35 },
    { date: '2026-02-11', day: 'Wednesday', startTime: '09:00', endTime: '11:00', myStress: 30, friendStress: 28 },
    { date: '2026-02-12', day: 'Thursday', startTime: '13:00', endTime: '15:00', myStress: 42, friendStress: 38 },
];

export async function initFriends() {
    try {
        const data = await api.getConnections();
        connections = data.filter(c => c.status === 'accepted');
        pendingRequests = data.filter(c => c.status === 'pending');
    } catch {
        connections = [...DEMO_CONNECTIONS];
        pendingRequests = [...DEMO_PENDING];
    }

    renderPendingRequests();
    renderFriends();
    setupInvite();
}

function getInitials(name) {
    if (!name) return '?';
    return name.split(' ').map(w => w[0]).join('').toUpperCase().slice(0, 2);
}

function formatDate(dateStr) {
    if (!dateStr) return '';
    const d = new Date(dateStr);
    return d.toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });
}

function renderPendingRequests() {
    const section = document.getElementById('friendRequests');
    const list = document.getElementById('requestsList');
    const badge = document.getElementById('requestCount');
    if (!section || !list) return;

    if (!pendingRequests.length) {
        section.style.display = 'none';
        return;
    }

    section.style.display = '';
    badge.textContent = pendingRequests.length;

    list.innerHTML = pendingRequests.map(r => `
        <div class="friend-request-card" data-id="${r.connectionId}">
            <div class="friend-request-avatar">${getInitials(r.friendName)}</div>
            <div class="friend-request-info">
                <div class="friend-request-name">${r.friendName}</div>
                <div class="friend-request-email">${r.friendEmail}</div>
            </div>
            <div class="friend-request-actions">
                <button class="btn btn-primary btn-sm request-accept" data-id="${r.connectionId}">Accept</button>
                <button class="btn btn-secondary btn-sm request-decline" data-id="${r.connectionId}">Decline</button>
            </div>
        </div>
    `).join('');

    list.querySelectorAll('.request-accept').forEach(btn => {
        btn.addEventListener('click', async () => {
            const id = parseInt(btn.dataset.id);
            let result;
            try {
                result = await api.acceptConnection(id);
            } catch { /* demo mode */ }

            const req = pendingRequests.find(r => r.connectionId === id);
            if (req) {
                req.status = 'accepted';
                // Use the friendshipId returned by the server for safe-zone lookups
                if (result && result.friendshipId) {
                    req.connectionId = result.friendshipId;
                }
                connections.push(req);
                pendingRequests = pendingRequests.filter(r => r.connectionId !== id);
            }
            renderPendingRequests();
            renderFriends();
            showToast('Friend request accepted!');
        });
    });

    list.querySelectorAll('.request-decline').forEach(btn => {
        btn.addEventListener('click', async () => {
            const id = parseInt(btn.dataset.id);
            try {
                await api.declineConnection(id);
            } catch { /* demo mode */ }

            pendingRequests = pendingRequests.filter(r => r.connectionId !== id);
            renderPendingRequests();
            showToast('Request declined');
        });
    });
}

function renderFriends() {
    const el = document.getElementById('friendsList');
    if (!el) return;

    if (!connections.length) {
        el.innerHTML = `<div class="empty-state">
            <div class="empty-state-icon">&#129309;</div>
            <h3>No friends yet</h3>
            <p>Invite classmates to connect and find study times together</p>
        </div>`;
        return;
    }

    el.innerHTML = connections.map(c => `
        <div class="friend-card" data-id="${c.connectionId}">
            <div class="friend-card-header">
                <div class="friend-card-avatar">${getInitials(c.friendName)}</div>
                <button class="btn btn-ghost btn-sm friend-remove" data-id="${c.connectionId}" title="Remove">&times;</button>
            </div>
            <div class="friend-card-body">
                <div class="friend-card-name">${c.friendName}</div>
                <div class="friend-card-email">${c.friendEmail}</div>
                <div class="friend-card-date">Connected ${formatDate(c.connectedDate)}</div>
            </div>
            <div class="friend-card-footer">
                <button class="btn btn-primary w-full friend-safezone" data-id="${c.connectionId}" data-name="${c.friendName}">Find Safe Zone</button>
            </div>
        </div>
    `).join('');

    el.querySelectorAll('.friend-remove').forEach(btn => {
        btn.addEventListener('click', async (e) => {
            e.stopPropagation();
            const id = parseInt(btn.dataset.id);
            try {
                await api.removeConnection(id);
            } catch { /* demo mode */ }

            connections = connections.filter(c => c.connectionId !== id);
            renderFriends();
            showToast('Friend removed');
        });
    });

    el.querySelectorAll('.friend-safezone').forEach(btn => {
        btn.addEventListener('click', () => {
            const id = parseInt(btn.dataset.id);
            const name = btn.dataset.name;
            openSafeZone(id, name);
        });
    });
}

function setupInvite() {
    document.getElementById('inviteFriendBtn')?.addEventListener('click', () => {
        document.getElementById('inviteForm')?.reset();
        openModal('inviteModal');
    });

    document.getElementById('inviteForm')?.addEventListener('submit', async (e) => {
        e.preventDefault();
        const email = document.getElementById('inviteEmail').value.trim();

        const user = getUser();
        if (user && email.toLowerCase() === user.email.toLowerCase()) {
            showToast("You can't invite yourself!", 'error');
            return;
        }

        const alreadyConnected = connections.some(c => c.friendEmail.toLowerCase() === email.toLowerCase());
        const alreadyPending = pendingRequests.some(r => r.friendEmail.toLowerCase() === email.toLowerCase());
        if (alreadyConnected || alreadyPending) {
            showToast('Already connected or pending with this person', 'error');
            return;
        }

        try {
            await api.inviteConnection(email);
        } catch { /* demo mode */ }

        closeModal('inviteModal');
        showToast('Invitation sent!');
    });
}

async function openSafeZone(connectionId, name) {
    document.getElementById('safeZoneTitle').textContent = `Safe Zones with ${name}`;
    document.getElementById('safeZoneContent').innerHTML = '<div class="spinner-center"><div class="spinner"></div></div>';
    openModal('safeZoneModal');

    let zones;
    try {
        zones = await api.getSafeZones(connectionId);
    } catch {
        zones = DEMO_SAFE_ZONES;
    }

    const content = document.getElementById('safeZoneContent');

    if (!zones.length) {
        content.innerHTML = `
            <div class="empty-state" style="padding: var(--space-8) 0;">
                <div class="empty-state-icon">&#128337;</div>
                <h3>No safe zones found</h3>
                <p>No overlapping free time this week</p>
            </div>`;
        return;
    }

    // Group by date
    const grouped = {};
    zones.forEach(z => {
        const key = z.date;
        if (!grouped[key]) grouped[key] = { day: z.day, date: z.date, slots: [] };
        grouped[key].slots.push(z);
    });

    const days = Object.values(grouped);

    content.innerHTML = `
        <p class="safezone-intro">Mutual free time slots this week — stress levels shown for reference:</p>
        <div class="safezone-days">
            ${days.map(d => `
                <div class="safezone-day">
                    <div class="safezone-day-header">
                        <span>${d.day}</span>
                        <span class="text-muted text-sm">${formatDate(d.date)}</span>
                    </div>
                    <div class="safezone-slots">
                        ${d.slots.map(s => {
                            const maxStress = Math.max(s.myStress, s.friendStress);
                            const level = maxStress <= 40 ? 'low' : maxStress <= 70 ? 'medium' : 'high';
                            return `
                            <div class="safezone-slot">
                                <span class="safezone-slot-time">${s.startTime} - ${s.endTime}</span>
                                <span class="badge badge-${level}">${maxStress}% stress</span>
                            </div>`;
                        }).join('')}
                    </div>
                </div>
            `).join('')}
        </div>
    `;
}
