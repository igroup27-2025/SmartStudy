import { api } from './api.js';
import { saveAuth, isLoggedIn } from './auth.js';
import { showToast } from './modals.js';

export function initLogin() {
    if (isLoggedIn()) {
        window.location.href = '/Pages/Dashboard.html';
        return;
    }

    const loginForm = document.getElementById('loginForm');
    const registerForm = document.getElementById('registerForm');
    const showRegister = document.getElementById('showRegister');
    const showLogin = document.getElementById('showLogin');
    const loginSection = document.getElementById('loginSection');
    const registerSection = document.getElementById('registerSection');

    showRegister?.addEventListener('click', (e) => {
        e.preventDefault();
        loginSection.style.display = 'none';
        registerSection.style.display = 'block';
    });

    showLogin?.addEventListener('click', (e) => {
        e.preventDefault();
        registerSection.style.display = 'none';
        loginSection.style.display = 'block';
    });

    loginForm?.addEventListener('submit', async (e) => {
        e.preventDefault();
        const email = document.getElementById('loginEmail').value;
        const password = document.getElementById('loginPassword').value;
        const btn = loginForm.querySelector('button[type="submit"]');

        try {
            btn.disabled = true;
            btn.textContent = 'Signing in...';
            const res = await api.login(email, password);
            saveAuth(res);
            window.location.href = '/Pages/Dashboard.html';
        } catch (err) {
            showToast(err.message || 'Login failed', 'error');
        } finally {
            btn.disabled = false;
            btn.textContent = 'Sign In';
        }
    });

    registerForm?.addEventListener('submit', async (e) => {
        e.preventDefault();
        const data = {
            email: document.getElementById('regEmail').value,
            firstName: document.getElementById('regFirstName').value,
            lastName: document.getElementById('regLastName').value,
            password: document.getElementById('regPassword').value,
        };
        const btn = registerForm.querySelector('button[type="submit"]');

        try {
            btn.disabled = true;
            btn.textContent = 'Creating account...';
            const res = await api.register(data);
            saveAuth(res);
            window.location.href = '/Pages/Dashboard.html';
        } catch (err) {
            showToast(err.message || 'Registration failed', 'error');
        } finally {
            btn.disabled = false;
            btn.textContent = 'Create Account';
        }
    });
}
