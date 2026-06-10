import { AppConfig, DeviceStatuses } from './state.js';
import { markUnsavedChanges, openDeleteModal, showToast } from './ui-utils.js';
import { openLauncherModal } from './launcher.js';

export let editingDeviceId = null;

export function timeAgo(date) {
    const seconds = Math.floor((Date.now() - date) / 1000);
    if (seconds < 60)   return `${seconds}s ago`;
    const minutes = Math.floor(seconds / 60);
    if (minutes < 60)   return `${minutes}m ago`;
    const hours = Math.floor(minutes / 60);
    if (hours < 24)     return `${hours}h ago`;
    const days = Math.floor(hours / 24);
    return `${days}d ago`;
}

export function renderDeviceCards() {
    const container = document.getElementById('devicesContainer');
    container.innerHTML = '';

    const devices   = AppConfig.devices || {};
    const deviceIds = Object.keys(devices);

    if (deviceIds.length === 0) {
        container.appendChild(document.getElementById('tpl-device-empty').content.cloneNode(true));
        return;
    }

    deviceIds.forEach(id => {
        const dev = devices[id];
        const tpl = document.getElementById('tpl-device-card').content.cloneNode(true);
        const f   = name => tpl.querySelector(`[data-field="${name}"]`);

        let iconName  = 'tablet';
        let labelText = 'Device';
        const svc = (dev.serviceName || "").toLowerCase();
        if (svc === 'calendar') { iconName = 'calendar-days'; labelText = 'Calendar'; }
        else if (svc === 'immich') { iconName = 'image';      labelText = 'Photo'; }
        else if (svc === 'artchicago') { iconName = 'palette'; labelText = 'Art'; }
        else if (svc === 'meteo')  { iconName = 'cloud-sun';  labelText = 'Meteo'; }

        f('typeLabel').textContent = labelText;
        f('typeIcon').setAttribute('data-lucide', iconName);
        f('frameConfigId').textContent = dev.configId;
        f('deviceId').textContent      = id;
        f('serviceName').textContent   = dev.serviceName;
        f('metaConfigId').textContent  = dev.configId;

        const isDisabled = dev.disabled === true;

        const rowEl = tpl.querySelector('.device-row');
        if (isDisabled) rowEl.classList.add('device-row--disabled');

        const toggleEl = tpl.querySelector('[data-action="toggle"]');
        toggleEl.checked = !isDisabled;
        f('toggleLabel').textContent = isDisabled ? 'Disabled' : 'Enabled';
        toggleEl.onchange = () => {
            AppConfig.devices[id].disabled = !toggleEl.checked;
            renderDeviceCards();
            markUnsavedChanges(true);
        };

        const status  = DeviceStatuses[id];
        const battEl  = f('battery');
        const lastEl  = f('lastSeen');

        if (status) {
            const battVal = status.battery !== null ? `${status.battery}%` : '---';
            if (status.battery !== null && status.battery < 20) battEl.style.color = 'var(--error)';
            battEl.textContent = battVal;

            const updateDate = new Date(status.lastUpdate);
            lastEl.textContent = `${updateDate.toLocaleTimeString()} (${timeAgo(updateDate)})`;
        } else {
            battEl.className   = 'text-secondary';
            battEl.textContent = 'Unknown';
            lastEl.className   = 'text-secondary';
            lastEl.textContent = 'Never';
        }

        tpl.querySelector('[data-action="edit"]').onclick    = () => openDeviceModal(id);
        tpl.querySelector('[data-action="delete"]').onclick  = () => deleteDevice(id);
        tpl.querySelector('[data-action="launcher"]').onclick = () => openLauncherModal(id);

        container.appendChild(tpl);
    });
    lucide.createIcons();
}

export function openDeviceModal(id = null) {
    editingDeviceId = id;
    const titleEl      = document.getElementById('deviceModalTitle').querySelector('span');
    const idInput      = document.getElementById('modalDeviceId');
    const serviceSelect = document.getElementById('modalDeviceService');

    if (id) {
        titleEl.innerText  = `Configure Device: ${id}`;
        idInput.value      = id;
        idInput.disabled   = true;
        const dev          = AppConfig.devices[id];
        serviceSelect.value = dev.serviceName;
        populateModalConfigDropdown(dev.configId);
    } else {
        titleEl.innerText    = 'Register New Paperframe Device';
        idInput.value        = '';
        idInput.disabled     = false;
        serviceSelect.value  = 'Calendar';
        populateModalConfigDropdown();
    }

    document.getElementById('deviceModal').classList.add('active');
    lucide.createIcons();
}

export function closeDeviceModal() {
    document.getElementById('deviceModal').classList.remove('active');
    editingDeviceId = null;
}

export function populateModalConfigDropdown(selectedConfigId = null) {
    const service  = document.getElementById('modalDeviceService').value;
    const dropdown = document.getElementById('modalDeviceConfigId');
    dropdown.innerHTML = '';

    let options = [];
    if (service === 'Calendar') options = Object.keys(AppConfig.calendar || {});
    else if (service === 'Immich') options = Object.keys(AppConfig.immich || {});
    else if (service === 'ArtChicago') options = Object.keys(AppConfig.artChicago || {});

    const saveBtn = document.querySelector('#deviceModal .modal-footer .btn-primary');
    if (options.length === 0) {
        dropdown.innerHTML = `<option value="">No ${service} configurations — add one first</option>`;
        if (saveBtn) saveBtn.setAttribute('disabled', 'true');
    } else {
        dropdown.innerHTML = options.map(opt => `<option value="${opt}">${opt}</option>`).join('');
        if (saveBtn) saveBtn.removeAttribute('disabled');
    }

    if (selectedConfigId && options.includes(selectedConfigId)) {
        dropdown.value = selectedConfigId;
    }
}

export function saveDeviceModalData() {
    const idInput      = document.getElementById('modalDeviceId');
    const serviceSelect = document.getElementById('modalDeviceService');
    const configSelect  = document.getElementById('modalDeviceConfigId');

    const deviceId = idInput.value.trim();
    if (!deviceId) { alert('Device ID is required.'); return; }

    if (!editingDeviceId && AppConfig.devices?.[deviceId]) {
        alert(`A device mapping with ID '${deviceId}' already exists.`);
        return;
    }

    AppConfig.devices        = AppConfig.devices || {};
    AppConfig.devices[deviceId] = {
        serviceName: serviceSelect.value,
        configId:    configSelect.value,
        disabled:    editingDeviceId ? (AppConfig.devices[editingDeviceId]?.disabled ?? false) : false
    };

    closeDeviceModal();
    renderDeviceCards();
    markUnsavedChanges(true);
    showToast(`Device '${deviceId}' mapped successfully. Remember to Save Settings!`, 'info');
}

export function deleteDevice(id) {
    openDeleteModal(`Remove device mapping for '${id}'?`, () => {
        delete AppConfig.devices[id];
        renderDeviceCards();
        markUnsavedChanges(true);
        showToast(`Device '${id}' removed from local list. Save settings to persist.`, 'info');
    });
}
