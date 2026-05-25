# Programmer Interview Task – Meter Readings Backend

## 1 Overview

Build a small, self-contained backend system for receiving and processing meter readings.

The goal is to demonstrate your skills in:

- ASP.NET Core
- PostgreSQL and SQL
- Message queues
- Kubernetes
- Git usage

## 2. Requirements

### 2.1 Ingestion API

- ASP.NET Core Web API
- Must be accessible from outside the Kubernetes cluster

### 2.2 Readings Endpoint

- `POST /api/readings`

#### 2.2.1 Responsibilities

- Validate input format
- Reject invalid requests (`400`)
- Accept valid requests (`202`)
- Publish the message to the queue

#### 2.2.2 Example payload

```json
{
  "meter_number": 12345,
  "readings": {
    "2026-03-18T10:15:00Z": 1234.56,
    "2026-03-18T10:00:00Z": 1234.51
  }
}
```

#### 2.2.3 Notes

- `meter_number` is globally unique (see `database/schema.sql`)
- Readings may arrive out of order
- There is no maximum payload size requirement for this task
- Validation rules are limited to ensuring the request format is valid


### 2.3 Raw Readings Endpoint (Optional)

This optional task allows you to demonstrate experience with binary protocols.

- `POST /api/readings/raw`

#### 2.3.1 Responsibilities

- Parse and validate input
- Reject invalid requests (`400`)
- Accept valid requests (`202`)
- Publish the message to the queue

#### 2.3.2 Example payload

```json
{
  "meter_number": 12345,
  "data": "ChEKBgik9unNBhEK16NwPUqTQAoRCgYIoO/pzQYR16NwPQpKk0A="
}
```

The `data` field is a Base64-encoded protobuf message with the schema in Section 2.3.3.
The above example represents the same values as the example in Section 2.2.2.

#### 2.3.3 Protobuf schema

```protobuf
edition = "2023";

import "google/protobuf/timestamp.proto";

message MeterData {
    message MeterReading {
        google.protobuf.Timestamp timestamp = 1;
        double value = 2;
    }

    repeated MeterReading readings = 1;
}
```

Only attempt this after the core functionality is complete.

## 3. Worker Service

- Background service (ASP.NET Core worker)

### 3.1 Responsibilities

- Consume messages from the queue
- Create the meter if it does not exist
- Insert readings into PostgreSQL
- Ensure idempotent inserts

#### 3.1.1 Deduplication rules

- Deduplication is based on `(meter_id, value_at)` as defined by the database schema
- The first received reading for a given meter and timestamp should be kept
- Later duplicates should be ignored

## 4. PostgreSQL

- Use PostgreSQL running inside Kubernetes

### 4.1 Requirements

- Schema is provided in `database/schema.sql`. You may modify it if needed.
- A Kubernetes manifest is provided in `database/deploy.yaml`. You may modify it if needed.
- Default configuration is sufficient. No persistence or tuning is required.

## 5. Message Queue

- Use a message queue running inside Kubernetes
- RabbitMQ is recommended (manifests provided in `queue/deploy.yaml`)

### 5.1 Requirements

- Minimal setup is sufficient
- No persistence or advanced configuration is required


## 6. Kubernetes

The solution should run locally using **Minikube**.

### 6.1 Requirements

- Basic Kubernetes manifests are already provided for:
  - API service (`src/MeterSystem.Api/deploy.yaml`)
  - Worker service (`src/MeterSystem.Worker/deploy.yaml`)
- These manifests are intentionally incomplete and should be completed as part of the task
- You may modify them as needed to make the solution work end-to-end
- Service configuration should be defined in the Kubernetes manifests, not in `appsettings.json`

- Keep infrastructure simple:

  - Minimal manifests are sufficient
  - A single replica is sufficient
  - No production-grade configuration required

The provided deployment script (`deploy.sh`) can be used as-is or modified to suit your needs.


## 7. Expected Behaviour

- Accept batch readings per meter
- Readings may arrive out of order
- Process asynchronously via the queue
- Ensure idempotent inserts (no duplicates)
- The system should run end-to-end locally

## 8. Technical Requirements

- C# / .NET
- ASP.NET Core
- PostgreSQL
- SQL — do **not** use full ORM frameworks (for example, Entity Framework). Use raw SQL or a lightweight mapper (for example, Dapper)
- Kubernetes manifests
- Message queue
- README with instructions

## 9. Git Requirements

Structure your work using meaningful Git commits.

### 9.1 Expectations

- Small, logical commits
- Clear commit messages
- A history that explains how the solution evolved

## 10. Deliverables

Provide a Git repository with a full history containing:

- Source code
- Kubernetes manifests
- SQL / migration setup
- README with run instructions

## 11. Timebox

Please spend approximately **4 hours** on this task.

- It is not expected to be fully production-ready
- If something is incomplete, explain what you would do next

## 12. Evaluation Criteria

Submissions will mainly be evaluated on:

- Correctness of the end-to-end flow
- Clarity and structure of the solution
- Handling of asynchronous processing and idempotency
- Code quality and maintainability
- Appropriate use of SQL and database constraints
- Practicality and simplicity of the Kubernetes setup
- Quality of the Git history
- Trade-offs and reasoning (especially where shortcuts were taken due to time constraints)

## 13. Notes

- Keep the solution simple and focused
- Prefer a working end-to-end flow over completeness
- Prioritize clarity over production-grade design

