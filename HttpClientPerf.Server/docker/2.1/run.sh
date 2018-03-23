#!/bin/sh

docker run -it --rm --network host --name httpclientperfserver-2.1 httpclientperfserver:2.1 "$@"
