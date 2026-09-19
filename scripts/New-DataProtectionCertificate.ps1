#requires -Version 7.0
# Run manually once. Does not configure User Secrets, Render or the database.
$ErrorActionPreference = 'Stop'
$certificateDirectory = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'GuiaDoGit'
$certificateFile = Join-Path $certificateDirectory 'dataprotection-secrets.json'
if (Test-Path -LiteralPath $certificateFile) {
    throw 'O arquivo já existe. Preserve o certificado atual; não gere outro para o mesmo keyring.'
}
$null = New-Item -ItemType Directory -Path $certificateDirectory -Force
$certificatePassword = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
$certificateRsa = [Security.Cryptography.RSA]::Create(3072)
try {
    $certificateRequest = [Security.Cryptography.X509Certificates.CertificateRequest]::new(
        'CN=GuiaDoGit Data Protection', $certificateRsa,
        [Security.Cryptography.HashAlgorithmName]::SHA256, [Security.Cryptography.RSASignaturePadding]::Pkcs1)
    $certificate = $certificateRequest.CreateSelfSigned([DateTimeOffset]::UtcNow.AddDays(-1), [DateTimeOffset]::UtcNow.AddYears(5))
    try {
        $certificateBytes = $certificate.Export([Security.Cryptography.X509Certificates.X509ContentType]::Pfx, $certificatePassword)
        $certificateJson = @{
            'DataProtection:CertificateBase64' = [Convert]::ToBase64String($certificateBytes)
            'DataProtection:CertificatePassword' = $certificatePassword
        } | ConvertTo-Json
        $certificateStream = [IO.File]::Open($certificateFile, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
        try {
            $certificateWriter = [IO.StreamWriter]::new($certificateStream, [Text.UTF8Encoding]::new($false))
            try { $certificateWriter.Write($certificateJson) } finally { $certificateWriter.Dispose() }
        } finally { $certificateStream.Dispose() }
    } finally { $certificate.Dispose() }
} finally { $certificateRsa.Dispose() }
Write-Host "Arquivo privado criado fora do repositório: $certificateFile"
Write-Host 'Guarde uma cópia em armazenamento privado. O conteúdo não foi exibido nem aplicado a nenhum ambiente.'
