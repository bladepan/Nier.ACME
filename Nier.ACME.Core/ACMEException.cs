using System;

namespace Nier.ACME.Core
{
    public class ACMEException: Exception
    {
        public ACMEException(string message) : base(message)
        {
        }
    }
}