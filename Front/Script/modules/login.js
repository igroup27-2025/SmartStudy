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
    const forgotForm = document.getElementById('forgotForm');
    const resetForm = document.getElementById('resetForm');

    const loginSection = document.getElementById('loginSection');
    const registerSection = document.getElementById('registerSection');
    const forgotSection = document.getElementById('forgotSection');
    const resetSection = document.getElementById('resetSection');

    function showSection(section) {
        [loginSection, registerSection, forgotSection, resetSection].forEach(s => {
            if (s) s.style.display = 'none';
        });
        if (section) section.style.display = 'block';
    }

    document.getElementById('showRegister')?.addEventListener('click', (e) => {
        e.preventDefault();
        showSection(registerSection);
    });

    document.getElementById('showLogin')?.addEventListener('click', (e) => {
        e.preventDefault();
        showSection(loginSection);
    });

    document.getElementById('showForgot')?.addEventListener('click', (e) => {
        e.preventDefault();
        showSection(forgotSection);
    });

    document.getElementById('showLoginFromForgot')?.addEventListener('click', (e) => {
        e.preventDefault();
        showSection(loginSection);
    });

    document.getElementById('showLoginFromReset')?.addEventListener('click', (e) => {
        e.preventDefault();
        showSection(loginSection);
    });

    // Login
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

    // Register
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

    // Forgot Password
    forgotForm?.addEventListener('submit', async (e) => {
        e.preventDefault();
        const email = document.getElementById('forgotEmail').value;
        const btn = forgotForm.querySelector('button[type="submit"]');

        try {
            btn.disabled = true;
            btn.textContent = 'Sending...';
            const res = await api.forgotPassword(email);

            // Show token (demo mode returns it directly)
            const tokenResult = document.getElementById('resetTokenResult');
            const tokenDisplay = document.getElementById('resetTokenDisplay');
            if (tokenResult && tokenDisplay && res.resetToken) {
                tokenDisplay.textContent = res.resetToken;
                tokenResult.style.display = 'block';
            }

            showToast('Reset token generated! Use it to set a new password.');

            // Pre-fill reset form
            document.getElementById('resetEmail').value = email;
            if (res.resetToken) {
                document.getElementById('resetToken').value = res.resetToken;
            }

            // Show reset section after a moment
            setTimeout(() => showSection(resetSection), 2000);
        } catch (err) {
            showToast(err.message || 'Failed to send reset token', 'error');
        } finally {
            btn.disabled = false;
            btn.textContent = 'Send Reset Token';
        }
    });

    // Reset Password
    resetForm?.addEventListener('submit', async (e) => {
        e.preventDefault();
        const btn = resetForm.querySelector('button[type="submit"]');

        try {
            btn.disabled = true;
            btn.textContent = 'Resetting...';
            await api.resetPassword({
                email: document.getElementById('resetEmail').value,
                token: document.getElementById('resetToken').value,
                newPassword: document.getElementById('resetNewPassword').value,
            });
            showToast('Password reset successfully! Please sign in.');
            showSection(loginSection);
        } catch (err) {
            showToast(err.message || 'Failed to reset password', 'error');
        } finally {
            btn.disabled = false;
            btn.textContent = 'Reset Password';
        }
    });

    // Google Sign-In
    document.getElementById('googleSignIn')?.addEventListener('click', async () => {
        try {
            // Use Google Identity Services popup
            if (typeof google !== 'undefined' && google.accounts) {
                google.accounts.id.prompt();
            } else {
                showToast('Google Sign-In is not available', 'error');
            }
        } catch {
            showToast('Google Sign-In failed', 'error');
        }
    });

    // Initialize Google Identity Services if available
    initGoogleSignIn();
}

function initGoogleSignIn() {
    // Load Google Identity Services script dynamically
    const script = document.createElement('script');
    script.src = 'https://accounts.google.com/gsi/client';
    script.async = true;
    script.defer = true;
    script.onload = () => {
        if (typeof google !== 'undefined' && google.accounts) {
            google.accounts.id.initialize({
                client_id: window.__GOOGLE_CLIENT_ID || '',
                callback: handleGoogleCallback,
            });
        }
    };
    document.head.appendChild(script);
}

async function handleGoogleCallback(response) {
    if (!response.credential) return;

    try {
        const res = await api.googleLogin(response.credential);
        saveAuth(res);
        window.location.href = '/Pages/Dashboard.html';
    } catch (err) {
        showToast(err.message || 'Google login failed', 'error');
    }
}
