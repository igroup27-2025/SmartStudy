import { api } from './api.js';
import { openModal, closeModal, showToast } from './modals.js';

let allTasks = [];
let courses = [];
let friends = [];
let sharedTasks = [];
let completingTaskId = null;

export async function initTasks() {
    try {
        [allTasks, courses] = await Promise.all([api.getTasks(), api.getCourses()]);
        // Load friends and shared tasks in background
        try {
            const connections = await api.getConnections();
            friends = (Array.isArray(connections) ? connections : []).filter(c => c.status === 'accepted').map(c => ({ email: c.friendEmail, name: c.friendName }));
        } catch { friends = []; }
        try { sharedTasks = await api.getSharedTasks(); } catch { sharedTasks = []; }

        renderTasks(allTasks);
        renderSharedInvitations();
        populateCourseFilter();
        setupFilters();
        setupAddTask();
        setupReschedule();
        setupCompletionModal();
        setupSplitModal();
        setupSharedToggle();
        setupHoursSuggestion();

        // Handle ?edit=ID URL param to open edit modal
        const params = new URLSearchParams(window.location.search);
        const editId = params.get('edit');
        if (editId) editTask(parseInt(editId));
    } catch (err) {
        showToast('Failed to load tasks', 'error');
    }
}

function renderTasks(tasks) {
    const el = document.getElementById('taskList');
    if (!el) return;

    if (!tasks.length) {
        el.innerHTML = `<div class="empty-state">
            <div class="empty-state-icon">&#9745;</div>
            <h3>No tasks found</h3>
            <p>Add your first task to get started</p>
        </div>`;
        return;
    }

    el.innerHTML = tasks.map(t => {
        const dueDate = t.dueDate ? new Date(t.dueDate) : null;
        const daysLeft = dueDate ? Math.ceil((dueDate - new Date()) / (1000 * 60 * 60 * 24)) : null;
        const isOverdue = daysLeft !== null && daysLeft < 0 && !t.isCompleted;

        // Scheduling badge
        let scheduleBadge = '';
        if (!t.isCompleted && t.schedulingStatus) {
            if (t.schedulingStatus === 'Scheduled' && t.scheduledDate) {
                const sd = new Date(t.scheduledDate);
                const dateStr = sd.toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
                scheduleBadge = `<span class="badge badge-scheduled">Scheduled: ${dateStr}</span>`;
            } else if (t.schedulingStatus === 'Partial') {
                scheduleBadge = '<span class="badge badge-partial">Partially Scheduled</span>';
            } else if (t.schedulingStatus === 'Unscheduled') {
                scheduleBadge = '<span class="badge badge-unscheduled">Not Scheduled</span>';
            }
        }

        // Shared badge
        let sharedBadge = '';
        if (t.isShared) {
            const cls = t.sharedStatus === 'Confirmed' ? 'badge-success' : t.sharedStatus === 'Cancelled' ? 'badge-danger' : 'badge-warning';
            sharedBadge = `<span class="badge ${cls}">Shared${t.sharedWithName ? ': ' + t.sharedWithName : ''} (${t.sharedStatus})</span>`;
        }

        // Sub-task progress
        let subtaskHtml = '';
        if (t.subTaskCount > 0) {
            subtaskHtml = `
                <div class="subtask-progress">
                    <div class="progress-bar"><div class="progress-fill" style="width:${t.subTaskProgress}%"></div></div>
                    <small>${t.completedSubTaskCount}/${t.subTaskCount} sub-tasks</small>
                </div>
                <div class="subtask-list" id="subtasks-${t.taskId}" style="display:none;">
                    ${(t.subTasks || []).map(s => `
                        <div class="subtask-card ${s.isCompleted ? 'completed' : ''}">
                            <input type="checkbox" ${s.isCompleted ? 'checked' : ''} class="subtask-check" data-task-id="${s.taskId}">
                            <span>${s.title}</span>
                            ${s.estimatedHours ? `<span class="text-muted">${s.estimatedHours}h</span>` : ''}
                        </div>
                    `).join('')}
                </div>`;
        }

        return `
        <div class="task-card ${t.isCompleted ? 'completed' : ''}" data-id="${t.taskId}">
            <div class="task-checkbox">
                <input type="checkbox" ${t.isCompleted ? 'checked' : ''} data-task-id="${t.taskId}" class="task-check">
            </div>
            <div class="task-info">
                <div class="task-title">${t.title}</div>
                <div class="task-meta">
                    <span class="task-course-tag">${t.courseName}</span>
                    <span class="task-type">${t.type}</span>
                    ${t.estimatedHours ? `<span class="task-hours">${t.estimatedHours}h</span>` : ''}
                    ${t.actualHours ? `<span class="task-hours actual">(actual: ${t.actualHours}h)</span>` : ''}
                    ${scheduleBadge}
                    ${sharedBadge}
                </div>
                ${subtaskHtml}
            </div>
            <div class="task-right">
                ${t.priority ? `<span class="badge badge-priority-${t.priority.toLowerCase()}">${t.priority}</span>` : ''}
                ${dueDate ? `<span class="task-due ${isOverdue ? 'overdue' : ''}">${isOverdue ? 'Overdue' : daysLeft + 'd left'}</span>` : ''}
                <div class="task-actions">
                    ${!t.isCompleted && !t.subTaskCount ? `<button class="btn btn-ghost btn-sm task-split" data-id="${t.taskId}" title="Split">&#9986;</button>` : ''}
                    ${t.subTaskCount > 0 ? `<button class="btn btn-ghost btn-sm task-expand" data-id="${t.taskId}" title="Expand">&#9660;</button>` : ''}
                    <button class="btn btn-ghost btn-sm task-edit" data-id="${t.taskId}" title="Edit">&#9998;</button>
                    <button class="btn btn-ghost btn-sm task-delete" data-id="${t.taskId}" title="Delete">&#128465;</button>
                </div>
            </div>
        </div>`;
    }).join('');

    // Event listeners
    el.querySelectorAll('.task-check').forEach(cb => {
        cb.addEventListener('change', (e) => {
            e.preventDefault();
            cb.checked = !cb.checked; // Revert - handled by modal
            const taskId = parseInt(cb.dataset.taskId);
            const task = allTasks.find(t => t.taskId === taskId);
            if (task && !task.isCompleted) {
                showCompletionModal(taskId);
            } else {
                doComplete(taskId, null);
            }
        });
    });

    el.querySelectorAll('.subtask-check').forEach(cb => {
        cb.addEventListener('change', async () => {
            const taskId = parseInt(cb.dataset.taskId);
            try {
                await api.completeTask(taskId, {});
                allTasks = await api.getTasks();
                renderTasks(applyFilters());
                showToast('Sub-task updated');
            } catch { showToast('Failed to update', 'error'); }
        });
    });

    el.querySelectorAll('.task-delete').forEach(btn => {
        btn.addEventListener('click', async () => {
            const id = parseInt(btn.dataset.id);
            try {
                await api.deleteTask(id);
                allTasks = allTasks.filter(t => t.taskId !== id);
                renderTasks(applyFilters());
                showToast('Task deleted');
            } catch { showToast('Failed to delete task', 'error'); }
        });
    });

    el.querySelectorAll('.task-edit').forEach(btn => {
        btn.addEventListener('click', () => editTask(parseInt(btn.dataset.id)));
    });

    el.querySelectorAll('.task-split').forEach(btn => {
        btn.addEventListener('click', () => showSplitModal(parseInt(btn.dataset.id)));
    });

    el.querySelectorAll('.task-expand').forEach(btn => {
        btn.addEventListener('click', () => {
            const id = btn.dataset.id;
            const list = document.getElementById(`subtasks-${id}`);
            if (list) {
                list.style.display = list.style.display === 'none' ? 'block' : 'none';
                btn.innerHTML = list.style.display === 'none' ? '&#9660;' : '&#9650;';
            }
        });
    });
}

function renderSharedInvitations() {
    const el = document.getElementById('sharedInvitations');
    if (!el) return;

    const userEmail = JSON.parse(localStorage.getItem('smartstudy_user') || '{}').email;
    const pending = sharedTasks.filter(st =>
        st.sharedStatus === 'Pending' && st.members?.some(m => m.email === userEmail && m.responseStatus === 'Pending'));

    if (!pending.length) { el.style.display = 'none'; return; }

    el.style.display = 'block';
    el.innerHTML = `<h3 style="margin-bottom:12px;">Shared Task Invitations</h3>` +
        pending.map(st => `
        <div class="invitation-card">
            <div>
                <strong>${st.taskTitle}</strong>
                <span class="text-muted"> from ${st.createdByName}</span>
                ${st.courseName ? `<span class="task-course-tag">${st.courseName}</span>` : ''}
            </div>
            <div class="invitation-actions">
                <button class="btn btn-sm btn-primary accept-shared" data-id="${st.taskId}">Accept</button>
                <button class="btn btn-sm btn-secondary decline-shared" data-id="${st.taskId}">Decline</button>
            </div>
        </div>`).join('');

    el.querySelectorAll('.accept-shared').forEach(btn => {
        btn.addEventListener('click', async () => {
            try {
                await api.respondSharedTask(parseInt(btn.dataset.id), true);
                sharedTasks = await api.getSharedTasks();
                allTasks = await api.getTasks();
                renderSharedInvitations();
                renderTasks(applyFilters());
                showToast('Task accepted');
            } catch { showToast('Failed to respond', 'error'); }
        });
    });

    el.querySelectorAll('.decline-shared').forEach(btn => {
        btn.addEventListener('click', async () => {
            try {
                await api.respondSharedTask(parseInt(btn.dataset.id), false);
                sharedTasks = await api.getSharedTasks();
                renderSharedInvitations();
                showToast('Task declined');
            } catch { showToast('Failed to respond', 'error'); }
        });
    });
}

export function showCompletionModal(taskId) {
    const task = allTasks.find(t => t.taskId === taskId);
    if (!task) return;
    completingTaskId = taskId;
    document.getElementById('completeTaskName').textContent = task.title;
    document.getElementById('completeEstimated').textContent = task.estimatedHours ? `Estimated: ${task.estimatedHours} hours` : '';
    document.getElementById('actualHours').value = '';
    openModal('completeModal');
}

function setupCompletionModal() {
    document.getElementById('completeConfirm')?.addEventListener('click', () => {
        const hours = parseFloat(document.getElementById('actualHours').value) || null;
        doComplete(completingTaskId, hours);
        closeModal('completeModal');
    });
    document.getElementById('completeSkip')?.addEventListener('click', () => {
        closeModal('completeModal');
    });
}

async function doComplete(taskId, actualHours) {
    try {
        const task = allTasks.find(t => t.taskId === taskId);
        const result = await api.completeTask(taskId, { actualHours });

        // Machine Learning hint: if actual hours deviate >30% from estimate
        if (actualHours && task?.estimatedHours && parseFloat(task.estimatedHours) > 0) {
            const ratio = actualHours / parseFloat(task.estimatedHours);
            if (ratio > 1.3 || ratio < 0.7) {
                const direction = ratio > 1 ? 'longer' : 'shorter';
                setTimeout(() => showToast(`Tip: This task took ${direction} than estimated (${ratio.toFixed(1)}x). Future estimates will improve.`, 'info'), 500);
            }
        }

        // Show course accuracy feedback from Machine Learning stats
        if (result?.mlStats?.sampleSize >= 3) {
            const bias = result.mlStats.estimationBias;
            if (bias && bias !== 'accurate') {
                const biasMsg = bias === 'underestimate'
                    ? 'You tend to underestimate tasks in this course.'
                    : 'You tend to overestimate tasks in this course.';
                setTimeout(() => showToast(`${biasMsg} Avg ratio: ${result.mlStats.courseAvgRatio.toFixed(2)}x (${result.mlStats.sampleSize} tasks)`, 'info'), 1000);
            }
        }

        allTasks = await api.getTasks();
        renderTasks(applyFilters());
        showToast('Task completed!', 'success');
    } catch { showToast('Failed to update task', 'error'); }
}

function showSplitModal(taskId) {
    const task = allTasks.find(t => t.taskId === taskId);
    if (!task) return;
    document.getElementById('splitParentName').textContent = task.title;
    document.getElementById('subtaskRows').innerHTML = '';
    document.getElementById('splitConfirm').dataset.taskId = taskId;
    addSubtaskRow();
    addSubtaskRow();
    openModal('splitTaskModal');
}

function addSubtaskRow() {
    const container = document.getElementById('subtaskRows');
    const row = document.createElement('div');
    row.className = 'subtask-row form-row';
    row.innerHTML = `
        <div class="form-group" style="flex:3">
            <input type="text" class="form-input subtask-title" placeholder="Sub-task title" required>
        </div>
        <div class="form-group" style="flex:1">
            <input type="number" class="form-input subtask-hours" placeholder="Hours" step="0.5" min="0">
        </div>
        <button type="button" class="btn btn-ghost btn-sm subtask-remove" style="align-self:center;">&times;</button>`;
    container.appendChild(row);
    row.querySelector('.subtask-remove').addEventListener('click', () => row.remove());
}

function setupSplitModal() {
    document.getElementById('addSubtaskRow')?.addEventListener('click', addSubtaskRow);
    document.getElementById('splitConfirm')?.addEventListener('click', async () => {
        const taskId = parseInt(document.getElementById('splitConfirm').dataset.taskId);
        const rows = document.querySelectorAll('#subtaskRows .subtask-row');
        const subTasks = [];
        rows.forEach(row => {
            const title = row.querySelector('.subtask-title').value.trim();
            const hours = parseFloat(row.querySelector('.subtask-hours').value) || null;
            if (title) subTasks.push({ title, estimatedHours: hours });
        });
        if (!subTasks.length) { showToast('Add at least one sub-task', 'error'); return; }
        try {
            await api.splitTask(taskId, { subTasks });
            allTasks = await api.getTasks();
            renderTasks(applyFilters());
            closeModal('splitTaskModal');
            showToast('Task split into sub-tasks');
        } catch (err) { showToast(err.message || 'Failed to split', 'error'); }
    });
}

function setupSharedToggle() {
    const toggle = document.getElementById('taskShared');
    const selector = document.getElementById('friendSelector');
    const friendSelect = document.getElementById('taskFriend');
    if (!toggle || !selector || !friendSelect) return;

    toggle.addEventListener('change', () => {
        selector.style.display = toggle.checked ? 'block' : 'none';
    });

    // Populate friends
    friendSelect.innerHTML = '<option value="">Select friend...</option>' +
        friends.map(f => `<option value="${f.email}">${f.name} (${f.email})</option>`).join('');
}

function setupHoursSuggestion() {
    const courseSelect = document.getElementById('taskCourseId');
    const hoursInput = document.getElementById('taskHours');
    const suggestion = document.getElementById('hoursSuggestion');
    if (!courseSelect || !hoursInput || !suggestion) return;

    const fetchSuggestion = async () => {
        const courseId = courseSelect.value;
        const hours = hoursInput.value;
        if (!courseId) { suggestion.style.display = 'none'; return; }
        try {
            const data = await api.getSuggestedHours(courseId, hours || null);
            if (data.hasSuggestion) {
                const text = data.suggestedHours
                    ? `Machine Learning suggests ~${data.suggestedHours}h (based on ${data.sampleSize} past tasks, ${data.adjustmentFactor}x ratio)`
                    : `Past accuracy: ${data.adjustmentFactor}x estimated (${data.sampleSize} tasks)`;
                suggestion.textContent = text;
                suggestion.style.display = 'block';
            } else {
                suggestion.style.display = 'none';
            }
        } catch { suggestion.style.display = 'none'; }
    };

    courseSelect.addEventListener('change', fetchSuggestion);
    hoursInput.addEventListener('change', fetchSuggestion);
}

function populateCourseFilter() {
    const container = document.getElementById('filterCourseCheckboxes');
    const formSelect = document.getElementById('taskCourseId');
    if (container) {
        container.innerHTML = courses.map(c =>
            `<label><input type="checkbox" name="filterCourse" value="${c.courseId}"> ${c.courseName}</label>`
        ).join('');
    }
    if (formSelect) {
        formSelect.innerHTML = '<option value="">Select course...</option>' +
            courses.map(c => `<option value="${c.courseId}">${c.courseName}</option>`).join('');
    }
}

function setupFilters() {
    const toggle = document.getElementById('filterToggle');
    const panel = document.getElementById('filterPanel');
    if (toggle && panel) {
        toggle.addEventListener('click', () => {
            const open = panel.style.display !== 'none';
            panel.style.display = open ? 'none' : 'flex';
        });
    }
    // Listen to checkbox changes inside the filter panel
    document.getElementById('filterPanel')?.addEventListener('change', () => {
        renderTasks(applyFilters());
        updateFilterBadge();
    });
}

function updateFilterBadge() {
    const toggle = document.getElementById('filterToggle');
    if (!toggle) return;
    const checked = document.querySelectorAll('#filterPanel input[type="checkbox"]:checked').length;
    toggle.textContent = checked > 0 ? `Filter (${checked})` : 'Filter';
}

function applyFilters() {
    let filtered = [...allTasks];

    // Status filter
    const statuses = [...document.querySelectorAll('input[name="filterStatus"]:checked')].map(cb => cb.value);
    if (statuses.length) {
        filtered = filtered.filter(t => {
            if (statuses.includes('completed') && t.isCompleted) return true;
            if (statuses.includes('pending') && !t.isCompleted) return true;
            return false;
        });
    }

    // Course filter
    const courseIds = [...document.querySelectorAll('input[name="filterCourse"]:checked')].map(cb => parseInt(cb.value));
    if (courseIds.length) {
        filtered = filtered.filter(t => courseIds.includes(t.courseId));
    }

    // Priority filter
    const priorities = [...document.querySelectorAll('input[name="filterPriority"]:checked')].map(cb => cb.value);
    if (priorities.length) {
        filtered = filtered.filter(t => priorities.includes(t.priority?.toLowerCase()));
    }

    return filtered;
}

function setupAddTask() {
    document.getElementById('addTaskBtn')?.addEventListener('click', () => {
        document.getElementById('taskForm')?.reset();
        document.getElementById('taskModalTitle').textContent = 'Add Task';
        document.getElementById('taskId').value = '';
        document.getElementById('sharedSection').style.display = 'block';
        document.getElementById('friendSelector').style.display = 'none';
        document.getElementById('hoursSuggestion').style.display = 'none';
        const dueDateInput = document.getElementById('taskDueDate');
        if (dueDateInput) dueDateInput.min = new Date().toISOString().split('T')[0];
        openModal('taskModal');
    });

    document.getElementById('taskForm')?.addEventListener('submit', async (e) => {
        e.preventDefault();
        const taskId = document.getElementById('taskId').value;
        const data = {
            courseId: parseInt(document.getElementById('taskCourseId').value),
            title: document.getElementById('taskTitle').value,
            type: document.getElementById('taskType').value,
            estimatedHours: parseFloat(document.getElementById('taskHours').value) || null,
            dueDate: document.getElementById('taskDueDate').value || null,
            allowSplitting: document.getElementById('taskAllowSplitting')?.checked || false,
        };

        try {
            if (taskId) {
                const updated = await api.updateTask(parseInt(taskId), data);
                const idx = allTasks.findIndex(t => t.taskId === parseInt(taskId));
                if (idx >= 0) allTasks[idx] = updated;
                showToast('Task updated');
            } else {
                const created = await api.createTask(data);
                allTasks.push(created);
                showToast('Task created');

                // Share task if toggle is on
                const isShared = document.getElementById('taskShared')?.checked;
                const friendEmail = document.getElementById('taskFriend')?.value;
                if (isShared && friendEmail && created.taskId) {
                    try {
                        await api.createSharedTask({ taskId: created.taskId, partnerEmail: friendEmail });
                        showToast('Task shared with friend');
                    } catch (err) { showToast('Task created but sharing failed: ' + err.message, 'error'); }
                }
            }
            closeModal('taskModal');
            allTasks = await api.getTasks();
            renderTasks(applyFilters());
        } catch (err) {
            showToast(err.message || 'Failed to save task', 'error');
        }
    });
}

function setupReschedule() {
    const btn = document.getElementById('rescheduleBtn');
    if (!btn) return;
    btn.addEventListener('click', async () => {
        try {
            btn.disabled = true;
            btn.textContent = 'Scheduling...';
            const result = await api.runScheduling();
            showToast(`Scheduled ${result.scheduledCount} task(s)${result.unscheduledCount ? `, ${result.unscheduledCount} could not be scheduled` : ''}`);
            allTasks = await api.getTasks();
            renderTasks(applyFilters());
        } catch (err) {
            showToast('Failed to reschedule', 'error');
        } finally {
            btn.disabled = false;
            btn.textContent = 'Reschedule All';
        }
    });
}

function editTask(id) {
    const task = allTasks.find(t => t.taskId === id);
    if (!task) return;

    document.getElementById('taskModalTitle').textContent = 'Edit Task';
    document.getElementById('taskId').value = task.taskId;
    document.getElementById('taskCourseId').value = task.courseId;
    document.getElementById('taskTitle').value = task.title;
    document.getElementById('taskType').value = task.type;
    document.getElementById('taskHours').value = task.estimatedHours || '';
    document.getElementById('taskDueDate').value = task.dueDate ? task.dueDate.split('T')[0] : '';
    const splitToggle = document.getElementById('taskAllowSplitting');
    if (splitToggle) splitToggle.checked = task.allowSplitting || false;
    document.getElementById('sharedSection').style.display = 'block';
    // Pre-populate shared toggle if task is already shared
    const sharedToggle = document.getElementById('taskShared');
    const friendSelector = document.getElementById('friendSelector');
    if (sharedToggle && friendSelector) {
        if (task.isShared && task.sharedWithEmail) {
            sharedToggle.checked = true;
            friendSelector.style.display = 'block';
            const friendSelect = document.getElementById('taskFriend');
            if (friendSelect) friendSelect.value = task.sharedWithEmail;
        } else {
            sharedToggle.checked = false;
            friendSelector.style.display = 'none';
        }
    }
    document.getElementById('hoursSuggestion').style.display = 'none';
    const dueDateInput = document.getElementById('taskDueDate');
    if (dueDateInput) dueDateInput.removeAttribute('min');
    openModal('taskModal');
}
