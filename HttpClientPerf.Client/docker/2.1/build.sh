#!/usr/bin/env bash

docker build -t httpclientperfclient:2.1 -f `dirname $0`/Dockerfile `dirname $0`/../.. "$@"