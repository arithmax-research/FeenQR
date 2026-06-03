#!/bin/bash
set -euo pipefail

cd "$(dirname "$0")"

mkdir -p logs WebApp/Server/uploads
echo "Publishing WebApp/Server..."
dotnet publish WebApp/Server/Server.csproj -c Release -o ./publish /p:UseAppHost=false

echo "Building Docker image from published output..."
docker build -f Dockerfile -t feenqr-web:local .

echo "Bringing up compose stack..."
docker compose up -d