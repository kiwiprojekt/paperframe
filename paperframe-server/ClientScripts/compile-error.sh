#!/bin/sh
# @SERVICE@ COMPILE ERROR RUNTIME FALLBACK
#
# Exits 0 on purpose: the server already logged the failure and the device
# should show this diagnostic, then retry on its next scheduled wake.

FBINK="/mnt/us/libkh/bin/fbink"
$FBINK -q -k
$FBINK -q "@SERVICE@ COMPILE ERROR" -t size=20,top=200 -O -m -C GRAY9
$FBINK -q "Config ID: @CONFIG_ID@" -t size=12,top=260 -O -m -C GRAY6
$FBINK -q "Error: @ERROR@" -t size=10,top=320 -O -m -C GRAY3
