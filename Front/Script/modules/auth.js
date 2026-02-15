// Auth module - manages authentication state

export function isLoggedIn() {
    return !!localStorage.getItem('smartstudy_token');
}

export function getUser() {
    const data = localStorage.getItem('smartstudy_user');
    return data ? JSON.parse(data) : null;
}

export function saveAuth(response) {
    localStorage.setItem('smartstudy_token', response.token);
    localStorage.setItem('smartstudy_user', JSON.stringify({
        email: response.email,
        firstName: response.firstName,
        lastName: response.lastName,
        onboardingCompleted: response.onboardingCompleted ?? true
    }));
}

export function isOnboardingCompleted() {
    const user = getUser();
    return user?.onboardingCompleted !== false;
}

export function logout() {
    localStorage.removeItem('smartstudy_token');
    localStorage.removeItem('smartstudy_user');
    window.location.href = '/Pages/Login.html';
}

export function requireAuth() {
    if (!isLoggedIn()) {
        window.location.href = '/Pages/Login.html';
        return false;
    }
    return true;
}
