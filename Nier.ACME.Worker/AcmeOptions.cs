using System.Collections.Generic;

namespace Nier.ACME.Worker
{
    public class AcmeOptions
    {
        
        public const int DefaultRsaKeySize = 2048;
        public const int DefaultEcKeySize = 256;
        
        public IEnumerable<string> AccountContactEmails { get; set; }
        
        public bool AcceptTermsOfService { get; set; }
        
        public IEnumerable<string> DnsNames { get; set; }
        
        public int WaitForAuthorizations { get; set; } = 60;

        public int WaitForCertificate { get; set; } = 360;
        
        public string CertificateKeyAlgor { get; set; } = "ec";

        public int? CertificateKeySize { get; set; }
    }
}