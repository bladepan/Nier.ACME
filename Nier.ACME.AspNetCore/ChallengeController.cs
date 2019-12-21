using System.Threading.Tasks;
using ACMESharp.Authorizations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Nier.ACME.Core;

namespace Nier.ACME.AspNetCore
{
    /// <summary>
    /// see https://tools.ietf.org/html/draft-ietf-acme-acme-12#section-8.3
    /// </summary>
    [Route(Http01ChallengeValidationDetails.HttpPathPrefix)]
    public class ChallengeController : Controller
    {
        private readonly IChallengeHandler _challengeHandler;
        private readonly ILogger<ChallengeController> _logger;

        public ChallengeController(IChallengeHandler challengeHandler, ILogger<ChallengeController> logger)
        {
            _challengeHandler = challengeHandler;
            _logger = logger;
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<string>> HandleChallengeAsync(string id)
        {
            string fullPath = Request.Path;
            _logger.LogInformation($"receive challenge request for {fullPath}");
            ChallengeDetails challengeState =
                await _challengeHandler.GetChallengeDetailsByRequestPathAsync(fullPath);
            if (challengeState != null)
            {
                _logger.LogInformation($"find challenge detail for {fullPath}.");
                await _challengeHandler.CompleteChallengeValidationStatusAsync(challengeState.Id);

                return new ContentResult()
                {
                    Content = challengeState.HttpResourceValue,
                    ContentType = challengeState.HttpResourceContentType
                };
            }
            return NotFound("NotFound");
        }
    }
}