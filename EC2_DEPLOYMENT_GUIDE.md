# FeenQR EC2 Deployment Guide

> Deploy FeenQR as an independent app behind the permanent Embeddify Caddy reverse proxy.

## Prerequisites

- An EC2 instance (Ubuntu) already running with ports `22`, `80`, `443` open
- SSH key access (e.g., `~/.ssh/arithmax-base.pem`)
- AWS CLI configured (`~/.aws/credentials`)
- A domain whose DNS you can manage (e.g., `misango.me`)

---

## Quick Reference
## Shared reverse proxy

Embeddify owns the single Caddy container and public ports 80 and 443. FeenQR only runs `feenqr-web` and `qdrant`, attached to the `embeddify_proxy` Docker network. The Embeddify Caddyfile contains the `feenqr.misango.me` route and manages TLS for both applications.

Create the shared network once on EC2:

```bash
docker network create embeddify_proxy
```

After changing the central Caddyfile, rebuild and restart only Caddy. Its named data and config volumes must remain intact:
The standalone `caddy-central` project owns the single Caddy container and public ports 80 and 443. FeenQR only runs `feenqr-web` and `qdrant`, attached to the `caddy-central_proxy` Docker network. The central Caddyfile contains the `feenqr.misango.me` route and manages TLS for all applications.
```bash
cd ~/codechest/Embeddify
docker compose build caddy
docker compose up -d caddy
docker network create caddy-central_proxy

Do not start a FeenQR Caddy container and do not run `docker compose down -v`.
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

## Standalone operation

FeenQR owns its Caddy container, Caddyfile, and persistent certificate volumes. Run the normal deployment script on a dedicated EC2 host or on a host/public IP where ports 80 and 443 are available.

## Running beside another standalone app

Two standalone Caddys can share one EC2 only when each is assigned a different private IP on the EC2 network interface, with a separate Elastic IP mapped to each private IP. Configure the private address in each project's `.env` file:

```dotenv
HOST_BIND_IP=10.0.1.10
```

For example, assign `10.0.1.10` to FeenQR and `10.0.1.11` to the other app. Point each domain's DNS `A` record to its corresponding Elastic IP. Both Caddy containers can then use ports 80 and 443 because they bind to different host IP addresses.

Without separate IPs, Docker cannot bind two containers to ports 80 and 443 simultaneously. In that case, use separate EC2 instances or one shared public reverse proxy.

---

## Appendix B: Common Errors

| Error | Cause | Fix |
|-------|-------|-----|
| `CSC : error CS2015: '._File.cs' is a binary file` | Apple Double files from macOS tar | `find . -name '._*' -delete` on EC2 |
| `port is already allocated` | Two Caddy instances trying to bind 80/443 | Use a single shared Caddy |
| `failed to obtain certificate` | DNS hasn't propagated or doesn't point to this IP | Verify `dig +short app3.misango.me` returns EC2 IP |
| `no such host` in container networking | Service names don't match | Ensure Caddyfile's `reverse_proxy` matches the compose service name |
| `Cannot start service: driver failed programming external connectivity` | Port conflict | `docker ps` to see what's already on that port |
