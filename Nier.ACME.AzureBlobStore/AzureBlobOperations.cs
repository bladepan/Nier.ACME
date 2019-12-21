using System.Threading.Tasks;
using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Blob;
using Microsoft.Extensions.Logging;
using Nier.ACME.FileSystemStore;

namespace Nier.ACME.AzureBlobStore
{
    public class AzureBlobOperations : IFileSystemOperations
    {
        private readonly CloudBlobContainer _blobContainer;
        private readonly ILogger<AzureBlobOperations> _logger;
        private readonly string _prefix;

        public AzureBlobOperations(CloudBlobContainer blobContainer,
            AzureBlobOptions options,
            ILogger<AzureBlobOperations> logger)
        {
            _blobContainer = blobContainer;
            _prefix = options.PathPrefix;
            _logger = logger;
        }

        public Task WriteStringAsync(string file, string str)
        {
            var blobPath = $"{_prefix}{file}";
            _logger.LogInformation($"writing blob {blobPath}");
            CloudBlockBlob blobRef = _blobContainer.GetBlockBlobReference(blobPath);
            return blobRef.UploadTextAsync(str);
        }

        public async Task<string> ReadStringAsync(string file)
        {
            var blobPath = $"{_prefix}{file}";
            _logger.LogInformation($"reading blob {blobPath}");
            CloudBlockBlob blobRef = _blobContainer.GetBlockBlobReference(blobPath);
            try
            {
                return await blobRef.DownloadTextAsync();
            }
            catch (StorageException e)
            {
                if (e.RequestInformation.HttpStatusCode == 404)
                {
                    _logger.LogInformation($"blob {blobPath} does not exist. return null.");
                    return null;
                }

                throw;
            }
        }

        public Task DeleteAsync(string file)
        {
            var blobPath = $"{_prefix}{file}";
            _logger.LogInformation($"reading blob {blobPath}");
            CloudBlockBlob blobRef = _blobContainer.GetBlockBlobReference(blobPath);
            return blobRef.DeleteIfExistsAsync();
        }
    }
}