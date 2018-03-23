@echo off

docker build -t httpclientperfserver:2.0 -f %~dp0/Dockerfile %~dp0/../.. %*