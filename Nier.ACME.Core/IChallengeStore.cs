using System.Threading.Tasks;

namespace Nier.ACME.Core
{
    public interface IChallengeStore
    {
        Task<ChallengeValidationStatus> GetChallengeValidationStatusAsync(string id);

        Task<string> SaveChallengeValidationDetailsAsync(ChallengeDetails challengeDetails);
    }
}