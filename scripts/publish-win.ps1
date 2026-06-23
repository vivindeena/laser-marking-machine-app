param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$project = Join-Path $repoRoot "src\LaserMarkingApp\LaserMarkingApp.vbproj"
$dist = Join-Path $repoRoot "dist"

if (Test-Path $dist) {
    Remove-Item $dist -Recurse -Force
}

New-Item -ItemType Directory -Path $dist | Out-Null

$runtimes = @("win-x64", "win-x86")

foreach ($runtime in $runtimes) {
    $outDir = Join-Path $dist "laser-marking-machine-app-$runtime"
    dotnet publish $project `
        -c $Configuration `
        -r $runtime `
        --self-contained true `
        -p:PublishSingleFile=false `
        -p:PublishTrimmed=false `
        -o $outDir

    $zipPath = Join-Path $dist "laser-marking-machine-app-$runtime.zip"
    Compress-Archive -Path (Join-Path $outDir "*") -DestinationPath $zipPath -Force
}

Write-Host "Published release zips in $dist"
