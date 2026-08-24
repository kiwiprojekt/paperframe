#!/bin/sh
# Launcher update, downloaded and run by the Paperframe launcher like any other
# rendering script. Replaces the launcher on disk and restarts the loop with it.
#
# Every check happens before anything irreversible, and everything past that point can be
# rolled back, because the failure this guards against is a frame that never comes back
# without a USB cable.
#
# Only launchers that can restart themselves are ever sent here. Taking an older one over
# from the outside was tried and removed: it reads the decline code as a script failure and
# stops, so any recoverable hiccup mid-update left the frame dark.

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
mkdir -p "@STATE_DIR@" 2>/dev/null

attempts=0
if [ "$(head -n 1 "$ATTEMPTS_FILE" 2>/dev/null)" = "@TARGET_VERSION@" ]; then
    recorded=$(tail -n 1 "$ATTEMPTS_FILE" 2>/dev/null)

    # A count is only a count if it is digits. A file cut short by a full disk leaves the
    # version on the last line instead, and feeding that to $(( )) is a fatal shell error,
    # which this launcher would read as a failed render.
    case "$recorded" in
        ''|*[!0-9]*) recorded=0 ;;
    esac

    attempts=$recorded
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

# Everything past this line has already changed the launcher on disk, so every way out of
# it goes through one rollback rather than each failure remembering to undo its own share.
roll_back() {
    mv -f "$BACKUP" "$LAUNCHER" 2>/dev/null
    chmod +x "$LAUNCHER" 2>/dev/null
    sync
    give_up "$1, rolled back"
}

mv -f "$STAGED" "$LAUNCHER" || give_up "could not replace the launcher"
chmod +x "$LAUNCHER" || roll_back "could not make the new launcher executable"

# /mnt/us is a fuse overlay over vfat: a file written and reopened without a flush can
# come back stale or short. exec is a fresh open, so verify what is actually on disk now.
sync
sh -n "$LAUNCHER" 2>/dev/null || roll_back "launcher unreadable after write"

log_line EXEC "launcher written, handing over to @TARGET_VERSION@"

# The attempt counter is not cleared here. The new launcher clears it as it starts, which
# is the only evidence that actually means the update worked — clearing it on the way out
# would let a handover that fails every time reset its own budget and retry for ever.
#
# The launcher execs the new file itself when it sees this code: no handover, no second
# process, no guard juggling. Only launchers that understand it are ever sent here.
exit @PROVISIONED_EXIT_CODE@
