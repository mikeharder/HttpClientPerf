FROM mikeharder/dotnet-nightly:2.0.0-sdk-msbuild-alpha

WORKDIR /app

# copy NuGet.config, project.json, and restore as distinct layers
COPY NuGet.config .
COPY *.csproj .
RUN dotnet restore

# copy and build everything else
COPY . .
RUN dotnet publish -c Release -o out
