// Slide Navigation
let currentSlide = 1;
const totalSlides = 10;
document.getElementById('totalSlides').textContent = totalSlides;

function changeSlide(direction) {
    const next = currentSlide + direction;
    if (next < 1 || next > totalSlides) return;
    goToSlide(next);
}

function goToSlide(n) {
    const oldSlide = document.getElementById(`slide-${currentSlide}`);
    const newSlide = document.getElementById(`slide-${n}`);
    if (!oldSlide || !newSlide) return;

    // Remove active and reset animations on old slide
    oldSlide.classList.remove('active');
    oldSlide.querySelectorAll('.animate-in').forEach(el => {
        el.classList.remove('visible');
    });

    currentSlide = n;
    newSlide.classList.add('active');
    document.getElementById('currentSlide').textContent = n;

    // Progress bar
    const pct = ((n - 1) / (totalSlides - 1)) * 100;
    document.getElementById('progressFill').style.width = pct + '%';

    // Trigger animations on new slide
    setTimeout(() => triggerAnimations(newSlide), 100);
}

function triggerAnimations(slide) {
    const items = slide.querySelectorAll('.animate-in');
    items.forEach((el, i) => {
        const delayClass = [...el.classList].find(c => c.startsWith('delay-'));
        const delay = delayClass ? parseInt(delayClass.replace('delay-', '')) * 150 : 0;
        setTimeout(() => el.classList.add('visible'), delay);
    });
}

// Keyboard navigation
document.addEventListener('keydown', (e) => {
    if (e.key === 'ArrowRight' || e.key === ' ') { e.preventDefault(); changeSlide(1); }
    if (e.key === 'ArrowLeft') { e.preventDefault(); changeSlide(-1); }
    if (e.key === 'Home') { e.preventDefault(); goToSlide(1); }
    if (e.key === 'End') { e.preventDefault(); goToSlide(totalSlides); }
});

// Touch / Swipe
let touchStartX = 0;
document.addEventListener('touchstart', e => { touchStartX = e.changedTouches[0].screenX; });
document.addEventListener('touchend', e => {
    const diff = touchStartX - e.changedTouches[0].screenX;
    if (Math.abs(diff) > 60) changeSlide(diff > 0 ? 1 : -1);
});

// Particles
function createParticles(containerId) {
    const container = document.getElementById(containerId);
    if (!container) return;
    for (let i = 0; i < 40; i++) {
        const p = document.createElement('div');
        p.className = 'particle';
        p.style.left = Math.random() * 100 + '%';
        p.style.top = Math.random() * 100 + '%';
        p.style.width = p.style.height = (Math.random() * 4 + 1) + 'px';
        p.style.animationDuration = (Math.random() * 8 + 4) + 's';
        p.style.animationDelay = (Math.random() * 4) + 's';
        container.appendChild(p);
    }
}
createParticles('particles1');
createParticles('particles10');

// Init first slide animations
setTimeout(() => triggerAnimations(document.getElementById('slide-1')), 300);
