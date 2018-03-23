@echo off

docker run -it --rm -p 8080:8080 -p 8081:8081 --name httpclientperfserver-2.0 httpclientperfserver:2.0 %*