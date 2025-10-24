function WaitFor-Command {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Command,
        [int]$IntervalSeconds = 5,
        [int]$MaxAttempts = 60
    )

    for ($i = 1; $i -le $MaxAttempts; $i++) {
        try {
            $output = Invoke-Expression $Command

            if ($output -and $LASTEXITCODE -eq 0) {
                return $output
            }
        }
        catch {
        }

        Write-Host "Waiting... ($i/$MaxAttempts)"
        Start-Sleep -Seconds $IntervalSeconds
    }

    throw "❌ Max attempts reached. Failing..."
}