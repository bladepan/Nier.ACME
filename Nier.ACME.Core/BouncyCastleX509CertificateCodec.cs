using System.IO;
using Org.BouncyCastle.X509;

namespace Nier.ACME.Core
{
    public class BouncyCastleX509CertificateCodec
    {
        public X509Certificate Decode(byte[] bytes)
        {
            using (var crtStream = new MemoryStream(bytes))
            {
                X509Certificate cert = CertHelper.ImportCertificate(EncodingFormat.PEM, crtStream);
                return cert;
            }
        }

        public byte[] Encode(X509Certificate cert)
        {
            using (var stream = new MemoryStream())
            {
                CertHelper.ExportCertificate(cert, EncodingFormat.PEM, stream);
                stream.Position = 0;
                return stream.ToArray();
            }
        }
    }
}