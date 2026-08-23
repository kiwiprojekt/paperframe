#!/bin/sh
# Launcher update, downloaded and run by the Paperframe launcher like any other
# rendering script. Replaces the launcher on disk and restarts the loop with it.
#
# The order below is start-verify-then-kill on purpose: if the new launcher dies on
# startup, the old one is still running and still working. Every check happens before
# anything irreversible, because the failure this guards against is a frame that never
# comes back without a USB cable.

LAUNCHER="@LAUNCHER_PATH@"
STAGED="$LAUNCHER.new"
BACKUP="$LAUNCHER.bak"
ATTEMPTS_FILE="@STATE_DIR@/provision-attempts"
MAX_ATTEMPTS=3
SENTINEL="@SENTINEL@"

@LOG_FN@

@RETRY_WGET_FN@

# Stop trying, and tell the launcher to render normally instead. Exiting 0 would leave
# the screen on the blank the launcher cleared before running this, and the next wake
# would land right back here — a device that updates itself into a permanently empty
# frame. A stale client with a working frame beats a current client with a dead one.
give_up() {
    log_line EXEC "provision aborted: $1"
    rm -f "$STAGED"
    exit @DECLINED_EXIT_CODE@
}

# A device that cannot complete an update must not spend the rest of its life trying.
#
# The budget is recorded against the version being installed. Without that, a device that
# exhausted its attempts on one bad launcher would refuse the fixed one that follows, and
# the only reset would be a USB cable — the exact cost this whole design exists to avoid.
attempts=0
if [ "$(head -n 1 "$ATTEMPTS_FILE" 2>/dev/null)" = "@TARGET_VERSION@" ]; then
    attempts=$(($(tail -n 1 "$ATTEMPTS_FILE" 2>/dev/null || echo 0)))
fi

attempts=$((attempts + 1))
printf '%s\n%s\n' "@TARGET_VERSION@" "$attempts" > "$ATTEMPTS_FILE" 2>/dev/null
[ "$attempts" -gt "$MAX_ATTEMPTS" ] && give_up "@TARGET_VERSION@ failed $MAX_ATTEMPTS times"

# "sh -n" is the only check that parses the whole file without running it, but it is not
# guaranteed to exist on every busybox build. Calibrate it against the launcher already
# known to be good: if it rejects that, the option is unusable and an unverified script
# is not worth installing.
sh -n "$LAUNCHER" 2>/dev/null || give_up "sh -n unavailable, cannot verify a new launcher"

retry_wget --header="device_id: $DEVICE_ID" \
    --header="screen_res: $SCREEN_RES" \
    --header="script_version: $SCRIPT_VERSION" \
    -O "$STAGED" "$SERVICES_URL@LAUNCHER_PATH_URL@"
[ $? -eq 0 ] || give_up "download failed"

[ -s "$STAGED" ] || give_up "downloaded launcher is empty"
tail -n 1 "$STAGED" | grep -q "^$SENTINEL$" || give_up "downloaded launcher is truncated"
sh -n "$STAGED" 2>/dev/null || give_up "downloaded launcher failed syntax check"

# Everything below can roll back, but only while this copy exists. On a full filesystem
# it does not, and installing anyway would leave an unverified launcher and no way home.
cp -pf "$LAUNCHER" "$BACKUP" 2>/dev/null
[ -s "$BACKUP" ] || give_up "could not back up the current launcher"

mv -f "$STAGED" "$LAUNCHER" || give_up "could not replace the launcher"
chmod +x "$LAUNCHER" || give_up "could not make the new launcher executable"

# /mnt/us is a fuse overlay over vfat: a file written and reopened without a flush can
# come back stale or short. exec is a fresh open, so verify what is actually on disk now.
sync
sh -n "$LAUNCHER" 2>/dev/null || {
    mv -f "$BACKUP" "$LAUNCHER" 2>/dev/null
    sync
    give_up "launcher unreadable after write, rolled back"
}

log_line EXEC "launcher written, handing over to @TARGET_VERSION@"

# The attempt counter is not cleared here. The new launcher clears it as it starts, which
# is the only evidence that actually means the update worked — clearing it on the way out
# would let a handover that fails every time reset its own budget and retry for ever.
#
# How the loop gets restarted onto the new launcher depends on which launcher is running
# this script.
#
# Launchers older than the exit-code contract have no idea what code @PROVISIONED_EXIT_CODE@ means and
# would report it as a script failure and stop. They have to be taken over from the
# outside — started, checked, and only then killed, so a launcher that cannot start leaves
# the working one untouched.
#
# Which of the two applies is decided by the server, which already knows the version that
# checked in and where the cutoff is; comparing versions in shell would be a second,
# looser copy of that rule. This branch goes away once no device predates the contract.
if [ -n "@LEGACY_HANDOVER@" ]; then
    nohup "$LAUNCHER" > /dev/null 2>&1 &
    new_pid=$!
    sleep 2

    if ! kill -0 "$new_pid" 2>/dev/null; then
        mv -f "$BACKUP" "$LAUNCHER" 2>/dev/null
        sync
        give_up "new launcher did not stay up, rolled back"
    fi

    # The parent's exit trap hands the display back to powerd, so the new launcher has to
    # reclaim it once the old one is gone.
    kill "$PPID" 2>/dev/null
    sleep 1
    lipc-set-prop com.lab126.powerd preventScreenSaver 1

    exit 0
fi

# Everything since: the launcher execs the new file itself when it sees this code, which
# needs no handover, no second process, and no guard juggling.
exit @PROVISIONED_EXIT_CODE@
