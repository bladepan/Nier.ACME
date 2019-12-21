using System;
using System.Collections.Generic;
using Nier.ACME.Core;
using ACMESharpId = ACMESharp.Protocol.Resources.Identifier;
using ACMESharpAuthorization = ACMESharp.Protocol.Resources.Authorization;


namespace Nier.ACME.Worker
{
    public enum AuthorizationStatus
    {
        None,
        Pending,
        Processing,
        Valid,
        Invalid
    }

    public class AuthorizationDetails
    {
        public ACMESharpId Identifier { get; set; }
        public AuthorizationStatus Status { get; set; }
        public long Expires { get; set; }
        public IEnumerable<ChallengeDetails> Challenges { get; set; }

        public bool Wildcard { get; set; }

        public AuthorizationDetails()
        {
        }

        public AuthorizationDetails(ACMESharpAuthorization acmeSharpAuthorization)
        {
            Identifier = acmeSharpAuthorization.Identifier;
            Status = Enum.Parse<AuthorizationStatus>(acmeSharpAuthorization.Status, true);
            Expires = DateTimeOffset.Parse(acmeSharpAuthorization.Expires).ToUnixTimeMilliseconds();
            Wildcard = acmeSharpAuthorization.Wildcard ?? false;
        }
    }
}