#!/usr/bin/env bash
# Run the Travel Pathways API container with a persistent uploads volume.
# Usage (on EC2 / Ubuntu):
#   chmod +x run-myapi.sh
#   ./run-myapi.sh [image-name]
#
# Uploads are stored on the host at /home/ubuntu/uploads and survive redeploys.

set -euo pipefail

IMAGE="${1:-myapi:latest}"
HOST_UPLOADS="/home/ubuntu/uploads"
CONTAINER_UPLOADS="/app/wwwroot/uploads"

mkdir -p "$HOST_UPLOADS"

docker run -d \
  --name myapi \
  --restart unless-stopped \
  -p 8080:8080 \
  -v "${HOST_UPLOADS}:${CONTAINER_UPLOADS}" \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e Uploads__Path="${CONTAINER_UPLOADS}" \
  "$IMAGE"

echo "myapi started. Uploads persisted at ${HOST_UPLOADS} -> ${CONTAINER_UPLOADS}"
