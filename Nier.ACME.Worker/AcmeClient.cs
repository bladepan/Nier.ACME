using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nier.ACME.Core;
using IACMESharpChallengeValidationDetails = ACMESharp.Authorizations.IChallengeValidationDetails;
using ACMESharpAuthorizationDecoder = ACMESharp.Authorizations.AuthorizationDecoder;
using ACMESharpClient = ACMESharp.Protocol.AcmeProtocolClient;
using ACMESharpOrderDetails = ACMESharp.Protocol.OrderDetails;
using ACMESharpOrder = ACMESharp.Protocol.Resources.Order;
using ACMESharpAuthorization = ACMESharp.Protocol.Resources.Authorization;
using ACMESharpChallenge = ACMESharp.Protocol.Resources.Challenge;


namespace Nier.ACME.Worker
{
    public class AcmeClient : IDisposable
    {
        private readonly ACMESharpClient _client;
        private readonly ILogger<AcmeClient> _logger;
        private BouncyCastleX509CertificateCodec _certCodec = new BouncyCastleX509CertificateCodec();

        public AcmeClient(string caUrl, ILogger<AcmeClient> logger)
        {
            _client = new ACMESharpClient(new Uri(caUrl));
            _logger = logger;
        }

        public async Task InitAsync()
        {
            ACMESharp.Protocol.Resources.ServiceDirectory serviceDirectory = await _client.GetDirectoryAsync();
            _logger.LogInformation(JsonSerializer.Serialize(serviceDirectory));
            _client.Directory = serviceDirectory;

            await _client.GetNonceAsync();
        }

        public async Task<AccountDetails> CreateAccountAsync(
            IEnumerable<string> contacts,
            bool termsOfServiceAgreed)
        {
            ACMESharp.Protocol.AccountDetails
                account = await _client.CreateAccountAsync(contacts, termsOfServiceAgreed);
            _logger.LogInformation($"Account created {account.Payload.Id}.");
            return new AccountDetails(account, _client.Signer);
        }

        public void SetAccount(AccountDetails accountDetails)
        {
            var acmeSharpAcctDetails = new ACMESharp.Protocol.AccountDetails
            {
                Kid = accountDetails.Kid,
                TosLink = accountDetails.TosLink,
                Payload = new ACMESharp.Protocol.Resources.Account
                {
                    Id = accountDetails.Id,
                    Contact = accountDetails.Contact,
                    Status = accountDetails.Status.ToString().ToLower(),
                    TermsOfServiceAgreed = accountDetails.TermsOfServiceAgreed,
                    Orders = accountDetails.OrdersUrl,
                    InitialIp = accountDetails.InitialIp,
                    CreatedAt = accountDetails.CreatedAt,
                    Agreement = accountDetails.Agreement
                }
            };
            _client.Account = acmeSharpAcctDetails;
            _client.Signer.Import(accountDetails.KeyExport);
        }

        public async Task<OrderDetails> CreateOrderAsync(IEnumerable<string> dnsIdentifiers)
        {
            ACMESharpOrderDetails acmeSharpOrderDetails = await _client.CreateOrderAsync(dnsIdentifiers);
            var orderDetails = new OrderDetails(acmeSharpOrderDetails);
            orderDetails.AssertOkResponse();
            return orderDetails;
        }

        public async Task<OrderDetails> GetOrderDetailsAsync(OrderDetails orderDetails)
        {
            ACMESharpOrderDetails acmeSharpOrderDetails = await _client.GetOrderDetailsAsync(orderDetails.Url);
            var newOrderDetails = new OrderDetails(acmeSharpOrderDetails);
            newOrderDetails.AssertOkResponse();
            newOrderDetails.Merge(orderDetails);
            return newOrderDetails;
        }

        public async Task<AuthorizationDetails> GetAuthorizationDetailsAsync(string authorizationDetailsUrl, long orderExpires)
        {
            ACMESharpAuthorization acmeSharpAuthorization =
                await _client.GetAuthorizationDetailsAsync(authorizationDetailsUrl);

            var challengeDetailsList = new List<ChallengeDetails>();

            foreach (ACMESharpChallenge challenge in acmeSharpAuthorization.Challenges)
            {
                string challengeTypeStr = challenge.Type;
                if (ChallengeTypeMethods.TryParseFromString(challengeTypeStr, out ChallengeType challengeType))
                {
                    // FIXME the implementation of the decode method is odd, should fix
                    IACMESharpChallengeValidationDetails acmeSharpChallenge =
                        ACMESharpAuthorizationDecoder.DecodeChallengeValidation(
                            acmeSharpAuthorization, challenge.Type, _client.Signer);
                    var challengeDetails = new ChallengeDetails(challenge, acmeSharpChallenge, orderExpires);
                    challengeDetails.AssertOkResponse();
                    challengeDetailsList.Add(challengeDetails);
                }
                else
                {
                    // tls-alpn-01 or other fancy pants
                    _logger.LogInformation($"Received unrecognized challenge type {challengeTypeStr}, ignoring.");
                }
            }

            return new AuthorizationDetails(acmeSharpAuthorization)
            {
                Challenges = challengeDetailsList
            };
        }

        public async Task<OrderDetails> FinalizeOrderAsync(
            OrderDetails orderDetails,
            byte[] derEncodedCsr)
        {
            ACMESharpOrderDetails acmeOrderDetails =
                await _client.FinalizeOrderAsync(orderDetails.FinalizeUrl, derEncodedCsr);
            OrderDetails newOrderDetails = new OrderDetails(acmeOrderDetails);
            newOrderDetails.AssertOkResponse();

            newOrderDetails.Merge(orderDetails);
            return newOrderDetails;
        }

        public async Task AnswerChallengeAsync(
            string challengeDetailsUrl)
        {
            ACMESharpChallenge challenge = await _client.AnswerChallengeAsync(challengeDetailsUrl);
            object error = challenge.Error;
            if (error != null)
            {
                throw new ACMEClientException($"challenge error {error}");
            }
        }

        public async Task<Org.BouncyCastle.X509.X509Certificate> GetOrderCertificateAsync(
            OrderDetails order
        )
        {
            ACMESharpOrderDetails acmeSharpOrderDetails = new ACMESharpOrderDetails
            {
                Payload = new ACMESharpOrder
                {
                    Certificate = order.CertificateUrl
                }
            };
            byte[] certBytes = await _client.GetOrderCertificateAsync(acmeSharpOrderDetails);
            return _certCodec.Decode(certBytes);
        }

        public void Dispose()
        {
            _client.Dispose();
        }
    }
}