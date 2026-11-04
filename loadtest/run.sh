#!/usr/bin/env bash
# Reproducible benchmark: for each run, recreate the SQL Server volume, start the
# API image, seed the ds-100x500 dataset through the API, then run the workload.
#   loadtest/run.sh <label> <api-image>      e.g. loadtest/run.sh sqlserver-baseline tms:baseline
# Env: RUNS (default 3), VUS (50), DURATION (60s). Requires .env (see .env.example)
# with AUTH_RATE_LIMIT >= 100, because seeding registers 100 users from one IP.
set -euo pipefail
cd "$(dirname "$0")/.."

label=$1
image=$2
runs=${RUNS:-3}
port=$(grep -E '^API_PORT=' .env | cut -d= -f2 || true)
base="http://localhost:${port:-8080}"
export API_IMAGE=$image

for run in $(seq 1 "$runs"); do
  echo "== $label run $run/$runs ($image)"
  docker compose down -v --remove-orphans >/dev/null 2>&1
  docker compose up -d --no-build >/dev/null 2>&1
  for _ in $(seq 1 90); do
    curl -sf -o /dev/null "$base/health/ready" && break
    sleep 2
  done
  k6 run -q -e BASE_URL="$base" loadtest/k6/seed.js >/dev/null
  k6 run -q -e BASE_URL="$base" -e LABEL="$label-run$run" \
    -e VUS="${VUS:-50}" -e DURATION="${DURATION:-60s}" loadtest/k6/tasks.js
done

docker compose down -v >/dev/null 2>&1
