#!/bin/bash

# Quick Update Script for FeenQR AWS Deployment
# This script rebuilds and pushes your updated app to AWS

set -e

# Colors
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

echo -e "${GREEN}=== FeenQR Quick Update ===${NC}\n"

# Get AWS credentials
export AWS_ACCOUNT_ID=$(aws sts get-caller-identity --query Account --output text 2>/dev/null)
export AWS_REGION="${AWS_REGION:-us-east-1}"

if [ -z "$AWS_ACCOUNT_ID" ]; then
    echo "Error: Unable to get AWS Account ID. Run 'aws configure' first."
    exit 1
fi

echo -e "${YELLOW}Building updated Docker image...${NC}"
docker build -f Dockerfile.webapp -t feenqr:webapp .

echo -e "\n${YELLOW}Tagging for ECR...${NC}"
docker tag feenqr:webapp $AWS_ACCOUNT_ID.dkr.ecr.$AWS_REGION.amazonaws.com/feenqr:webapp

echo -e "\n${YELLOW}Authenticating with ECR...${NC}"
aws ecr get-login-password --region $AWS_REGION | \
    docker login --username AWS --password-stdin \
    $AWS_ACCOUNT_ID.dkr.ecr.$AWS_REGION.amazonaws.com

echo -e "\n${YELLOW}Pushing to ECR...${NC}"
docker push $AWS_ACCOUNT_ID.dkr.ecr.$AWS_REGION.amazonaws.com/feenqr:webapp

echo -e "\n${GREEN}========================================${NC}"
echo -e "${GREEN}✓ Update Successful!${NC}"
echo -e "${GREEN}========================================${NC}"
echo ""
echo "App Runner will automatically deploy the new version in 2-3 minutes."
echo ""
echo "Monitor deployment:"
echo "  aws apprunner list-services --region $AWS_REGION"
echo ""
echo "View logs:"
echo "  aws logs tail /aws/apprunner/feenqr-webapp/service --follow --region $AWS_REGION"
echo ""
