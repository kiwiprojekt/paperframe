# Retries a wget call after a short delay. Kindle wifi needs a moment to reassociate
# after waking from sleep; check-in logs show connectivity clearing within a couple of
# seconds, so a short retry absorbs that instead of reporting a spurious failure.
RETRY_ATTEMPTS=3
RETRY_DELAY_S=3

retry_wget() {
    attempt=1
    while true; do
        wget "$@"
        result=$?
        [ $result -eq 0 ] && return 0
        [ $attempt -ge $RETRY_ATTEMPTS ] && return $result
        attempt=$((attempt + 1))
        sleep $RETRY_DELAY_S
    done
}