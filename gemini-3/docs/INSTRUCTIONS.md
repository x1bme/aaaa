# Get same initial state (static resources like images most likely OK, running containers/pods can cause conflicts)

## Docker Cleanup
```bash
# Remove all Docker containers
docker rm -f $(docker ps -aq)

# Remove all Docker images
docker rmi -f $(docker images -aq)

# Remove unused Docker resources (networks, volumes)
docker system prune -af --volumes

## Kubernetes Cleanup
# Delete all resources in all namespaces
kubectl delete all --all -n web
kubectl delete all --all -n dau

# Delete the namespaces themselves
kubectl delete namespace web
kubectl delete namespace dau

# Delete persistent volumes and claims
kubectl delete pv --all
kubectl delete pvc --all --all-namespaces

# Delete configmaps and secrets
kubectl delete configmap --all --all-namespaces
kubectl delete secret --all --all-namespaces

# Remove any custom resource definitions
kubectl delete crd --all

# Optional: Complete K3s uninstall if needed
/usr/local/bin/k3s-uninstall.sh

# Initialization

# Start local registry if not running
docker run -d -p 5000:5000 --restart=always --name registry registry:2

# DeviceCommunication Service Setup and Testing

## 1. Clean Project
```bash
# Navigate to DeviceCommunication directory
cd src/dau/device-communication

# Clean all build artifacts
dotnet clean

# Restore dependencies and build
dotnet restore
dotnet build

# Build image
docker build -t device-communication:latest .

# Tag for local registry
docker tag device-communication:latest localhost:5000/device-communication:latest

# Push to local registry
docker push localhost:5000/device-communication:latest


# Create namespace if it doesn't exist
kubectl apply -f deploy/k3s/dau/namespace.yaml

# Apply deployment
kubectl apply -f deploy/k3s/dau/device-communication/deployment.yaml
kubectl apply -f deploy/k3s/dau/device-communication/service.yaml

# Verify pod is running
kubectl get pods -n dau

# Verify the service is running
kubectl get services -n dau

# Check logs if needed
kubectl logs -n dau $(kubectl get pod -n dau -l app=device-communication -o name)

# TESTING
# Port forward to access the service (preferred)
kubectl port-forward -n dau service/device-communication 5002:80

# OR direct port forwarding to pod (not preferred. Pods are ephemeral, so always always always rely on the service, use this as backup only if necessary)
kubectl port-forward -n dau pod/pod_id 5002:80

# Run the grpcurl commands in /tests/scripts/grpcurl.sh in another terminal


# DeviceProxy Service Setup and Testing
TODO (similar to DeviceCommunication except for different ports and different gprcurl commands; see respective /tests/scripts/grpcurl.sh)
 
# ApiGateway Service Setup and Testing
TODO
 
# AuthNZ Service Setup and Testing
TODO
TODO
