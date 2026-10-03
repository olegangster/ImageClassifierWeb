# Запускати з кореня рішення (папка, де лежить ImageClassifierWeb):
#   powershell -ExecutionPolicy Bypass -File deploy\make-eb-zip.ps1
$ErrorActionPreference = "Stop"

if (Test-Path publish) { Remove-Item publish -Recurse -Force }
if (Test-Path ImageClassifierWeb-eb.zip) { Remove-Item ImageClassifierWeb-eb.zip -Force }

dotnet publish ImageClassifierWeb\ImageClassifierWeb.csproj -c Release -r linux-x64 --self-contained false -o publish

# Перевірка, що все потрібне потрапило в публікацію
$need = @("MLModel1.mlnet", "ImageClassifierWeb.dll", ".platform\nginx\conf.d\proxy.conf")
foreach ($f in $need) {
    if (-not (Test-Path (Join-Path publish $f))) { throw "У publish немає файлу: $f" }
}
if (-not (Get-ChildItem publish -Recurse -Filter "libtensorflow*.so*" -ErrorAction SilentlyContinue)) {
    Write-Warning "Не знайдено libtensorflow*.so у publish (runtimes/linux-x64/native). Перевірте пакет SciSharp.TensorFlow.Redist."
}

# tar (є у Windows 10+) створює zip зі слешами '/', на відміну від Compress-Archive у Windows PowerShell 5
tar -a -c -f ImageClassifierWeb-eb.zip -C publish .
Write-Host "Готово: ImageClassifierWeb-eb.zip  -> завантажте в Elastic Beanstalk"
