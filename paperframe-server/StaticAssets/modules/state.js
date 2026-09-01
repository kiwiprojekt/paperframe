export let AppConfig = {
    devices: {},
    calendar: {},
    immich: {},
    artChicago: {},
    meteo: {},
    homeAssistant: { apiUrl: '', oauthBearerToken: '' },
    settings: { serverAddress: '', enableCalendar: true, enableImmich: true, enableArtChicago: true, enableHomeAssistant: true, managerPassword: '', timeZoneId: '' }
};

export function setAppConfig(config) {
    // Merge or reassign config keys to keep references in other files intact
    Object.assign(AppConfig, config);
    
    // Safeguard dictionaries from being null
    AppConfig.devices = AppConfig.devices || {};
    AppConfig.calendar = AppConfig.calendar || {};
    AppConfig.immich = AppConfig.immich || {};
    AppConfig.artChicago = AppConfig.artChicago || {};

    // Remove keys that are not present in the new config
    for (const key of Object.keys(AppConfig)) {
        if (!(key in config)) {
            delete AppConfig[key];
        }
    }
}

export let DeviceStatuses = {};

// The launcher version this server would hand out today, so a card can tell an
// up-to-date client from one that needs re-installing.
export let ExpectedScriptVersion = '';

export function setDeviceStatuses(payload) {
    const devices = payload?.devices ?? {};
    ExpectedScriptVersion = payload?.expectedScriptVersion ?? '';

    Object.assign(DeviceStatuses, devices);
    for (const key of Object.keys(DeviceStatuses)) {
        if (!(key in devices)) {
            delete DeviceStatuses[key];
        }
    }
}

export let hasUnsavedChanges = false;

export function setUnsavedChanges(val) {
    hasUnsavedChanges = val;
}
