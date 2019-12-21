using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommandLine;
using CommandLine.Text;
using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Blob;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nier.ACME.AzureBlobStore;
using Nier.ACME.Core;
using Nier.ACME.FileSystemStore;
using Nier.ACME.Worker;

namespace Nier.ACME.Cli
{
    public enum StorageType
    {
        None,
        LocalFs,
        AzureBlob
    }

    public class Options
    {
        [Option("caUrl", Required = false,
            HelpText = "CA url. Pass https://acme-staging-v02.api.letsencrypt.org for letsencrypt staging environment.",
            Default = "https://acme-v02.api.letsencrypt.org")]
        public string CaUrl { get; set; }

        [Option("accountContactEmails", Required = true, Separator = ',',
            HelpText = "Contact emails. Separated by ','.")]
        public IEnumerable<string> AccountContactEmails { get; set; }

        [Option("dnsNames", Required = true, Separator = ',', HelpText = "DNS names. Separated by ','.")]
        public IEnumerable<string> DnsNames { get; set; }

        [Option("storageType", Required = true, HelpText = "Storage type for storing certificates.")]
        public StorageType StorageType { get; set; }

        [Option("azureKeyVaultUrl", Required = false,
            HelpText = "Azure KeyVault used to store secrets. Required when storageType is azureBlob.")]
        public string AzureKeyVaultUrl { get; set; }

        [Option("azureStorageConnectionStringConfigPath", Required = false,
            HelpText =
                "Configuration path for azure storage connection string. Required when storageType is azureBlob. If the config value is stored in keyVault, replace '--' in secret name with ':'.")]
        public string AzureStorageConnectionStringConfigPath { get; set; }

        [Option("azureBlobContainerName", Required = false,
            HelpText = "Azure blob container name")]
        public string AzureBlobContainerName { get; set; }

        [Option("azureBlobPathPrefix", Required = false,
            HelpText = "Azure blob path prefix where certificates are stored.", Default = "NierACMEv0/")]
        public string AzureBlobPathPrefix { get; set; }

        [Option("localFsWorkingDirectory", Required = false,
            HelpText = "The directory where certificates are stored when storageType is LocalFs.",
            Default = "NierACMEv0/")]
        public string LocalFsWorkingDirectory { get; set; }

        [Usage]
        public static IEnumerable<Example> Examples
        {
            get
            {
                yield return new Example("Use azure blob", new Options
                {
                    CaUrl = "https://acme-v02.api.letsencrypt.org",
                    AccountContactEmails = new[] {"admin@yourdomain.com", "dev@yourdomain.com"},
                    DnsNames = new[] {"yourdomain.com", "www.yourdomain.com"},
                    StorageType = StorageType.AzureBlob,
                    AzureKeyVaultUrl = "https://yourVault.vault.azure.net/",
                    AzureStorageConnectionStringConfigPath = "ConnectionStrings:StorageConnectionString",
                    AzureBlobContainerName = "mySecretBlob",
                    AzureBlobPathPrefix = "NierACMEv0/"
                });
                yield return new Example("Use local directory", new Options
                {
                    CaUrl = "https://acme-v02.api.letsencrypt.org",
                    AccountContactEmails = new[] {"admin@yourdomain.com", "dev@yourdomain.com"},
                    DnsNames = new[] {"yourdomain.com", "www.yourdomain.com"},
                    StorageType = StorageType.LocalFs,
                    LocalFsWorkingDirectory = "NierACMEv0/"
                });
            }
        }
    }

    public class Program
    {
        private readonly Options _options;
        private readonly ServiceCollection _services;

        public Program(Options options)
        {
            _options = options;
            switch (_options.StorageType)
            {
                case StorageType.AzureBlob:
                    if (_options.AzureKeyVaultUrl == null)
                    {
                        throw new ArgumentNullException(nameof(_options.AzureKeyVaultUrl));
                    }

                    if (_options.AzureStorageConnectionStringConfigPath == null)
                    {
                        throw new ArgumentNullException(nameof(_options.AzureStorageConnectionStringConfigPath));
                    }

                    if (_options.AzureBlobContainerName == null)
                    {
                        throw new ArgumentNullException(nameof(_options.AzureBlobContainerName));
                    }

                    if (_options.AzureBlobPathPrefix == null)
                    {
                        throw new ArgumentNullException(nameof(_options.AzureBlobPathPrefix));
                    }

                    break;

                case StorageType.None:
                    throw new ArgumentException("Invalid value", nameof(_options.StorageType));
            }

            _services = new ServiceCollection();
        }

        public async Task ExecAsync()
        {
            var configBuilder = new ConfigurationBuilder();
            if (_options.AzureKeyVaultUrl != null)
            {
                configBuilder.AddAzureKeyVault(_options.AzureKeyVaultUrl);
            }

            var config = configBuilder.Build();
            _services.AddLogging((builder => { builder.AddConsole(); }));
            _services.AddSingleton<IConfiguration>(config);

            switch (_options.StorageType)
            {
                case StorageType.AzureBlob:
                    SetupAzureBlobStore();
                    break;
                case StorageType.LocalFs:
                    SetupLocalFsStore();
                    break;
                default:
                    throw new ArgumentException($"Invalid storage type {_options.StorageType}");
            }

            _services.AddSingleton<IAccountStore, FileSystemAccountStore>();
            _services.AddSingleton<IChallengeStore, FileSystemChallengeStore>();
            _services.AddSingleton<ICertStore, FileSystemCertStore>();
            _services.AddSingleton<ICertReader, FileSystemCertStore>();
            _services.AddSingleton<IClientStateStore, DelegatedClientStateStore>();
            _services.AddSingleton((provider) =>
            {
                var client = new AcmeClient(_options.CaUrl, CreateLogger<AcmeClient>(provider));
                client.InitAsync().Wait();
                return client;
            });
            _services.AddSingleton((provider) => new AcmeOptions
            {
                AcceptTermsOfService = true,
                AccountContactEmails = _options.AccountContactEmails,
                DnsNames = _options.DnsNames,
                CertificateKeyAlgor = "rsa"
            });
            _services.AddSingleton<ACMEWorker>();

            var serviceProvider = _services.BuildServiceProvider();
            var worker = serviceProvider.GetService(typeof(ACMEWorker)) as ACMEWorker;

            await worker.WorkAsync();
            await serviceProvider.DisposeAsync();
        }

        private void SetupAzureBlobStore()
        {
            _services.AddSingleton<IFileSystemOperations>((provider) =>
            {
                string connectionString = GetConfigString(provider, _options.AzureStorageConnectionStringConfigPath);
                var storageAccount = CloudStorageAccount.Parse(
                    connectionString);
                CloudBlobClient cloudBlobClient = storageAccount.CreateCloudBlobClient();
                CloudBlobContainer cloudBlobContainer =
                    cloudBlobClient.GetContainerReference(_options.AzureBlobContainerName);
                var azureBlobOptions = new AzureBlobOptions
                {
                    PathPrefix = _options.AzureBlobPathPrefix
                };
                return new AzureBlobOperations(cloudBlobContainer, azureBlobOptions,
                    CreateLogger<AzureBlobOperations>(provider));
            });
        }

        private void SetupLocalFsStore()
        {
            _services.AddSingleton<IFileSystemOperations>((provider) =>
            {
                return new LocalFileSystemOperations(_options.LocalFsWorkingDirectory);
            });
        }

        private string GetConfigString(IServiceProvider serviceProvider, string configPath)
        {
            var config = serviceProvider.GetService<IConfiguration>();
            return config[configPath];
        }

        private ILogger<T> CreateLogger<T>(IServiceProvider serviceProvider)
        {
            var loggerFactory = serviceProvider.GetService<ILoggerFactory>();
            return loggerFactory.CreateLogger<T>();
        }

        static void Main(string[] args)
        {
            var parserResult = Parser.Default.ParseArguments<Options>(args);
            parserResult.WithParsed(options =>
            {
                var program = new Program(options);
                program.ExecAsync().Wait();
            });
        }
    }
}