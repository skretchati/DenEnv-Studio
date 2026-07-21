using System;
using System.Security.Cryptography.X509Certificates;
using DevEnvStudio.Services.Backup.Models;

namespace DevEnvStudio.Services.Backup;

public sealed class CertificateSnapshotService
{
    private static readonly Lazy<CertificateSnapshotService> _instance =
        new(() => new CertificateSnapshotService());

    public static CertificateSnapshotService Instance => _instance.Value;

    private CertificateSnapshotService()
    {
    }

    public CertificateSnapshot Collect()
    {
        var snapshot = new CertificateSnapshot();

        try
        {
            using var store =
                new X509Store(
                    StoreName.My,
                    StoreLocation.CurrentUser);

            store.Open(OpenFlags.ReadOnly);

            foreach (var cert in store.Certificates)
            {
                try
                {
                    snapshot.Certificates.Add(
                        new CertificateInfo
                        {
                            Subject = cert.Subject,
                            Issuer = cert.Issuer,
                            Thumbprint = cert.Thumbprint ?? string.Empty,
                            NotAfter = cert.NotAfter.ToString("yyyy-MM-dd")
                        });
                }
                catch
                {
                }
            }
        }
        catch
        {
        }

        return snapshot;
    }
}