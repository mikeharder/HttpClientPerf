@echo off

docker build -t httpclientperfclient:2.1 -f %~dp0/Dockerfile %~dp0/../.. %*
