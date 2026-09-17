function antiForgeryHeaders(headers) {
    headers = headers || {};
    var tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
    if (tokenInput) {
        headers['RequestVerificationToken'] = tokenInput.value;
    }
    return headers;
}

function showToast(message, type) {
    var container = document.getElementById('toastHost');
    if (!container) {
        container = document.createElement('div');
        container.id = 'toastHost';
        container.className = 'toast-container position-fixed top-0 start-50 translate-middle-x p-3';
        container.style.zIndex = '11000';
        document.body.appendChild(container);
    }
    var el = document.createElement('div');
    el.className = 'toast align-items-center text-bg-' + (type === 'danger' ? 'danger' : 'success') + ' border-0';
    el.setAttribute('role', 'status');
    el.innerHTML = '<div class="d-flex"><div class="toast-body">' + message + '</div>' +
        '<button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast" aria-label="إغلاق"></button></div>';
    container.appendChild(el);
    var toast = new bootstrap.Toast(el, { delay: 5000 });
    el.addEventListener('hidden.bs.toast', function () { el.remove(); });
    toast.show();
}

function togglePassword(id) {
    var input = document.getElementById(id);
    if (!input) return;
    input.type = input.type === 'password' ? 'text' : 'password';
}

function currentTheme() {
    return document.documentElement.getAttribute('data-theme') === 'dark' ? 'dark' : 'light';
}

function applyTheme(theme) {
    document.documentElement.setAttribute('data-theme', theme);
    localStorage.setItem('sama-theme', theme);
    syncThemeButtons(theme);
}

function syncThemeButtons(theme) {
    document.querySelectorAll('[data-theme-toggle]').forEach(function (btn) {
        var icon = btn.querySelector('i');
        if (!icon) return;
        icon.className = theme === 'dark' ? 'ti ti-sun' : 'ti ti-moon-stars';
        btn.setAttribute('title', theme === 'dark' ? 'الوضع الفاتح' : 'الوضع الداكن');
        btn.setAttribute('aria-label', theme === 'dark' ? 'الوضع الفاتح' : 'الوضع الداكن');
    });
}

function toggleTheme() {
    applyTheme(currentTheme() === 'dark' ? 'light' : 'dark');
}

function applySidebar(collapsed) {
    document.documentElement.classList.toggle('sidebar-collapsed', collapsed);
    localStorage.setItem('sama-sidebar', collapsed ? 'collapsed' : 'expanded');
    document.querySelectorAll('[data-sidebar-toggle]').forEach(function (btn) {
        var icon = btn.querySelector('i');
        if (icon) icon.className = collapsed ? 'ti ti-layout-sidebar-right-expand' : 'ti ti-layout-sidebar-right-collapse';
        btn.setAttribute('title', collapsed ? 'فتح القائمة' : 'طي القائمة');
        btn.setAttribute('aria-label', collapsed ? 'فتح القائمة' : 'طي القائمة');
        btn.setAttribute('aria-expanded', collapsed ? 'false' : 'true');
    });
}

function toggleSidebar() {
    applySidebar(!document.documentElement.classList.contains('sidebar-collapsed'));
}

document.addEventListener('DOMContentLoaded', function () {
    syncThemeButtons(currentTheme());
    applySidebar(localStorage.getItem('sama-sidebar') === 'collapsed');

    document.querySelectorAll('[data-theme-toggle]').forEach(function (btn) {
        btn.addEventListener('click', toggleTheme);
    });
    document.querySelectorAll('[data-sidebar-toggle]').forEach(function (btn) {
        btn.addEventListener('click', toggleSidebar);
    });
    document.querySelectorAll('.app-nav-toggle').forEach(function (btn) {
        btn.addEventListener('click', function () {
            if (document.documentElement.classList.contains('sidebar-collapsed')) {
                applySidebar(false);
            }
        });
    });

    document.addEventListener('click', function (e) {
        var tip = e.target.closest('.help-tip');
        document.querySelectorAll('.help-tip.is-open').forEach(function (open) {
            if (open !== tip) {
                open.classList.remove('is-open');
                open.setAttribute('aria-expanded', 'false');
            }
        });
        if (!tip) return;
        if (e.target.closest('.help-tip-pop')) return;
        e.preventDefault();
        var willOpen = !tip.classList.contains('is-open');
        tip.classList.toggle('is-open', willOpen);
        tip.setAttribute('aria-expanded', willOpen ? 'true' : 'false');
        if (willOpen) {
            var pop = tip.querySelector('.help-tip-pop');
            var rect = tip.getBoundingClientRect();
            var width = Math.min(280, window.innerWidth - 24);
            var left = rect.right - width;
            if (left < 8) left = 8;
            if (left + width > window.innerWidth - 8) left = window.innerWidth - width - 8;
            var top = rect.bottom + 8;
            if (top + 120 > window.innerHeight) top = Math.max(8, rect.top - 8 - 88);
            pop.style.left = left + 'px';
            pop.style.top = top + 'px';
        }
    });

    document.addEventListener('keydown', function (e) {
        if (e.key !== 'Escape') return;
        document.querySelectorAll('.help-tip.is-open').forEach(function (open) {
            open.classList.remove('is-open');
            open.setAttribute('aria-expanded', 'false');
        });
    });

    setupAutoFullscreen();
});

function isAppFullscreen() {
    if (document.fullscreenElement || document.webkitFullscreenElement || document.msFullscreenElement) {
        return true;
    }
    return Math.abs(window.screen.height - window.innerHeight) <= 16
        && Math.abs(window.screen.width - window.innerWidth) <= 16;
}

function enterAppFullscreen() {
    if (isAppFullscreen()) {
        return true;
    }
    var el = document.documentElement;
    var req = el.requestFullscreen || el.webkitRequestFullscreen || el.msRequestFullscreen;
    if (!req) {
        return false;
    }
    try {
        var result = req.call(el);
        if (result && result.catch) {
            result.catch(function () { });
        }
        return true;
    } catch (e) {
        return false;
    }
}

function setupAutoFullscreen() {
    var form = document.getElementById('loginForm') || document.querySelector('form[action*="Login"]');
    if (form) {
        form.addEventListener('submit', function () {
            sessionStorage.setItem('sama-enter-fs', '1');
            enterAppFullscreen();
        });
    }

    if (sessionStorage.getItem('sama-enter-fs') === '1' || !isAppFullscreen()) {
        enterAppFullscreen();
        if (isAppFullscreen()) {
            sessionStorage.removeItem('sama-enter-fs');
        }
    }

    function retryFullscreen() {
        enterAppFullscreen();
        if (isAppFullscreen()) {
            sessionStorage.removeItem('sama-enter-fs');
            document.removeEventListener('pointerdown', retryFullscreen, true);
        }
    }

    if (!isAppFullscreen()) {
        document.addEventListener('pointerdown', retryFullscreen, true);
    }
}
