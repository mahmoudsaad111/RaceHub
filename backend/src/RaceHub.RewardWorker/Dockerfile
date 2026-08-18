# syntax=docker/dockerfile:1

#################################
# Build stage
#################################
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj files first (and the solution) so `dotnet restore` is cached
# as its own layer. This needs every project in RewardWorker's reference
# graph, not just RewardWorker.csproj itself — Infrastructure references
# Domain/Application/Contracts, so restore fails without all of them
# present, even though this Dockerfile never builds those other apps.
COPY RaceHub.slnx ./
COPY src/RaceHub.RewardWorker/RaceHub.RewardWorker.csproj src/RaceHub.RewardWorker/
COPY src/RaceHub.Application/RaceHub.Application.csproj src/RaceHub.Application/
COPY src/RaceHub.Domain/RaceHub.Domain.csproj src/RaceHub.Domain/
COPY src/RaceHub.Infrastructure/RaceHub.Infrastructure.csproj src/RaceHub.Infrastructure/
COPY src/RaceHub.Contracts/RaceHub.Contracts.csproj src/RaceHub.Contracts/

RUN dotnet restore src/RaceHub.RewardWorker/RaceHub.RewardWorker.csproj

# Now copy the rest of the source and publish.
COPY src/ src/

RUN dotnet publish src/RaceHub.RewardWorker/RaceHub.RewardWorker.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

#################################
# Runtime stage
#################################
# Uses the `aspnet` runtime image, not the leaner plain `runtime` one, even
# though this worker never serves HTTP itself — it references
# RaceHub.Infrastructure, which pulls in ASP.NET Core Identity/JwtBearer
# packages for the API's auth. That makes the published app declare a
# runtime dependency on the Microsoft.AspNetCore.App shared framework, which
# the plain `runtime` image doesn't ship ("No frameworks were found" at
# startup is exactly that missing-framework error).
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Run as the built-in non-root user shipped in the .NET runtime image.
USER app
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "RaceHub.RewardWorker.dll"]
