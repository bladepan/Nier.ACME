using System;

namespace Nier.ACME.Worker
{
    public class ACMEClientException: Exception
    {
        public ACMEClientException(string message) : base(message)
        {
        }
    }
}