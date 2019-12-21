namespace Nier.ACME.Core
{
    public class InvalidOrderStatusException: ACMEException
    {
        public InvalidOrderStatusException(string message) : base(message)
        {
        }
    }
}