
function Ensure-LocalMongoStopped {
    param (
        [int]$mongoPort
    )

    $mongoInUse = Test-PortInUse -port $mongoPort
    if ($mongoInUse) {

        Write-Host "Mongo port $mongoPort is in use. First checking maybe it is k8s mongo..."
        # check maybe it is k8s mongo
        $isMongoAlreadyOnK8s = kubectl get pods -A | findstr mongo | findstr Running

        if (-not $isMongoAlreadyOnK8s) {
            Write-Host "Couldn't find a Mongo container to shutdown. Failing deploy."
            throw
        } else {
            Write-Host "Mongo already running on k8s. Proceeding with deploy."
        }


        Write-Host "Mongo port $mongoPort is in use. Checking if the listener is a Docker container..."

        # Check docker for any running container that exposes port 32017
        $dockerCandidates = @()
        try {
            $dockerCandidates = docker ps --format "{{.ID}}|{{.Names}}|{{.Ports}}|{{.Image}}" 2>$null
        } catch {
            $dockerCandidates = @()
        }

        $dockerMongoLine = $null
        foreach ($line in $dockerCandidates) {
            if ($line -and $line -match $mongoPort) {
                $dockerMongoLine = $line
                break
            }
        }

        if ($dockerMongoLine) {
            $parts = $dockerMongoLine -split '\|'
            $cid = $parts[0]
            $cname = $parts[1]
            $cports = $parts[2]
            $cimage = $parts[3]
            Write-Host "Detected Docker container exposing port ${mongoPort}: $cname ($cid) image:$cimage ports:$cports"
            Write-Host "Stopping and removing Docker container so Helm can deploy Mongo in k8s..."
            docker stop $cid | Out-Null

            # Retry check: give the OS / Docker a moment to release the port.
            # If the port is still in use, retry up to 5 times with 2s delay.
            $maxRetries = 5
            $waitSeconds = 2
            $attempt = 0
            $mongoInUse = Test-PortInUse -port $mongoPort
            while ($mongoInUse -and ($attempt -lt $maxRetries)) {
                $attempt++
                Write-Host "Port $mongoPort still in use. Retry $attempt of $maxRetries after $waitSeconds seconds..."
                Start-Sleep -Seconds $waitSeconds

                # Try to find any lingering docker container mapping the port and remove it again
                try {
                    $lingering = docker ps -a --filter "publish=$mongoPort" --format "{{.ID}}" 2>$null
                    if ($lingering) {
                        foreach ($lid in $lingering) {
                            Write-Host "Found lingering container $lid; stopping and removing..."
                            docker stop $lid | Out-Null
                            docker rm $lid | Out-Null
                        }
                    }
                } catch {
                    # ignore docker errors on cleanup
                }

                $mongoInUse = Test-PortInUse -port $mongoPort
            }

            if ($mongoInUse) {
                Write-Host "Port $mongoPort is still in use after $maxRetries retries. Failing deploy."
                throw
            }
        }
    }
}