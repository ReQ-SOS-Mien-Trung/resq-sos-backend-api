#!/bin/bash

# =============================================================
# Script để build và push Docker image lên Docker Hub
# Chạy: ./scripts/build-and-push.sh your-username latest
# =============================================================

set -e

DOCKER_USERNAME=${1:?"Usage: $0 <docker-username> [tag]"}
TAG=${2:-"latest"}

IMAGE_NAME="$DOCKER_USERNAME/resq-backend"
FULL_IMAGE_NAME="${IMAGE_NAME}:${TAG}"

echo "========================================"
echo "Building RESQ Backend Docker Image"
echo "Image: $FULL_IMAGE_NAME"
echo "========================================"

# Navigate to project root
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
cd "$PROJECT_ROOT"

echo ""
echo "[1/3] Building Docker image..."
docker build -t "$FULL_IMAGE_NAME" -f RESQ.Presentation/Dockerfile .

echo ""
echo "[2/3] Tagging as latest..."
docker tag "$FULL_IMAGE_NAME" "${IMAGE_NAME}:latest"

echo ""
echo "[3/3] Pushing to Docker Hub..."
echo "Make sure you're logged in: docker login"

docker push "$FULL_IMAGE_NAME"
docker push "${IMAGE_NAME}:latest"

echo ""
echo "========================================"
echo "SUCCESS! Image pushed to Docker Hub"
echo "Image: $FULL_IMAGE_NAME"
echo "========================================"

echo ""
echo "Frontend team can now use:"
echo "  docker pull $FULL_IMAGE_NAME"
