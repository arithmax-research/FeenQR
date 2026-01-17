# AWS Deployment Guide for FeenQR

This guide explains how to deploy and manage the FeenQR Quantitative Research Platform on AWS App Runner.

## Prerequisites

1. **AWS CLI** - Installed and configured
   ```bash
   aws configure
   # Enter your AWS Access Key ID, Secret Access Key, and region
   ```

2. **Docker** - Installed and running
   ```bash
   docker --version
   ```

3. **AWS Account** with appropriate permissions:
   - ECR (Elastic Container Registry) access
   - IAM role creation
   - App Runner service management

---

## Initial Deployment

### Step 1: Quick Deploy (Automated)

Run the deployment script:
```bash
./deploy-apprunner.sh
```

This script will automatically:
- ✅ Create ECR repository
- ✅ Build the Docker image from `Dockerfile.webapp`
- ✅ Push image to ECR
- ✅ Create IAM roles for App Runner
- ✅ Deploy to AWS App Runner
- ✅ Wait for deployment to complete

**Expected time:** 5-7 minutes

### Step 2: Get Your App URL

After successful deployment, the script will output your app URL:
```
https://xxxxx.us-east-1.awsapprunner.com
```

---

## Manual Deployment (Step-by-Step)

If you prefer manual control:

### 1. Set Environment Variables
```bash
export AWS_ACCOUNT_ID=$(aws sts get-caller-identity --query Account --output text)
export AWS_REGION=us-east-1
```

### 2. Create ECR Repository
```bash
aws ecr create-repository \
  --repository-name feenqr \
  --region $AWS_REGION \
  --image-scanning-configuration scanOnPush=true
```

### 3. Build and Push Docker Image
```bash
# Login to ECR
aws ecr get-login-password --region $AWS_REGION | \
  docker login --username AWS --password-stdin \
  $AWS_ACCOUNT_ID.dkr.ecr.$AWS_REGION.amazonaws.com

# Build image
docker build -f Dockerfile.webapp -t feenqr:webapp .

# Tag for ECR
docker tag feenqr:webapp \
  $AWS_ACCOUNT_ID.dkr.ecr.$AWS_REGION.amazonaws.com/feenqr:webapp

# Push to ECR
docker push $AWS_ACCOUNT_ID.dkr.ecr.$AWS_REGION.amazonaws.com/feenqr:webapp
```

### 4. Create IAM Role for App Runner
```bash
# Create trust policy
cat > /tmp/apprunner-trust-policy.json <<EOF
{
  "Version": "2012-10-17",
  "Statement": [{
    "Effect": "Allow",
    "Principal": {"Service": "build.apprunner.amazonaws.com"},
    "Action": "sts:AssumeRole"
  }]
}
EOF

# Create role
aws iam create-role \
  --role-name AppRunnerECRAccessRole \
  --assume-role-policy-document file:///tmp/apprunner-trust-policy.json

# Attach ECR access policy
aws iam attach-role-policy \
  --role-name AppRunnerECRAccessRole \
  --policy-arn arn:aws:iam::aws:policy/service-role/AWSAppRunnerServicePolicyForECRAccess
```

### 5. Create App Runner Service
```bash
aws apprunner create-service \
  --service-name feenqr-webapp \
  --region $AWS_REGION \
  --source-configuration '{
    "ImageRepository": {
      "ImageIdentifier": "'$AWS_ACCOUNT_ID'.dkr.ecr.'$AWS_REGION'.amazonaws.com/feenqr:webapp",
      "ImageConfiguration": {"Port": "8080"},
      "ImageRepositoryType": "ECR"
    },
    "AutoDeploymentsEnabled": true,
    "AuthenticationConfiguration": {
      "AccessRoleArn": "arn:aws:iam::'$AWS_ACCOUNT_ID':role/AppRunnerECRAccessRole"
    }
  }' \
  --instance-configuration "Cpu=1 vCPU,Memory=2 GB" \
  --health-check-configuration "Protocol=TCP,Interval=10,Timeout=5,HealthyThreshold=1,UnhealthyThreshold=5"
```

### 6. Monitor Deployment
```bash
aws apprunner describe-service \
  --service-arn $(aws apprunner list-services \
    --region $AWS_REGION \
    --query "ServiceSummaryList[?ServiceName=='feenqr-webapp'].ServiceArn" \
    --output text) \
  --region $AWS_REGION \
  --query 'Service.[Status,ServiceUrl]' \
  --output table
```

---

## Updating Your Deployment

### Quick Update
```bash
# Make your code changes, then run:
docker build -f Dockerfile.webapp -t feenqr:webapp .
docker tag feenqr:webapp $AWS_ACCOUNT_ID.dkr.ecr.$AWS_REGION.amazonaws.com/feenqr:webapp
docker push $AWS_ACCOUNT_ID.dkr.ecr.$AWS_REGION.amazonaws.com/feenqr:webapp
```

**Auto-deployment is enabled** - App Runner will automatically detect the new image and deploy it within 2-3 minutes.

### Manual Update Script
Create a quick update script:
```bash
#!/bin/bash
export AWS_ACCOUNT_ID=$(aws sts get-caller-identity --query Account --output text)
export AWS_REGION=us-east-1

echo "Building and pushing updated image..."
docker build -f Dockerfile.webapp -t feenqr:webapp .
docker tag feenqr:webapp $AWS_ACCOUNT_ID.dkr.ecr.$AWS_REGION.amazonaws.com/feenqr:webapp
docker push $AWS_ACCOUNT_ID.dkr.ecr.$AWS_REGION.amazonaws.com/feenqr:webapp

echo "✓ Image pushed. App Runner will auto-deploy in 2-3 minutes."
```

---

## Environment Variables Configuration

Your app needs API keys and configuration. Set these in AWS Console:

1. Go to **AWS Console** → **App Runner** → **feenqr-webapp**
2. Click **Configuration** → **Configure**
3. Scroll to **Environment variables**
4. Add your keys from `appsettings.json`:

```
OPENAI__APIKEY=your-openai-key
ALPACA__APIKEY=your-alpaca-key
ALPACA__APISECRET=your-alpaca-secret
BINANCE__APIKEY=your-binance-key
BINANCE__APISECRET=your-binance-secret
YOUTUBE__APIKEY=your-youtube-key
```

5. Click **Deploy** to restart with new variables

---

## Monitoring & Management

### View Logs
```bash
# List log groups
aws logs tail /aws/apprunner/feenqr-webapp/service \
  --follow \
  --region $AWS_REGION
```

### Check Service Status
```bash
aws apprunner list-services --region $AWS_REGION
```

### View Deployment Operations
```bash
aws apprunner list-operations \
  --service-arn <your-service-arn> \
  --region $AWS_REGION
```

### Scale Service
```bash
aws apprunner update-service \
  --service-arn <your-service-arn> \
  --instance-configuration "Cpu=2 vCPU,Memory=4 GB" \
  --region $AWS_REGION
```

---

## Custom Domain Setup

1. **AWS Console** → **App Runner** → **feenqr-webapp** → **Custom domains**
2. Click **Link domain**
3. Enter your domain (e.g., `app.yourdomain.com`)
4. Add the provided CNAME records to your DNS provider
5. Wait for validation (5-10 minutes)

---

## Costs

Estimated AWS App Runner costs:
- **Compute:** $0.064/hour for 1 vCPU, 2GB RAM = ~$46/month
- **Build:** $0.005/build minute (minimal if using pre-built images)
- **Free tier:** 2000 build minutes + 5 GB/month

**ECR Storage:** ~$0.10/GB/month (your image is ~500MB = $0.05/month)

---

## Troubleshooting

### Deployment Failed
```bash
# Check operations log
aws apprunner list-operations \
  --service-arn <service-arn> \
  --region $AWS_REGION

# View detailed logs
aws logs get-log-events \
  --log-group-name /aws/apprunner/feenqr-webapp/service \
  --log-stream-name <stream-name> \
  --region $AWS_REGION
```

### App Not Responding
1. Check service status: `aws apprunner describe-service`
2. Verify health check is passing (TCP port 8080)
3. Check logs for errors
4. Test image locally:
   ```bash
   docker run -p 8080:8080 feenqr:webapp
   curl http://localhost:8080
   ```

### Image Push Fails
```bash
# Re-authenticate
aws ecr get-login-password --region $AWS_REGION | \
  docker login --username AWS --password-stdin \
  $AWS_ACCOUNT_ID.dkr.ecr.$AWS_REGION.amazonaws.com
```

---

## Cleanup / Delete Deployment

### Delete App Runner Service
```bash
aws apprunner delete-service \
  --service-arn $(aws apprunner list-services \
    --region $AWS_REGION \
    --query "ServiceSummaryList[?ServiceName=='feenqr-webapp'].ServiceArn" \
    --output text) \
  --region $AWS_REGION
```

### Delete ECR Repository
```bash
aws ecr delete-repository \
  --repository-name feenqr \
  --force \
  --region $AWS_REGION
```

### Delete IAM Role
```bash
aws iam detach-role-policy \
  --role-name AppRunnerECRAccessRole \
  --policy-arn arn:aws:iam::aws:policy/service-role/AWSAppRunnerServicePolicyForECRAccess

aws iam delete-role --role-name AppRunnerECRAccessRole
```

---

## Files Reference

- **`Dockerfile.webapp`** - Multi-stage build for the Blazor WebApp
- **`deploy-apprunner.sh`** - Automated deployment script
- **`appsettings.json`** - Configuration file (copied into container)
- **`WebApp/Server/`** - ASP.NET Core web server
- **`WebApp/Client/`** - Blazor WebAssembly client

---

## Architecture

```
┌─────────────────────────────────────────┐
│         AWS App Runner                   │
│  ┌───────────────────────────────────┐  │
│  │  FeenQR Container (Port 8080)     │  │
│  │  - ASP.NET Core Server            │  │
│  │  - Blazor WebAssembly Client      │  │
│  │  - Semantic Kernel Integration    │  │
│  └───────────────────────────────────┘  │
│         ↓ Auto-scaling ↓                 │
└─────────────────────────────────────────┘
              ↓ HTTPS
┌─────────────────────────────────────────┐
│      Load Balancer (AWS Managed)        │
└─────────────────────────────────────────┘
              ↓
         🌐 Internet

Storage:
┌─────────────────┐
│   AWS ECR       │ ← Docker Images
└─────────────────┘
```

---

## Support

- **AWS App Runner Docs:** https://docs.aws.amazon.com/apprunner/
- **Deployment Issues:** Check `AWS_DEPLOYMENT.md` (this file)
- **App Issues:** Check main `README.md`

---

## Quick Reference Commands

```bash
# Deploy
./deploy-apprunner.sh

# Update
docker build -f Dockerfile.webapp -t feenqr:webapp . && \
docker tag feenqr:webapp $AWS_ACCOUNT_ID.dkr.ecr.$AWS_REGION.amazonaws.com/feenqr:webapp && \
docker push $AWS_ACCOUNT_ID.dkr.ecr.$AWS_REGION.amazonaws.com/feenqr:webapp

# Check status
aws apprunner describe-service --service-arn <arn> --region us-east-1

# View logs
aws logs tail /aws/apprunner/feenqr-webapp/service --follow --region us-east-1

# Delete
aws apprunner delete-service --service-arn <arn> --region us-east-1
```

---

**Last Updated:** January 17, 2026
**Deployment Target:** AWS App Runner (us-east-1)
