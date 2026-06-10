$cert = New-SelfSignedCertificate `
  -Type Custom `
  -Subject "CN=BuningSoftware" `
  -KeyUsage DigitalSignature `
  -FriendlyName "UnifiProtectClient" `
  -CertStoreLocation "Cert:\CurrentUser\My" `
  -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}")

$password = Read-Host -Prompt "Enter certificate password" -AsSecureString
$certPath = Join-Path $PSScriptRoot "cert.pfx"
Export-PfxCertificate -Cert $cert -FilePath $certPath -Password $password

# Remove from certificate store — only the .pfx file is needed
Remove-Item -Path "Cert:\CurrentUser\My\$($cert.Thumbprint)"

[Convert]::ToBase64String([IO.File]::ReadAllBytes($certPath)) | Set-Clipboard

Write-Host "Done! Base64 copied to clipboard. Add it as the CERTIFICATE_BASE64 secret on GitHub."