import { AppConfig } from './state.js';
import { markUnsavedChanges, openDeleteModal, showToast } from './ui-utils.js';
import { apiFetch, AuthError } from './api.js';

export let editingImmichConfigId = null;
export let tempNewImmichConfig   = null;

export function renderImmichConfigUI() {
    const tbody = document.getElementById('immichListBody');
    tbody.innerHTML = '';

    AppConfig.immich = AppConfig.immich || {};
    const keys = Object.keys(AppConfig.immich);

    if (keys.length === 0) {
        tbody.appendChild(document.getElementById('tpl-immich-empty').content.cloneNode(true));
        return;
    }

    keys.forEach(k => {
        const config = AppConfig.immich[k];
        const mapped = Object.keys(AppConfig.devices || {}).filter(d =>
            AppConfig.devices[d].serviceName.toLowerCase() === 'immich' &&
            AppConfig.devices[d].configId === k
        );

        const tpl = document.getElementById('tpl-immich-row').content.cloneNode(true);
        const f   = name => tpl.querySelector(`[data-field="${name}"]`);

        f('configId').textContent   = k;
        f('apiUrl').textContent     = config.apiUrl || '';
        f('albumName').textContent  = config.albumName || '';
        f('brightness').textContent = `${config.brightness || 0}%`;
        f('contrast').textContent   = `${config.contrast || 0}%`;

        const mappedEl = f('mappedDevices');
        if (mapped.length > 0) {
            mapped.forEach(d => {
                const chip = document.createElement('code');
                chip.className   = 'code-chip';
                chip.textContent = d;
                mappedEl.appendChild(chip);
            });
        } else {
            const none = document.createElement('span');
            none.className   = 'text-secondary text-sm';
            none.textContent = 'None';
            mappedEl.appendChild(none);
        }

        tpl.querySelector('[data-action="edit"]').onclick   = () => openImmichConfigModal(k);
        tpl.querySelector('[data-action="delete"]').onclick = () => deleteImmichConfig(k);

        tbody.appendChild(tpl);
    });

    lucide.createIcons();
}

export function openImmichConfigModal(id = null) {
    editingImmichConfigId = id;

    const titleEl   = document.getElementById('immichConfigModalTitle').querySelector('span');
    const idField   = document.getElementById('editImmichConfigIdField');
    const deleteBtn = document.getElementById('editImmichDeleteBtn');

    if (id) {
        const config = AppConfig.immich[id];
        titleEl.innerText = `Edit Immich Configuration: ${id}`;
        idField.value     = id;
        idField.disabled  = true;
        deleteBtn.style.display = 'inline-flex';

        document.getElementById('editImmichConfigId').value   = id;
        document.getElementById('editImmichApiUrl').value     = config.apiUrl   || '';
        document.getElementById('editImmichApiKey').value     = config.apiKey   || '';
        document.getElementById('editImmichAlbumName').value  = config.albumName || '';
        document.getElementById('editImmichFbink').value      = config.fbinkPath || '/mnt/us/libkh/bin/fbink';

        const bright = config.brightness !== undefined ? config.brightness : 10;
        const contr  = config.contrast   !== undefined ? config.contrast   : 30;
        document.getElementById('editImmichBrightness').value    = bright;
        document.getElementById('editImmichBrightnessVal').innerText = bright;
        document.getElementById('editImmichContrast').value      = contr;
        document.getElementById('editImmichContrastVal').innerText   = contr;
    } else {
        titleEl.innerText = 'Add Immich Configuration';
        idField.value     = '';
        idField.disabled  = false;
        deleteBtn.style.display = 'none';

        tempNewImmichConfig = { apiUrl: '', apiKey: '', albumName: 'frame', fbinkPath: '/mnt/us/libkh/bin/fbink', brightness: 10, contrast: 30 };

        document.getElementById('editImmichConfigId').value      = '';
        document.getElementById('editImmichApiUrl').value        = '';
        document.getElementById('editImmichApiKey').value        = '';
        document.getElementById('editImmichAlbumName').value     = 'frame';
        document.getElementById('editImmichFbink').value         = '/mnt/us/libkh/bin/fbink';
        document.getElementById('editImmichBrightness').value    = 10;
        document.getElementById('editImmichBrightnessVal').innerText = 10;
        document.getElementById('editImmichContrast').value      = 30;
        document.getElementById('editImmichContrastVal').innerText   = 30;
    }

    document.getElementById('editImmichAlbumSelect').style.display = 'none';
    document.getElementById('immichConfigModal').classList.add('active');
    lucide.createIcons();
}

export function closeImmichConfigModal() {
    document.getElementById('immichConfigModal').classList.remove('active');
    editingImmichConfigId = null;
    tempNewImmichConfig   = null;
}

export async function loadAlbumsForEditModal() {
    const apiUrl = document.getElementById('editImmichApiUrl').value;
    const apiKey = document.getElementById('editImmichApiKey').value;

    if (!apiUrl || !apiKey) {
        showToast('Please fill in API URL and API Key before browsing albums.', 'error');
        return;
    }

    const btn = document.getElementById('editModalBrowseBtn');
    btn.innerHTML = `<span class="loader loader-sm"></span> Loading...`;
    btn.disabled  = true;

    try {
        const response = await apiFetch('/api/config/immich/albums', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ apiUrl, apiKey })
        });
        if (!response.ok) {
            const err = await response.json();
            throw new Error(err.message || 'Failed request');
        }

        const albums   = await response.json();
        const dropdown = document.getElementById('editImmichAlbumSelect');
        dropdown.innerHTML = '<option value="">-- Choose Album from Immich Server --</option>' +
            albums.map(a => `<option value="${a.name}">${a.name} (${a.count} assets)</option>`).join('');

        dropdown.style.display = 'block';
        showToast(`Located ${albums.length} albums on your Immich account!`);
    } catch (err) {
        if (err instanceof AuthError) return;
        showToast('Failed to fetch albums: ' + err.message, 'error');
    } finally {
        btn.innerHTML = `<i data-lucide="refresh-cw"></i> Load Albums`;
        btn.disabled  = false;
        lucide.createIcons();
    }
}

export function selectEditModalDropdownAlbum(val) {
    if (!val) return;
    document.getElementById('editImmichAlbumName').value = val;
    markUnsavedChanges(true);
    showToast(`Selected album: '${val}'`);
}

export function saveImmichConfigModalData() {
    let id = editingImmichConfigId;
    if (!id) {
        const newId = document.getElementById('editImmichConfigIdField').value.trim();
        if (!newId) { alert('Config ID is required.'); return; }
        if (AppConfig.immich[newId]) { alert('A configuration with this ID already exists.'); return; }
        id = newId;
        AppConfig.immich[id] = tempNewImmichConfig;
    }

    const config = AppConfig.immich[id];
    config.apiUrl     = document.getElementById('editImmichApiUrl').value;
    config.apiKey     = document.getElementById('editImmichApiKey').value;
    config.albumName  = document.getElementById('editImmichAlbumName').value;
    config.fbinkPath  = document.getElementById('editImmichFbink').value;
    config.brightness = parseInt(document.getElementById('editImmichBrightness').value) || 0;
    config.contrast   = parseInt(document.getElementById('editImmichContrast').value)   || 0;

    closeImmichConfigModal();
    renderImmichConfigUI();
    markUnsavedChanges(true);
    showToast(`Immich config '${id}' updated locally. Remember to Save Settings!`, 'info');
}

export function deleteImmichConfigFromModal() {
    const id = editingImmichConfigId;
    closeImmichConfigModal();
    openDeleteModal(`Delete Immich configuration '${id}'? This cannot be undone without saving first.`, () => {
        delete AppConfig.immich[id];
        renderImmichConfigUI();
        markUnsavedChanges(true);
        showToast(`Immich configuration '${id}' deleted locally. Save settings to persist.`, 'info');
    });
}

export function deleteImmichConfig(id) {
    openDeleteModal(`Delete Immich configuration '${id}'? This cannot be undone without saving first.`, () => {
        delete AppConfig.immich[id];
        renderImmichConfigUI();
        markUnsavedChanges(true);
        showToast(`Immich configuration '${id}' deleted locally. Save settings to persist.`, 'info');
    });
}

export async function validateImmichConfigFromModal() {
    const config = editingImmichConfigId ? AppConfig.immich[editingImmichConfigId] : tempNewImmichConfig;
    if (!config) return;

    const tempConfig = {
        apiUrl:    document.getElementById('editImmichApiUrl').value,
        apiKey:    document.getElementById('editImmichApiKey').value,
        albumName: document.getElementById('editImmichAlbumName').value,
        fbinkPath: document.getElementById('editImmichFbink').value
    };

    showToast('Testing connection credentials to Immich...', 'info');
    try {
        const resp = await apiFetch('/api/config/validate/immich', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(tempConfig)
        });
        const data = await resp.json();
        showToast(data.message, data.success ? 'success' : 'error');
    } catch (err) {
        if (err instanceof AuthError) return;
        showToast('Validation failed: ' + err.message, 'error');
    }
}
