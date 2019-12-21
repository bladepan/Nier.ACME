using System;
using ACMESharp.Crypto.JOSE;
using ACMESharp.Protocol;
using ACMESharp.Protocol.Resources;


namespace Nier.ACME.Core
{
    /// <summary>
    /// https://tools.ietf.org/html/draft-ietf-acme-acme-12#section-7.1.6
    /// </summary>
    public enum AccountStatus
    {
        None, Valid, Deactivated, Revoked
    }

    public class AccountDetails
    {
        public string Kid { get; set; }

        public string TosLink { get; set; }

        public string Id { get; set; }

        public string[] Contact { get; set; }

        public AccountStatus Status { get; set; }

        public bool TermsOfServiceAgreed { get; set; }

        public string OrdersUrl { get; set; }

        public string InitialIp { get; set; }

        public string CreatedAt { get; set; }

        public string Agreement { get; set; }

        public string KeyType { get; set; }
        public string KeyExport { get; set; }

        public AccountDetails()
        {
        }

        public AccountDetails(ACMESharp.Protocol.AccountDetails accountDetails, IJwsTool clientSigner)
        {
            Kid = accountDetails.Kid;
            TosLink = accountDetails.TosLink;

            Account accountPayload = accountDetails.Payload;
            if (null == accountPayload)
            {
                throw new ArgumentNullException("accountDetails.Payload");
            }

            Id = accountPayload.Id;
            Contact = accountPayload.Contact;
            // test server returns testing in status
            if (Enum.TryParse<AccountStatus>(accountPayload.Status, true, out AccountStatus accountStatus))
            {
                Status = accountStatus;
            }

            TermsOfServiceAgreed = accountPayload.TermsOfServiceAgreed ?? false;
            OrdersUrl = accountPayload.Orders;
            InitialIp = accountPayload.InitialIp;
            CreatedAt = accountPayload.CreatedAt;
            Agreement = accountPayload.Agreement;

            KeyType = clientSigner.JwsAlg;
            KeyExport = clientSigner.Export();
        }
    }
}