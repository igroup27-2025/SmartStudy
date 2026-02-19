// API Client module - handles all API calls with authentication (jQuery $.ajax)
import { BASE_PATH, API_BASE_PATH } from './config.js';
const API_BASE = API_BASE_PATH + '/api';

function toLocalISO(d) {
    return `${d.getFullYear()}-${String(d.getMonth()+1).padStart(2,'0')}-${String(d.getDate()).padStart(2,'0')}T${String(d.getHours()).padStart(2,'0')}:${String(d.getMinutes()).padStart(2,'0')}:${String(d.getSeconds()).padStart(2,'0')}`;
}

function getToken() {
    return localStorage.getItem('smartstudy_token');
}

function getHeaders() {
    const headers = { 'Content-Type': 'application/json' };
    const token = getToken();
    if (token) headers['Authorization'] = `Bearer ${token}`;
    return headers;
}

function request(method, path, body = null) {
    return new Promise((resolve, reject) => {
        $.ajax({
            url: `${API_BASE}${path}`,
            method: method,
            headers: getHeaders(),
            data: body ? JSON.stringify(body) : undefined,
            contentType: 'application/json',
            dataType: method === 'DELETE' ? 'text' : 'json',
            success: (data) => resolve(data),
            error: (xhr) => {
                if (xhr.status === 401 && !path.startsWith('/auth/')) {
                    localStorage.removeItem('smartstudy_token');
                    localStorage.removeItem('smartstudy_user');
                    window.location.href = BASE_PATH + '/Pages/Login.html';
                }
                reject(new Error(xhr.responseJSON?.message || `Request failed: ${xhr.status}`));
            }
        });
    });
}

export const api = {
    // Auth
    login: (email, password) => request('POST', '/auth/login', { email, password }),
    register: (data) => request('POST', '/auth/register', data),
    logout: () => request('POST', '/auth/logout'),
    forgotPassword: (email) => request('POST', '/auth/forgot-password', { email }),
    resetPassword: (data) => request('POST', '/auth/reset-password', data),
    googleLogin: (idToken) => request('POST', '/auth/google', { idToken }),
    getAuthConfig: () => request('GET', '/auth/config'),

    // Dashboard
    getDashboard: () => request('GET', '/dashboard'),
    getWeeklySuggestions: () => request('GET', '/dashboard/weekly-suggestions'),

    // Courses
    getCourses: () => request('GET', '/courses'),
    getCourse: (id) => request('GET', `/courses/${id}`),
    createCourse: (data) => request('POST', '/courses', data),
    updateCourse: (id, data) => request('PUT', `/courses/${id}`, data),
    deleteCourse: (id) => request('DELETE', `/courses/${id}`),
    setStudyPartner: (courseId, email) => request('PUT', `/courses/${courseId}/partner`, { email }),

    // Tasks
    getTasks: (params = {}) => {
        const qs = new URLSearchParams();
        if (params.courseId) qs.set('courseId', params.courseId);
        if (params.completed !== undefined) qs.set('completed', params.completed);
        const query = qs.toString();
        return request('GET', `/tasks${query ? '?' + query : ''}`);
    },
    getTask: (id) => request('GET', `/tasks/${id}`),
    createTask: (data) => request('POST', '/tasks', data),
    updateTask: (id, data) => request('PUT', `/tasks/${id}`, data),
    deleteTask: (id) => request('DELETE', `/tasks/${id}`),
    completeTask: (id, data = {}) => request('POST', `/tasks/${id}/complete`, data),
    splitTask: (id, data) => request('POST', `/tasks/${id}/split`, data),
    getSuggestedHours: (courseId, estimatedHours) => {
        const qs = new URLSearchParams({ courseId });
        if (estimatedHours) qs.set('estimatedHours', estimatedHours);
        return request('GET', `/tasks/suggest-hours?${qs.toString()}`);
    },
    getLearningInsights: () => request('GET', '/tasks/learning-insights'),

    // Exams
    getExams: () => request('GET', '/exams'),
    getExam: (id) => request('GET', `/exams/${id}`),
    createExam: (data) => request('POST', '/exams', data),
    updateExam: (id, data) => request('PUT', `/exams/${id}`, data),
    deleteExam: (id) => request('DELETE', `/exams/${id}`),
    toggleExamTaking: (id) => request('PUT', `/exams/${id}/toggle-taking`),

    // Events
    getEvents: (from, to) => {
        const qs = new URLSearchParams();
        if (from) qs.set('from', toLocalISO(from));
        if (to) qs.set('to', toLocalISO(to));
        const query = qs.toString();
        return request('GET', `/events${query ? '?' + query : ''}`);
    },
    createClassEvent: (data) => request('POST', '/events/class', data),
    createTaskEvent: (data) => request('POST', '/events/task', data),
    createWorkEvent: (data) => request('POST', '/events/work', data),
    createPersonalEvent: (data) => request('POST', '/events/personal', data),
    updateClassEvent: (id, data) => request('PUT', `/events/class/${id}`, data),
    updateTaskEvent: (id, data) => request('PUT', `/events/task/${id}`, data),
    updateWorkEvent: (id, data) => request('PUT', `/events/work/${id}`, data),
    updatePersonalEvent: (id, data) => request('PUT', `/events/personal/${id}`, data),
    deleteEvent: (id) => request('DELETE', `/events/${id}`),
    changeEventType: (id, data) => request('PUT', `/events/${id}/change-type`, data),
    checkConflicts: (data) => request('POST', '/events/check-conflicts', data),

    // Schedule Import
    importSchedule: (file) => {
        return new Promise((resolve, reject) => {
            const formData = new FormData();
            formData.append('file', file);
            const token = localStorage.getItem('smartstudy_token');
            const headers = {};
            if (token) headers['Authorization'] = `Bearer ${token}`;

            $.ajax({
                url: `${API_BASE}/schedule/import`,
                method: 'POST',
                headers: headers,
                data: formData,
                processData: false,
                contentType: false,
                success: (data) => resolve(data),
                error: (xhr) => {
                    if (xhr.status === 401) {
                        localStorage.removeItem('smartstudy_token');
                        window.location.href = BASE_PATH + '/Pages/Login.html';
                    }
                    reject(new Error(xhr.responseJSON?.message || 'Import failed'));
                }
            });
        });
    },

    // Scheduling
    runScheduling: () => request('POST', '/scheduling/run'),
    getSchedulingStatus: () => request('GET', '/scheduling/status'),
    approveScheduledTask: (taskId) => request('POST', `/scheduling/approve/${taskId}`),

    // Stress
    getStressScore: () => request('GET', '/stress/score'),
    getWeeklyStress: () => request('GET', '/stress/weekly'),

    // Connections / Friends
    getConnections: () => request('GET', '/connections'),
    inviteConnection: (email) => request('POST', '/connections/invite', { email }),
    acceptConnection: (id) => request('POST', `/connections/${id}/accept`),
    declineConnection: (id) => request('POST', `/connections/${id}/decline`),
    removeConnection: (id) => request('DELETE', `/connections/${id}`),

    // Collaboration
    getSafeZones: (connectionId) => request('GET', `/collaboration/safe-zones?connectionId=${connectionId}`),
    approveCourseSharing: (courseId) => request('POST', `/collaboration/approve-course-sharing/${courseId}`),

    // Shared Tasks
    getSharedTasks: () => request('GET', '/shared-tasks'),
    getSharedTask: (taskId) => request('GET', `/shared-tasks/${taskId}`),
    createSharedTask: (data) => request('POST', '/shared-tasks', data),
    respondSharedTask: (taskId, accept) => request('POST', `/shared-tasks/${taskId}/respond`, { accept }),
    cancelSharedTask: (taskId) => request('POST', `/shared-tasks/${taskId}/cancel`),

    // Notifications
    getNotifications: () => request('GET', '/notifications'),
    getUnreadCount: () => request('GET', '/notifications/unread-count'),
    markNotificationsRead: (ids) => request('POST', '/notifications/mark-read', { notificationIds: ids }),
    markAllNotificationsRead: () => request('POST', '/notifications/mark-all-read'),
    generateNotifications: () => request('POST', '/notifications/generate'),

    // Settings
    getProfile: () => request('GET', '/settings/profile'),
    updateProfile: (data) => request('PUT', '/settings/profile', data),
    updateNotifications: (data) => request('PUT', '/settings/notifications', data),
    getInstructors: () => request('GET', '/settings/instructors'),

    // Calendar Sync (Composio)
    connectGoogleCalendar: (callbackUrl) => request('POST', '/calendar-sync/connect', { callbackUrl }),
    syncGoogleCalendar: (accessToken) => request('POST', '/calendar-sync/google', accessToken ? { accessToken } : {}),
    getCalendarSyncStatus: () => request('GET', '/calendar-sync/status'),
    disconnectGoogleCalendar: () => {
        return new Promise((resolve, reject) => {
            $.ajax({
                url: `${API_BASE}/calendar-sync/disconnect`,
                method: 'DELETE',
                headers: getHeaders(),
                success: (data) => resolve(typeof data === 'string' ? JSON.parse(data || '{}') : data),
                error: (xhr) => {
                    if (xhr.status === 401) {
                        localStorage.removeItem('smartstudy_token');
                        window.location.href = BASE_PATH + '/Pages/Login.html';
                    }
                    reject(new Error(xhr.responseJSON?.message || 'Disconnect failed'));
                }
            });
        });
    },

    // Scheduling Preferences
    getSchedulingPrefs: () => request('GET', '/settings/scheduling'),
    updateSchedulingPrefs: (data) => request('PUT', '/settings/scheduling', data),
    saveOnboarding: (data) => request('PUT', '/settings/onboarding', data),
};
