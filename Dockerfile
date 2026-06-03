FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# This Dockerfile expects a pre-published app in ./publish
# Use `deploy.sh` which will run `dotnet publish` before building the image.
COPY publish/ .
COPY Scripts/ ./Scripts/

RUN mkdir -p /app/uploads /app/logs

ENTRYPOINT ["dotnet", "Server.dll"]
