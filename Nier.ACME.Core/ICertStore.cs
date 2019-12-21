using System.Threading.Tasks;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.X509;

namespace Nier.ACME.Core
{
    public interface ICertStore 
    {
        Task SavePrivateKeyAsync(string orderUrl, AsymmetricCipherKeyPair privateKey, long expires);

        Task SaveCertificateAsync(string orderUrl, X509Certificate certificate);
    }
}