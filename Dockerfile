# Pins the real .NET 8 SDK and runtime, independent of whichever SDK is installed locally.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /source

# Restore against the project files alone so this layer is cached until dependencies change.
COPY VendorRisk.sln ./
COPY src/VendorRisk.Domain/VendorRisk.Domain.csproj src/VendorRisk.Domain/
COPY src/VendorRisk.Application/VendorRisk.Application.csproj src/VendorRisk.Application/
COPY src/VendorRisk.Infrastructure/VendorRisk.Infrastructure.csproj src/VendorRisk.Infrastructure/
COPY src/VendorRisk.Api/VendorRisk.Api.csproj src/VendorRisk.Api/
COPY tests/VendorRisk.UnitTests/VendorRisk.UnitTests.csproj tests/VendorRisk.UnitTests/
RUN dotnet restore

COPY . .
RUN dotnet publish src/VendorRisk.Api/VendorRisk.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

COPY --from=build /app/publish ./
# The seeder reads this at startup; see Database:SeedDatasetPath.
COPY --from=build /source/data ./data

ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_RUNNING_IN_CONTAINER=true

EXPOSE 8080

# Runs as the non-root "app" user shipped with the aspnet image.
USER app

ENTRYPOINT ["dotnet", "VendorRisk.Api.dll"]
