using System.IO;

namespace Nier.ACME.Core
{
    public class CertPrivateKeyCodec
    {
        public byte[] Encode(CertPrivateKey key)
        {
            using (var keyPem = new MemoryStream())
            {
                CertHelper.ExportPrivateKey(key, EncodingFormat.PEM, keyPem);
                keyPem.Position = 0L;
                return keyPem.ToArray();
            }
        }

        public CertPrivateKey Decode(byte[] bytes)
        {
            using (var stream = new MemoryStream(bytes))
            {
                return CertHelper.ImportPrivateKey(EncodingFormat.PEM, stream);
            }
        }
    }
}