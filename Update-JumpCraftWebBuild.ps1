# Find the newest complete Unity WebGL build and publish it to the React app.
$ErrorActionPreference = 'Stop'

$buildsRoot = 'C:\JumpCraft\CS499Group8\JumpCraftGame\Builds\WebGL'
$webGamePath = 'C:\JumpCraft\CS499Group8\JumpCraftWeb\public\game'

if (-not (Test-Path -LiteralPath $buildsRoot -PathType Container)) {
    throw "Builds directory not found: $buildsRoot"
}

# A complete Unity WebGL build normally contains index.html and a Build folder.
# Compare the timestamps of generated files inside Build, not just folder dates.
$builds = @(
    Get-ChildItem -LiteralPath $buildsRoot -Filter 'index.html' -File -Recurse |
        ForEach-Object {
            $candidatePath = $_.Directory.FullName
            $artifactPath = Join-Path $candidatePath 'Build'
            if (Test-Path -LiteralPath $artifactPath -PathType Container) {
                $newestArtifact = Get-ChildItem -LiteralPath $artifactPath -File -Recurse |
                    Sort-Object LastWriteTime -Descending |
                    Select-Object -First 1
                if ($null -ne $newestArtifact) {
                    [pscustomobject]@{
                        Path      = $candidatePath
                        BuiltAt   = $newestArtifact.LastWriteTime
                    }
                }
            }
        } |
        Sort-Object BuiltAt -Descending
)

if ($builds.Count -eq 0) {
    throw "No complete Unity WebGL builds (index.html + Build folder) found under: $buildsRoot"
}

$source = $builds[0].Path
Write-Host "Latest build: $source"
Write-Host "Last built file: $($builds[0].BuiltAt)"
Write-Host "Publishing to: $webGamePath"

# /MIR replaces the destination contents and removes obsolete files there.
# WARNING: Anything currently inside public\game that is absent from the build is deleted.
& robocopy.exe $source $webGamePath /MIR /R:2 /W:1 /NFL /NDL /NJH /NJS /NP
$robocopyExitCode = $LASTEXITCODE
if ($robocopyExitCode -ge 8) {
    throw "Copy failed (Robocopy exit code $robocopyExitCode)."
}

Write-Host 'JumpCraft WebGL build updated successfully.' -ForegroundColor Green
