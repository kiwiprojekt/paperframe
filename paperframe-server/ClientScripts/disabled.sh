#!/bin/sh
# Name: DisableDevice
# Author: Paperframe Server
# Device is disabled on the Paperframe Server
#
# Exits with the agreed disable code so the launcher stops its loop cleanly
# instead of reporting a client error. The launcher's own cleanup releases the
# screensaver guard.

echo "Device @DEVICE_ID@ is disabled."
exit @DISABLED_EXIT_CODE@
