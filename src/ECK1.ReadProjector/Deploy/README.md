This Deploy folder contains Helm chart and helper scripts for running the Queries API locally and in k8s.

Scenarios:

1) Local k8s
- Use Helm (this chart) to deploy the Read Projector (depends on mongo on 32017)

2) Local run without Docker
- Press F5 in Visual Studio to run the project (depends on mongo on 32017).

3) CI / Real environment
- On CI you can set `runMongo` in Helm values to true/false. If false, supply an external connection string via `mongo.connectionString` or set environment variable `MongoDb__ConnectionString`.

Run the local script (PowerShell):

```powershell
cd Deploy
.\RunLocally.ps1
```
