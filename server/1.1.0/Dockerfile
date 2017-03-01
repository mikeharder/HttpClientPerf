FROM microsoft/dotnet:1.1.0-sdk-msbuild-rc4

EXPOSE 8080
EXPOSE 8081

WORKDIR /dotnetapp

# copy csproj and restore as distinct layers
COPY *.csproj .
RUN dotnet restore

# copy and build everything else
COPY . .
RUN dotnet publish -c Release -o out

ENTRYPOINT ["dotnet", "out/server.dll"]
