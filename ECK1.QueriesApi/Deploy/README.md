This Deploy folder contains Helm chart and helper scripts for running the Queries API locally and in k8s.

Scenarios:

1) Local k8s
- Use Helm (this chart) to deploy the API and optionally Mongo (controlled by `runMongo` value in `values.yaml`).
	- You can provide `values.secret.yaml` (not committed) to set `mongo.hostPath` so you know where Mongo data is persisted on the host.

2) Local run without Docker
- Press F5 in Visual Studio to run the project.
- If you need a local Mongo container (only for local dev without k8s), use `RunLocally.OnlyMongo.ps1` which will check port 27017 and pull/start the official `mongo:6.0` image if nothing is listening.

3) CI / Real environment
- On CI you can set `runMongo` in Helm values to true/false. If false, supply an external connection string via `mongo.connectionString` or set environment variable `MongoDb__ConnectionString`.

Run the local script (PowerShell):

```powershell
cd Deploy
.\RunLocally.ps1
```

Helm deploy example (requires Helm):

```powershell
helm upgrade --install queriesapi ./Deploy -f ./Deploy/values.local.yaml -f ./Deploy/values.secret.yaml
```
