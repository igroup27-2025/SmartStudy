export function initOnboarding() {
    const body = document.body;
    const step = parseInt(body.dataset.step || '1');

    // Update step indicator
    document.querySelectorAll('.onboarding-dot').forEach((dot, i) => {
        if (i < step) dot.classList.add('active');
        if (i === step - 1) dot.classList.add('current');
    });

    // Update progress bar
    const progress = document.getElementById('onboardingProgress');
    if (progress) {
        progress.style.width = `${(step / 4) * 100}%`;
    }

    // Navigation buttons
    document.getElementById('onboardingNext')?.addEventListener('click', () => {
        if (step < 4) {
            window.location.href = `/Pages/Onboarding${step + 1}.html`;
        } else {
            window.location.href = '/Pages/Dashboard.html';
        }
    });

    document.getElementById('onboardingSkip')?.addEventListener('click', () => {
        window.location.href = '/Pages/Dashboard.html';
    });

    document.getElementById('onboardingBack')?.addEventListener('click', () => {
        if (step > 1) {
            window.location.href = `/Pages/Onboarding${step - 1}.html`;
        }
    });
}
