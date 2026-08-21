#!/bin/sh
# Name: Paperframe Client
# Author: Michal Sadurski
# DontUseFBInk
# Auto-provisioned for Device: @DEVICE_ID@

# -------- Configuration --------
DEVICE_ID="@DEVICE_ID@"
SERVICES_URL="@SERVER_URL@"
SCRIPT_VERSION="@SCRIPT_VERSION@"
# -------------------------------
SCREEN_RES="$(eips -i | grep 'xres:' | tr -d ' xres:' | tr 'y' ',')"
# -------------------------------

# Release the screensaver guard. This is the only place that hands the display
# back to powerd, so every exit path has to go through here.
cleanup() {
    lipc-set-prop com.lab126.powerd preventScreenSaver 0
    exit ${1:-0}
}
trap 'cleanup 0' INT TERM

# Report a failure to the server, then stop. Best effort: when the network is
# the thing that broke, this call fails too and we exit anyway.
die() {
    wget -qO- \
        --header="device_id: $DEVICE_ID" \
        --header="battery: $BATT_PERCENT" \
        --header="screen_res: $SCREEN_RES" \
        --header="script_version: $SCRIPT_VERSION" \
        --header="x-client-error: $1" \
        "$SERVICES_URL/client/error"
    cleanup 1
}

@RETRY_WGET_FN@

# mount filesystem as writeable
mntroot rw

# hold the display for as long as this script owns it
lipc-set-prop com.lab126.powerd preventScreenSaver 1

# move to documents directory
cd /mnt/us/documents

while true; do
    # get battery status
    BATT_PERCENT="$(gasgauge-info -s)"

    # download script to execute
    retry_wget --header="device_id: $DEVICE_ID" \
        --header="battery: $BATT_PERCENT" \
        --header="screen_res: $SCREEN_RES" \
        --header="script_version: $SCRIPT_VERSION" \
        -S -O script.sh "$SERVICES_URL" 2> wget_headers.log
    wget_result=$?

    if [ $wget_result -ne 0 ]; then
        die "wget_failed_$wget_result"
    fi

    # the server asks us to stop by stamping this header on its answer, which is
    # checked before anything downloaded gets executed
    if grep -qi '@DISABLED_HEADER@:' wget_headers.log; then
        cleanup 0
    fi

    # the server dictates the wake interval; there is deliberately no local default
    SLEEP_TIME_S=$(grep -i '@SLEEP_HEADER@:' wget_headers.log | awk '{print $2}' | tr -d '\r' | tail -n 1)
    if [ -z "$SLEEP_TIME_S" ]; then
        die "missing_sleep_header"
    fi

    # clear display
    eips -f
    sleep 1
    eips -f

    # make variables available to downloaded script
    export DEVICE_ID SCREEN_RES SERVICES_URL BATT_PERCENT SCRIPT_VERSION

    # run downloaded script
    ./script.sh > script_out.log 2> script_err.log
    script_result=$?

    if [ $script_result -ne 0 ]; then
        err=$(head -c 150 script_err.log | tr '\n\r\t' '   ' | sed 's/[^a-zA-Z0-9 ._:/-]//g')
        die "script_failed_${script_result}_$err"
    fi

    sleep 3

    # set powersave mode
    echo powersave > /sys/devices/system/cpu/cpu0/cpufreq/scaling_governor

    # schedule next wakeup
    rtcwake -d /dev/rtc1 -m no -s $SLEEP_TIME_S

    # set sleep mode
    echo "mem" > /sys/power/state

    sleep 5
done
