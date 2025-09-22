param(
    [string]$ParamsFile,
    [string]$GhAction,
    [string]$Repo
)

$params = Get-Content $ParamsFile | ConvertFrom-Json

foreach ($param in $params) {
    $assemblyPath = $param.assemblyPath
    $typeName = $param.typeName
    $currentLatestSchemaPath = $param.currentLatestSchemaPath
    $format = $param.format
    $outputPath = $param.outputPath

    Write-Host "Running schema evolution action for type $typeName in $GhAction..."

    gh workflow run $GhAction `
        -R $Repo `
        -f assembly-path="$assemblyPath" `
        -f type-name="$typeName" `
        -f current-latest-schema-path="$currentLatestSchemaPath" `
        -f format="$format" `
        -f output-path="$outputPath" `

    if ($LASTEXITCODE -ne 0) {
        Write-Error "❌ Workflow failed for $typeName"
        exit $LASTEXITCODE
    }

    $run = gh run list -R $Repo --workflow $GhAction --limit 1 --json databaseId | ConvertFrom-Json
    $runId = $run[0].databaseId

    if (-not $runId) {
        Write-Error "❌ Can find run-id for workflow $GhAction"
        exit 1
    }

    Write-Host "⌛ Waiting for workflow run-id=$runId to finish..."
    gh run watch $runId -R $Repo

    if ($LASTEXITCODE -ne 0) {
        Write-Error "❌ Workflow failed for $typeName (run-id=$runId)"
        exit $LASTEXITCODE
    }

    Write-Host "✅ Workflow succeeded for $typeName"
}
