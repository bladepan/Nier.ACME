using System;
using IACMESharpChallengeValidationDetails = ACMESharp.Authorizations.IChallengeValidationDetails;
using ACMESharpAuthorizationDecoder = ACMESharp.Authorizations.AuthorizationDecoder;
using ACMESharpHttp01ChallengeValidationDetails = ACMESharp.Authorizations.Http01ChallengeValidationDetails;
using ACMESharpClient = ACMESharp.Protocol.AcmeProtocolClient;
using ACMESharpOrderDetails = ACMESharp.Protocol.OrderDetails;
using ACMESharpAuthorization = ACMESharp.Protocol.Resources.Authorization;
using ACMESharpChallenge = ACMESharp.Protocol.Resources.Challenge;

namespace Nier.ACME.Core
{
    public enum ChallengeStatus
    {
        None,
        Pending,
        Processing,
        Valid,
        Invalid
    }

    public class ChallengeDetails
    {
        public string Id { get; set; }

        public ChallengeValidationStatus ValidationStatus { get; set; }
        
        public ChallengeStatus Status { get; set; }

        public ChallengeType ChallengeType { get; set; }

        public object Error { get; set; }


        public string Url { get; set; }


        public string HttpResourceUrl { get; set; }

        public string HttpResourcePath { get; set; }

        public string HttpResourceContentType { get; set; }

        public string HttpResourceValue { get; set; }
        
        public long Expires { get; set; }

        public ChallengeDetails()
        {
        }

        public ChallengeDetails(ACMESharpChallenge acmeSharpChallenge,
            IACMESharpChallengeValidationDetails acmeSharpChallengeValidationDetails, long expires)
        {
            if (Enum.TryParse(acmeSharpChallenge.Status, out ChallengeStatus status))
            {
                // testing server may pass "testing"
                Status = status;
            }

            Error = acmeSharpChallenge.Error;
            Url = acmeSharpChallenge.Url;
            Expires = expires;

            ChallengeType = ChallengeTypeMethods.ParseFromString(acmeSharpChallengeValidationDetails.ChallengeType);
            switch (acmeSharpChallengeValidationDetails)
            {
                case ACMESharpHttp01ChallengeValidationDetails acmeSharpHttpChallengeDetail:
                    HttpResourceUrl = acmeSharpHttpChallengeDetail.HttpResourceUrl;
                    HttpResourcePath = acmeSharpHttpChallengeDetail.HttpResourcePath;
                    HttpResourceContentType = acmeSharpHttpChallengeDetail.HttpResourceContentType;
                    HttpResourceValue = acmeSharpHttpChallengeDetail.HttpResourceValue;
                    break;
            }
        }

        public void AssertOkResponse()
        {
            if (Error != null)
            {
                throw new ACMEException($"Challenge error {Error}");
            }

            if (Status == ChallengeStatus.Invalid)
            {
                throw new ACMEException($"Challenge status invalid");
            }
        }
    }
}