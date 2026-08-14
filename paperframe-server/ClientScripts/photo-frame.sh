#!/bin/sh
# @SERVICE@ photo frame renderer, run by the Paperframe launcher.
#
# Exits non-zero on a failed download so the launcher reports it. Using "return"
# here would be an error in ash/dash and the stale image would be rendered anyway.

FBINK="@FBINK_PATH@"
IMAGE_URL=$SERVICES_URL"@IMAGE_PATH@"

$FBINK -q -k

wget --header="device_id: $DEVICE_ID" \
    --header="screen_res: $SCREEN_RES" \
    --header="script_version: $SCRIPT_VERSION" \
    -O image.jpeg "$IMAGE_URL"
image_result=$?

if [ $image_result -ne 0 ]; then
    exit 1
fi

$FBINK --image file=image.jpeg,dither
