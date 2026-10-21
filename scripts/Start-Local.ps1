$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $projectRoot
$localRoot = Join-Path $projectRoot '.local'
$mysqlRoot = Join-Path $localRoot 'mysql\mysql-8.4.6-winx64'
$mysqlServer = Join-Path $mysqlRoot 'bin\mysqld.exe'
$mysqlClient = Join-Path $mysqlRoot 'bin\mysql.exe'
$dataRoot = Join-Path $localRoot 'mysql-data'
$clientConfig = Join-Path $localRoot 'mysql-client.cnf'
$pidPath = Join-Path $localRoot 'mysql.pid'
function Test-LocalMySqlPort {
    $probe = New-Object System.Net.Sockets.TcpClient
    try { $pending = $probe.ConnectAsync('127.0.0.1', 33316); if (!$pending.Wait(300)) { return $false }; return $probe.Connected }
    catch { return $false }
    finally { $probe.Dispose() }
}
New-Item -ItemType Directory -Path $localRoot -Force | Out-Null
if (!(Test-Path -LiteralPath $mysqlServer)) {
    $archive = Join-Path $localRoot 'mysql.zip'
    if (!(Test-Path -LiteralPath $archive)) {
        Write-Host 'Downloading MySQL Community 8.4.6 portable for local development...'
        Invoke-WebRequest -Uri 'https://dev.mysql.com/get/Downloads/MySQL-8.4/mysql-8.4.6-winx64.zip' -OutFile $archive
    }
    Expand-Archive -LiteralPath $archive -DestinationPath (Join-Path $localRoot 'mysql') -Force
}
if (!(Test-Path -LiteralPath (Join-Path $dataRoot 'mysql'))) {
    & $mysqlServer --no-defaults --initialize-insecure "--basedir=$mysqlRoot" "--datadir=$dataRoot" "--log-error=$localRoot\mysql-init.log"
    if ($LASTEXITCODE -ne 0) { throw 'Could not initialize the private MySQL directory.' }
}
$ownedServer = $null
if (Test-LocalMySqlPort) {
    if (!(Test-Path -LiteralPath $pidPath)) { throw 'Port 33316 is already in use. Stop the private server or choose a separate database configuration.' }
    $serverPid = [int](Get-Content -LiteralPath $pidPath -Raw).Trim()
    $running = Get-Process -Id $serverPid -ErrorAction SilentlyContinue
    if (!$running -or $running.Path -ne $mysqlServer) { throw 'Port 33316 belongs to another process. No changes made.' }
} else {
    $arguments = @('--no-defaults', "--basedir=`"$mysqlRoot`"", "--datadir=`"$dataRoot`"", '--port=33316', '--bind-address=127.0.0.1', '--mysqlx=OFF', "--pid-file=`"$pidPath`"", "--log-error=`"$localRoot\mysql.log`"")
    $ownedServer = Start-Process -FilePath $mysqlServer -ArgumentList $arguments -PassThru -WindowStyle Hidden
    for ($attempt = 0; $attempt -lt 40; $attempt++) {
        Start-Sleep -Milliseconds 500
        if ($ownedServer.HasExited) { throw 'Private MySQL failed to start. See .local/mysql.log.' }
        if (Test-LocalMySqlPort) { break }
    }
}
try {
    if (!(Test-Path -LiteralPath $clientConfig)) {
        $rootPassword = [Guid]::NewGuid().ToString('N') + 'Aa!7'
        $appPassword = [Guid]::NewGuid().ToString('N') + 'Bb!8'
        $sql = "ALTER USER 'root'@'localhost' IDENTIFIED BY '$rootPassword'; CREATE DATABASE IF NOT EXISTS vega_dev CHARACTER SET utf8mb4; CREATE USER IF NOT EXISTS 'vega_app'@'localhost' IDENTIFIED BY '$appPassword'; GRANT ALL ON vega_dev.* TO 'vega_app'@'localhost';"
        $sql | & $mysqlClient --no-defaults --default-character-set=utf8mb4 --host=127.0.0.1 --port=33316 --user=root
        if ($LASTEXITCODE -ne 0) { throw 'Could not configure private MySQL accounts.' }
        "[client]`r`nhost=127.0.0.1`r`nport=33316`r`nuser=root`r`npassword=$rootPassword`r`n" | Set-Content -LiteralPath $clientConfig -Encoding ASCII
        $settingsPath = Join-Path $projectRoot 'appsettings.Local.json'
        if (Test-Path -LiteralPath $settingsPath) { $settings = Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json }
        else {
            $demoPassword = 'Vega!' + [Guid]::NewGuid().ToString('N').Substring(0,12) + '9a'
            $settings = @{Database=@{AutoMigrate=$true;SeedDemo=$true};Seed=@{DemoPassword=$demoPassword};Payment=@{SigningKey=[Guid]::NewGuid().ToString('N')+[Guid]::NewGuid().ToString('N')}}
            "Demo accounts: customer@vega.local, admin@vega.local, staff@vega.local, checker@vega.local, manager@vega.local`r`nPassword: $demoPassword" | Set-Content -LiteralPath (Join-Path $localRoot 'demo-access.txt') -Encoding UTF8
        }
        if ($settings -is [hashtable]) { $settings.ConnectionStrings = @{VegaDatabase="Server=127.0.0.1;Port=33316;Database=vega_dev;User=vega_app;Password=$appPassword;SslMode=None;ServerRSAPublicKeyFile=$dataRoot\public_key.pem"} }
        else { $settings | Add-Member -Force -NotePropertyName ConnectionStrings -NotePropertyValue @{VegaDatabase="Server=127.0.0.1;Port=33316;Database=vega_dev;User=vega_app;Password=$appPassword;SslMode=None;ServerRSAPublicKeyFile=$dataRoot\public_key.pem"} }
        $settings | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $settingsPath -Encoding UTF8
    }
    $settingsPath = Join-Path $projectRoot 'appsettings.Local.json'
    $settings = Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json
    if ($settings.ConnectionStrings.VegaDatabase -like '*Port=33316*' -and $settings.ConnectionStrings.VegaDatabase -notlike '*ServerRSAPublicKeyFile=*') {
        $settings.ConnectionStrings.VegaDatabase += ";ServerRSAPublicKeyFile=$dataRoot\public_key.pem"
        $settings | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $settingsPath -Encoding UTF8
    }
    $certificatePath = Join-Path $localRoot 'https.pem'
    $privateKeyPath = Join-Path $localRoot 'https.key'
    if (!(Test-Path -LiteralPath $certificatePath) -or !(Test-Path -LiteralPath $privateKeyPath)) {
        dotnet dev-certs https --export-path $certificatePath --format Pem --no-password
        if ($LASTEXITCODE -ne 0) { throw 'Could not prepare the HTTPS development certificate.' }
    }
    $settings | Add-Member -Force -NotePropertyName Kestrel -NotePropertyValue @{Certificates=@{Default=@{Path=$certificatePath;KeyPath=$privateKeyPath}}}
    $settings | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $settingsPath -Encoding UTF8
    Write-Host 'Demo credentials: .local/demo-access.txt (private; do not commit).'
    Write-Host 'Application: https://localhost:7175. Stop with Ctrl+C.'
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    dotnet run --project VegaCinema.csproj --launch-profile https
    if ($LASTEXITCODE -ne 0) { throw 'Application failed to start. See the error above.' }
} finally {
    if ($ownedServer -and !$ownedServer.HasExited -and (Test-Path -LiteralPath $clientConfig)) {
        & (Join-Path $mysqlRoot 'bin\mysqladmin.exe') "--defaults-extra-file=$clientConfig" --protocol=tcp --connect-timeout=5 --ssl-mode=DISABLED "--server-public-key-path=$dataRoot\public_key.pem" shutdown
    }
}
