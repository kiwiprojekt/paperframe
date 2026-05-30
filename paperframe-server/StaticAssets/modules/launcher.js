import { getServerUrl, apiFetch } from './api.js';
import { showToast } from './ui-utils.js';

export let launcherDeviceId = null;

export function setLauncherDeviceId(val) {
    launcherDeviceId = val;
}

export function openLauncherModal(deviceId) {
    launcherDeviceId = deviceId;
    document.getElementById('launcherSleepHours').value   = 2;
    document.getElementById('launcherSleepMinutes').value = 0;
    document.getElementById('launcherSleepSeconds').value = 0;
    updateLauncherScriptPreview();
    document.getElementById('launcherModal').classList.add('active');
    lucide.createIcons();
}

export function closeLauncherModal() {
    document.getElementById('launcherModal').classList.remove('active');
    launcherDeviceId = null;
}

export async function updateLauncherScriptPreview() {
    if (!launcherDeviceId) return;
    const hours     = parseInt(document.getElementById('launcherSleepHours').value)   || 0;
    const minutes   = parseInt(document.getElementById('launcherSleepMinutes').value) || 0;
    const seconds   = parseInt(document.getElementById('launcherSleepSeconds').value) || 0;
    const sleepTime = (hours * 3600) + (minutes * 60) + seconds;

    document.getElementById('totalSleepSecondsDisplay').innerText = sleepTime;
    
    try {
        const resp = await apiFetch(`/api/config/download-client/${launcherDeviceId}?sleepSeconds=${sleepTime}`);
        if (resp.ok) {
            const text = await resp.text();
            document.getElementById('launcherCodeContent').innerText = text;
        } else {
            document.getElementById('launcherCodeContent').innerText = "Failed to fetch launcher script preview.";
        }
    } catch (e) {
        console.error('Failed to load script preview', e);
        document.getElementById('launcherCodeContent').innerText = "Network error loading launcher script preview.";
    }
}

export function downloadConfiguredLauncher() {
    if (!launcherDeviceId) return;
    const hours     = parseInt(document.getElementById('launcherSleepHours').value)   || 0;
    const minutes   = parseInt(document.getElementById('launcherSleepMinutes').value) || 0;
    const seconds   = parseInt(document.getElementById('launcherSleepSeconds').value) || 0;
    const sleepTime = (hours * 3600) + (minutes * 60) + seconds;

    if (sleepTime < 10) { alert('Sleep interval must be at least 10 seconds.'); return; }

    const link = document.createElement('a');
    link.href     = `/api/config/download-client/${launcherDeviceId}?sleepSeconds=${sleepTime}`;
    link.download = 'paperframe.sh';
    link.click();

    const parts = [];
    if (hours   > 0) parts.push(`${hours}h`);
    if (minutes > 0) parts.push(`${minutes}m`);
    if (seconds > 0 || parts.length === 0) parts.push(`${seconds}s`);

    showToast(`Downloaded pre-configured paperframe.sh with sleep interval ${parts.join(' ')} (${sleepTime}s)!`);
    closeLauncherModal();
}
