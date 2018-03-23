#!/bin/sh

docker run -it --rm --network host --name httpclientperfserver-2.0 httpclientperfserver:2.0 "$@"
