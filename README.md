# Rimonim Tech Task

This repository implements a meter-reading backend using .NET 10, RabbitMQ, PostgreSQL, and Kubernetes manifests.

## What was completed

- Implemented `MeterSystem.Api`:
  - `GET /health` health check endpoint
  - `POST /api/readings` accepts JSON meter readings and publishes to RabbitMQ
  - `POST /api/readings/raw` accepts base64 protobuf payloads and publishes them to RabbitMQ
- Implemented `MeterSystem.Worker`:
  - Consumes the `meter_readings` queue
  - Persists meter readings into PostgreSQL
  - Handles malformed queue messages and acknowledges correctly
- Added shared message contract in `MeterSystem.Shared`
- Added Protobuf contract in `src/MeterSystem.Api/Protos/meterdata.proto`
- Added Kubernetes manifests for:
  - RabbitMQ: `queue/deploy.yaml`
  - PostgreSQL: `database/deploy.yaml`
  - API: `src/MeterSystem.Api/deploy.yaml`
  - Worker: `src/MeterSystem.Worker/deploy.yaml`
- Added PostgreSQL schema helper file: `database/schema.sql`
- Verified local build and tests

## Project structure

- `MeterSystem.slnx` - solution containing API, worker, shared library, and tests
- `src/MeterSystem.Api` - API project
- `src/MeterSystem.Worker` - worker project
- `src/MeterSystem.Shared` - shared DTOs and contracts
- `tests` - xUnit test project
- `database` - PostgreSQL manifest and schema
- `queue` - RabbitMQ manifest

## Validation results

The code has been verified locally:

- `dotnet build MeterSystem.slnx -c Release` ✅
- `dotnet test tests/MeterSystem.Tests/MeterSystem.Tests.csproj -c Release --no-restore` ✅
- API health check verified on `http://127.0.0.1:8081/health` with response:
  - `200 OK`
  - `{"status":"ok"}`

## Run locally

### API only

Ensure RabbitMQ is available when using the API endpoint.

```powershell
cd C:\Users\USER\.vscode\task
dotnet build MeterSystem.slnx -c Release
dotnet run --project src/MeterSystem.Api/MeterSystem.Api.csproj --urls=http://127.0.0.1:8081
```

Then check:

```powershell
curl.exe http://127.0.0.1:8081/health
```

### Full local run

A ready-to-use PowerShell script is available to start dependencies, build the solution, run the API and worker, send a test reading, and verify persistence.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\run-all.ps1
```

### API endpoints

- `POST /api/readings`
- `POST /api/readings/raw`

### Run tests

```powershell
dotnet test tests/MeterSystem.Tests/MeterSystem.Tests.csproj -c Release --no-restore
```

## Kubernetes deployment

Apply manifests in the following order:

```bash
kubectl apply -f database/deploy.yaml
kubectl apply -f queue/deploy.yaml
kubectl apply -f src/MeterSystem.Api/deploy.yaml
kubectl apply -f src/MeterSystem.Worker/deploy.yaml
```

## Environment variables

The API and worker read configuration from environment variables.

### API

- `RABBITMQ_HOST` = `rabbitmq`
- `RABBITMQ_PORT` = `5672`
- `RABBITMQ_USERNAME` = `guest`
- `RABBITMQ_PASSWORD` = `guest`
- `RABBITMQ_QUEUE` = `meter_readings`

### Worker

- `RABBITMQ_HOST` = `rabbitmq`
- `RABBITMQ_PORT` = `5672`
- `RABBITMQ_USERNAME` = `guest`
- `RABBITMQ_PASSWORD` = `guest`
- `RABBITMQ_QUEUE` = `meter_readings`
- `POSTGRES_HOST` = `postgres`
- `POSTGRES_PORT` = `5432`
- `POSTGRES_DB` = `meters`
- `POSTGRES_USER` = `postgres`
- `POSTGRES_PASSWORD` = `postgres`

## Notes

- The worker persists a normalized meter model into PostgreSQL
- The API supports both JSON and raw protobuf ingestion
- Kubernetes manifests include CPU/memory requests and limits
- The repository is ready for submission with build, test, and deployment artifacts
