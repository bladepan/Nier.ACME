using System;
using ACMESharp.Authorizations;

namespace Nier.ACME.Core
{
    public enum ChallengeType
    {
        None,
        Http01,
        Dns01
    }

    public static class ChallengeTypeMethods
    {
        public static ChallengeType ParseFromString(string s)
        {
            if (TryParseFromString(s, out ChallengeType result))
            {
                return result;
            }

            throw new ArgumentException($"Invalid {nameof(ChallengeType)} {s}");
        }

        public static bool TryParseFromString(string s, out ChallengeType type)
        {
            type = ChallengeType.None;
            if (Http01ChallengeValidationDetails.Http01ChallengeType == s)
            {
                type = ChallengeType.Http01;
                return true;
            }

            if (Dns01ChallengeValidationDetails.Dns01ChallengeType == s)
            {
                type = ChallengeType.Dns01;
                return true;
            }

            return false;
        }
    }
}