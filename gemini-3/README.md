# Gemini Server Software

## Background
TODO. For now see docs/cnuclear/{srs,sdd}.pdf

## Categories of microservices 
* **DAU** : TODO. For now see docs/cnuclear/sdd.pdf
* **Web** : TODO. For now see docs/cnuclear/sdd.pdf
* **Database** : TODO. For now see docs/cnuclear/sdd.pdf
* **VOTES Infinity** : TODO. For now see docs/cnuclear/sdd.pdf

## How to add another microservice
We're following this structure (please follow accordingly):
* **Service.Api** : Entrypoint, actual service (gRPC) implementation here 
* **Service.Core** : Business-logic
* **Service.Grpc** : Proto files, gRPC generated code
* **Service.Infrastructure** : Infrastructure (e.g., storage backend access)

## Installation
TODO
