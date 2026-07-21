using System.Collections.Generic;

namespace DevEnvStudio.Services.Backup.Models;

public sealed class CertificateSnapshot
{
    public List<CertificateInfo> Certificates { get; set; } = new();
}

public sealed class CertificateInfo
{
    public string Subject { get; set; } = string.Empty;

    public string Issuer { get; set; } = string.Empty;

    public string Thumbprint { get; set; } = string.Empty;

    public string NotAfter { get; set; } = string.Empty;
}