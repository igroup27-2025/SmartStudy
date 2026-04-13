import { api } from './api.js';
import { openModal, closeModal, showToast } from './modals.js';
import { getUser } from './auth.js';

let connections = [];
let pendingRequests = [];
let sharedTasks = [];

export async function initFriends() {
    try {
        const data = await api.getConnections();
        connections = data.filter(c => c.status === 'accepted');
        // 'pending' = incoming (can accept/decline); 'sent' = outgoing (awaiting response)
        pendingRequests = data.filter(c => c.status === 'pending' || c.status === 'sent');
    } catch (err) {
        console.error('Failed to load connections:', err);
        showToast('Failed to load friends', 'error');
        connections = [];
        pendingRequests = [];
    }

    try {
        sharedTasks = await api.getSharedTasks();
    } catch {
        sharedTasks = [];
    }

    renderPendingRequests();
    renderFriends();
    renderSharedTasks();
    setupInvite();
    setupShareTask();
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
                ${r.status === 'sent'
                    ? `<span style="color: var(--text-muted, #888); font-size: 0.9rem; font-style: italic;">Waiting for response…</span>`
                    : `<button class="btn btn-primary btn-sm request-accept" data-id="${r.connectionId}">Accept</button>
                       <button class="btn btn-secondary btn-sm request-decline" data-id="${r.connectionId}">Decline</button>`}
            </div>
        </div>
    `).join('');

    list.querySelectorAll('.request-accept').forEach(btn => {
        btn.addEventListener('click', async () => {
            const id = parseInt(btn.dataset.id);
            let result;
            try {
                result = await api.acceptConnection(id);
            } catch (err) {
                showToast(err.message || 'Failed to accept request', 'error');
                return;
            }

            const req = pendingRequests.find(r => r.connectionId === id);
            if (req) {
                // Remove from pending first — reassigning connectionId below
                // would otherwise break the filter predicate.
                pendingRequests = pendingRequests.filter(r => r.connectionId !== id);
                req.status = 'accepted';
                // Swap to the friendshipId so later remove/delete calls target the friendship row.
                if (result && result.friendshipId) {
                    req.connectionId = result.friendshipId;
                }
                connections.push(req);
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
            } catch (err) {
                showToast(err.message || 'Failed to decline request', 'error');
                return;
            }

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
                <div class="friend-card-footer-buttons">
                    <button class="btn btn-secondary btn-sm friend-share-task" data-email="${c.friendEmail}" data-name="${c.friendName}">Share Task</button>
                </div>
            </div>
        </div>
    `).join('');

    el.querySelectorAll('.friend-remove').forEach(btn => {
        btn.addEventListener('click', async (e) => {
            e.stopPropagation();
            const id = parseInt(btn.dataset.id);
            try {
                await api.removeConnection(id);
            } catch (err) {
                showToast(err.message || 'Failed to remove friend', 'error');
                return;
            }

            connections = connections.filter(c => c.connectionId !== id);
            renderFriends();
            showToast('Friend removed');
        });
    });

    el.querySelectorAll('.friend-share-task').forEach(btn => {
        btn.addEventListener('click', () => {
            openShareTaskModal(btn.dataset.email, btn.dataset.name);
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
        } catch (err) {
            showToast(err.message || 'Failed to send invitation', 'error');
            return;
        }

        closeModal('inviteModal');
        showToast('Invitation sent!');
    });
}

/* ── Shared Tasks ── */

function getPartner(task, myEmail) {
    const members = task.members || [];
    const other = members.find(m => m.email.toLowerCase() !== myEmail);
    return other || { email: '', name: '' };
}

function getMyMember(task, myEmail) {
    const members = task.members || [];
    return members.find(m => m.email.toLowerCase() === myEmail);
}

function renderSharedTasks() {
    const section = document.getElementById('sharedTasksSection');
    const list = document.getElementById('sharedTasksList');
    const badge = document.getElementById('sharedTaskCount');
    if (!section || !list) return;

    // Filter out cancelled tasks
    const visible = sharedTasks.filter(t => t.sharedStatus !== 'Cancelled');

    if (!visible.length) {
        section.style.display = 'none';
        return;
    }

    section.style.display = '';
    badge.textContent = visible.length;

    const user = getUser();
    const myEmail = user ? user.email.toLowerCase() : '';

    const pending = visible.filter(t => t.sharedStatus === 'Pending');
    const confirmed = visible.filter(t => t.sharedStatus === 'Confirmed');

    let html = '';

    if (pending.length) {
        // Group pending tasks I need to respond to by course for bulk approval
        const myPending = pending.filter(t => {
            const myMember = getMyMember(t, myEmail);
            return myMember && myMember.responseStatus === 'Pending';
        });
        const otherPending = pending.filter(t => {
            const myMember = getMyMember(t, myEmail);
            return !myMember || myMember.responseStatus !== 'Pending';
        });

        // Group my pending by courseId
        const courseGroups = {};
        myPending.forEach(t => {
            const cid = t.courseId || 0;
            if (!courseGroups[cid]) courseGroups[cid] = { courseName: t.courseName, tasks: [] };
            courseGroups[cid].tasks.push(t);
        });

        html += `<div class="shared-tasks-group">
            <h4 class="shared-tasks-group-title">Pending Invitations</h4>`;

        // Show course-level approval for groups with 2+ tasks
        Object.entries(courseGroups).forEach(([courseId, group]) => {
            if (group.tasks.length >= 2 && courseId !== '0') {
                html += `
                <div class="shared-task-course-group" data-course-id="${courseId}">
                    <div class="shared-task-course-header">
                        <span>&#128218; ${escapeHtml(group.courseName || 'Course')} — ${group.tasks.length} shared tasks</span>
                        <button class="btn btn-primary btn-sm course-share-approve" data-course-id="${courseId}">Accept All for This Course</button>
                    </div>
                </div>`;
            }

            group.tasks.forEach(t => {
                const partner = getPartner(t, myEmail);
                html += `
                <div class="shared-task-card" data-task-id="${t.taskId}">
                    <div class="shared-task-card__body">
                        <div class="shared-task-card__title">${escapeHtml(t.taskTitle || 'Untitled Task')}</div>
                        <div class="shared-task-card__meta">
                            ${t.courseName ? `<span>${escapeHtml(t.courseName)}</span> · ` : ''}
                            <span>From: ${escapeHtml(partner.name || partner.email)}</span>
                        </div>
                    </div>
                    <div class="shared-task-card__actions">
                        <button class="btn btn-primary btn-sm shared-task-accept" data-task-id="${t.taskId}">Accept</button>
                        <button class="btn btn-secondary btn-sm shared-task-decline" data-task-id="${t.taskId}">Decline</button>
                    </div>
                </div>`;
            });
        });

        // Show tasks awaiting partner response
        otherPending.forEach(t => {
            const partner = getPartner(t, myEmail);
            html += `
            <div class="shared-task-card" data-task-id="${t.taskId}">
                <div class="shared-task-card__body">
                    <div class="shared-task-card__title">${escapeHtml(t.taskTitle || 'Untitled Task')}</div>
                    <div class="shared-task-card__meta">
                        ${t.courseName ? `<span>${escapeHtml(t.courseName)}</span> · ` : ''}
                        <span>To: ${escapeHtml(partner.name || partner.email)}</span>
                    </div>
                </div>
                <div class="shared-task-card__actions">
                    <span class="badge badge-medium">Awaiting response</span>
                </div>
            </div>`;
        });

        html += `</div>`;
    }

    if (confirmed.length) {
        html += `<div class="shared-tasks-group">
            <h4 class="shared-tasks-group-title">Active Shared Tasks</h4>
            ${confirmed.map(t => {
                const partner = getPartner(t, myEmail);
                return `
                <div class="shared-task-card shared-task-card--confirmed" data-task-id="${t.taskId}">
                    <div class="shared-task-card__body">
                        <div class="shared-task-card__title">${escapeHtml(t.taskTitle || 'Untitled Task')}</div>
                        <div class="shared-task-card__meta">
                            ${t.courseName ? `<span>${escapeHtml(t.courseName)}</span> · ` : ''}
                            <span>With: ${escapeHtml(partner.name || partner.email)}</span>
                        </div>
                    </div>
                    <div class="shared-task-card__actions">
                        <span class="badge badge-low">Confirmed</span>
                        <button class="btn btn-ghost btn-sm shared-task-view" data-task-id="${t.taskId}">View</button>
                    </div>
                </div>`;
            }).join('')}
        </div>`;
    }

    list.innerHTML = html;

    // Bind course-level approval buttons
    list.querySelectorAll('.course-share-approve').forEach(btn => {
        btn.addEventListener('click', async () => {
            const courseId = parseInt(btn.dataset.courseId);
            try {
                await api.approveCourseSharing(courseId);
                showToast('Course sharing approved — all tasks accepted!');
                sharedTasks = await api.getSharedTasks();
                renderSharedTasks();
            } catch (err) {
                showToast(err.message || 'Failed to approve', 'error');
            }
        });
    });

    // Bind accept/decline buttons
    list.querySelectorAll('.shared-task-accept').forEach(btn => {
        btn.addEventListener('click', () => respondToSharedTask(parseInt(btn.dataset.taskId), true));
    });
    list.querySelectorAll('.shared-task-decline').forEach(btn => {
        btn.addEventListener('click', () => respondToSharedTask(parseInt(btn.dataset.taskId), false));
    });
    list.querySelectorAll('.shared-task-view').forEach(btn => {
        btn.addEventListener('click', () => openSharedTaskDetail(parseInt(btn.dataset.taskId)));
    });
}

async function openShareTaskModal(friendEmail, friendName) {
    document.getElementById('shareTaskTitle').textContent = `Share Task with ${friendName}`;
    document.getElementById('shareTaskSubtitle').textContent = `Select a task to share with ${friendName}.`;
    document.getElementById('shareTaskFriendEmail').value = friendEmail;
    document.getElementById('shareTaskForm')?.reset();
    document.getElementById('shareTaskFriendEmail').value = friendEmail;

    const select = document.getElementById('shareTaskSelect');
    select.innerHTML = '<option value="">Loading tasks...</option>';
    openModal('shareTaskModal');

    let tasks = [];
    try {
        tasks = await api.getTasks();
    } catch { /* fallback empty */ }

    // Filter out already-shared tasks with this friend
    const alreadyShared = new Set(
        sharedTasks
            .filter(st => {
                const members = st.members || [];
                const hasFriend = members.some(m => m.email.toLowerCase() === friendEmail.toLowerCase());
                return hasFriend && st.sharedStatus !== 'Cancelled';
            })
            .map(st => st.taskId)
    );
    const available = tasks.filter(t => !t.isCompleted && !alreadyShared.has(t.taskId));

    if (!available.length) {
        select.innerHTML = '<option value="">No available tasks</option>';
        return;
    }

    select.innerHTML = '<option value="">Choose a task...</option>' +
        available.map(t => `<option value="${t.taskId}">${escapeHtml(t.title)}${t.courseName ? ` (${escapeHtml(t.courseName)})` : ''}</option>`).join('');
}

function setupShareTask() {
    document.getElementById('shareTaskForm')?.addEventListener('submit', async (e) => {
        e.preventDefault();
        const taskId = parseInt(document.getElementById('shareTaskSelect').value);
        const partnerEmail = document.getElementById('shareTaskFriendEmail').value;

        if (!taskId || !partnerEmail) {
            showToast('Please select a task', 'error');
            return;
        }

        try {
            await api.createSharedTask({ taskId, partnerEmail });
            showToast('Task shared successfully!');
        } catch (err) {
            showToast(err.message || 'Failed to share task', 'error');
            return;
        }

        closeModal('shareTaskModal');

        try {
            sharedTasks = await api.getSharedTasks();
        } catch { /* keep existing */ }
        renderSharedTasks();
    });
}

async function respondToSharedTask(taskId, accept) {
    try {
        await api.respondSharedTask(taskId, accept);
        showToast(accept ? 'Shared task accepted!' : 'Shared task declined');
    } catch (err) {
        showToast(err.message || 'Failed to respond', 'error');
        return;
    }

    try {
        sharedTasks = await api.getSharedTasks();
    } catch { /* keep existing */ }
    renderSharedTasks();
}

async function openSharedTaskDetail(taskId) {
    const content = document.getElementById('sharedTaskDetailContent');
    content.innerHTML = '<div class="spinner-center"><div class="spinner"></div></div>';
    openModal('sharedTaskDetailModal');

    let detail;
    try {
        detail = await api.getSharedTask(taskId);
    } catch {
        detail = sharedTasks.find(t => t.taskId === taskId);
    }

    if (!detail) {
        content.innerHTML = '<p class="text-muted">Could not load task details.</p>';
        return;
    }

    const statusClass = detail.sharedStatus === 'Confirmed' ? 'low' : detail.sharedStatus === 'Pending' ? 'medium' : 'high';
    const members = detail.members || [];

    content.innerHTML = `
        <div class="shared-task-detail">
            <div class="shared-task-detail__header">
                <h4>${escapeHtml(detail.taskTitle || 'Untitled Task')}</h4>
                <span class="badge badge-${statusClass}">${detail.sharedStatus}</span>
            </div>
            ${detail.courseName ? `<p class="shared-task-card__meta">Course: ${escapeHtml(detail.courseName)}</p>` : ''}
            ${detail.dueDate ? `<p class="shared-task-card__meta">Due: ${formatDate(detail.dueDate)}</p>` : ''}
            <div class="shared-task-detail__members">
                <h5 style="margin: var(--space-4) 0 var(--space-2);">Members</h5>
                ${members.map(m => {
                    const isOwner = m.email.toLowerCase() === detail.createdByEmail?.toLowerCase();
                    const mStatusClass = m.responseStatus === 'Accepted' ? 'low' : m.responseStatus === 'Pending' ? 'medium' : 'high';
                    return `
                    <div class="shared-task-member">
                        <span class="friend-request-avatar" style="width:32px;height:32px;font-size:var(--fs-xs);">${getInitials(m.name || m.email)}</span>
                        <span>${escapeHtml(m.name || m.email)}</span>
                        <span class="badge badge-${mStatusClass}">${isOwner ? 'Owner' : m.responseStatus}</span>
                    </div>`;
                }).join('')}
            </div>
        </div>
    `;
}

function escapeHtml(str) {
    if (!str) return '';
    return str.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
}
