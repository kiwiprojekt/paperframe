export let AppConfig = {
    devices: {},
    calendar: {},
    immich: {},
    meteo: {},
    homeAssistant: { apiUrl: '', oauthBearerToken: '' },
    settings: { serverAddress: '', enableCalendar: true, enableImmich: true, enableHomeAssistant: true, managerPassword: '' }
};

export function setAppConfig(config) {
    // Merge or reassign config keys to keep references in other files intact
    Object.assign(AppConfig, config);
    // Remove keys that are not present in the new config
    for (const key of Object.keys(AppConfig)) {
        if (!(key in config)) {
            delete AppConfig[key];
        }
    }
}

export let DeviceStatuses = {};

export function setDeviceStatuses(statuses) {
    Object.assign(DeviceStatuses, statuses);
    for (const key of Object.keys(DeviceStatuses)) {
        if (!(key in statuses)) {
            delete DeviceStatuses[key];
        }
    }
}

export let hasUnsavedChanges = false;

export function setUnsavedChanges(val) {
    hasUnsavedChanges = val;
}
