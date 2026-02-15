import { api } from './api.js';
import { openModal, closeModal, showToast } from './modals.js';

let courses = [];
let instructors = [];
let friends = [];

const COURSE_COLORS = ['#4A90D9', '#F28D35', '#9B76FF', '#43B88C', '#E84B6A', '#54BFB5'];

export async function initCourses() {
    try {
        [courses, instructors] = await Promise.all([api.getCourses(), api.getInstructors()]);
        // Load friends for study partner selection
        try {
            const connections = await api.getConnections();
            friends = (connections.friends || []);
        } catch { friends = []; }

        renderCourses();
        setupAddCourse();
    } catch (err) {
        showToast('Failed to load courses', 'error');
    }
}

function renderCourses() {
    const el = document.getElementById('courseGrid');
    if (!el) return;

    if (!courses.length) {
        el.innerHTML = `<div class="empty-state">
            <div class="empty-state-icon">&#128218;</div>
            <h3>No courses yet</h3>
            <p>Add your first course to get started</p>
        </div>`;
        return;
    }

    el.innerHTML = courses.map((c, i) => `
        <div class="course-card" data-id="${c.courseId}">
            <div class="course-card-header" style="background: ${COURSE_COLORS[i % COURSE_COLORS.length]}"></div>
            <div class="course-card-body">
                <div class="course-card-title-row">
                    <h3 class="course-card-name">${c.courseName}</h3>
                    <div class="course-card-actions">
                        <button class="btn btn-ghost btn-sm course-edit" data-id="${c.courseId}" title="Edit">&#9998;</button>
                        <button class="btn btn-ghost btn-sm course-delete" data-id="${c.courseId}" title="Delete">&#128465;</button>
                    </div>
                </div>
                ${c.instructorName ? `<div class="course-instructor">${c.instructorName}</div>` : ''}
                <div class="course-stats">
                    <div class="course-stat">
                        <span class="course-stat-value">${c.credits || '-'}</span>
                        <span class="course-stat-label">Credits</span>
                    </div>
                    <div class="course-stat">
                        <span class="course-stat-value">${c.weeklyHours || '-'}</span>
                        <span class="course-stat-label">Hrs/Week</span>
                    </div>
                    <div class="course-stat">
                        <span class="course-stat-value">${c.taskCount || 0}</span>
                        <span class="course-stat-label">Tasks</span>
                    </div>
                    <div class="course-stat">
                        <span class="course-stat-value">${c.examCount || 0}</span>
                        <span class="course-stat-label">Exams</span>
                    </div>
                </div>
                ${c.semester ? `<div class="course-semester">Semester: ${c.semester}</div>` : ''}
                ${c.studyPartnerName ? `<div class="course-partner">Study Partner: ${c.studyPartnerName}</div>` : ''}
                ${c.sharedByDefault ? '<span class="badge badge-info" style="margin-top:6px;display:inline-block;">Shared by default</span>' : ''}
            </div>
        </div>
    `).join('');

    el.querySelectorAll('.course-delete').forEach(btn => {
        btn.addEventListener('click', async (e) => {
            e.stopPropagation();
            const id = parseInt(btn.dataset.id);
            try {
                await api.deleteCourse(id);
                courses = courses.filter(c => c.courseId !== id);
                renderCourses();
                showToast('Course removed');
            } catch { showToast('Failed to remove course', 'error'); }
        });
    });

    el.querySelectorAll('.course-edit').forEach(btn => {
        btn.addEventListener('click', (e) => {
            e.stopPropagation();
            editCourse(parseInt(btn.dataset.id));
        });
    });
}

function setupAddCourse() {
    const instructorSelect = document.getElementById('courseInstructorId');
    if (instructorSelect) {
        instructorSelect.innerHTML = '<option value="">None</option>' +
            instructors.map(i => `<option value="${i.instructorId}">${i.instructorName}</option>`).join('');
    }

    // Populate study partner dropdown
    const partnerSelect = document.getElementById('courseStudyPartner');
    if (partnerSelect) {
        partnerSelect.innerHTML = '<option value="">None</option>' +
            friends.map(f => `<option value="${f.email}">${f.firstName} ${f.lastName}</option>`).join('');
    }

    document.getElementById('addCourseBtn')?.addEventListener('click', () => {
        document.getElementById('courseForm')?.reset();
        document.getElementById('courseModalTitle').textContent = 'Add Course';
        document.getElementById('courseId').value = '';
        if (partnerSelect) partnerSelect.value = '';
        openModal('courseModal');
    });

    document.getElementById('courseForm')?.addEventListener('submit', async (e) => {
        e.preventDefault();
        const courseId = document.getElementById('courseId').value;
        const data = {
            courseName: document.getElementById('courseName').value,
            weeklyHours: parseFloat(document.getElementById('courseWeeklyHours').value) || null,
            credits: parseFloat(document.getElementById('courseCredits').value) || null,
            semester: document.getElementById('courseSemester').value || null,
            instructorId: parseInt(document.getElementById('courseInstructorId').value) || null,
            sharedByDefault: document.getElementById('courseSharedByDefault')?.checked || false,
        };

        try {
            if (courseId) {
                const updated = await api.updateCourse(parseInt(courseId), data);
                const idx = courses.findIndex(c => c.courseId === parseInt(courseId));
                if (idx >= 0) courses[idx] = { ...courses[idx], ...updated };
                showToast('Course updated');
            } else {
                const created = await api.createCourse(data);
                courses.push(created);
                showToast('Course added');
            }

            // Save study partner if selected
            const partnerEmail = document.getElementById('courseStudyPartner')?.value;
            const targetCourseId = courseId ? parseInt(courseId) : courses[courses.length - 1]?.courseId;
            if (targetCourseId) {
                const currentPartner = courses.find(c => c.courseId === targetCourseId)?.studyPartnerEmail;
                if (partnerEmail !== (currentPartner || '')) {
                    try {
                        await api.setStudyPartner(targetCourseId, partnerEmail || null);
                    } catch { /* silent - partner set is optional */ }
                }
            }

            closeModal('courseModal');
            // Refresh courses to get updated partner info
            courses = await api.getCourses();
            renderCourses();
        } catch (err) {
            showToast(err.message || 'Failed to save course', 'error');
        }
    });
}

function editCourse(id) {
    const course = courses.find(c => c.courseId === id);
    if (!course) return;

    document.getElementById('courseModalTitle').textContent = 'Edit Course';
    document.getElementById('courseId').value = course.courseId;
    document.getElementById('courseName').value = course.courseName;
    document.getElementById('courseWeeklyHours').value = course.weeklyHours || '';
    document.getElementById('courseCredits').value = course.credits || '';
    document.getElementById('courseSemester').value = course.semester || '';
    document.getElementById('courseInstructorId').value = course.instructorId || '';

    // Set study partner
    const partnerSelect = document.getElementById('courseStudyPartner');
    if (partnerSelect) {
        partnerSelect.value = course.studyPartnerEmail || '';
    }

    // Set shared by default
    const sharedCheck = document.getElementById('courseSharedByDefault');
    if (sharedCheck) sharedCheck.checked = course.sharedByDefault || false;

    openModal('courseModal');
}
