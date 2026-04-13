import { api } from './api.js';
import { saveAuth, isLoggedIn, isOnboardingCompleted } from './auth.js';
import { showToast } from './modals.js';
import { BASE_PATH } from './config.js';

export function initLogin() {
    if (isLoggedIn()) {
        window.location.href = BASE_PATH + '/Pages/Dashboard.html';
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
            window.location.href = BASE_PATH + (res.onboardingCompleted ? '/Pages/Dashboard.html' : '/Pages/Onboarding1.html');
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
            window.location.href = BASE_PATH + '/Pages/Onboarding1.html';
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

    // Google Sign-In — proxy clicks from the styled button to the hidden
    // Google-rendered button (google.accounts.id.prompt() is unreliable on
    // production due to One Tap cooldown / FedCM).
    document.getElementById('googleSignIn')?.addEventListener('click', () => {
        const rendered = document.querySelector('#googleSignInRender [role="button"]');
        if (rendered) {
            rendered.click();
        } else {
            showToast('Google Sign-In is still loading. Please try again in a moment.', 'error');
        }
    });

    // Initialize Google Identity Services if available
    initGoogleSignIn();
}

async function initGoogleSignIn() {
    // Fetch Google client ID from backend config
    try {
        const config = await api.getAuthConfig();
        const clientId = config.googleClientId;
        if (!clientId || clientId === 'PLACEHOLDER_GOOGLE_CLIENT_ID') return;

        // Load Google Identity Services script dynamically
        const script = document.createElement('script');
        script.src = 'https://accounts.google.com/gsi/client';
        script.async = true;
        script.defer = true;
        script.onload = () => {
            if (typeof google === 'undefined' || !google.accounts) return;

            google.accounts.id.initialize({
                client_id: clientId,
                callback: handleGoogleCallback,
                ux_mode: 'popup',
                auto_select: false,
            });

            // Render Google's official button into a hidden container. The
            // visible styled button forwards clicks to it.
            let container = document.getElementById('googleSignInRender');
            if (!container) {
                container = document.createElement('div');
                container.id = 'googleSignInRender';
                container.style.position = 'absolute';
                container.style.left = '-9999px';
                container.style.top = '-9999px';
                container.setAttribute('aria-hidden', 'true');
                document.body.appendChild(container);
            }
            google.accounts.id.renderButton(container, {
                type: 'standard',
                theme: 'outline',
                size: 'large',
            });
        };
        document.head.appendChild(script);
    } catch {
        // Google Sign-In not available — silently skip
    }
}

async function handleGoogleCallback(response) {
    if (!response.credential) return;

    try {
        const res = await api.googleLogin(response.credential);
        saveAuth(res);
        window.location.href = BASE_PATH + (res.onboardingCompleted ? '/Pages/Dashboard.html' : '/Pages/Onboarding1.html');
    } catch (err) {
        showToast(err.message || 'Google login failed', 'error');
    }
}
