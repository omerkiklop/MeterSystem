#!/usr/bin/env bash
set -euo pipefail

echo "Downloading Images..."
minikube image pull postgres:18
minikube image pull rabbitmq:3-management
minikube image pull redis:7-alpine

echo "Deploying Queue..."
kubectl apply -f queue/deploy.yaml

echo "Deploying Redis..."
kubectl apply -f redis/deploy.yaml

echo "Deploying Database..."
kubectl apply -f database/deploy.yaml
kubectl wait --for=condition=ready pod -l app=postgres --timeout=300s
kubectl exec -i "$(kubectl get pod -l app=postgres -o jsonpath='{.items[0].metadata.name}')" --  sh -c 'psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB"' < database/schema.sql

echo "Publishing and Deploying API..."
dotnet publish ./src/MeterSystem.Api/MeterSystem.Api.csproj -t:PublishContainer
minikube image load metersystem-api:latest
kubectl apply -f ./src/MeterSystem.Api/deploy.yaml

echo "Deploying Worker..."
dotnet publish ./src/MeterSystem.Worker/MeterSystem.Worker.csproj -t:PublishContainer
minikube image load metersystem-worker:latest
kubectl apply -f ./src/MeterSystem.Worker/deploy.yaml

echo "done"
