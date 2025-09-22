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
        --wait

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Workflow failed for $typeName"
        exit $LASTEXITCODE
    }
}
