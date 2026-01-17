#!/bin/bash

# FeenQR AWS App Runner Deployment Script
# This script automates the deployment of FeenQR to AWS App Runner

set -e  # Exit on any error

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo -e "${GREEN}=== FeenQR AWS App Runner Deployment ===${NC}\n"

# Check if AWS CLI is installed
if ! command -v aws &> /dev/null; then
    echo -e "${RED}Error: AWS CLI is not installed${NC}"
    echo "Install it with: curl 'https://awscli.amazonaws.com/awscli-exe-linux-x86_64.zip' -o 'awscliv2.zip' && unzip awscliv2.zip && sudo ./aws/install"
    exit 1
fi

# Check if Docker is installed
if ! command -v docker &> /dev/null; then
    echo -e "${RED}Error: Docker is not installed${NC}"
    exit 1
fi

# Get AWS Account ID
echo -e "${YELLOW}Getting AWS Account ID...${NC}"
AWS_ACCOUNT_ID=$(aws sts get-caller-identity --query Account --output text 2>/dev/null || echo "")

if [ -z "$AWS_ACCOUNT_ID" ]; then
    echo -e "${RED}Error: Unable to get AWS Account ID. Please run 'aws configure' first${NC}"
    exit 1
fi

echo -e "${GREEN}AWS Account ID: $AWS_ACCOUNT_ID${NC}"

# Configuration
AWS_REGION="${AWS_REGION:-us-east-1}"
ECR_REPOSITORY_NAME="feenqr"
APP_RUNNER_SERVICE_NAME="feenqr-webapp"
IMAGE_TAG="${IMAGE_TAG:-webapp}"
DOCKERFILE="Dockerfile.webapp"

echo -e "${YELLOW}Configuration:${NC}"
echo "  Region: $AWS_REGION"
echo "  ECR Repository: $ECR_REPOSITORY_NAME"
echo "  Service Name: $APP_RUNNER_SERVICE_NAME"
echo "  Image Tag: $IMAGE_TAG"
echo ""

# Step 1: Create ECR repository if it doesn't exist
echo -e "${YELLOW}Step 1: Creating ECR repository (if not exists)...${NC}"
aws ecr describe-repositories --repository-names $ECR_REPOSITORY_NAME --region $AWS_REGION &>/dev/null || \
    aws ecr create-repository \
        --repository-name $ECR_REPOSITORY_NAME \
        --region $AWS_REGION \
        --image-scanning-configuration scanOnPush=true \
        --encryption-configuration encryptionType=AES256

echo -e "${GREEN}✓ ECR repository ready${NC}\n"

# Step 2: Authenticate Docker to ECR
echo -e "${YELLOW}Step 2: Authenticating Docker to ECR...${NC}"
aws ecr get-login-password --region $AWS_REGION | \
    docker login --username AWS --password-stdin $AWS_ACCOUNT_ID.dkr.ecr.$AWS_REGION.amazonaws.com

echo -e "${GREEN}✓ Docker authenticated${NC}\n"

# Step 3: Build Docker image
echo -e "${YELLOW}Step 3: Building Docker image...${NC}"
docker build -f $DOCKERFILE -t $ECR_REPOSITORY_NAME:$IMAGE_TAG .

echo -e "${GREEN}✓ Docker image built${NC}\n"

# Step 4: Tag image for ECR
echo -e "${YELLOW}Step 4: Tagging image...${NC}"
ECR_IMAGE_URI="$AWS_ACCOUNT_ID.dkr.ecr.$AWS_REGION.amazonaws.com/$ECR_REPOSITORY_NAME:$IMAGE_TAG"
docker tag $ECR_REPOSITORY_NAME:$IMAGE_TAG $ECR_IMAGE_URI

echo -e "${GREEN}✓ Image tagged: $ECR_IMAGE_URI${NC}\n"

# Step 5: Push image to ECR
echo -e "${YELLOW}Step 5: Pushing image to ECR...${NC}"
docker push $ECR_IMAGE_URI

echo -e "${GREEN}✓ Image pushed to ECR${NC}\n"

# Step 6: Create IAM role for App Runner (if not exists)
echo -e "${YELLOW}Step 6: Setting up IAM roles for App Runner...${NC}"

# Create access role for ECR
ACCESS_ROLE_NAME="AppRunnerECRAccessRole"
ACCESS_ROLE_ARN=$(aws iam get-role --role-name $ACCESS_ROLE_NAME --query 'Role.Arn' --output text 2>/dev/null || echo "")

if [ -z "$ACCESS_ROLE_ARN" ]; then
    echo "Creating IAM access role..."
    
    # Create trust policy
    cat > /tmp/apprunner-trust-policy.json <<EOF
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Principal": {
        "Service": "build.apprunner.amazonaws.com"
      },
      "Action": "sts:AssumeRole"
    }
  ]
}
EOF

    # Create role
    aws iam create-role \
        --role-name $ACCESS_ROLE_NAME \
        --assume-role-policy-document file:///tmp/apprunner-trust-policy.json \
        --description "Allows App Runner to access ECR"

    # Attach policy
    aws iam attach-role-policy \
        --role-name $ACCESS_ROLE_NAME \
        --policy-arn arn:aws:iam::aws:policy/service-role/AWSAppRunnerServicePolicyForECRAccess

    # Wait for role to be available
    sleep 10
    
    ACCESS_ROLE_ARN=$(aws iam get-role --role-name $ACCESS_ROLE_NAME --query 'Role.Arn' --output text)
fi

echo -e "${GREEN}✓ IAM roles ready${NC}"
echo "  Access Role ARN: $ACCESS_ROLE_ARN"
echo ""

# Step 7: Check if App Runner service exists
echo -e "${YELLOW}Step 7: Deploying to App Runner...${NC}"

SERVICE_ARN=$(aws apprunner list-services --region $AWS_REGION --query "ServiceSummaryList[?ServiceName=='$APP_RUNNER_SERVICE_NAME'].ServiceArn" --output text 2>/dev/null || echo "")

if [ -z "$SERVICE_ARN" ]; then
    echo "Creating new App Runner service..."
    
    SERVICE_ARN=$(aws apprunner create-service \
        --service-name $APP_RUNNER_SERVICE_NAME \
        --region $AWS_REGION \
        --source-configuration "{
          \"ImageRepository\": {
            \"ImageIdentifier\": \"$ECR_IMAGE_URI\",
            \"ImageConfiguration\": {
              \"Port\": \"8080\",
              \"RuntimeEnvironmentVariables\": {
                \"ASPNETCORE_ENVIRONMENT\": \"Production\",
                \"ASPNETCORE_URLS\": \"http://+:8080\"
              }
            },
            \"ImageRepositoryType\": \"ECR\"
          },
          \"AutoDeploymentsEnabled\": true,
          \"AuthenticationConfiguration\": {
            \"AccessRoleArn\": \"$ACCESS_ROLE_ARN\"
          }
        }" \
        --instance-configuration "Cpu=1 vCPU,Memory=2 GB" \
        --health-check-configuration "Protocol=TCP,Interval=10,Timeout=5,HealthyThreshold=1,UnhealthyThreshold=5" \
        --query 'Service.ServiceArn' \
        --output text)
    
    echo -e "${GREEN}✓ App Runner service created${NC}"
else
    echo "Updating existing App Runner service..."
    
    aws apprunner update-service \
        --service-arn $SERVICE_ARN \
        --source-configuration "ImageRepository={ImageIdentifier=$ECR_IMAGE_URI,ImageRepositoryType=ECR,ImageConfiguration={Port=8080}}" \
        --region $AWS_REGION \
        --output text > /dev/null
    
    echo -e "${GREEN}✓ App Runner service updated${NC}"
fi

echo ""
echo -e "${YELLOW}Waiting for service to become available...${NC}"
echo "This may take 3-5 minutes..."

# Wait for service to be running
MAX_ATTEMPTS=60
ATTEMPT=0
while [ $ATTEMPT -lt $MAX_ATTEMPTS ]; do
    STATUS=$(aws apprunner describe-service \
        --service-arn $SERVICE_ARN \
        --region $AWS_REGION \
        --query 'Service.Status' \
        --output text)
    
    if [ "$STATUS" == "RUNNING" ]; then
        break
    fi
    
    echo -n "."
    sleep 10
    ATTEMPT=$((ATTEMPT + 1))
done

echo ""

if [ "$STATUS" == "RUNNING" ]; then
    # Get service URL
    SERVICE_URL=$(aws apprunner describe-service \
        --service-arn $SERVICE_ARN \
        --region $AWS_REGION \
        --query 'Service.ServiceUrl' \
        --output text)
    
    echo ""
    echo -e "${GREEN}========================================${NC}"
    echo -e "${GREEN}   Deployment Successful! 🚀${NC}"
    echo -e "${GREEN}========================================${NC}"
    echo ""
    echo -e "${GREEN}Your application is now live at:${NC}"
    echo -e "${GREEN}https://$SERVICE_URL${NC}"
    echo ""
    echo -e "${YELLOW}Service Details:${NC}"
    echo "  Service Name: $APP_RUNNER_SERVICE_NAME"
    echo "  Service ARN: $SERVICE_ARN"
    echo "  Region: $AWS_REGION"
    echo "  ECR Image: $ECR_IMAGE_URI"
    echo ""
    echo -e "${YELLOW}Next Steps:${NC}"
    echo "  1. Visit your application: https://$SERVICE_URL"
    echo "  2. Configure environment variables in AWS Console if needed"
    echo "  3. Set up custom domain (optional)"
    echo "  4. Monitor logs: aws apprunner list-operations --service-arn $SERVICE_ARN --region $AWS_REGION"
    echo ""
    echo -e "${YELLOW}To view logs:${NC}"
    echo "  aws logs tail /aws/apprunner/$APP_RUNNER_SERVICE_NAME/service --follow --region $AWS_REGION"
    echo ""
else
    echo -e "${RED}Service deployment timed out or failed. Current status: $STATUS${NC}"
    echo "Check the AWS Console for more details: https://console.aws.amazon.com/apprunner/home?region=$AWS_REGION"
    exit 1
fi
