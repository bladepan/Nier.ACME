Nier.ACME
------------------
Nier.ACME is a collection of libraries that help you to manage [ACME](https://letsencrypt.org/how-it-works/) processes. It is built on top of [ACMESharpCore](https://github.com/PKISharp/ACMESharpCore). A lot of code is from [ACMEKestrel example](https://github.com/PKISharp/ACMESharpCore/tree/master/src/examples/ACMEKestrel).

Nier.ACME provides abstraction layers of running ACME client, answers ACME challenges, loading certificates and storing ACME states. It is possible to
- Run ACME client on your laptop or as a cloud function instead of run it on production servers. Nier.ACME provides a cli tool to run ACME client.
- Store ACME states (certs, orders, challenge states, etc) using any storage solution (keyvault, s3, database, etc). Nier.ACME comes with local folder and Azure blob storage support.
- AspNetCore server components to handle ACME chanlelges and load certificates.

# Nier.ACME.Cli
Run ACME client as a command line program.

```
~/git/Nier.ACME/Nier.ACME.Cli/bin/Debug/netcoreapp3.0$ ./Nier.ACME.Cli --help
Nier.ACME.Cli 1.0.0
Copyright (C) 2019 Nier.ACME.Cli
USAGE:
Use azure blob:
  Nier.ACME.Cli --accountContactEmails admin@yourdomain.com,dev@yourdomain.com --azureBlobContainerName mySecretBlob --azureBlobPathPrefix NierACMEv0/
  --azureKeyVaultUrl https://yourVault.vault.azure.net/ --azureStorageConnectionStringConfigPath ConnectionStrings:StorageConnectionString --caUrl
  https://acme-v02.api.letsencrypt.org --dnsNames yourdomain.com,www.yourdomain.com --storageType AzureBlob
Use local directory:
  Nier.ACME.Cli --accountContactEmails admin@yourdomain.com,dev@yourdomain.com --caUrl https://acme-v02.api.letsencrypt.org --dnsNames
  yourdomain.com,www.yourdomain.com --localFsWorkingDirectory NierACMEv0/ --storageType LocalFs

  --caUrl                                     (Default: https://acme-v02.api.letsencrypt.org) CA url. Pass https://acme-staging-v02.api.letsencrypt.org for
                                              letsencrypt staging environment.

  --accountContactEmails                      Required. Contact emails. Separated by ','.

  --dnsNames                                  Required. DNS names. Separated by ','.

  --storageType                               Required. Storage type for storing certificates.

  --azureKeyVaultUrl                          Azure KeyVault used to store secrets. Required when storageType is azureBlob.

  --azureStorageConnectionStringConfigPath    Configuration path for azure storage connection string. Required when storageType is azureBlob. If the config value is
                                              stored in keyVault, replace '--' in secret name with ':'.

  --azureBlobContainerName                    Azure blob container name

  --azureBlobPathPrefix                       (Default: NierACMEv0/) Azure blob path prefix where certificates are stored.

  --localFsWorkingDirectory                   (Default: NierACMEv0/) The directory where certificates are stored when storageType is LocalFs.

  --help                                      Display this help screen.

  --version                                   Display version information.

```

# Nier.ACME.Worker
[ACMEWorker](Nier.ACME.Worker/ACMEWorker.cs) : Starts ACME domain verification process.

# Nier.ACME.AspNetCore
AspNetCore server components.

In service registration:

```csharp
// register the store you used in ACMEWorker
services.AddSingleton<ICertReader, FileSystemCertStore>();
services.AddSingleton<IServerCertificateSelector, AutoRefreshServerCertificateSelector>();
```

Configure the server to use IServerCertificateSelector to load the certificate.

```csharp
public class Program
{
    private static IServerCertificateSelector _certificateSelector;

    public static void Main(string[] args)
    {
        var host = CreateHostBuilder(args).Build();
        _certificateSelector =
            host.Services.GetService(typeof(IServerCertificateSelector)) as IServerCertificateSelector;
        host.Run();
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
      Host.CreateDefaultBuilder(args)
        .ConfigureWebHostDefaults(webBuilder =>
          {
              webBuilder.UseStartup<Startup>().ConfigureKestrel((context, options) =>
              {
                  options.ListenAnyIP(5001,
                      listenOptions =>
                      {
                          listenOptions.UseHttps(httpsOptions =>
                          {
                              httpsOptions.ServerCertificateSelector = (connectionContext, name) =>
                              {
                                  if (_certificateSelector != null)
                                  {
                                      return _certificateSelector.SelectServerCertificate(connectionContext,
                                          name);
                                  }

                                  return null;
                              };
                          });
                      });
              });
          });
    }
}
```
