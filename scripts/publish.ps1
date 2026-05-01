# VibeVoice Studio Publish Script
# 実行ファイル、モデル、辞書を一箇所に集約します。

$publishDir = ".\publish"
if (Test-Path $publishDir) { Remove-Item -Recurse -Force $publishDir }
New-Item -ItemType Directory -Path $publishDir

Write-Host "Building project..." -ForegroundColor Cyan
dotnet publish VibeVoiceNative.UI\VibeVoiceNative.UI.csproj -c Release -r win-x64 -p:Platform=x64 -o "$publishDir\bin" --self-contained true

Write-Host "Copying models and dictionaries..." -ForegroundColor Cyan
$modelSource = "E:\app\VibeVoiceStudio\publish_v1.1.1_final_v4\models"
if (Test-Path $modelSource) {
    Copy-Item -Path $modelSource -Destination $publishDir -Recurse -Force
}
$dicSource = "E:\app\VibeVoiceStudio\publish_v1.1.1_final_v4\dic"
if (Test-Path $dicSource) {
    Copy-Item -Path $dicSource -Destination $publishDir -Recurse -Force
}

# 起動用ランチャー (バッチファイル) の作成
# カレントディレクトリを bin\ に設定してから起動（ネイティブDLL探索パス解決のため）
$launcherContent = "@echo off`r`ncd /d `"%~dp0bin`"`r`nstart `"`" `"VibeVoiceNative.UI.exe`"`r`n"
[System.IO.File]::WriteAllText("$publishDir\VibeVoiceStudio.bat", $launcherContent, [System.Text.Encoding]::ASCII)

Write-Host "Publish complete! Output at: $publishDir" -ForegroundColor Green
Write-Host "To run: Execute VibeVoiceStudio.bat"
