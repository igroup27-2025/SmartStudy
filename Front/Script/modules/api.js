// API Client module - handles all API calls with authentication
const API_BASE = '/api';

function getToken() {
    return localStorage.getItem('smartstudy_token');
}

function getHeaders() {
    const headers = { 'Content-Type': 'application/json' };
    const token = getToken();
    if (token) headers['Authorization'] = `Bearer ${token}`;
    return headers;
}

async function request(method, path, body = null) {
    const options = { method, headers: getHeaders() };
    if (body) options.body = JSON.stringify(body);

    const res = await fetch(`${API_BASE}${path}`, options);

    if (res.status === 401) {
        localStorage.removeItem('smartstudy_token');
        localStorage.removeItem('smartstudy_user');
        window.location.href = '/Pages/Login.html';
        throw new Error('Unauthorized');
    }

    if (res.status === 204) return null;

    const data = await res.json();
    if (!res.ok) throw new Error(data.message || `Request failed: ${res.status}`);
    return data;
}

export const api = {
    // Auth
    login: (email, password) => request('POST', '/auth/login', { email, password }),
    register: (data) => request('POST', '/auth/register', data),

    // Dashboard
    getDashboard: () => request('GET', '/dashboard'),

    // Courses
    getCourses: () => request('GET', '/courses'),
    getCourse: (id) => request('GET', `/courses/${id}`),
    createCourse: (data) => request('POST', '/courses', data),
    updateCourse: (id, data) => request('PUT', `/courses/${id}`, data),
    deleteCourse: (id) => request('DELETE', `/courses/${id}`),

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
    completeTask: (id) => request('POST', `/tasks/${id}/complete`),

    // Exams
    getExams: () => request('GET', '/exams'),
    getExam: (id) => request('GET', `/exams/${id}`),
    createExam: (data) => request('POST', '/exams', data),
    updateExam: (id, data) => request('PUT', `/exams/${id}`, data),
    deleteExam: (id) => request('DELETE', `/exams/${id}`),

    // Events
    getEvents: (from, to) => {
        const qs = new URLSearchParams();
        if (from) qs.set('from', from.toISOString());
        if (to) qs.set('to', to.toISOString());
        const query = qs.toString();
        return request('GET', `/events${query ? '?' + query : ''}`);
    },
    createClassEvent: (data) => request('POST', '/events/class', data),
    createTaskEvent: (data) => request('POST', '/events/task', data),
    createWorkEvent: (data) => request('POST', '/events/work', data),
    createPersonalEvent: (data) => request('POST', '/events/personal', data),
    deleteEvent: (id) => request('DELETE', `/events/${id}`),

    // Scheduling
    runScheduling: () => request('POST', '/scheduling/run'),
    getSchedulingStatus: () => request('GET', '/scheduling/status'),

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

    // Settings
    getProfile: () => request('GET', '/settings/profile'),
    updateProfile: (data) => request('PUT', '/settings/profile', data),
    updateNotifications: (data) => request('PUT', '/settings/notifications', data),
    getInstructors: () => request('GET', '/settings/instructors'),
};
