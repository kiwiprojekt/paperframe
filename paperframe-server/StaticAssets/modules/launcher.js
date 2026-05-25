import { getServerUrl } from './api.js';
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

export function getLauncherScriptTemplate(deviceId, sleepTime, serverUrl) {
    return `#!/bin/sh
# Name: Paperframe Client
# Author: Michal Sadurski
# DontUseFBInk
# Auto-provisioned for Device: ${deviceId}

# -------- Configuration --------
DEVICE_ID="${deviceId}" 
SLEEP_TIME_S=${sleepTime}
SERVICES_URL="${serverUrl}"
# -------------------------------
SCREEN_RES="$(eips -i | grep 'xres:' | tr -d ' xres:' | tr 'y' ',')"
# -------------------------------

# mount filesystem as writeable
mntroot rw

# disable screensaver
lipc-set-prop com.lab126.powerd preventScreenSaver 1

# move to documents directory
cd /mnt/us/documents

while true; do
    # get battery status
    BATT_PERCENT="$(gasgauge-info -s)"

    # download script to execute
    wget --header="device_id: $DEVICE_ID" \\
        --header="battery: $BATT_PERCENT" \\
        --header="screen_res: $SCREEN_RES" \\
        -O script.sh $SERVICES_URL; \\
        wget_result=$?

    #if download failed
    if [ $wget_result -ne 0 ]; then
        #reenable screensaver 
        lipc-set-prop com.lab126.powerd preventScreenSaver 0
        # exit
        return 1;
    fi

    # clear display
    eips -f
    sleep 1
    eips -f

    #make variables available to downloaded script
    export DEVICE_ID SCREEN_RES SERVICES_URL BATT_PERCENT

    # run downloaded script
    ./script.sh; script_result=$?
    
    if [ $script_result -ne 0 ]; then
        #reenable screensaver 
        lipc-set-prop com.lab126.powerd preventScreenSaver 0
        # exit
        return 1;
    fi

    sleep 3

    # set powersave mode
    echo powersave > /sys/devices/system/cpu/cpu0/cpufreq/scaling_governor

    # schedule next wakeup
    rtcwake -d /dev/rtc1 -m no -s $SLEEP_TIME_S

    # set sleep mode
    echo "mem" > /sys/power/state

    sleep 5;
done`;
}

export function updateLauncherScriptPreview() {
    if (!launcherDeviceId) return;
    const hours     = parseInt(document.getElementById('launcherSleepHours').value)   || 0;
    const minutes   = parseInt(document.getElementById('launcherSleepMinutes').value) || 0;
    const seconds   = parseInt(document.getElementById('launcherSleepSeconds').value) || 0;
    const sleepTime = (hours * 3600) + (minutes * 60) + seconds;

    document.getElementById('totalSleepSecondsDisplay').innerText = sleepTime;
    document.getElementById('launcherCodeContent').innerText = getLauncherScriptTemplate(launcherDeviceId, sleepTime, getServerUrl());
}

export function downloadConfiguredLauncher() {
    if (!launcherDeviceId) return;
    const hours     = parseInt(document.getElementById('launcherSleepHours').value)   || 0;
    const minutes   = parseInt(document.getElementById('launcherSleepMinutes').value) || 0;
    const seconds   = parseInt(document.getElementById('launcherSleepSeconds').value) || 0;
    const sleepTime = (hours * 3600) + (minutes * 60) + seconds;

    if (sleepTime < 10) { alert('Sleep interval must be at least 10 seconds.'); return; }

    const scriptText = getLauncherScriptTemplate(launcherDeviceId, sleepTime, getServerUrl());
    const blob = new Blob([scriptText], { type: 'application/x-sh' });
    const link = document.createElement('a');
    link.href     = URL.createObjectURL(blob);
    link.download = 'paperframe.sh';
    link.click();

    const parts = [];
    if (hours   > 0) parts.push(`${hours}h`);
    if (minutes > 0) parts.push(`${minutes}m`);
    if (seconds > 0 || parts.length === 0) parts.push(`${seconds}s`);

    showToast(`Downloaded pre-configured paperframe.sh with sleep interval ${parts.join(' ')} (${sleepTime}s)!`);
    closeLauncherModal();
}
