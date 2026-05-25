import { AppConfig, DeviceStatuses, hasUnsavedChanges, setAppConfig, setDeviceStatuses, setUnsavedChanges } from './modules/state.js';
import { showToast, openConfirmModal, closeConfirmModal, openDeleteModal, toggleSecretVisibility, markUnsavedChanges, switchTab } from './modules/ui-utils.js';
import { AuthError, apiFetch, showLoginOverlay, hideLoginOverlay, checkAuth, submitLogin, logout, loadConfigData, saveConfiguration, reloadConfigData, loadTelemetryLogs, getServerUrl } from './modules/api.js';
import { timeAgo, renderDeviceCards, openDeviceModal, closeDeviceModal, populateModalConfigDropdown, saveDeviceModalData, deleteDevice } from './modules/devices.js';
import { renderCalendarConfigUI, openCalendarConfigModal, closeCalendarConfigModal, renderEditCalIcalUrls, updateEditCalIcalUrlValue, addEditCalIcalUrlRow, removeEditCalIcalUrlRow, saveCalendarConfigModalData, deleteCalendarConfigFromModal, deleteCalendarConfig, validateCalendarConfigFromModal } from './modules/calendar.js';
import { renderImmichConfigUI, openImmichConfigModal, closeImmichConfigModal, loadAlbumsForEditModal, selectEditModalDropdownAlbum, saveImmichConfigModalData, deleteImmichConfigFromModal, deleteImmichConfig, validateImmichConfigFromModal } from './modules/immich.js';
import { updateSidebarVisibility, validateHomeAssistantConnection } from './modules/homeassistant.js';
import { openLauncherModal, closeLauncherModal, getLauncherScriptTemplate, updateLauncherScriptPreview, downloadConfiguredLauncher, setLauncherDeviceId, launcherDeviceId } from './modules/launcher.js';

// Bind methods to window for inline HTML attributes (onclick, oninput, etc.)
window.submitLogin = submitLogin;
window.toggleSecretVisibility = toggleSecretVisibility;
window.switchTab = switchTab;
window.saveConfiguration = saveConfiguration;
window.reloadConfigData = reloadConfigData;
window.logout = logout;
window.openDeviceModal = openDeviceModal;
window.closeDeviceModal = closeDeviceModal;
window.saveDeviceModalData = saveDeviceModalData;
window.populateModalConfigDropdown = populateModalConfigDropdown;
window.validateHomeAssistantConnection = validateHomeAssistantConnection;
window.markUnsavedChanges = markUnsavedChanges;
window.updateSidebarVisibility = updateSidebarVisibility;
window.closeConfirmModal = closeConfirmModal;
window.closeLauncherModal = closeLauncherModal;
window.updateLauncherScriptPreview = updateLauncherScriptPreview;
window.downloadConfiguredLauncher = downloadConfiguredLauncher;
window.openCalendarConfigModal = openCalendarConfigModal;
window.closeCalendarConfigModal = closeCalendarConfigModal;
window.addEditCalIcalUrlRow = addEditCalIcalUrlRow;
window.validateCalendarConfigFromModal = validateCalendarConfigFromModal;
window.saveCalendarConfigModalData = saveCalendarConfigModalData;
window.deleteCalendarConfigFromModal = deleteCalendarConfigFromModal;
window.openImmichConfigModal = openImmichConfigModal;
window.closeImmichConfigModal = closeImmichConfigModal;
window.loadAlbumsForEditModal = loadAlbumsForEditModal;
window.selectEditModalDropdownAlbum = selectEditModalDropdownAlbum;
window.validateImmichConfigFromModal = validateImmichConfigFromModal;
window.saveImmichConfigModalData = saveImmichConfigModalData;
window.deleteImmichConfigFromModal = deleteImmichConfigFromModal;

// Initialize app on load
window.onload = async () => {
    lucide.createIcons();

    const authed = await checkAuth();
    if (authed) {
        await loadConfigData();
        await loadTelemetryLogs();
    }

    // Always start polling — auth errors will re-show the login overlay
    setInterval(async () => {
        const activeTab = document.querySelector('.nav-item.active')?.dataset.tab;
        if (activeTab === 'dashboard') {
            await loadTelemetryLogs();
        } else if (activeTab === 'devices') {
            try {
                const resp = await apiFetch('/api/config/devices/status');
                if (resp.ok) {
                    const statuses = await resp.json();
                    setDeviceStatuses(statuses);
                    renderDeviceCards();
                }
            } catch (e) {
                if (!(e instanceof AuthError)) console.error('Failed to poll device status', e);
            }
        }
    }, 5000);
};

// Register testing hooks for Vitest if tests are running
if (globalThis.__PAPERFRAME_TEST__) {
    globalThis.PaperframeTestApi = {
        AuthError,
        apiFetch,
        showLoginOverlay,
        hideLoginOverlay,
        checkAuth,
        submitLogin,
        logout,
        markUnsavedChanges,
        switchTab,
        loadConfigData,
        saveConfiguration,
        renderDeviceCards,
        openDeviceModal,
        saveDeviceModalData,
        renderCalendarConfigUI,
        openCalendarConfigModal,
        renderEditCalIcalUrls,
        updateEditCalIcalUrlValue,
        addEditCalIcalUrlRow,
        removeEditCalIcalUrlRow,
        saveCalendarConfigModalData,
        renderImmichConfigUI,
        openImmichConfigModal,
        loadAlbumsForEditModal,
        saveImmichConfigModalData,
        updateSidebarVisibility,
        getServerUrl,
        getLauncherScriptTemplate,
        updateLauncherScriptPreview,
        downloadConfiguredLauncher,
        getAppConfig: () => AppConfig,
        setAppConfig: value => { setAppConfig(value); },
        getDeviceStatuses: () => DeviceStatuses,
        setDeviceStatuses: value => { setDeviceStatuses(value); },
        setLauncherDeviceId: value => { setLauncherDeviceId(value); }
    };
}
