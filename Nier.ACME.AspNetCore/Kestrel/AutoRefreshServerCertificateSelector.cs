using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Logging;
using Nier.ACME.Core;

namespace Nier.ACME.AspNetCore.Kestrel
{
    public class AutoRefreshServerCertificateSelector : IServerCertificateSelector, IDisposable
    {
        private readonly ICertReader _certReader;
        private readonly ILogger<AutoRefreshServerCertificateSelector> _logger;
        private X509Certificate2 _currentCert;
        private readonly TimeSpan _refreshThreshold = TimeSpan.FromHours(1);
        private readonly TimeSpan _errorBackoffTime = TimeSpan.FromMinutes(10);
        private bool _stopped;

        public AutoRefreshServerCertificateSelector(ICertReader certReader,
            ILogger<AutoRefreshServerCertificateSelector> logger)
        {
            _certReader = certReader;
            _logger = logger;
            FirstRun();
        }

        public X509Certificate2 SelectServerCertificate(ConnectionContext connectionContext, string hostName)
        {
            return _currentCert;
        }

        private void FirstRun()
        {
            Task.Run(async () =>
            {
                while (!_stopped)
                {
                    var hasError = false;
                    long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    try
                    {
                        RefreshCert(now);
                    }
                    catch (Exception e)
                    {
                        hasError = true;
                        _logger.LogWarning($"Error when refresh cert {e}");
                    }

                    TimeSpan waitTime = _errorBackoffTime;

                    if (hasError || _currentCert == null)
                    {
                        waitTime = _errorBackoffTime;
                    }
                    else
                    {
                        long notAfter = new DateTimeOffset(_currentCert.NotAfter).ToUnixTimeMilliseconds();
                        if (notAfter > now)
                        {
                            long diff = notAfter - now;
                            if (diff > 4 * _refreshThreshold.TotalMilliseconds)
                            {
                                waitTime = TimeSpan.FromMilliseconds(diff - 4 * _refreshThreshold.TotalMilliseconds);
                            }
                            else
                            {
                                waitTime = _refreshThreshold / 2;
                            }
                        }
                    }

                    _logger.LogInformation($"Wait after {waitTime.TotalMilliseconds}ms to refresh cert");
                    await Task.Delay(waitTime);
                }
            });
        }

        private void RefreshCert(long now)
        {
            if (_currentCert == null || (now + _refreshThreshold.TotalMilliseconds >
                                         new DateTimeOffset(_currentCert.NotAfter).ToUnixTimeMilliseconds()))
            {
                SetCert(now, now);
                if (_currentCert == null)
                {
                    // fallback to any cert
                    SetCert(0, now);
                }
            }
        }

        private void SetCert(long fromTs, long toTs)
        {
            X509Certificate2 cert = _certReader.GetActiveCertificateAsync(fromTs, toTs).Result;
            if (cert != null)
            {
                _logger.LogInformation(
                    $"Set current cert active in ts {fromTs}->{toTs}, cert NotAfter {cert.NotAfter}");
                _currentCert = cert;
            }
        }

        public void Dispose()
        {
            _stopped = true;
        }
    }
}