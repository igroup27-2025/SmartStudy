import { api } from './api.js';
import { openModal, closeModal, showToast } from './modals.js';

let allTasks = [];
let courses = [];

export async function initTasks() {
    try {
        [allTasks, courses] = await Promise.all([api.getTasks(), api.getCourses()]);
        renderTasks(allTasks);
        populateCourseFilter();
        setupFilters();
        setupAddTask();
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
                </div>
            </div>
            <div class="task-right">
                ${t.priority ? `<span class="badge badge-priority-${t.priority.toLowerCase()}">${t.priority}</span>` : ''}
                ${dueDate ? `<span class="task-due ${isOverdue ? 'overdue' : ''}">${isOverdue ? 'Overdue' : daysLeft + 'd left'}</span>` : ''}
                <div class="task-actions">
                    <button class="btn btn-ghost btn-sm task-edit" data-id="${t.taskId}" title="Edit">&#9998;</button>
                    <button class="btn btn-ghost btn-sm task-delete" data-id="${t.taskId}" title="Delete">&#128465;</button>
                </div>
            </div>
        </div>`;
    }).join('');

    // Event listeners
    el.querySelectorAll('.task-check').forEach(cb => {
        cb.addEventListener('change', async () => {
            try {
                await api.completeTask(parseInt(cb.dataset.taskId));
                const task = allTasks.find(t => t.taskId === parseInt(cb.dataset.taskId));
                if (task) task.isCompleted = !task.isCompleted;
                renderTasks(applyFilters());
                showToast('Task updated');
            } catch { showToast('Failed to update task', 'error'); }
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
}

function populateCourseFilter() {
    const select = document.getElementById('filterCourse');
    const formSelect = document.getElementById('taskCourseId');
    if (select) {
        select.innerHTML = '<option value="">All Courses</option>' +
            courses.map(c => `<option value="${c.courseId}">${c.courseName}</option>`).join('');
    }
    if (formSelect) {
        formSelect.innerHTML = '<option value="">Select course...</option>' +
            courses.map(c => `<option value="${c.courseId}">${c.courseName}</option>`).join('');
    }
}

function setupFilters() {
    document.getElementById('filterCourse')?.addEventListener('change', () => renderTasks(applyFilters()));
    document.getElementById('filterStatus')?.addEventListener('change', () => renderTasks(applyFilters()));
    document.getElementById('filterPriority')?.addEventListener('change', () => renderTasks(applyFilters()));
}

function applyFilters() {
    let filtered = [...allTasks];
    const courseId = document.getElementById('filterCourse')?.value;
    const status = document.getElementById('filterStatus')?.value;
    const priority = document.getElementById('filterPriority')?.value;

    if (courseId) filtered = filtered.filter(t => t.courseId === parseInt(courseId));
    if (status === 'completed') filtered = filtered.filter(t => t.isCompleted);
    if (status === 'pending') filtered = filtered.filter(t => !t.isCompleted);
    if (priority) filtered = filtered.filter(t => t.priority?.toLowerCase() === priority);
    return filtered;
}

function setupAddTask() {
    document.getElementById('addTaskBtn')?.addEventListener('click', () => {
        document.getElementById('taskForm')?.reset();
        document.getElementById('taskModalTitle').textContent = 'Add Task';
        document.getElementById('taskId').value = '';
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
            priority: document.getElementById('taskPriority').value || null,
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
            }
            closeModal('taskModal');
            renderTasks(applyFilters());
        } catch (err) {
            showToast(err.message || 'Failed to save task', 'error');
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
    document.getElementById('taskPriority').value = task.priority || '';
    openModal('taskModal');
}
