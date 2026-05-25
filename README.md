# MeterSystem — Meter Readings Backend

A distributed backend for ingesting and processing electricity meter readings.

## Architecture

```
POST /api/readings
       │
[MeterSystem.Api]  — validates input, publishes to RabbitMQ
       │
  [RabbitMQ]  — message queue (meter.readings)
       │
[MeterSystem.Worker]  — consumes messages, upserts to PostgreSQL
       │
  [PostgreSQL]  — meters + meter_readings tables
```

## Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop/)
- [Minikube](https://minikube.sigs.k8s.io/docs/start/)
- [kubectl](https://kubernetes.io/docs/tasks/tools/)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8)

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
1. Pull `postgres:18` and `rabbitmq:3-management` images into Minikube
2. Deploy PostgreSQL and apply the schema
3. Deploy RabbitMQ
4. Build and load the API image, deploy it
5. Build and load the Worker image, deploy it

### 3. Get the API URL

```bash
minikube service metersystem-api --url
```

This prints the external URL, e.g. `http://192.168.49.2:30080`.

## Testing the Endpoint

### Submit readings (valid)

```bash
curl -X POST http://<API_URL>/api/readings \
  -H "Content-Type: application/json" \
  -d '{
    "meter_number": 12345,
    "readings": {
      "2026-03-18T10:15:00Z": 1234.56,
      "2026-03-18T10:00:00Z": 1234.51
    }
  }'
# → 202 Accepted
```

### Submit invalid payload (validation)

```bash
curl -X POST http://<API_URL>/api/readings \
  -H "Content-Type: application/json" \
  -d '{"meter_number": -1, "readings": {}}'
# → 400 Bad Request
```

### Health checks

```bash
curl http://<API_URL>/health/live   # → 200 Healthy
curl http://<API_URL>/health/ready  # → 200 Healthy
```

## Configuration

All service configuration is injected via environment variables in the Kubernetes manifests — not in `appsettings.json`.

| Variable | Service | Description |
|---|---|---|
| `RabbitMq__Host` | API, Worker | RabbitMQ hostname |
| `RabbitMq__QueueName` | API, Worker | Queue name (`meter.readings`) |
| `RabbitMq__Username` | API, Worker | RabbitMQ username |
| `RabbitMq__Password` | API, Worker | RabbitMQ password |
| `Database__ConnectionString` | Worker | PostgreSQL connection string |

## Project Structure

```
src/
  MeterSystem.Api/        # ASP.NET Core Web API — POST /api/readings
  MeterSystem.Worker/     # Worker service — RabbitMQ consumer + PostgreSQL writer
  MeterSystem.Shared/     # Shared message contracts
database/
  schema.sql              # PostgreSQL schema (meters, meter_readings)
  deploy.yaml             # PostgreSQL Kubernetes manifest
queue/
  deploy.yaml             # RabbitMQ Kubernetes manifest
deploy.sh                 # End-to-end deployment script
```

## Idempotency

Deduplication is enforced at the database level. The primary key `(meter_id, value_at)` on `meter_readings` guarantees that the first received reading for a given meter and timestamp is kept; all later duplicates are silently dropped via `ON CONFLICT DO NOTHING`.

## What I Would Add Next

- **Secrets management** — move RabbitMQ and PostgreSQL credentials to Kubernetes Secrets
- **Retry / dead-letter queue** — configure a DLX in RabbitMQ for permanent failures
- **Integration tests** — Testcontainers-based tests covering the full ingestion + write flow
- **Observability** — structured log correlation ID across API and Worker; OpenTelemetry traces
- **`POST /api/readings/raw`** — Base64-encoded Protobuf endpoint (schema provided in `Instructions.md`)
