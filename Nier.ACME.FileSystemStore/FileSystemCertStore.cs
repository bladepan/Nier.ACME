using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nier.ACME.Core;
using Org.BouncyCastle.Crypto;
using X509Certificate = Org.BouncyCastle.X509.X509Certificate;

namespace Nier.ACME.FileSystemStore
{
    internal class FileSystemCertCollectionFormat
    {
        public IDictionary<string, FileSystemCertFormat> Certs { get; set; }
    }

    internal class FileSystemCertFormat
    {
        public string Val { get; set; }
        public long NotAfter { get; set; }
        public long NotBefore { get; set; }
    }

    internal class FileSystemKeysCollectionFormat
    {
        public IDictionary<string, FileSystemPrivateKeyFormat> Keys { get; set; }
    }

    internal class FileSystemPrivateKeyFormat
    {
        public string Val { get; set; }
        public long Expires { get; set; }
    }

    public class FileSystemCertStore : ICertReader, ICertStore
    {
        private IFileSystemOperations _fileSystem;
        private ILogger<FileSystemCertStore> _logger;

        private CertPrivateKeyCodec _privateKeyCodec = new CertPrivateKeyCodec();
        private BouncyCastleX509CertificateCodec _certCodec = new BouncyCastleX509CertificateCodec();

        public FileSystemCertStore(IFileSystemOperations fileSystem, ILogger<FileSystemCertStore> logger)
        {
            _fileSystem = fileSystem;
            _logger = logger;
        }

        public async Task<X509Certificate2> GetActiveCertificateAsync(long fromTimeStamp, long toTimeStamp)
        {
            if (toTimeStamp < fromTimeStamp)
            {
                throw new ArgumentOutOfRangeException(
                    $"Invalid time range. FromTimeStamp {fromTimeStamp}, ToTimeStamp {toTimeStamp}");
            }

            FileSystemPrivateKeyFormat keyFormat = null;
            FileSystemCertFormat certFormat = null;
            FileSystemCertCollectionFormat certsCollection = await GetFileSystemCertCollectionFormat();
            FileSystemKeysCollectionFormat keysCollection = await GetFileSystemKeyCollectionFormat();
            foreach (KeyValuePair<string, FileSystemCertFormat> keyValuePair in certsCollection.Certs)
            {
                string orderUrl = keyValuePair.Key;
                if (keysCollection.Keys.TryGetValue(orderUrl, out FileSystemPrivateKeyFormat currentPrivateKey))
                {
                    FileSystemCertFormat currentCert = keyValuePair.Value;
                    if (currentCert.NotAfter > fromTimeStamp && currentCert.NotBefore <= toTimeStamp)
                    {
                        if (certFormat == null || certFormat.NotAfter < currentCert.NotAfter)
                        {
                            certFormat = currentCert;
                            keyFormat = currentPrivateKey;
                        }
                    }
                }
            }

            if (certFormat == null)
            {
                return null;
            }

            byte[] certBytes = Convert.FromBase64String(certFormat.Val);
            X509Certificate certificate = _certCodec.Decode(certBytes);
            byte[] privateKeyBytes = Convert.FromBase64String(keyFormat.Val);
            CertPrivateKey privateKey = _privateKeyCodec.Decode(privateKeyBytes);
            return CertHelper.ToX509Certificate2(privateKey, certificate);
        }


        public async Task SavePrivateKeyAsync(string orderUrl, AsymmetricCipherKeyPair privateKey, long expires)
        {
            FileSystemKeysCollectionFormat keysFormat = await GetFileSystemKeyCollectionFormat();
            if (keysFormat.Keys.Count > 64)
            {
                _logger.LogInformation("Too many saved private keys, culling old records");
                IEnumerable<KeyValuePair<string, FileSystemPrivateKeyFormat>> oldRecords =
                    keysFormat.Keys.ToArray().OrderBy(i => i.Value.Expires).Take(32);
                foreach (KeyValuePair<string, FileSystemPrivateKeyFormat> oldRecord in oldRecords)
                {
                    _logger.LogInformation($"Remove private key {oldRecord.Key}");
                    keysFormat.Keys.Remove(oldRecord.Key);
                }
            }

            byte[] serialized = _privateKeyCodec.Encode(new CertPrivateKey
            {
                KeyPair = privateKey
            });
            string serializedStr = Convert.ToBase64String(serialized);
            keysFormat.Keys[orderUrl] = new FileSystemPrivateKeyFormat()
            {
                Expires = expires,
                Val = serializedStr
            };
            await SaveFileSystemKeyCollectionFormat(keysFormat);
        }

        public async Task SaveCertificateAsync(string orderUrl, X509Certificate certificate)
        {
            FileSystemCertCollectionFormat collectionFormat = await GetFileSystemCertCollectionFormat();

            if (collectionFormat.Certs.Count > 64)
            {
                _logger.LogInformation("Too many saved certs, culling old records");
                IEnumerable<KeyValuePair<string, FileSystemCertFormat>> oldRecords =
                    collectionFormat.Certs.ToArray().OrderBy(i => i.Value.NotAfter).Take(32);
                foreach (var oldRecord in oldRecords)
                {
                    _logger.LogInformation($"Remove cert {oldRecord.Key}");
                    collectionFormat.Certs.Remove(oldRecord.Key);
                }
            }

            byte[] serialized = _certCodec.Encode(certificate);
            string serializedStr = Convert.ToBase64String(serialized);
            collectionFormat.Certs[orderUrl] = new FileSystemCertFormat
            {
                NotAfter = new DateTimeOffset(certificate.NotAfter).ToUnixTimeMilliseconds(),
                NotBefore = new DateTimeOffset(certificate.NotBefore).ToUnixTimeMilliseconds(),
                Val = serializedStr
            };
            await SaveFileSystemCertCollectionFormat(collectionFormat);
        }

        private async Task<FileSystemCertCollectionFormat> GetFileSystemCertCollectionFormat()
        {
            string json = await _fileSystem.ReadStringAsync("certs.json");
            if (string.IsNullOrEmpty(json))
            {
                return new FileSystemCertCollectionFormat {Certs = new Dictionary<string, FileSystemCertFormat>()};
            }

            return JsonSerializer.Deserialize<FileSystemCertCollectionFormat>(json);
        }

        private Task SaveFileSystemCertCollectionFormat(FileSystemCertCollectionFormat fileSystemCertCollectionFormat)
        {
            string json = JsonSerializer.Serialize(fileSystemCertCollectionFormat);
            return _fileSystem.WriteStringAsync("certs.json", json);
        }

        private async Task<FileSystemKeysCollectionFormat> GetFileSystemKeyCollectionFormat()
        {
            string json = await _fileSystem.ReadStringAsync("keys.json");
            if (string.IsNullOrEmpty(json))
            {
                return new FileSystemKeysCollectionFormat {Keys = new Dictionary<string, FileSystemPrivateKeyFormat>()};
            }

            return JsonSerializer.Deserialize<FileSystemKeysCollectionFormat>(json);
        }

        private Task SaveFileSystemKeyCollectionFormat(FileSystemKeysCollectionFormat fileSystemCertCollectionFormat)
        {
            string json = JsonSerializer.Serialize(fileSystemCertCollectionFormat);
            return _fileSystem.WriteStringAsync("keys.json", json);
        }
    }
}