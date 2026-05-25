import { AppConfig } from './state.js';
import { markUnsavedChanges, openDeleteModal, showToast } from './ui-utils.js';
import { apiFetch, AuthError } from './api.js';

export let editingCalConfigId = null;
export let tempNewCalConfig   = null;

export function renderCalendarConfigUI() {
    const tbody = document.getElementById('calendarListBody');
    tbody.innerHTML = '';

    AppConfig.calendar = AppConfig.calendar || {};
    const keys = Object.keys(AppConfig.calendar);

    if (keys.length === 0) {
        tbody.appendChild(document.getElementById('tpl-calendar-empty').content.cloneNode(true));
        return;
    }

    keys.forEach(k => {
        const config     = AppConfig.calendar[k];
        const feedsCount = config.icalUrls ? config.icalUrls.filter(u => u.trim().length > 0).length : 0;
        const mapped     = Object.keys(AppConfig.devices || {}).filter(d =>
            AppConfig.devices[d].serviceName.toLowerCase() === 'calendar' &&
            AppConfig.devices[d].configId === k
        );

        const tpl = document.getElementById('tpl-calendar-row').content.cloneNode(true);
        const f   = name => tpl.querySelector(`[data-field="${name}"]`);

        f('configId').textContent  = k;
        f('culture').textContent   = config.cultureInfoName || 'en-US';
        f('timezone').textContent  = config.timeZoneId || 'UTC';
        f('feedCount').textContent = `${feedsCount} feed(s)`;

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

        tpl.querySelector('[data-action="edit"]').onclick   = () => openCalendarConfigModal(k);
        tpl.querySelector('[data-action="delete"]').onclick = () => deleteCalendarConfig(k);

        tbody.appendChild(tpl);
    });

    lucide.createIcons();
}

export function openCalendarConfigModal(id = null) {
    editingCalConfigId = id;

    const titleEl   = document.getElementById('calendarConfigModalTitle').querySelector('span');
    const idField   = document.getElementById('editCalConfigIdField');
    const deleteBtn = document.getElementById('editCalDeleteBtn');

    if (id) {
        const config = AppConfig.calendar[id];
        titleEl.innerText = `Edit Calendar Configuration: ${id}`;
        idField.value     = id;
        idField.disabled  = true;
        deleteBtn.style.display = 'inline-flex';

        document.getElementById('editCalConfigId').value    = id;
        document.getElementById('editCalCulture').value    = config.cultureInfoName || 'en-US';
        document.getElementById('editCalTimezone').value   = config.timeZoneId      || 'UTC';
        document.getElementById('editCalToday').value      = config.headerToday     || 'Today';
        document.getElementById('editCalTomorrow').value   = config.headerTomorrow  || 'Tomorrow';
        document.getElementById('editCalFbink').value      = config.fbinkPath       || '/mnt/us/libkh/bin/fbink';
        document.getElementById('editCalFont').value       = config.fontPath        || '/mnt/us/documents/Cal_Sans/CalSans-Regular.ttf';
        renderEditCalIcalUrls();
    } else {
        titleEl.innerText = 'Add Calendar Configuration';
        idField.value     = '';
        idField.disabled  = false;
        deleteBtn.style.display = 'none';

        tempNewCalConfig = {
            icalUrls:       [],
            cultureInfoName: 'en-US',
            timeZoneId:      'UTC',
            headerToday:     'Today',
            headerTomorrow:  'Tomorrow',
            fbinkPath:       '/mnt/us/libkh/bin/fbink',
            fontPath:        '/mnt/us/documents/Cal_Sans/CalSans-Regular.ttf'
        };

        document.getElementById('editCalConfigId').value    = '';
        document.getElementById('editCalCulture').value    = 'en-US';
        document.getElementById('editCalTimezone').value   = 'UTC';
        document.getElementById('editCalToday').value      = 'Today';
        document.getElementById('editCalTomorrow').value   = 'Tomorrow';
        document.getElementById('editCalFbink').value      = '/mnt/us/libkh/bin/fbink';
        document.getElementById('editCalFont').value       = '/mnt/us/documents/Cal_Sans/CalSans-Regular.ttf';
        renderEditCalIcalUrls();
    }

    document.getElementById('calendarConfigModal').classList.add('active');
    lucide.createIcons();
}

export function closeCalendarConfigModal() {
    document.getElementById('calendarConfigModal').classList.remove('active');
    editingCalConfigId = null;
    tempNewCalConfig   = null;
}

export function renderEditCalIcalUrls() {
    const container = document.getElementById('editCalIcalUrlsList');
    container.innerHTML = '';
    const config = editingCalConfigId ? AppConfig.calendar[editingCalConfigId] : tempNewCalConfig;
    if (!config) return;
    config.icalUrls = config.icalUrls || [];

    if (config.icalUrls.length === 0) {
        const msg = document.createElement('span');
        msg.className   = 'text-secondary text-sm';
        msg.style.cssText = 'display: block; margin-bottom: 0.5rem;';
        msg.textContent = 'No iCal URLs added. Click Add feed below to configure.';
        container.appendChild(msg);
        return;
    }

    config.icalUrls.forEach((url, i) => {
        const tpl    = document.getElementById('tpl-ical-url-row').content.cloneNode(true);
        const input  = tpl.querySelector('input');
        const btn    = tpl.querySelector('button');
        input.value  = url;
        input.oninput  = e => updateEditCalIcalUrlValue(i, e.target.value);
        btn.onclick    = () => removeEditCalIcalUrlRow(i);
        container.appendChild(tpl);
    });
    lucide.createIcons();
}

export function updateEditCalIcalUrlValue(idx, val) {
    const config = editingCalConfigId ? AppConfig.calendar[editingCalConfigId] : tempNewCalConfig;
    config.icalUrls[idx] = val;
    markUnsavedChanges(true);
}

export function addEditCalIcalUrlRow() {
    const config = editingCalConfigId ? AppConfig.calendar[editingCalConfigId] : tempNewCalConfig;
    config.icalUrls = config.icalUrls || [];
    config.icalUrls.push('');
    renderEditCalIcalUrls();
    markUnsavedChanges(true);
}

export function removeEditCalIcalUrlRow(idx) {
    const config = editingCalConfigId ? AppConfig.calendar[editingCalConfigId] : tempNewCalConfig;
    config.icalUrls.splice(idx, 1);
    renderEditCalIcalUrls();
    markUnsavedChanges(true);
}

export function saveCalendarConfigModalData() {
    let id = editingCalConfigId;
    if (!id) {
        const newId = document.getElementById('editCalConfigIdField').value.trim();
        if (!newId) { alert('Config ID is required.'); return; }
        if (AppConfig.calendar[newId]) { alert('A configuration with this ID already exists.'); return; }
        id = newId;
        AppConfig.calendar[id] = tempNewCalConfig;
    }

    const config = AppConfig.calendar[id];
    config.cultureInfoName = document.getElementById('editCalCulture').value;
    config.timeZoneId      = document.getElementById('editCalTimezone').value;
    config.headerToday     = document.getElementById('editCalToday').value;
    config.headerTomorrow  = document.getElementById('editCalTomorrow').value;
    config.fbinkPath       = document.getElementById('editCalFbink').value;
    config.fontPath        = document.getElementById('editCalFont').value;
    config.icalUrls        = config.icalUrls.map(u => u.trim()).filter(u => u.length > 0);

    closeCalendarConfigModal();
    renderCalendarConfigUI();
    markUnsavedChanges(true);
    showToast(`Calendar config '${id}' changes applied locally. Remember to Save Settings!`, 'info');
}

export function deleteCalendarConfigFromModal() {
    const id = editingCalConfigId;
    closeCalendarConfigModal();
    openDeleteModal(`Delete calendar configuration '${id}'?`, () => {
        delete AppConfig.calendar[id];
        renderCalendarConfigUI();
        markUnsavedChanges(true);
        showToast(`Calendar configuration '${id}' removed from local list. Save settings to persist.`, 'info');
    });
}

export function deleteCalendarConfig(id) {
    openDeleteModal(`Delete calendar configuration '${id}'? `, () => {
        delete AppConfig.calendar[id];
        renderCalendarConfigUI();
        markUnsavedChanges(true);
        showToast(`Calendar configuration '${id}' removed from local list. Save settings to persist.`, 'info');
    });
}

export async function validateCalendarConfigFromModal() {
    const config = editingCalConfigId ? AppConfig.calendar[editingCalConfigId] : tempNewCalConfig;
    if (!config) return;

    const tempConfig = {
        cultureInfoName: document.getElementById('editCalCulture').value,
        timeZoneId:      document.getElementById('editCalTimezone').value,
        headerToday:     document.getElementById('editCalToday').value,
        headerTomorrow:  document.getElementById('editCalTomorrow').value,
        fbinkPath:       document.getElementById('editCalFbink').value,
        fontPath:        document.getElementById('editCalFont').value,
        icalUrls:        config.icalUrls.map(u => u.trim()).filter(u => u.length > 0)
    };

    showToast('Querying iCal feeds for validation...', 'info');
    try {
        const resp = await apiFetch('/api/config/validate/calendar', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(tempConfig)
        });
        const data = await resp.json();
        showToast(data.message, data.success ? 'success' : 'error');
    } catch (err) {
        if (err instanceof AuthError) return;
        showToast('Validation network error: ' + err.message, 'error');
    }
}
