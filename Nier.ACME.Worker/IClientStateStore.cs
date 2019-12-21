using Nier.ACME.Core;

namespace Nier.ACME.Worker
{
    public interface IClientStateStore : IAccountStore, IChallengeStore, ICertStore, ICertReader
    {
    }
}