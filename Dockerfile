FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["QuantResearchAgent.csproj", "./"]
RUN dotnet restore "QuantResearchAgent.csproj"
COPY . .
RUN dotnet build "QuantResearchAgent.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "QuantResearchAgent.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
COPY appsettings.json .

# Create a simple startup script that keeps the container running
RUN echo '#!/bin/bash\nwhile true; do sleep 3600; done' > /app/keep-alive.sh && chmod +x /app/keep-alive.sh

# Use the keep-alive script to keep container running for health checks
CMD ["/app/keep-alive.sh"]
