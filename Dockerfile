FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# This Dockerfile expects a pre-published app in ./publish
# Use `deploy.sh` which will run `dotnet publish` before building the image.
COPY publish/ .
COPY Scripts/ ./Scripts/

RUN apt-get update && \
    apt-get install -y --no-install-recommends openvpn iproute2 curl && \
    rm -rf /var/lib/apt/lists/*

RUN mkdir -p /app/uploads /app/logs
COPY entrypoint.sh /usr/local/bin/feenqr-entrypoint.sh
RUN chmod +x /usr/local/bin/feenqr-entrypoint.sh

ENTRYPOINT ["/usr/local/bin/feenqr-entrypoint.sh"]
