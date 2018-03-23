@echo off

docker build -t httpclientperfclient:2.0 -f %~dp0/Dockerfile %~dp0/../.. %*