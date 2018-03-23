#!/bin/sh

docker run -it --rm --network host --name httpclientperfclient-2.0 httpclientperfclient:2.0 "$@"
