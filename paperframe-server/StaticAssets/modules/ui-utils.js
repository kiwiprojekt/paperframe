import { setUnsavedChanges } from './state.js';

export function showToast(message, type = 'success') {
    const container = document.getElementById('toastContainer');
    const tpl       = document.getElementById('tpl-toast').content.cloneNode(true);
    const toast     = tpl.querySelector('.toast');

    let icon  = 'check-circle2';
    let color = 'var(--success)';
    if (type === 'error') {
        icon  = 'alert-triangle';
        color = 'var(--error)';
    } else if (type === 'info') {
        icon  = 'info';
        color = 'var(--accent-secondary)';
    }

    toast.style.borderLeftColor = color;
    const iconEl = toast.querySelector('[data-field="icon"]');
    iconEl.setAttribute('data-lucide', icon);
    iconEl.style.color = color;
    toast.querySelector('[data-field="message"]').textContent = message;

    container.appendChild(tpl);
    lucide.createIcons();

    setTimeout(() => toast.classList.add('show'), 50);
    setTimeout(() => {
        toast.classList.remove('show');
        setTimeout(() => toast.remove(), 300);
    }, 4000);
}

let _confirmModalCallback = null;

export function openConfirmModal({ message, title = 'Confirm', confirmText = 'Confirm', confirmClass = 'btn-danger', icon = 'alert-triangle', onConfirm }) {
    _confirmModalCallback = onConfirm;

    document.getElementById('confirmModalTitle').textContent  = title;
    document.getElementById('confirmModalMessage').textContent = message;

    const confirmBtn = document.getElementById('confirmModalBtn');
    confirmBtn.className   = `btn ${confirmClass}`;
    confirmBtn.innerHTML   = `<i data-lucide="${icon}"></i> ${confirmText}`;

    document.getElementById('confirmModal').classList.add('active');

    confirmBtn.onclick = () => {
        const cb = _confirmModalCallback;   // capture BEFORE close (which nulls it)
        closeConfirmModal();
        if (cb) cb();
    };

    lucide.createIcons();
}

export function closeConfirmModal() {
    document.getElementById('confirmModal').classList.remove('active');
    _confirmModalCallback = null;
}

export function openDeleteModal(message, onConfirm) {
    openConfirmModal({ message, title: 'Confirm Deletion', confirmText: 'Delete', confirmClass: 'btn-danger', icon: 'trash-2', onConfirm });
}

export function toggleSecretVisibility(inputId, btn) {
    const input   = document.getElementById(inputId);
    const isHidden = input.type === 'password';
    input.type    = isHidden ? 'text' : 'password';
    const icon    = btn.querySelector('i');
    icon.setAttribute('data-lucide', isHidden ? 'eye-off' : 'eye');
    lucide.createIcons({ nodes: [icon] });
}

export function markUnsavedChanges(unsaved = true) {
    setUnsavedChanges(unsaved);
    const saveBtn   = document.getElementById('saveConfigBtn');
    const indicator = document.getElementById('unsavedChangesIndicator');

    if (unsaved) {
        saveBtn.removeAttribute('disabled');
        saveBtn.classList.add('pulse-animation');
        indicator.classList.add('visible');
    } else {
        saveBtn.setAttribute('disabled', 'true');
        saveBtn.classList.remove('pulse-animation');
        indicator.classList.remove('visible');
    }
}

export function switchTab(tabId) {
    document.querySelectorAll('.nav-item').forEach(item => item.classList.remove('active'));
    document.querySelector(`.nav-item[data-tab="${tabId}"]`).classList.add('active');

    document.querySelectorAll('.tab-panel').forEach(panel => panel.classList.remove('active'));
    document.getElementById(`tab-${tabId}`).classList.add('active');

    const titles = {
        dashboard:     'Telemetry Logs',
        devices:       'Device Manager',
        calendar:      'Calendar Configurations',
        immich:        'Immich Configurations',
        homeassistant: 'Home Assistant Integration',
        settings:      'Settings'
    };
    const subtitles = {
        dashboard:     'Monitor real-time client requests',
        devices:       'Map your frames and track status',
        calendar:      'Manage iCal schedules and settings',
        immich:        'Configure dithered photograph streams',
        homeassistant: 'Configure background HA battery reporting',
        settings:      'Server, integrations and security'
    };

    document.getElementById('pageTitle').innerText    = titles[tabId]    || 'Dashboard';
    document.getElementById('pageSubtitle').innerText = subtitles[tabId] || '';
}
