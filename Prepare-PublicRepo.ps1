$ErrorActionPreference = "Stop"

$name = git config user.name
$email = git config user.email

if ([string]::IsNullOrWhiteSpace($name) -or [string]::IsNullOrWhiteSpace($email)) {
    Write-Output "Git identity is not configured for this repo."
    Write-Output "Run these with your real GitHub name/email, then rerun this script:"
    Write-Output '  git config user.name "Your Name"'
    Write-Output '  git config user.email "you@example.com"'
    exit 1
}

Write-Output "Checking publishable files..."
git ls-files --others --exclude-standard | Out-File -Encoding utf8 .publishable-files.txt

$secretMatches = rg -n "AIza|baf1d6|sk-|OPENAI_API_KEY" `
    -g "!tools/**" `
    -g "!dist/**" `
    -g "!packages/**" `
    -g "!data/settings.json" `
    -g "!docs/incoming/**" `
    -g "!Prepare-PublicRepo.ps1" `
    -g "!src/LightWorship/bin/**" `
    -g "!src/LightWorship/obj/**" `
    -g "!tests/LightWorship.Tests/bin/**" `
    -g "!tests/LightWorship.Tests/obj/**" `
    2>$null

if (-not [string]::IsNullOrWhiteSpace($secretMatches)) {
    Write-Output "Potential secret-like text found. Review before committing:"
    Write-Output $secretMatches
    exit 1
}

git add .gitignore BUILD_CHECKLIST.md CODE_OF_CONDUCT.md CONTRIBUTING.md LICENSE LightWorship.sln OPENAI_OSS_APPLICATION_PACKET.md PUBLISH_TO_GITHUB.md Prepare-PublicRepo.ps1 README.md SECURITY.md TOOLING.md data docs installer src tests
git commit -m "Initial open source release"

Write-Output "Local commit created."
Write-Output "Next: create a public GitHub repo, then run:"
Write-Output "  git remote add origin https://github.com/YOUR_USERNAME/LightWorship.git"
Write-Output "  git branch -M main"
Write-Output "  git push -u origin main"
