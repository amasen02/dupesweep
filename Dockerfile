# syntax=docker/dockerfile:1

# ---- Build ------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY DupeSweep.slnx ./
COPY src/DupeSweep/DupeSweep.csproj src/DupeSweep/
COPY tests/DupeSweep.Tests/DupeSweep.Tests.csproj tests/DupeSweep.Tests/
RUN dotnet restore DupeSweep.slnx

COPY src/ src/
COPY tests/ tests/
RUN dotnet test DupeSweep.slnx --configuration Release --no-restore
RUN dotnet publish src/DupeSweep/DupeSweep.csproj \
    --configuration Release \
    --no-restore \
    --output /app \
    -p:UseAppHost=false

# ---- Runtime ------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app

COPY --from=build /app ./
USER app

# Mount the directory you want to scan at /scan, e.g.:
#   docker run --rm -v "$PWD":/scan dupesweep /scan
WORKDIR /scan
ENTRYPOINT ["dotnet", "/app/dsweep.dll"]
CMD ["--help"]
