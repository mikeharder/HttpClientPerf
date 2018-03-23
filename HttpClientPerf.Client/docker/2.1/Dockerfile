FROM microsoft/dotnet-nightly:2.1-sdk AS build
WORKDIR /app

# copy csproj and restore as distinct layers
COPY NuGet.config ./
COPY *.csproj ./
RUN dotnet restore

# copy everything else and build
COPY . ./
RUN dotnet publish -f netcoreapp2.1 -c Release -o out

# build runtime image
FROM microsoft/dotnet-nightly:2.1-runtime
WORKDIR /app
COPY --from=build /app/out ./

# ensure managed handler is used regardless of default in runtime
ENV COMPlus_UseManagedHttpClientHandler true
ENV DOTNET_SYSTEM_NET_HTTP_USEMANAGEDHTTPCLIENTHANDLER true
ENV DOTNET_SYSTEM_NET_HTTP_USESOCKETSHTTPHANDLER true

ENTRYPOINT ["dotnet", "HttpClientPerf.Client.dll"]
