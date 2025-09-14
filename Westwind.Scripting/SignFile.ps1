# winget install --exact --id Microsoft.AzureCLI
# winget install -e --id Microsoft.Azure.TrustedSigningClientTools

param(
    [string]$file = "",
    [string]$file1 = "",
    [string]$file2 = "",
    [string]$file3 = "",
    [string]$file4 = "",
    [string]$file5 = "",
    [string]$file6 = "",
    [string]$file7 = "",
    [boolean]$login = $false
)
if (-not $file) {
    Write-Host "Usage: SignFile.ps1 -file <path to file to sign>"
    exit 1
}

# if (-not (az account show --only-show-errors | Out-Null 2>&1)) {
#      Write-Host "Already logged in."
# } else {
if ($login) {
    az config set core.enable_broker_on_windows=false
    az login
    az account set --subscription "Pay-As-You-Go"
}

$args = @(
    "sign", "/v", "/debug", "/fd", "SHA256",
    "/tr", "http://timestamp.acs.microsoft.com",
    "/td", "SHA256",
    "/dlib", "$env:LOCALAPPDATA\Microsoft\MicrosoftTrustedSigningClientTools\Azure.CodeSigning.Dlib.dll",
    "/dmdf", ".\SignfileMetadata.json"
)
# Add non-empty file arguments
foreach ($f in @($file, $file1, $file2, $file3, $file4, $file5, $file6, $file7)) {
    if (![string]::IsNullOrWhiteSpace($f)) {
        $args += $f
    }
}

# Run signtool and capture the exit code
.\signtool.exe $args
$exitCode = $LASTEXITCODE

if ($exitCode -eq 0) {
    Write-Host "File(s) signed successfully." -ForegroundColor Green
    exit 0
} else {
    Write-Host "Signing failed with exit code: $exitCode" -ForegroundColor Red
    exit $exitCode
}
