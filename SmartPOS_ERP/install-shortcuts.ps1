$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$launcher = Join-Path $root "Start-POS.exe"
if (-not (Test-Path $launcher)) {
    throw "Start-POS.exe was not found."
}

$p = Start-Process -FilePath $launcher -ArgumentList "--shortcuts-only" -WorkingDirectory $root -Wait -PassThru
if ($p.ExitCode -ne 0) {
    throw "Shortcut setup failed."
}
