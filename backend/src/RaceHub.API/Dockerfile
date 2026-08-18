# syntax=docker/dockerfile:1

#################################
# Build stage
#################################
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj files first (and the solution) so `dotnet restore` is cached
# as its own layer — it only re-runs when a .csproj actually changes, not
# on every source edit.
COPY RaceHub.slnx ./
COPY src/RaceHub.API/RaceHub.API.csproj src/RaceHub.API/
COPY src/RaceHub.Application/RaceHub.Application.csproj src/RaceHub.Application/
COPY src/RaceHub.Domain/RaceHub.Domain.csproj src/RaceHub.Domain/
COPY src/RaceHub.Infrastructure/RaceHub.Infrastructure.csproj src/RaceHub.Infrastructure/
COPY src/RaceHub.Contracts/RaceHub.Contracts.csproj src/RaceHub.Contracts/

RUN dotnet restore src/RaceHub.API/RaceHub.API.csproj

# Now copy the rest of the source and publish.
COPY src/ src/

RUN dotnet publish src/RaceHub.API/RaceHub.API.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

#################################
# Runtime stage
#################################
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# curl is used by the docker-compose healthcheck; the minimal aspnet image
# doesn't ship it by default.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# Run as a non-root user inside the container.
USER app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "RaceHub.API.dll"]
