# Append-only diagnostic log kept on the device, so a failure that cannot reach the
# network still leaves a record. It is read back over USB, and delivered to the server
# on the next check-in that gets through.
#
# Every write is best-effort. A full, missing, or read-only filesystem must never be
# the thing that kills the loop, so nothing in here is allowed to fail loudly.
LOG_DIR="@STATE_DIR@"
LOG_FILE="$LOG_DIR/paperframe.log"
LOG_OUTBOX="$LOG_DIR/outbox"
LOG_MAX_BYTES=65536
LOG_KEEP_BYTES=32768
# What one check-in can carry in a request header. The outbox is trimmed to this as it
# is written, so delivery is always all-or-nothing and never has to track an offset.
LOG_OUTBOX_MAX_BYTES=3072

# Trims a file to its last N bytes, in place. Rotating into a second file would double
# what has to be read back over USB for no gain: the interesting part of a log is the
# tail either way.
log_trim() {
    # The redirection is what fails on a missing file, and the shell reports that itself
    # before wc's own stderr redirect can apply — so the whole command has to be quiet.
    log_size=$(( $({ wc -c < "$1"; } 2>/dev/null || echo 0) ))
    [ "$log_size" -gt "$2" ] || return 0

    # Byte-trimming lands mid-line, so the now-partial first line is dropped. Losing one
    # whole line to the trim is better than shipping a fragment of one, which is what the
    # server would otherwise store and what a reader would have to puzzle over.
    { tail -c "$3" "$1" | tail -n +2; } > "$1.tmp" 2>/dev/null && mv -f "$1.tmp" "$1" 2>/dev/null
    return 0
}

# log_line CATEGORY MESSAGE, where CATEGORY is one of NET, API, EXEC, FS, INFO, ERROR.
log_line() {
    # Created here rather than once at startup: a single mkdir that happened to fail would
    # silently disable logging for the whole run, including the exit reason this exists to
    # capture. Idempotent, and nothing at this call rate notices the cost.
    mkdir -p "$LOG_DIR" 2>/dev/null

    # Messages carry whatever a failing renderer wrote to stderr, and every line here can
    # end up inside a request header. Control bytes would have the server or wget reject
    # the whole delivery, and a stray separator would split one line into two, so the text
    # is flattened once here rather than at each of the places that pass it on.
    log_message=$(printf '%s' "$2" | tr '\n\r\t~' '   -' | tr -cd '[:print:]')
    log_entry="$(date '+%Y-%m-%dT%H:%M:%S') $1 $log_message"

    log_trim "$LOG_FILE" "$LOG_MAX_BYTES" "$LOG_KEEP_BYTES"
    echo "$log_entry" >> "$LOG_FILE" 2>/dev/null

    log_trim "$LOG_OUTBOX" "$LOG_OUTBOX_MAX_BYTES" "$LOG_OUTBOX_MAX_BYTES"
    echo "$log_entry" >> "$LOG_OUTBOX" 2>/dev/null

    # Never report a logging failure to the caller.
    return 0
}

# Everything not yet delivered, flattened onto one line for a request header. Empty when
# there is nothing to say.
log_pending() {
    [ -s "$LOG_OUTBOX" ] || return 0
    tr '\n' '~' < "$LOG_OUTBOX" 2>/dev/null | tr -d '\r'
}

# Called once the server has the pending lines. They stay in the full log; only the
# outbox is cleared, so a delivery never costs the device its own history.
log_delivered() {
    : > "$LOG_OUTBOX" 2>/dev/null
    return 0
}
