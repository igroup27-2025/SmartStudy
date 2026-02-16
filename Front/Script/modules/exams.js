import { api } from './api.js';
import { openModal, closeModal, showToast } from './modals.js';

let exams = [];
let courses = [];

export async function initExams() {
    try {
        [exams, courses] = await Promise.all([api.getExams(), api.getCourses()]);
        renderExams();
        setupAddExam();
    } catch (err) {
        showToast('Failed to load exams', 'error');
    }
}

function renderExams() {
    const el = document.getElementById('examList');
    if (!el) return;

    if (!exams.length) {
        el.innerHTML = `<div class="empty-state">
            <div class="empty-state-icon">&#128221;</div>
            <h3>No exams scheduled</h3>
            <p>Add your first exam to track deadlines</p>
        </div>`;
        return;
    }

    el.innerHTML = exams.map(e => {
        const date = new Date(e.date);
        const urgency = e.daysUntil <= 3 ? 'imminent' : e.daysUntil <= 7 ? 'soon' : 'far';
        return `
        <div class="exam-card" data-id="${e.examId}">
            <div class="exam-card-left">
                <div class="exam-date-block">
                    <span class="exam-date-day">${date.getDate()}</span>
                    <span class="exam-date-month">${date.toLocaleDateString('en', { month: 'short' })}</span>
                </div>
            </div>
            <div class="exam-card-info">
                <h3 class="exam-card-title">${e.courseName}</h3>
                <div class="exam-card-details">
                    <span>Session ${e.session}</span>
                    <span>${e.time ? formatTime(e.time) : ''}</span>
                    <span>${e.duration ? e.duration + ' min' : ''}</span>
                </div>
            </div>
            <div class="exam-card-right">
                <div class="exam-countdown ${urgency}">
                    ${e.daysUntil <= 0 ? 'Today!' : e.daysUntil + ' days'}
                </div>
                <div class="exam-actions">
                    <button class="btn btn-ghost btn-sm exam-edit" data-id="${e.examId}">&#9998;</button>
                    <button class="btn btn-ghost btn-sm exam-delete" data-id="${e.examId}">&#128465;</button>
                </div>
            </div>
        </div>`;
    }).join('');

    el.querySelectorAll('.exam-delete').forEach(btn => {
        btn.addEventListener('click', async () => {
            const id = parseInt(btn.dataset.id);
            try {
                await api.deleteExam(id);
                exams = exams.filter(e => e.examId !== id);
                renderExams();
                showToast('Exam deleted');
            } catch { showToast('Failed to delete exam', 'error'); }
        });
    });

    el.querySelectorAll('.exam-edit').forEach(btn => {
        btn.addEventListener('click', () => editExam(parseInt(btn.dataset.id)));
    });
}

function setupAddExam() {
    const courseSelect = document.getElementById('examCourseId');
    if (courseSelect) {
        courseSelect.innerHTML = '<option value="">Select course...</option>' +
            courses.map(c => `<option value="${c.courseId}">${c.courseName}</option>`).join('');
    }

    document.getElementById('addExamBtn')?.addEventListener('click', () => {
        document.getElementById('examForm')?.reset();
        document.getElementById('examModalTitle').textContent = 'Add Exam';
        document.getElementById('examId').value = '';
        openModal('examModal');
    });

    document.getElementById('examForm')?.addEventListener('submit', async (e) => {
        e.preventDefault();
        const examId = document.getElementById('examId').value;
        const durationRaw = document.getElementById('examDuration').value;
        const duration = durationRaw ? parseInt(durationRaw) : null;
        if (duration !== null && duration <= 0) {
            showToast('Duration must be a positive number', 'error');
            return;
        }

        const data = {
            courseId: parseInt(document.getElementById('examCourseId').value),
            date: document.getElementById('examDate').value,
            time: document.getElementById('examTime').value,
            session: document.getElementById('examSession').value,
            duration,
        };

        try {
            if (examId) {
                const updated = await api.updateExam(parseInt(examId), data);
                const idx = exams.findIndex(ex => ex.examId === parseInt(examId));
                if (idx >= 0) exams[idx] = updated;
                showToast('Exam updated');
            } else {
                const created = await api.createExam(data);
                exams.push(created);
                exams.sort((a, b) => new Date(a.date) - new Date(b.date));
                showToast('Exam added');
            }
            closeModal('examModal');
            renderExams();
        } catch (err) {
            showToast(err.message || 'Failed to save exam', 'error');
        }
    });
}

function editExam(id) {
    const exam = exams.find(e => e.examId === id);
    if (!exam) return;

    document.getElementById('examModalTitle').textContent = 'Edit Exam';
    document.getElementById('examId').value = exam.examId;
    document.getElementById('examCourseId').value = exam.courseId;
    document.getElementById('examDate').value = exam.date.split('T')[0];
    document.getElementById('examTime').value = formatTime(exam.time);
    document.getElementById('examSession').value = exam.session;
    document.getElementById('examDuration').value = exam.duration || '';
    openModal('examModal');
}

function formatTime(time) {
    if (!time) return '';
    if (typeof time === 'string' && time.includes(':')) return time.substring(0, 5);
    return time;
}
