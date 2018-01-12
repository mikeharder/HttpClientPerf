#!/bin/sh

docker run -it --rm --network host --name httpclientperf-client-2.0.5 httpclientperf-client-2.0.5 $*
