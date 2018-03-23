#!/bin/sh

docker run -it --rm --network host --name httpclientperfclient-2.1 httpclientperfclient:2.1 "$@"
