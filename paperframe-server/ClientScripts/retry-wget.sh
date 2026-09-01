# Retries a wget call after a short delay. Even with the launcher waiting for the radio
# first, an association can drop between the check and the request, and the server may
# still be waking up; a couple of retries absorb that instead of reporting a failure the
# next attempt would not have hit.
RETRY_ATTEMPTS=3
RETRY_DELAY_S=3

retry_wget() {
    attempt=1
    while true; do
        wget "$@"
        result=$?
        if [ $result -eq 0 ]; then
            [ $attempt -gt 1 ] && log_line NET "wget succeeded on attempt $attempt"
            return 0
        fi
        log_line NET "wget attempt $attempt of $RETRY_ATTEMPTS failed rc=$result"
        [ $attempt -ge $RETRY_ATTEMPTS ] && return $result
        attempt=$((attempt + 1))
        sleep $RETRY_DELAY_S
    done
}
