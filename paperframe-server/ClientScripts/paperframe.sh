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
# How long to wait for the radio after a wake before giving up on the network.
WIFI_TIMEOUT_S=30
# How the provisioning script reports back: one code for "launcher replaced, restart",
# one for "gave up, serve me normally".
PROVISIONED_EXIT_CODE=@PROVISIONED_EXIT_CODE@
PROVISION_DECLINED_EXIT_CODE=@DECLINED_EXIT_CODE@
# Set once provisioning has declined, so the next check-in asks to be served instead.
SKIP_PROVISION=""
# -------------------------------

@LOG_FN@

# Why the loop is ending. Set before every exit; the exit handler reports it.
EXIT_REASON="unexpected"

# The single exit path. A signal, a failure, or a clean stop all arrive here, so the
# reason is recorded and reported once however the script leaves.
#
# Order matters: the display guard is released first because it is the one thing that
# must never be skipped — leaving it set means the Kindle never sleeps its screen again.
# Logging touches a FAT filesystem that can be unavailable or blocking while the device
# is mounted over USB, so it does not get to run before that.
on_exit() {
    exit_code=$?

    lipc-set-prop com.lab126.powerd preventScreenSaver 0
    log_line EXIT "$EXIT_REASON (code $exit_code)"

    [ "$exit_code" -ne 0 ] && report_failure
    return 0
}
trap on_exit EXIT
trap 'EXIT_REASON="signal_INT"; exit 130' INT
trap 'EXIT_REASON="signal_TERM"; exit 143' TERM

# Tell the server why the loop stopped, and hand over whatever the device has not managed
# to deliver yet.
#
# Best effort, and bounded: when the network is the thing that broke, this call is going
# to fail too, and a device that hangs here never reaches its next wake. The local log is
# what survives that case.
report_failure() {
    wget -q -T 15 -O /dev/null \
        --header="device_id: $DEVICE_ID" \
        --header="battery: $BATT_PERCENT" \
        --header="screen_res: $SCREEN_RES" \
        --header="script_version: $SCRIPT_VERSION" \
        --header="x-client-log: $(log_pending)" \
        --header="x-client-error: $EXIT_REASON" \
        "$SERVICES_URL/client/error" && log_delivered
}

# Stopping is deliberately how this script handles failure. A permanently broken condition
# — a changed wifi password, a moved server, revoked credentials — would otherwise put the
# device into an endless wake/fail/sleep loop, draining the battery with no way to report
# it. Stopping leaves the last image on screen and the fault recorded.
#
# Do not "fix" this by retrying forever. Fix the preconditions instead (see wait_for_wifi)
# and make sure the reason reaches the log.
die() {
    EXIT_REASON="$1"
    exit 1
}

# Kindle wifi does not reassociate instantly after a deep sleep, and a wget against a down
# interface fails in milliseconds — a retry budget alone is a race the device usually
# loses. Waiting on the readiness signal is what makes the retries behind it meaningful.
wait_for_wifi() {
    lipc-set-prop com.lab126.wifid enable 1

    waited=0
    while [ "$waited" -lt "$WIFI_TIMEOUT_S" ]; do
        if [ "$(lipc-get-prop com.lab126.wifid cmState)" = "CONNECTED" ]; then
            # Recorded on every check-in, not just the slow ones: the timeout above is only
            # tunable from evidence if the healthy case is in the log too.
            log_line NET "wifi connected after ${waited}s"
            # Association is not the same as a usable route; give it a moment.
            sleep 1
            return 0
        fi
        sleep 1
        waited=$((waited + 1))
    done

    log_line NET "wifi still not connected after ${WIFI_TIMEOUT_S}s"
    return 1
}

@RETRY_WGET_FN@

# mount filesystem as writeable
mntroot rw || log_line FS "mntroot rw failed"

# move to documents directory
cd "$(dirname "@LAUNCHER_PATH@")" || die "cannot_enter_launcher_directory"

log_line INFO "launcher started device=$DEVICE_ID version=$SCRIPT_VERSION screen=$SCREEN_RES"

# Running at all is the only proof an update worked, so this is where the provisioning
# attempt budget is retired — not anywhere inside the script that performs the update.
rm -f "@STATE_DIR@/provision-attempts"

while true; do
    # Reclaimed every iteration rather than once at startup: powerd hands the guard back
    # on its own across some suspend cycles, and a launcher that took over from another
    # one starts its life without it.
    lipc-set-prop com.lab126.powerd preventScreenSaver 1

    # get battery status
    BATT_PERCENT="$(gasgauge-info -s)"

    wait_for_wifi || die "wifi_timeout_${WIFI_TIMEOUT_S}s"

    # download script to execute, handing over anything still undelivered as we go
    retry_wget --header="device_id: $DEVICE_ID" \
        --header="battery: $BATT_PERCENT" \
        --header="screen_res: $SCREEN_RES" \
        --header="script_version: $SCRIPT_VERSION" \
        --header="x-client-log: $(log_pending)" \
        --header="x-skip-provision: $SKIP_PROVISION" \
        -S -O script.sh "$SERVICES_URL" 2> wget_headers.log
    wget_result=$?

    if [ $wget_result -ne 0 ]; then
        die "wget_failed_$wget_result"
    fi

    log_delivered

    # the server asks us to stop by stamping this header on its answer, which is
    # checked before anything downloaded gets executed
    if grep -qi '@DISABLED_HEADER@:' wget_headers.log; then
        EXIT_REASON="disabled_by_server"
        exit 0
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

    # The provisioning script replaced this launcher and wants the loop restarted on the
    # new one. exec rather than a call, so years of updates cannot stack up processes.
    if [ $script_result -eq $PROVISIONED_EXIT_CODE ]; then
        log_line EXEC "launcher replaced, restarting"
        # Through sh rather than bare, so a lost exec bit cannot strand the device. exec
        # replaces this process, so nothing after it runs — including the exit trap.
        exec /bin/sh "@LAUNCHER_PATH@"
    fi

    # Provisioning gave up. Ask for the normal script straight away rather than sleeping
    # on the blank screen the clear above left behind.
    if [ $script_result -eq $PROVISION_DECLINED_EXIT_CODE ]; then
        log_line EXEC "provisioning declined, requesting normal render"
        SKIP_PROVISION=1
        continue
    fi

    if [ $script_result -ne 0 ]; then
        # The local log takes the whole message; only the wire is length-limited.
        log_line EXEC "script failed rc=$script_result $(cat script_err.log)"
        err=$(head -c 150 script_err.log | tr '\n\r\t' '   ' | sed 's/[^a-zA-Z0-9 ._:/-]//g')
        die "script_failed_${script_result}_$err"
    fi

    log_line API "rendered ok, sleeping ${SLEEP_TIME_S}s battery=${BATT_PERCENT}"

    sleep 3

    # set powersave mode
    echo powersave > /sys/devices/system/cpu/cpu0/cpufreq/scaling_governor

    # schedule next wakeup
    rtcwake -d /dev/rtc1 -m no -s $SLEEP_TIME_S

    # set sleep mode
    echo "mem" > /sys/power/state

    sleep 5
done

@SENTINEL@
