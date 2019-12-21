using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nier.ACME.Core;
using X509Certificate = Org.BouncyCastle.X509.X509Certificate;

namespace Nier.ACME.Worker
{
    public class ACMEWorker
    {
        private readonly AcmeOptions _options;
        private readonly IClientStateStore _stateStore;
        private readonly ILogger<ACMEWorker> _logger;
        private X509Certificate2 _certificate;
        private AccountDetails _account;
        private OrderDetails _order;
        private readonly AcmeClient _acmeClient;

        private readonly Dictionary<string, AuthorizationDetails> _authorizations =
            new Dictionary<string, AuthorizationDetails>();

        private readonly IDictionary<string, ChallengeDetails> _challenges
            = new Dictionary<string, ChallengeDetails>();


        public ACMEWorker(AcmeOptions options, AcmeClient acmeClient, IClientStateStore stateStore,
            ILogger<ACMEWorker> logger)
        {
            _options = options;
            _stateStore = stateStore;
            _logger = logger;
            _acmeClient = acmeClient;
        }

        public async Task WorkAsync()
        {
            long ts = DateTimeOffset.UnixEpoch.ToUnixTimeMilliseconds() + TimeSpan.FromHours(1).Milliseconds;
            // restore
            _certificate = await _stateStore.GetActiveCertificateAsync(ts, ts);

            if (_certificate != null)
            {
                _logger.LogInformation("Existing certificate is Good!");
                return;
            }
            else
            {
                _logger.LogWarning("Missing Certificate");
            }

            await ResolveAccount(_acmeClient);
            await ResolveOrder(_acmeClient);
            await ResolveChallenges(_acmeClient);
            await ResolveAuthorizations(_acmeClient);
            await ResolveCertificate(_acmeClient);
        }

        private async Task ResolveAccount(AcmeClient acme)
        {
            _account = await _stateStore.GetAccountDetailsAsync();

            if (_account == null)
            {
                var contacts = _options.AccountContactEmails.Select(x => $"mailto:{x}");
                _logger.LogInformation("Creating ACME Account");
                var accountDetails = await acme.CreateAccountAsync(
                    contacts: contacts,
                    termsOfServiceAgreed: _options.AcceptTermsOfService);
                _account = accountDetails;
                await _stateStore.SaveAccountDetailsAsync(_account);
                _logger.LogInformation($"account {JsonSerializer.Serialize(_account)}");
            }

            acme.SetAccount(_account);
        }

        private async Task ResolveOrder(AcmeClient acme)
        {
            _order = await _stateStore.GetOrderAsync();

            long nowTs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (_order == null || _order.IsExpired(nowTs))
            {
                if (_order != null)
                {
                    _logger.LogInformation("previous order expired.");
                    await _stateStore.DeleteOrderAsync();
                }

                _logger.LogInformation("Creating NEW Order");
                _order = await acme.CreateOrderAsync(_options.DnsNames);
                await _stateStore.SaveOrderAsync(_order);
            }
            else
            {
                _logger.LogInformation("get order from store");
                // refresh order
                try
                {
                    _logger.LogInformation("Refreshing Order status");
                    _order = await acme.GetOrderDetailsAsync(_order);
                }
                catch (InvalidOrderStatusException)
                {
                    await _stateStore.DeleteOrderAsync();
                    throw;
                }
            }


            _logger.LogInformation(JsonSerializer.Serialize(_order));
        }

        private async Task ResolveChallenges(AcmeClient acme)
        {
            if (_order.Status == OrderStatus.Pending)
            {
                _logger.LogInformation("Order is pending, resolving Authorizations");
                foreach (string authorizationUrl in _order.AuthorizationUrls)
                {
                    AuthorizationDetails authorizationDetail =
                        await acme.GetAuthorizationDetailsAsync(authorizationUrl, _order.Expires);
                    _authorizations[authorizationUrl] = authorizationDetail;

                    if (AuthorizationStatus.Pending == authorizationDetail.Status)
                    {
                        foreach (ChallengeDetails challenge in authorizationDetail.Challenges)
                        {
                            if (challenge.ChallengeType == ChallengeType.Http01)
                            {
                                await SaveChallengeDetailsAsync(challenge);

                                _logger.LogInformation("Challenge Handler has handled challenge:");
                                _logger.LogInformation(JsonSerializer.Serialize(challenge));
                                // tells the server we are ready for challenge
                                await acme.AnswerChallengeAsync(challenge.Url);

                                _logger.LogInformation("Refreshing Authorization status");
                                authorizationDetail =
                                    await acme.GetAuthorizationDetailsAsync(authorizationUrl, _order.Expires);
                                _logger.LogInformation(
                                    $"auth detail {authorizationUrl}: {JsonSerializer.Serialize(authorizationDetail)}");
                            }
                        }
                    }
                }
            }
        }

        private async Task SaveChallengeDetailsAsync(ChallengeDetails httpDetails)
        {
            _logger.LogInformation($"Handling Challenges with HTTP full path of {httpDetails.HttpResourcePath}");

            string id = await _stateStore.SaveChallengeValidationDetailsAsync(httpDetails);
            _challenges[id] = httpDetails;
        }

        private async Task ResolveAuthorizations(AcmeClient acme)
        {
            bool allCompleted = await WaitChallengeCompleteAsync();
            if (!allCompleted)
            {
                throw new Exception("Failed to complete challenge within timeout");
            }

            var now = DateTime.Now;
            do
            {
                // Wait for all Authorizations to be valid or any one to go invalid
                int validCount = 0;
                foreach (KeyValuePair<string, AuthorizationDetails> authz in _authorizations)
                {
                    switch (authz.Value.Status)
                    {
                        case AuthorizationStatus.Valid:
                            ++validCount;
                            break;
                    }
                }

                if (validCount == _authorizations.Count)
                {
                    _logger.LogInformation("All Authorizations ({0}) are valid", validCount);
                    break;
                }


                _logger.LogWarning("Found {0} Authorization(s) NOT YET valid", _authorizations.Count - validCount);

                if (now.AddSeconds(_options.WaitForAuthorizations) < DateTime.Now)
                {
                    throw new TimeoutException("Timed out waiting for Authorizations; ABORTING");
                }

                // We wait in 5s increments
                await Task.Delay(5000);
                foreach (string authorizeUrl in _order.AuthorizationUrls)
                {
                    // Update all the Authorizations still pending
                    if (AuthorizationStatus.Pending == _authorizations[authorizeUrl].Status)
                        _authorizations[authorizeUrl] =
                            await acme.GetAuthorizationDetailsAsync(authorizeUrl, _order.Expires);
                }
            } while (true);
        }

        private async Task<bool> WaitChallengeCompleteAsync()
        {
            IEnumerable<string> challengeIds = new List<string>(_challenges.Keys);

            for (var i = 0; i < 60; i++)
            {
                if (i > 0)
                {
                    await Task.Delay(5000);
                }

                var completed = new List<string>();
                foreach (string challengeId in challengeIds)
                {
                    ChallengeValidationStatus status = await _stateStore.GetChallengeValidationStatusAsync(challengeId);
                    if (status == ChallengeValidationStatus.Validated)
                    {
                        _logger.LogInformation($"{challengeId} validation completed");
                        completed.Add(challengeId);
                    }
                }

                challengeIds = challengeIds.Except(completed).ToArray();

                if (!challengeIds.Any())
                {
                    return true;
                }
            }

            _logger.LogInformation($"not all challenges completed, remaining {challengeIds.Count()}");
            return false;
        }

        private async Task ResolveCertificate(AcmeClient acme)
        {
            _logger.LogInformation("Refreshing Order status");
            _order = await acme.GetOrderDetailsAsync(_order);

            // FIXME, wait for ready
            if (OrderStatus.Ready == _order.Status)
            {
                CertPrivateKey key = null;
                _logger.LogInformation("Generating CSR");
                byte[] csr;
                switch (_options.CertificateKeyAlgor)
                {
                    case "rsa":
                        key = CertHelper.GenerateRsaPrivateKey(
                            _options.CertificateKeySize ?? AcmeOptions.DefaultRsaKeySize);
                        csr = CertHelper.GenerateRsaCsr(_options.DnsNames, key);
                        break;
                    case "ec":
                        key = CertHelper.GenerateEcPrivateKey(
                            _options.CertificateKeySize ?? AcmeOptions.DefaultEcKeySize);
                        csr = CertHelper.GenerateEcCsr(_options.DnsNames, key);
                        break;
                    default:
                        throw new Exception("Unknown Certificate Key Algorithm: " + _options.CertificateKeyAlgor);
                }

                await _stateStore.SavePrivateKeyAsync(_order.Url, key.KeyPair, _order.Expires);
                _logger.LogInformation("Finalizing Order");
                _order = await acme.FinalizeOrderAsync(_order, csr);
            }

            // FIXME extract out
            if (string.IsNullOrEmpty(_order.CertificateUrl))
            {
                _logger.LogWarning("Order Certificate is NOT READY YET");
                var now = DateTime.Now;
                do
                {
                    _logger.LogInformation("Waiting...");
                    await Task.Delay(5000);

                    _order = await acme.GetOrderDetailsAsync(_order);

                    if (!string.IsNullOrEmpty(_order.CertificateUrl))
                        break;

                    _logger.LogInformation($"order status {_order.Status}");

                    if (DateTime.Now > now.AddSeconds(_options.WaitForCertificate))
                    {
                        throw new TimeoutException("Failed to get certificate url");
                    }
                } while (true);

                await _stateStore.SaveOrderAsync(_order);
            }

            _logger.LogInformation("Retrieving Certificate");
            X509Certificate newCert = await acme.GetOrderCertificateAsync(_order);
            await _stateStore.SaveCertificateAsync(_order.Url, newCert);
        }
    }
}