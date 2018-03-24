FROM microsoft/dotnet-nightly:2.1-sdk AS build
WORKDIR /app

# copy csproj and restore as distinct layers
COPY NuGet.config ./
COPY *.csproj ./

# must disable parallel to avoid timeouts on raspberry pi
RUN dotnet restore --disable-parallel

# copy everything else and build
COPY . ./
RUN dotnet publish --no-restore -f netcoreapp2.0 -c Release -o out

# build runtime image
FROM microsoft/dotnet:2.0-runtime
WORKDIR /app
COPY --from=build /app/out ./
ENTRYPOINT ["dotnet", "HttpClientPerf.Client.dll"]
