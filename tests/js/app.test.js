import { afterEach, describe, expect, it, vi } from 'vitest';
import { JSDOM } from 'jsdom';
import { readFileSync } from 'node:fs';
import { fileURLToPath, pathToFileURL } from 'node:url';
import path from 'node:path';

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const appJsUrl = pathToFileURL(path.join(repoRoot, 'paperframe-server/StaticAssets/app.js')).href;
let importCounter = 0;

async function loadApp() {
  const html = readFileSync(path.join(repoRoot, 'paperframe-server/StaticAssets/index.html'), 'utf8');
  const dom = new JSDOM(html, {
    url: 'http://paperframe.local',
    runScripts: 'outside-only',
    pretendToBeVisual: true
  });
  const previousGlobals = {
    window: globalThis.window,
    document: globalThis.document,
    lucide: globalThis.lucide,
    alert: globalThis.alert,
    URL: globalThis.URL,
    __PAPERFRAME_TEST__: globalThis.__PAPERFRAME_TEST__,
    PaperframeTestApi: globalThis.PaperframeTestApi,
    fetch: globalThis.fetch
  };

  dom.window.__PAPERFRAME_TEST__ = true;
  dom.window.lucide = { createIcons: vi.fn() };
  dom.window.alert = vi.fn();
  dom.window.URL.createObjectURL = vi.fn(() => 'blob:paperframe-test');

  globalThis.window = dom.window;
  globalThis.document = dom.window.document;
  globalThis.lucide = dom.window.lucide;
  globalThis.alert = dom.window.alert;
  globalThis.URL = dom.window.URL;
  globalThis.__PAPERFRAME_TEST__ = true;

  await import(`${appJsUrl}?test=${importCounter++}`);

  return {
    window: dom.window,
    document: dom.window.document,
    api: globalThis.PaperframeTestApi,
    close: () => {
      dom.window.close();
      for (const [key, value] of Object.entries(previousGlobals)) {
        if (value === undefined) {
          delete globalThis[key];
        } else {
          globalThis[key] = value;
        }
      }
    }
  };
}

function mockFetch(window, implementation) {
  const fetch = vi.fn(implementation);
  window.fetch = fetch;
  globalThis.fetch = fetch;
  return fetch;
}

afterEach(() => {
  vi.restoreAllMocks();
});

describe('Paperframe manager app.js', () => {
  it('apiFetch shows the login overlay and throws AuthError on 401', async () => {
    const { window, document, api, close } = await loadApp();
    mockFetch(window, async () => ({ status: 401 }));

    await expect(api.apiFetch('/api/config')).rejects.toBeInstanceOf(api.AuthError);

    expect(document.getElementById('loginOverlay').classList.contains('hidden')).toBe(false);
    expect(document.getElementById('logoutBtn').style.display).toBe('none');
    expect(window.lucide.createIcons).toHaveBeenCalled();
    close();
  });

  it('markUnsavedChanges toggles save affordances', async () => {
    const { document, api, close } = await loadApp();
    const saveButton = document.getElementById('saveConfigBtn');
    const indicator = document.getElementById('unsavedChangesIndicator');

    api.markUnsavedChanges(true);

    expect(saveButton.hasAttribute('disabled')).toBe(false);
    expect(saveButton.classList.contains('pulse-animation')).toBe(true);
    expect(indicator.classList.contains('visible')).toBe(true);

    api.markUnsavedChanges(false);

    expect(saveButton.hasAttribute('disabled')).toBe(true);
    expect(saveButton.classList.contains('pulse-animation')).toBe(false);
    expect(indicator.classList.contains('visible')).toBe(false);
    close();
  });

  it('saveConfiguration posts the current form state to the config API', async () => {
    const { window, document, api, close } = await loadApp();
    api.setAppConfig({ devices: {}, calendar: {}, immich: {}, homeAssistant: {}, settings: {} });
    const fetch = mockFetch(window, async () => ({
      ok: true,
      status: 200,
      json: async () => ({ message: 'saved' })
    }));

    document.getElementById('haApiUrl').value = 'http://ha.local/api';
    document.getElementById('haToken').value = 'ha-token';
    document.getElementById('haUpdateTemplate').value = 'input_datetime.{deviceId}.seen';
    document.getElementById('haBatteryTemplate').value = 'input_number.{deviceId}.battery';
    document.getElementById('haEnableUpdate').checked = true;
    document.getElementById('haEnableBattery').checked = false;
    document.getElementById('settingsServerAddress').value = 'http://paperframe.local';
    document.getElementById('settingsEnableCalendar').checked = true;
    document.getElementById('settingsEnableImmich').checked = false;
    document.getElementById('settingsEnableHa').checked = true;
    document.getElementById('settingsPassword').value = 'secret';

    await api.saveConfiguration();

    expect(fetch).toHaveBeenCalledTimes(1);
    const [url, options] = fetch.mock.calls[0];
    const payload = JSON.parse(options.body);
    expect(url).toBe('/api/config');
    expect(options.method).toBe('POST');
    expect(payload.homeAssistant).toMatchObject({
      apiUrl: 'http://ha.local/api',
      oauthBearerToken: 'ha-token',
      updateEntityTemplate: 'input_datetime.{deviceId}.seen',
      batteryEntityTemplate: 'input_number.{deviceId}.battery',
      disableUpdateEntity: false,
      disableBatteryEntity: true
    });
    expect(payload.settings).toMatchObject({
      serverAddress: 'http://paperframe.local',
      enableCalendar: true,
      enableImmich: false,
      enableHomeAssistant: true,
      managerPassword: 'secret'
    });
    close();
  });

  it('renders device cards with status and updates disabled state from the toggle', async () => {
    const { document, api, close } = await loadApp();
    api.setAppConfig({
      devices: {
        'kindle-a': { serviceName: 'Calendar', configId: 'family', disabled: false }
      },
      calendar: {},
      immich: {},
      homeAssistant: {},
      settings: {}
    });
    api.setDeviceStatuses({
      'kindle-a': {
        battery: 88,
        status: 'Success',
        lastUpdate: new Date().toISOString()
      }
    });

    api.renderDeviceCards();

    expect(document.getElementById('devicesContainer').textContent).toContain('kindle-a');
    expect(document.getElementById('devicesContainer').textContent).toContain('88%');

    const toggle = document.querySelector('[data-action="toggle"]');
    toggle.checked = false;
    toggle.onchange();

    expect(api.getAppConfig().devices['kindle-a'].disabled).toBe(true);
    close();
  });

  it('updates launcher preview from sleep inputs and custom server address', async () => {
    const { document, api, close } = await loadApp();
    api.setAppConfig({
      devices: {},
      calendar: {},
      immich: {},
      homeAssistant: {},
      settings: { serverAddress: 'http://custom.local' }
    });
    api.setLauncherDeviceId('kindle-a');
    document.getElementById('launcherSleepHours').value = '1';
    document.getElementById('launcherSleepMinutes').value = '2';
    document.getElementById('launcherSleepSeconds').value = '3';

    api.updateLauncherScriptPreview();

    expect(document.getElementById('totalSleepSecondsDisplay').innerText).toBe(3723);
    const script = document.getElementById('launcherCodeContent').innerText;
    expect(script).toContain('DEVICE_ID="kindle-a"');
    expect(script).toContain('SLEEP_TIME_S=3723');
    expect(script).toContain('SERVICES_URL="http://custom.local"');
    close();
  });
});
