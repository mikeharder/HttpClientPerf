@echo off

docker build -t httpclientperfserver:2.1 -f %~dp0/Dockerfile %~dp0/../.. %*