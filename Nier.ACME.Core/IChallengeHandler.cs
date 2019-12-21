using System.Threading.Tasks;

namespace Nier.ACME.Core
{
    public interface IChallengeHandler
    {
        Task<ChallengeDetails> GetChallengeDetailsByRequestPathAsync(string requestPath);

        Task CompleteChallengeValidationStatusAsync(string id);
    }
}