import { AppConfig, DeviceStatuses, setAppConfig, setDeviceStatuses, hasUnsavedChanges } from './state.js';
import { showToast, openConfirmModal, markUnsavedChanges } from './ui-utils.js';
import { renderDeviceCards } from './devices.js';
import { renderCalendarConfigUI } from './calendar.js';
import { renderImmichConfigUI } from './immich.js';
import { renderArtChicagoConfigUI } from './artchicago.js';
import { updateSidebarVisibility } from './homeassistant.js';

export class AuthError extends Error { constructor() { super('auth'); } }

export async function apiFetch(url, options) {
    const resp = await fetch(url, options);
    if (resp.status === 401) {
        showLoginOverlay();
        throw new AuthError();
    }
    return resp;
}

export function showLoginOverlay() {
    document.getElementById('loginOverlay').classList.remove('hidden');
    document.getElementById('loginPassword').value = '';
    document.getElementById('loginError').classList.add('hidden');
    document.getElementById('logoutBtn').style.display = 'none';
    lucide.createIcons();
}

export function hideLoginOverlay(authEnabled = false) {
    document.getElementById('loginOverlay').classList.add('hidden');
    document.getElementById('logoutBtn').style.display = authEnabled ? '' : 'none';
}

export async function checkAuth() {
    try {
        const resp = await fetch('/api/auth/check');
        const data = await resp.json();
        if (resp.status === 401) {
            showLoginOverlay();
            return false;
        }
        hideLoginOverlay(data.authEnabled === true);
        return true;
    } catch (e) {
        showLoginOverlay();
        return false;
    }
}

export async function submitLogin(e) {
    e.preventDefault();
    const password = document.getElementById('loginPassword').value;
    const errorEl  = document.getElementById('loginError');
    errorEl.classList.add('hidden');

    try {
        const resp = await fetch('/api/auth/login', {
            method:  'POST',
            headers: { 'Content-Type': 'application/json' },
            body:    JSON.stringify({ password })
        });
        if (resp.ok) {
            hideLoginOverlay(true);
            await loadConfigData();
            await loadTelemetryLogs();
        } else {
            const data = await resp.json();
            errorEl.textContent = data.message || 'Incorrect password.';
            errorEl.classList.remove('hidden');
        }
    } catch (err) {
        errorEl.textContent = 'Network error. Please try again.';
        errorEl.classList.remove('hidden');
    }
}

export async function logout() {
    await fetch('/api/auth/logout', { method: 'POST' });
    showLoginOverlay();
}

export async function loadConfigData() {
    try {
        const response = await apiFetch('/api/config');
        if (!response.ok) throw new Error('HTTP error: ' + response.status);
        const data = await response.json();
        setAppConfig(data);

        // Fetch device statuses
        try {
            const statusResp = await fetch('/api/config/devices/status');
            if (statusResp.ok) {
                const statuses = await statusResp.json();
                setDeviceStatuses(statuses);
            }
        } catch (statusErr) {
            console.error('Failed to load device statuses:', statusErr);
        }

        // Sync HA inputs
        document.getElementById('haApiUrl').value          = AppConfig.homeAssistant?.apiUrl            || '';
        document.getElementById('haToken').value           = AppConfig.homeAssistant?.oauthBearerToken  || '';
        document.getElementById('haUpdateTemplate').value  = AppConfig.homeAssistant?.updateEntityTemplate || '';
        document.getElementById('haBatteryTemplate').value = AppConfig.homeAssistant?.batteryEntityTemplate || '';

        // Entity sync toggles (inverted from 'disable' flags)
        const updateEnabled  = !AppConfig.homeAssistant?.disableUpdateEntity;
        const batteryEnabled = !AppConfig.homeAssistant?.disableBatteryEntity;
        document.getElementById('haEnableUpdate').checked  = updateEnabled;
        document.getElementById('haEnableBattery').checked = batteryEnabled;

        // Sync toggle label text
        document.getElementById('haEnableUpdate').closest('label').querySelector('.ha-toggle-label').textContent  = updateEnabled  ? 'Enabled' : 'Disabled';
        document.getElementById('haEnableBattery').closest('label').querySelector('.ha-toggle-label').textContent = batteryEnabled ? 'Enabled' : 'Disabled';

        // Sync Settings inputs
        const s = AppConfig.settings || {};
        document.getElementById('settingsServerAddress').value = s.serverAddress || '';
        document.getElementById('currentOriginHint').textContent = window.location.origin;
        document.getElementById('settingsPassword').value = s.managerPassword || '';

        const calEnabled = s.enableCalendar      !== false;
        const immEnabled = s.enableImmich        !== false;
        const artEnabled = s.enableArtChicago    !== false;
        const haEnabled  = s.enableHomeAssistant !== false;
        document.getElementById('settingsEnableCalendar').checked = calEnabled;
        document.getElementById('settingsEnableImmich').checked   = immEnabled;
        document.getElementById('settingsEnableArtChicago').checked = artEnabled;
        document.getElementById('settingsEnableHa').checked       = haEnabled;
        ['settingsEnableCalendar', 'settingsEnableImmich', 'settingsEnableArtChicago', 'settingsEnableHa'].forEach(id => {
            const el = document.getElementById(id);
            el.closest('label').querySelector('.ha-toggle-label').textContent = el.checked ? 'Enabled' : 'Disabled';
        });
        updateSidebarVisibility();

        // Populate sections
        renderDeviceCards();
        renderCalendarConfigUI();
        renderImmichConfigUI();
        renderArtChicagoConfigUI();

        markUnsavedChanges(false);
        showToast('Configuration successfully loaded from server.');
    } catch (err) {
        if (err instanceof AuthError) return;
        showToast('Failed to connect to configuration server: ' + err.message, 'error');
    }
}

export async function saveConfiguration() {
    AppConfig.homeAssistant = AppConfig.homeAssistant || {};
    AppConfig.homeAssistant.apiUrl                = document.getElementById('haApiUrl').value;
    AppConfig.homeAssistant.oauthBearerToken      = document.getElementById('haToken').value;
    AppConfig.homeAssistant.updateEntityTemplate  = document.getElementById('haUpdateTemplate').value;
    AppConfig.homeAssistant.batteryEntityTemplate = document.getElementById('haBatteryTemplate').value;
    AppConfig.homeAssistant.disableUpdateEntity   = !document.getElementById('haEnableUpdate').checked;
    AppConfig.homeAssistant.disableBatteryEntity  = !document.getElementById('haEnableBattery').checked;

    AppConfig.settings = AppConfig.settings || {};
    AppConfig.settings.serverAddress       = document.getElementById('settingsServerAddress').value;
    AppConfig.settings.enableCalendar      = document.getElementById('settingsEnableCalendar').checked;
    AppConfig.settings.enableImmich        = document.getElementById('settingsEnableImmich').checked;
    AppConfig.settings.enableArtChicago    = document.getElementById('settingsEnableArtChicago').checked;
    AppConfig.settings.enableHomeAssistant = document.getElementById('settingsEnableHa').checked;
    AppConfig.settings.managerPassword     = document.getElementById('settingsPassword').value;

    try {
        const response = await apiFetch('/api/config', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(AppConfig)
        });

        const data = await response.json();
        if (response.ok) {
            markUnsavedChanges(false);
            showToast(data.message || 'Configuration successfully saved and reloaded.');
        } else {
            showToast('Failed to save: ' + data.message, 'error');
        }
    } catch (err) {
        if (err instanceof AuthError) return;
        showToast('Network error saving configuration: ' + err.message, 'error');
    }
}

export async function reloadConfigData() {
    if (hasUnsavedChanges) {
        openConfirmModal({
            message:      'You have unsaved changes. Discard them and reload the configuration from the server?',
            title:        'Discard Changes',
            confirmText:  'Discard & Reload',
            confirmClass: 'btn-secondary',
            icon:         'refresh-cw',
            onConfirm:    () => loadConfigData()
        });
        return;
    }
    await loadConfigData();
}

export async function loadTelemetryLogs() {
    try {
        const response = await apiFetch('/api/logs');
        if (!response.ok) return;
        const logs = await response.json();

        const tbody = document.getElementById('logsTableBody');
        tbody.innerHTML = '';

        if (logs.length === 0) {
            tbody.appendChild(document.getElementById('tpl-log-empty').content.cloneNode(true));
            lucide.createIcons();
            return;
        }

        logs.forEach(l => {
            const tpl = document.getElementById('tpl-log-row').content.cloneNode(true);
            const f   = name => tpl.querySelector(`[data-field="${name}"]`);

            f('time').textContent       = new Date(l.timestamp).toLocaleTimeString();
            f('date').textContent       = new Date(l.timestamp).toLocaleDateString();
            f('deviceId').textContent   = l.deviceId;
            f('resolution').textContent = l.screenResolution;
            f('service').textContent    = l.service;
            f('configId').textContent   = `(${l.configId})`;
            f('message').textContent    = l.message;

            const battEl = f('battery');
            battEl.textContent  = l.battery !== null ? `${l.battery}%` : '---';
            battEl.style.color  = l.battery < 20 ? 'var(--error)' : 'var(--text-primary)';

            let badgeClass = 'badge-success';
            if (l.status.toLowerCase() === 'error')    badgeClass = 'badge-error';
            if (l.status.toLowerCase() === 'disabled') badgeClass = 'badge-error';
            if (l.status.toLowerCase() === 'redirect') badgeClass = 'badge-redirect';
            const statusBadge = f('statusBadge');
            statusBadge.textContent = l.status;
            statusBadge.className   = `badge ${badgeClass}`;

            tbody.appendChild(tpl);
        });
    } catch (e) {
        console.error('Logs fetching error', e);
    }
}

export function getServerUrl() {
    const custom = AppConfig.settings?.serverAddress?.trim();
    return custom || window.location.origin;
}
