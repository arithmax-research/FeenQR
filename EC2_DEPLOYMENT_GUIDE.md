# EC2 Multi-App Deployment Guide

> Deploy multiple containerized apps on the same EC2 instance, each on its own subdomain.

## Prerequisites

- An EC2 instance (Ubuntu) already running with ports `22`, `80`, `443` open
- SSH key access (e.g., `~/.ssh/arithmax-base.pem`)
- AWS CLI configured (`~/.aws/credentials`)
- A domain whose DNS you can manage (e.g., `misango.me`)

---

## Quick Reference

| Step | Command |
|------|---------|
| EC2 IP | `aws ec2 describe-instances --query 'Reservations[].Instances[].[InstanceId,State.Name,PublicIpAddress,Tags[?Key==\`Name\`].Value\|[0]]' --output table` |
| SSH in | `ssh -i ~/.ssh/arithmax-base.pem ubuntu@<EC2_IP>` |
| Transfer app | `tar czf /tmp/app.tar.gz --exclude='.git' --exclude='obj' --exclude='bin' --exclude='logs' --exclude='node_modules' --exclude='__pycache__' --exclude='.DS_Store' -C /path/to/project .`<br>`scp -i ~/.ssh/arithmax-base.pem /tmp/app.tar.gz ubuntu@<EC2_IP>:/home/ubuntu/` |
| Extract & clean | `ssh -i ~/.ssh/arithmax-base.pem ubuntu@<EC2_IP> "cd /home/ubuntu && tar xzf app.tar.gz && find . -name '._*' -delete"` |
| Deploy | `ssh -i ~/.ssh/arithmax-base.pem ubuntu@<EC2_IP> "cd /home/ubuntu && docker compose up -d --build"` |

---

## Step 1: Set up DNS

Before deploying, create an **A record** for your app's subdomain pointing to your EC2 IP.

```bash
# Find your EC2 public IP
aws ec2 describe-instances \
  --query 'Reservations[].Instances[].[InstanceId,PublicIpAddress,Tags[?Key==`Name`].Value|[0]]' \
  --output table
```

Then, in your DNS provider (e.g., Cloudflare, Route53, Namecheap), add:

| Type | Name | Value |
|------|------|-------|
| A | `app3` | `<EC2_IP>` |

Your app will be accessible at `https://app3.misango.me` once deployed.

---

## Step 2: Prepare your app for Docker Compose

Your project needs:

### 2a. A `Caddyfile`

Create this at the project root. Caddy will act as a reverse proxy and auto-provision TLS.

```Caddyfile
app3.misango.me {
    encode gzip
    reverse_proxy app3-web:8080
}
```

> **Note:** The service name after `reverse_proxy` (e.g., `app3-web`) must match the service name in `docker-compose.yml`.

### 2b. A `Dockerfile`

A Dockerfile for your app. For a .NET Blazor app, it would look like:

```Dockerfile
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish WebApp/Server/Server.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app/publish .
COPY appsettings.json ./appsettings.json

ENTRYPOINT ["dotnet", "Server.dll"]
```

### 2c. A `docker-compose.yml`

This defines your app stack. Each app on the same EC2 needs **unique service names, container names, and volume names** to avoid conflicts.

```yaml
services:
  app3-web:                              # ← unique service name
    build:
      context: .
      dockerfile: Dockerfile
    image: app3-web:local                # ← unique image tag
    container_name: app3-web             # ← unique container name
    restart: unless-stopped
    expose:
      - "8080"
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ASPNETCORE_URLS: http://+:8080
    volumes:
      - app3_logs:/app/logs              # ← unique volume name
      - app3_uploads:/app/uploads

  caddy:
    image: caddy:2.8-alpine
    container_name: app3-caddy           # ← unique container name
    restart: unless-stopped
    depends_on:
      - app3-web
    ports:
      - "80:80"
      - "443:443"
    volumes:
      - ./Caddyfile:/etc/caddy/Caddyfile:ro
      - app3_caddy_data:/data            # ← unique volume name
      - app3_caddy_config:/config

volumes:
  app3_logs:                             # ← unique volume name
  app3_uploads:
  app3_caddy_data:
  app3_caddy_config:
```

> **⚠️ Critical:** If two apps on the same EC2 both try to bind to ports 80/443, only one will succeed (the other's Caddy will fail to start). The solution is **only one Caddy should manage public ports**. See [Appendix A: Single Caddy for all apps](#appendix-a-single-caddy-reverse-proxy-for-all-apps).

---

## Step 3: Deploy the app

### 3a. Create a tarball

```bash
cd /path/to/your/project

tar czf /tmp/app3-deploy.tar.gz \
  --exclude='.git' \
  --exclude='obj' \
  --exclude='bin' \
  --exclude='publish' \
  --exclude='logs' \
  --exclude='node_modules' \
  --exclude='__pycache__' \
  --exclude='.DS_Store' \
  --exclude='.qodo' \
  --exclude='.vscode' \
  --exclude='.github' \
  -C "$(pwd)" .
```

### 3b. Transfer to EC2

```bash
scp -i ~/.ssh/arithmax-base.pem /tmp/app3-deploy.tar.gz ubuntu@<EC2_IP>:/home/ubuntu/
```

### 3c. Extract and clean up Apple Double files

When tarring from macOS, hidden `._*` files (Apple Double metadata) get included. They cause build errors in .NET.

```bash
ssh -i ~/.ssh/arithmax-base.pem ubuntu@<EC2_IP> \
  "cd /home/ubuntu && tar xzf app3-deploy.tar.gz && find . -name '._*' -delete && echo 'Ready'"
```

### 3d. Create required directories

```bash
ssh -i ~/.ssh/arithmax-base.pem ubuntu@<EC2_IP> \
  "cd /home/ubuntu && mkdir -p logs uploads"
```

### 3e. Build and start

```bash
ssh -i ~/.ssh/arithmax-base.pem ubuntu@<EC2_IP> \
  "cd /home/ubuntu && docker compose up -d --build"
```

Wait a minute, then verify:

```bash
ssh -i ~/.ssh/arithmax-base.pem ubuntu@<EC2_IP> "docker compose ps"
```

---

## Step 4: Verify the deployment

```bash
# Check from the server itself
ssh -i ~/.ssh/arithmax-base.pem ubuntu@<EC2_IP> \
  "curl -s -o /dev/null -w '%{http_code}' http://localhost:80/"

# Check from your local machine
curl -s -o /dev/null -w 'HTTP: %{http_code}\n' http://app3.misango.me/
curl -s -o /dev/null -w 'HTTPS: %{http_code}\n' https://app3.misango.me/
```

If Caddy got the TLS certificate, you should see:
- `HTTP: 308` (redirecting to HTTPS)
- `HTTPS: 200` (app is serving)

Check Caddy logs for TLS provisioning:
```bash
ssh -i ~/.ssh/arithmax-base.pem ubuntu@<EC2_IP> \
  "docker compose logs caddy --tail 20"
```

---

## Step 5: Monitoring & troubleshooting

```bash
# Check container status
ssh ... "docker ps --format 'table {{.Names}}\t{{.Status}}\t{{.Ports}}'"

# View app logs
ssh ... "docker compose logs --tail 50"

# View a specific service's logs
ssh ... "docker compose logs app3-web --tail 50"

# Restart a service
ssh ... "docker compose restart app3-web"

# Rebuild and restart (if you changed code)
ssh ... "docker compose up -d --build"

# Stop and remove everything
ssh ... "docker compose down"
```

---

## Appendix A: Single Caddy Reverse Proxy for All Apps

If you're deploying multiple apps on the same EC2, **only one Caddy instance** should bind to ports 80/443 on the host. The simplest approach:

### Option 1: Each app has its own compose file (recommended for isolation)

Each app's `docker-compose.yml` omits the Caddy service. Instead, run a **shared Caddy** that proxies to all your app containers.

1. Create a shared Caddy deployment:

```yaml
# /home/ubuntu/shared-caddy/docker-compose.yml
services:
  caddy:
    image: caddy:2.8-alpine
    container_name: shared-caddy
    restart: unless-stopped
    ports:
      - "80:80"
      - "443:443"
    volumes:
      - ./Caddyfile:/etc/caddy/Caddyfile:ro
      - caddy_data:/data
      - caddy_config:/config

volumes:
  caddy_data:
  caddy_config:
```

2. Create a shared Caddyfile:

```Caddyfile
app3.misango.me {
    encode gzip
    reverse_proxy app3-web:8080
}

app4.misango.me {
    encode gzip
    reverse_proxy app4-web:8080
}
```

3. Each app's compose file deploys **only its web service** (no caddy, no ports on host):

```yaml
services:
  app3-web:
    build:
      context: .
      dockerfile: Dockerfile
    image: app3-web:local
    container_name: app3-web
    restart: unless-stopped
    expose:
      - "8080"
    networks:
      - shared_network   # ← all apps on the same network

networks:
  shared_network:
    external: true
```

4. Create the shared Docker network:
```bash
ssh -i ~/.ssh/arithmax-base.pem ubuntu@<EC2_IP> \
  "docker network create shared_network"
```

5. Deploy the shared Caddy first, then your apps.

### Option 2: Single compose file with all apps

```yaml
services:
  app3-web:
    build:
      context: ./app3
      dockerfile: Dockerfile
    image: app3-web:local
    container_name: app3-web
    restart: unless-stopped
    expose:
      - "8080"

  app4-web:
    build:
      context: ./app4
      dockerfile: Dockerfile
    image: app4-web:local
    container_name: app4-web
    restart: unless-stopped
    expose:
      - "8080"

  caddy:
    image: caddy:2.8-alpine
    container_name: shared-caddy
    restart: unless-stopped
    depends_on:
      - app3-web
      - app4-web
    ports:
      - "80:80"
      - "443:443"
    volumes:
      - ./Caddyfile:/etc/caddy/Caddyfile:ro
      - caddy_data:/data
      - caddy_config:/config

volumes:
  caddy_data:
  caddy_config:
```

---

## Appendix B: Common Errors

| Error | Cause | Fix |
|-------|-------|-----|
| `CSC : error CS2015: '._File.cs' is a binary file` | Apple Double files from macOS tar | `find . -name '._*' -delete` on EC2 |
| `port is already allocated` | Two Caddy instances trying to bind 80/443 | Use a single shared Caddy |
| `failed to obtain certificate` | DNS hasn't propagated or doesn't point to this IP | Verify `dig +short app3.misango.me` returns EC2 IP |
| `no such host` in container networking | Service names don't match | Ensure Caddyfile's `reverse_proxy` matches the compose service name |
| `Cannot start service: driver failed programming external connectivity` | Port conflict | `docker ps` to see what's already on that port |
