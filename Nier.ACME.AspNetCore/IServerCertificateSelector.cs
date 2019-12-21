using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Connections;

namespace Nier.ACME.AspNetCore
{
    public interface IServerCertificateSelector
    {
        X509Certificate2 SelectServerCertificate(ConnectionContext connectionContext, string hostName);
    }
}