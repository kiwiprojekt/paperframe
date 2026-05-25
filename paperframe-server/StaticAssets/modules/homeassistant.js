import { apiFetch, AuthError } from './api.js';
import { showToast, switchTab } from './ui-utils.js';

export function updateSidebarVisibility() {
    const calEnabled = document.getElementById('settingsEnableCalendar')?.checked ?? true;
    const immEnabled = document.getElementById('settingsEnableImmich')?.checked   ?? true;
    const haEnabled  = document.getElementById('settingsEnableHa')?.checked       ?? true;

    const map = { calendar: calEnabled, immich: immEnabled, homeassistant: haEnabled };
    Object.entries(map).forEach(([tab, enabled]) => {
        const item = document.querySelector(`.nav-item[data-tab="${tab}"]`);
        if (item) item.style.display = enabled ? '' : 'none';
    });

    // If the currently active tab just became hidden, fall back to devices
    const activeTab = document.querySelector('.nav-item.active')?.dataset.tab;
    if (activeTab && map[activeTab] === false) switchTab('devices');
}

export async function validateHomeAssistantConnection() {
    const config = {
        apiUrl:             document.getElementById('haApiUrl').value,
        oauthBearerToken:   document.getElementById('haToken').value
    };

    showToast('Testing connection to Home Assistant Rest API...', 'info');
    try {
        const resp = await apiFetch('/api/config/validate/homeassistant', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(config)
        });
        const data = await resp.json();
        showToast(data.message, data.success ? 'success' : 'error');
    } catch (err) {
        if (err instanceof AuthError) return;
        showToast('Validation network error: ' + err.message, 'error');
    }
}
