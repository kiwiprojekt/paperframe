import { AppConfig } from './state.js';
import { markUnsavedChanges, openDeleteModal, showToast } from './ui-utils.js';
import { apiFetch, AuthError } from './api.js';

export let editingArtChicagoConfigId = null;
export let tempNewArtChicagoConfig   = null;

export function renderArtChicagoConfigUI() {
    const tbody = document.getElementById('artChicagoListBody');
    tbody.innerHTML = '';

    AppConfig.artChicago = AppConfig.artChicago || {};
    const keys = Object.keys(AppConfig.artChicago);

    if (keys.length === 0) {
        tbody.appendChild(document.getElementById('tpl-artchicago-empty').content.cloneNode(true));
        return;
    }

    keys.forEach(k => {
        const config = AppConfig.artChicago[k];
        const mapped = Object.keys(AppConfig.devices || {}).filter(d =>
            AppConfig.devices[d].serviceName &&
            AppConfig.devices[d].serviceName.toLowerCase() === 'artchicago' &&
            AppConfig.devices[d].configId === k
        );

        const tpl = document.getElementById('tpl-artchicago-row').content.cloneNode(true);
        const f   = name => tpl.querySelector(`[data-field="${name}"]`);

        f('configId').textContent   = k;
        f('query').textContent      = config.query || '(any public domain artwork)';
        f('orientation').textContent = config.orientation === 'portrait' ? 'Portrait Only' : (config.orientation === 'landscape' ? 'Landscape Only' : 'All');
        f('rotation').textContent    = config.rotation ? `${config.rotation}°` : '0°';
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

        tpl.querySelector('[data-action="edit"]').onclick   = () => openArtChicagoConfigModal(k);
        tpl.querySelector('[data-action="delete"]').onclick = () => deleteArtChicagoConfig(k);

        tbody.appendChild(tpl);
    });

    lucide.createIcons();
}

export function openArtChicagoConfigModal(id = null) {
    editingArtChicagoConfigId = id;

    const titleEl   = document.getElementById('artChicagoConfigModalTitle').querySelector('span');
    const idField   = document.getElementById('editArtChicagoConfigIdField');
    const deleteBtn = document.getElementById('editArtChicagoDeleteBtn');

    if (id) {
        const config = AppConfig.artChicago[id];
        titleEl.innerText = `Edit Art Institute Configuration: ${id}`;
        idField.value     = id;
        idField.disabled  = true;
        deleteBtn.style.display = 'inline-flex';

        document.getElementById('editArtChicagoConfigId').value = id;
        document.getElementById('editArtChicagoQuery').value    = config.query    || '';
        document.getElementById('editArtChicagoFbink').value    = config.fbinkPath || '/mnt/us/libkh/bin/fbink';
        document.getElementById('editArtChicagoOrientation').value = config.orientation || 'all';
        document.getElementById('editArtChicagoRotation').value    = config.rotation !== undefined ? config.rotation.toString() : '0';

        const bright = config.brightness !== undefined ? config.brightness : 10;
        const contr  = config.contrast   !== undefined ? config.contrast   : 30;
        document.getElementById('editArtChicagoBrightness').value    = bright;
        document.getElementById('editArtChicagoBrightnessVal').innerText = bright;
        document.getElementById('editArtChicagoContrast').value      = contr;
        document.getElementById('editArtChicagoContrastVal').innerText   = contr;
    } else {
        titleEl.innerText = 'Add Art Institute Configuration';
        idField.value     = '';
        idField.disabled  = false;
        deleteBtn.style.display = 'none';

        tempNewArtChicagoConfig = { query: 'painting', fbinkPath: '/mnt/us/libkh/bin/fbink', brightness: 10, contrast: 30, orientation: 'all', rotation: 0 };

        document.getElementById('editArtChicagoConfigId').value      = '';
        document.getElementById('editArtChicagoQuery').value         = 'painting';
        document.getElementById('editArtChicagoFbink').value         = '/mnt/us/libkh/bin/fbink';
        document.getElementById('editArtChicagoOrientation').value   = 'all';
        document.getElementById('editArtChicagoRotation').value      = '0';
        document.getElementById('editArtChicagoBrightness').value    = 10;
        document.getElementById('editArtChicagoBrightnessVal').innerText = 10;
        document.getElementById('editArtChicagoContrast').value      = 30;
        document.getElementById('editArtChicagoContrastVal').innerText   = 30;
    }

    document.getElementById('artChicagoConfigModal').classList.add('active');
    lucide.createIcons();
}

export function closeArtChicagoConfigModal() {
    document.getElementById('artChicagoConfigModal').classList.remove('active');
    editingArtChicagoConfigId = null;
    tempNewArtChicagoConfig   = null;
}

export function saveArtChicagoConfigModalData() {
    let id = editingArtChicagoConfigId;
    if (!id) {
        const newId = document.getElementById('editArtChicagoConfigIdField').value.trim();
        if (!newId) { alert('Config ID is required.'); return; }
        if (AppConfig.artChicago[newId]) { alert('A configuration with this ID already exists.'); return; }
        id = newId;
        AppConfig.artChicago[id] = tempNewArtChicagoConfig;
    }

    const config = AppConfig.artChicago[id];
    config.query       = document.getElementById('editArtChicagoQuery').value;
    config.fbinkPath   = document.getElementById('editArtChicagoFbink').value;
    config.orientation = document.getElementById('editArtChicagoOrientation').value || 'all';
    config.rotation    = parseInt(document.getElementById('editArtChicagoRotation').value) || 0;
    config.brightness  = parseInt(document.getElementById('editArtChicagoBrightness').value) || 0;
    config.contrast    = parseInt(document.getElementById('editArtChicagoContrast').value)   || 0;

    closeArtChicagoConfigModal();
    renderArtChicagoConfigUI();
    markUnsavedChanges(true);
    showToast(`Art Institute config '${id}' updated locally. Remember to Save Settings!`, 'info');
}

export function deleteArtChicagoConfigFromModal() {
    const id = editingArtChicagoConfigId;
    closeArtChicagoConfigModal();
    openDeleteModal(`Delete Art Institute configuration '${id}'? This cannot be undone without saving first.`, () => {
        delete AppConfig.artChicago[id];
        renderArtChicagoConfigUI();
        markUnsavedChanges(true);
        showToast(`Art Institute configuration '${id}' deleted locally. Save settings to persist.`, 'info');
    });
}

export function deleteArtChicagoConfig(id) {
    openDeleteModal(`Delete Art Institute configuration '${id}'? This cannot be undone without saving first.`, () => {
        delete AppConfig.artChicago[id];
        renderArtChicagoConfigUI();
        markUnsavedChanges(true);
        showToast(`Art Institute configuration '${id}' deleted locally. Save settings to persist.`, 'info');
    });
}

export async function validateArtChicagoConfigFromModal() {
    const config = editingArtChicagoConfigId ? AppConfig.artChicago[editingArtChicagoConfigId] : tempNewArtChicagoConfig;
    if (!config) return;

    const tempConfig = {
        query:     document.getElementById('editArtChicagoQuery').value,
        fbinkPath: document.getElementById('editArtChicagoFbink').value
    };

    showToast('Testing connectivity to Art Institute of Chicago API...', 'info');
    try {
        const resp = await apiFetch('/api/config/validate/artchicago', {
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
