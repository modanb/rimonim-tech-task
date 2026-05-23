param()

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $root
Start-Transcript -Path (Join-Path $root 'run-all.log') -Force

function Start-ContainerIfNeeded {
    param(
        [string]$Name,
        [string]$Image,
        [string[]]$Arguments
    )

    $existing = & docker ps -aq -f "name=$Name" 2>$null
    if ($null -ne $existing) {
        $running = & docker ps -q -f "name=$Name" 2>$null
        if ($null -eq $running) {
            $status = (& docker inspect -f '{{.State.Status}}' $Name 2>$null).Trim()
            if ($status -in @('exited','created','dead')) {
                Write-Host "Removing stale container $Name and recreating..."
                & docker rm $Name | Out-Null
                Write-Host "Creating and starting container $Name..."
                & docker run -d --name $Name @($Arguments) $Image | Out-Null
            } else {
                Write-Host "Starting existing container $Name..."
                & docker start $Name | Out-Null
            }
        } else {
            Write-Host "Container $Name already running."
        }
    } else {
        Write-Host "Creating and starting container $Name..."
        & docker run -d --name $Name @($Arguments) $Image | Out-Null
    }
}

function Test-Port($port) {
    $listener = Get-NetTCPConnection -LocalPort $port -ErrorAction SilentlyContinue
    return $null -ne $listener
}

Write-Host "Stopping existing API/Worker processes..."
Get-Process -Name MeterSystem.Api, MeterSystem.Worker -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

$apiPort = 5002
if (Test-Port $apiPort) {
    Write-Host "Port $apiPort is already in use; switching to port 8081."
    $apiPort = 8081
}
$apiUrl = "http://127.0.0.1:$apiPort"

Write-Host "Ensuring local Docker dependencies..."
Start-ContainerIfNeeded -Name "metersystem-rabbitmq" -Image "rabbitmq:3-management" -Arguments @('-p','5672:5672','-p','15672:15672')
Start-ContainerIfNeeded -Name "metersystem-postgres" -Image "postgres:18" -Arguments @('-e','POSTGRES_PASSWORD=postgres','-p','5432:5432')

Write-Host "Waiting for Postgres container readiness..."
for ($i = 0; $i -lt 30; $i++) {
    & docker exec metersystem-postgres pg_isready -U postgres 2>$null
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Postgres is ready."
        break
    }
    Start-Sleep -Seconds 2
}
if ($LASTEXITCODE -ne 0) {
    Write-Error "Postgres did not become ready in time."
    Stop-Transcript
    exit 1
}

Write-Host "Creating database and applying schema..."
$databaseExists = & docker exec metersystem-postgres psql -U postgres -tAc "SELECT 1 FROM pg_database WHERE datname='meters';" | Out-String
if (-not $databaseExists.Trim()) {
    & docker exec metersystem-postgres psql -U postgres -c "CREATE DATABASE meters;" | Out-Null
}
Get-Content .\database\schema.sql -Raw | & docker exec -i metersystem-postgres psql -U postgres -d meters -f - | Out-Null

Write-Host "Building solution..."
& dotnet build MeterSystem.slnx -c Release | Out-Null

Write-Host "Starting API on $apiUrl..."
Start-Process -FilePath dotnet -ArgumentList "run --project src/MeterSystem.Api/MeterSystem.Api.csproj --urls=$apiUrl" -WorkingDirectory $root -NoNewWindow | Out-Null

Write-Host "Starting Worker..."
Start-Process -FilePath dotnet -ArgumentList "run --project src/MeterSystem.Worker/MeterSystem.Worker.csproj" -WorkingDirectory $root -NoNewWindow | Out-Null

Write-Host "Waiting for API health..."
$healthy = $false
for ($i=0; $i -lt 30; $i++) {
    try {
        $response = Invoke-WebRequest -Uri "$apiUrl/health" -TimeoutSec 5 -UseBasicParsing
        if ($response.StatusCode -eq 200) {
            Write-Host "API is healthy."
            $healthy = $true
            break
        }
    } catch {
        Start-Sleep -Seconds 1
    }
    Start-Sleep -Seconds 1
}
if (-not $healthy) {
    Write-Error "API did not become healthy in time."
    exit 1
}

Write-Host "Sending a test reading to the API..."
$testPayload = Get-Content -Raw .\test_reading.json
$req = [System.Net.WebRequest]::Create("$apiUrl/api/readings")
$req.Method = 'POST'
$req.ContentType = 'application/json'
$bytes = [System.Text.Encoding]::UTF8.GetBytes($testPayload)
$req.ContentLength = $bytes.Length
$stream = $req.GetRequestStream()
$stream.Write($bytes, 0, $bytes.Length)
$stream.Close()

try {
    $res = $req.GetResponse()
    $httpRes = $res -as [System.Net.HttpWebResponse]
    if ($null -ne $httpRes) {
        $status = $httpRes.StatusCode.value__
    } else {
        $status = 'unknown'
    }
    $reader = New-Object System.IO.StreamReader($res.GetResponseStream())
    $body = $reader.ReadToEnd()
    Write-Host "API response status: $status"
    if ($body) { Write-Host "API response body: $body" }
} catch [System.Net.WebException] {
    if ($null -ne $_.Response) {
        $httpRes = $_.Response -as [System.Net.HttpWebResponse]
        if ($null -ne $httpRes) {
            $status = $httpRes.StatusCode.value__
        } else {
            $status = 'unknown'
        }
        $reader = New-Object System.IO.StreamReader($_.Response.GetResponseStream())
        Write-Host "API error status: $status"
        Write-Host "API error body: $($reader.ReadToEnd())"
    } else {
        Write-Host "API request failed: $($_.Exception.Message)"
    }
    exit 1
}

Write-Host "Waiting for Worker to process the message..."
Start-Sleep -Seconds 5

Write-Host "Querying Postgres for latest readings..."
& docker exec -i metersystem-postgres psql -U postgres -d meters -c "SELECT meter_id, value_at, value, received_at_utc FROM meter_readings ORDER BY received_at_utc DESC LIMIT 5;"

Write-Host "Run complete. API: $apiUrl"
Stop-Transcript