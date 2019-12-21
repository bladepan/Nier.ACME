using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nier.ACME.Core;

namespace Nier.ACME.FileSystemStore
{
    internal class FSChallengeFormat
    {
        public IDictionary<string, ChallengeDetails> Challenges { get; set; }
    }

    public class FileSystemChallengeStore : IChallengeStore, IChallengeHandler
    {
        private IFileSystemOperations _fileSystem;
        private ILogger<FileSystemChallengeStore> _logger;

        public FileSystemChallengeStore(IFileSystemOperations fileSystem, ILogger<FileSystemChallengeStore> logger)
        {
            _fileSystem = fileSystem;
            _logger = logger;
        }

        public async Task<ChallengeValidationStatus> GetChallengeValidationStatusAsync(string id)
        {
            FSChallengeFormat fsChallengeFormat = await GetFSChallengeFormat();
            if (fsChallengeFormat.Challenges.TryGetValue(id,
                out ChallengeDetails challengeDetails))
            {
                return challengeDetails.ValidationStatus;
            }

            return ChallengeValidationStatus.None;
        }

        public async Task<string> SaveChallengeValidationDetailsAsync(ChallengeDetails challengeDetails)
        {
            string id = challengeDetails.Id;
            if (string.IsNullOrEmpty(id))
            {
                if (!string.IsNullOrEmpty(challengeDetails.HttpResourceUrl))
                {
                    id = challengeDetails.HttpResourceUrl;
                }
                else
                {
                    id = Guid.NewGuid().ToString("N");
                }

                challengeDetails.Id = id;
            }

            FSChallengeFormat fsChallengeFormat = await GetFSChallengeFormat();
            if (fsChallengeFormat.Challenges.Count > 64)
            {
                // culling old records
                _logger.LogInformation($"too many saved challenges, culling old records");
                IEnumerable<ChallengeDetails> oldRecords =
                    fsChallengeFormat.Challenges.Values.OrderBy(i => i.Expires).Take(32);
                foreach (ChallengeDetails oldRecord in oldRecords)
                {
                    fsChallengeFormat.Challenges.Remove(oldRecord.Id);
                }
            }

            fsChallengeFormat.Challenges[id] = challengeDetails;
            await SaveFSChallengeFormat(fsChallengeFormat);
            return id;
        }

        public async Task<ChallengeDetails> GetChallengeDetailsByRequestPathAsync(string requestPath)
        {
            FSChallengeFormat fsChallengeFormat = await GetFSChallengeFormat();
            string normalizedRequestPath = requestPath.Trim('/');
            foreach (KeyValuePair<string, ChallengeDetails> keyValuePair in fsChallengeFormat.Challenges)
            {
                if (keyValuePair.Value.HttpResourcePath.Trim('/') == normalizedRequestPath)
                {
                    return keyValuePair.Value;
                }
            }

            return null;
        }

        public async Task CompleteChallengeValidationStatusAsync(string id)
        {
            FSChallengeFormat fsChallengeFormat = await GetFSChallengeFormat();
            if (fsChallengeFormat.Challenges.TryGetValue(id,
                out ChallengeDetails challengeDetails))
            {
                challengeDetails.ValidationStatus = ChallengeValidationStatus.Validated;
                await SaveFSChallengeFormat(fsChallengeFormat);
                return;
            }

            throw new ArgumentException($"Cannot get challenge with id {id}");
        }

        private async Task<FSChallengeFormat> GetFSChallengeFormat()
        {
            string json = await _fileSystem.ReadStringAsync("challenges.json");
            if (string.IsNullOrEmpty(json))
            {
                return new FSChallengeFormat()
                {
                    Challenges = new Dictionary<string, ChallengeDetails>()
                };
            }

            return JsonSerializer.Deserialize<FSChallengeFormat>(json);
        }

        private Task SaveFSChallengeFormat(FSChallengeFormat fsChallengeFormat)
        {
            string json = JsonSerializer.Serialize(fsChallengeFormat);
            return _fileSystem.WriteStringAsync("challenges.json", json);
        }
    }
}