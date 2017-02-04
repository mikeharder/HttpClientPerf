FROM microsoft/dotnet:1-sdk-msbuild

EXPOSE 8080
EXPOSE 8081

WORKDIR /app

# copy csproj and restore as distinct layers
COPY *.csproj .
RUN dotnet restore

# copy and build everything else
COPY . .
RUN dotnet publish -c Release -o out

ENTRYPOINT ["dotnet", "out/server.dll"]
