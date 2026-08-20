import { getServerUrl, apiFetch } from './api.js';
import { showToast } from './ui-utils.js';

export let launcherDeviceId = null;

export function setLauncherDeviceId(val) {
    launcherDeviceId = val;
}

export function openLauncherModal(deviceId) {
    launcherDeviceId = deviceId;
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

    try {
        const resp = await apiFetch(`/api/config/download-client/${launcherDeviceId}`);
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

    const link = document.createElement('a');
    link.href     = `/api/config/download-client/${launcherDeviceId}`;
    link.download = 'paperframe.sh';
    link.click();

    showToast(`Downloaded paperframe.sh for ${launcherDeviceId}!`);
    closeLauncherModal();
}
