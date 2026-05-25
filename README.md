# MeterSystem — Meter Readings Backend

A distributed backend for ingesting and processing electricity meter readings, built with ASP.NET Core, RabbitMQ, Redis, and PostgreSQL, deployed on Kubernetes.

## Architecture

```
POST /api/readings
        │
 [MeterSystem.Api]   — validates input, publishes to RabbitMQ
        │
   [RabbitMQ]        — durable queue (meter.readings)
        │
[MeterSystem.Worker] — consumes messages, deduplicates, persists
        │        │
     [Redis]    [PostgreSQL]
  (fast dedup)  (source of truth)
```

**Idempotency** is enforced in two layers:

1. **Redis** — a key per `(meter, timestamp)` with a 24 h TTL catches duplicates before touching the database. Redis failures are non-fatal; the worker falls back to layer 2.
2. **PostgreSQL** — `PRIMARY KEY (meter_id, value_at)` with `ON CONFLICT DO NOTHING` is the permanent safety net regardless of Redis state.

## Prerequisites

| Tool | Purpose |
|---|---|
| [Docker Desktop](https://www.docker.com/products/docker-desktop/) | Container runtime |
| [Minikube](https://minikube.sigs.k8s.io/docs/start/) | Local Kubernetes cluster |
| [kubectl](https://kubernetes.io/docs/tasks/tools/) | Cluster management |
| [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8) | Build and publish |

## Running the System

### 1. Start Minikube

```bash
minikube start
```

### 2. Deploy Everything

```bash
chmod +x deploy.sh
./deploy.sh
```

The script will:
1. Pull `postgres:18`, `rabbitmq:3-management`, and `redis:7-alpine` into Minikube
2. Deploy RabbitMQ, Redis, and PostgreSQL
3. Wait for PostgreSQL to be ready and apply the schema
4. Build and load the API image, then deploy it
5. Build and load the Worker image, then deploy it

### 3. Get the API URL

```bash
minikube service metersystem-api --url
```

This prints the external URL, e.g. `http://192.168.49.2:30080`.

## API Usage

### Submit readings

```bash
curl -X POST http://<API_URL>/api/readings \
  -H "Content-Type: application/json" \
  -d '{
    "meter_number": 12345,
    "readings": {
      "2026-03-18T10:00:00Z": 1234.51,
      "2026-03-18T10:15:00Z": 1234.56
    }
  }'
# → 202 Accepted
```

### Validation errors

```bash
curl -X POST http://<API_URL>/api/readings \
  -H "Content-Type: application/json" \
  -d '{"meter_number": -1, "readings": {}}'
# → 400 Bad Request {"error": "meter_number must be a positive integer"}
```

### Health checks

```bash
curl http://<API_URL>/health/live    # → 200 Healthy
curl http://<API_URL>/health/ready   # → 200 Healthy
```

## Configuration

All configuration is injected via environment variables in the Kubernetes manifests. Local development uses `appsettings.Development.json`.

| Variable | Service | Description |
|---|---|---|
| `RabbitMq__Host` | API, Worker | RabbitMQ hostname |
| `RabbitMq__Port` | API, Worker | RabbitMQ port (default `5672`) |
| `RabbitMq__Username` | API, Worker | RabbitMQ username |
| `RabbitMq__Password` | API, Worker | RabbitMQ password |
| `RabbitMq__QueueName` | API, Worker | Queue name (`meter.readings`) |
| `Database__ConnectionString` | Worker | PostgreSQL connection string |
| `Redis__ConnectionString` | Worker | Redis connection string |

## Project Structure

```
src/
  MeterSystem.Api/       # ASP.NET Core Web API — POST /api/readings
  MeterSystem.Worker/    # Background service — RabbitMQ consumer + PostgreSQL writer
  MeterSystem.Shared/    # Shared message contracts (MeterReadingMessage)
tests/
  MeterSystem.Worker.IntegrationTests/   # Testcontainers integration tests
database/
  schema.sql             # PostgreSQL DDL (meters, meter_readings)
  deploy.yaml            # PostgreSQL Kubernetes manifest
queue/
  deploy.yaml            # RabbitMQ Kubernetes manifest
redis/
  deploy.yaml            # Redis Kubernetes manifest
deploy.sh                # End-to-end Minikube deployment script
diagrams/                # Excalidraw architecture diagrams
```

## Running Tests

Integration tests use [Testcontainers](https://dotnet.testcontainers.org/) and require Docker.

```bash
dotnet test tests/MeterSystem.Worker.IntegrationTests/
```

**Test coverage:**

| Test | What it verifies |
|---|---|
| Happy path | New meter and readings are persisted correctly |
| DB deduplication | `ON CONFLICT DO NOTHING` silently drops a duplicate row |
| Redis cache-hit | Second delivery is short-circuited before touching the DB |
| Redis unavailable | Worker falls back to DB deduplication — result is still correct |
| Multi-meter isolation | Two distinct meters store independent readings |

## Logging

Both services use [Serilog](https://serilog.net/) configured from `appsettings.json`:

- **Production** — compact JSON to stdout, ready for log aggregators (Loki, ELK, Datadog)
- **Development** — human-readable console output at `Debug` level

Every log event is enriched with `MachineName`, `ThreadId`, and `Application` (service name).

## What I Would Add Next

- **Secrets management** — move credentials to Kubernetes Secrets or Vault instead of plain env vars
- **Dead-letter queue** — configure a RabbitMQ DLX so permanently rejected messages are not silently dropped
- **OpenTelemetry** — distributed traces and metrics across API and Worker with correlation IDs
- **`POST /api/readings/raw`** — Base64-encoded Protobuf endpoint (schema provided in `Instructions.md`)
- **HPA** — horizontal pod autoscaling on the Worker based on RabbitMQ queue depth
