# VibeVoice Studio Publish Script
# 実行ファイル群を 'app' フォルダに集約し、ルートをスッキリさせます。

$publishDir = ".\publish_portable_v1.1.2"
if (Test-Path $publishDir) { Remove-Item -Recurse -Force $publishDir }
New-Item -ItemType Directory -Path $publishDir
New-Item -ItemType Directory -Path "$publishDir\app"
New-Item -ItemType Directory -Path "$publishDir\logs"

Write-Host "Building project into app/ folder..." -ForegroundColor Cyan
dotnet publish VibeVoiceNative.UI\VibeVoiceNative.UI.csproj -c Release -r win-x64 -p:Platform=x64 -o "$publishDir\app" --self-contained true

Write-Host "Copying models and dictionaries to root..." -ForegroundColor Cyan
# 最新のモデルディレクトリを指定（適宜調整）
$modelSource = "E:\app\VibeVoiceStudio\publish_v1.1.1_final_v26\models"
if (Test-Path $modelSource) {
    Copy-Item -Path $modelSource -Destination "$publishDir\models" -Recurse -Force
}
$dicSource = "E:\app\VibeVoiceStudio\publish_v1.1.1_final_v26\dic"
if (Test-Path $dicSource) {
    Copy-Item -Path $dicSource -Destination "$publishDir\dic" -Recurse -Force
}

# ドキュメントのコピー
if (Test-Path "SPECIFICATION.md") { Copy-Item "SPECIFICATION.md" "$publishDir\仕様書.md" }

# 起動用ランチャー (バッチファイル) の作成
$launcherContent = "@echo off`r`necho VibeVoice Studio を起動しています...`r`nstart `"`" `"%~dp0app\VibeVoiceNative.UI.exe`"`r`n"
[System.IO.File]::WriteAllText("$publishDir\VibeVoiceStudio.bat", $launcherContent, [System.Text.Encoding]::ASCII)

Write-Host "Publish complete! Output at: $publishDir" -ForegroundColor Green
Write-Host "Structure:"
Write-Host "  - VibeVoiceStudio.bat (Launcher)"
Write-Host "  - app/ (System files)"
Write-Host "  - models/ (AI Models)"
Write-Host "  - dic/ (Dictionaries)"
Write-Host "  - logs/ (Application logs)"

