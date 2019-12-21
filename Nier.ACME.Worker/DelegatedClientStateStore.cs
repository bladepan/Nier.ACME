using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Nier.ACME.Core;
using Org.BouncyCastle.Crypto;
using X509Certificate = Org.BouncyCastle.X509.X509Certificate;

namespace Nier.ACME.Worker
{
    public class DelegatedClientStateStore : IClientStateStore
    {
        private readonly IAccountStore _accountStore;
        private readonly IChallengeStore _challengeStore;
        private readonly ICertStore _certStore;
        private readonly ICertReader _certReader;

        public DelegatedClientStateStore(IAccountStore accountStore, IChallengeStore challengeStore,
            ICertStore certStore, ICertReader certReader)
        {
            _accountStore = accountStore;
            _challengeStore = challengeStore;
            _certStore = certStore;
            _certReader = certReader;
        }

        public Task SaveAccountDetailsAsync(AccountDetails accountDetails)
        {
            return _accountStore.SaveAccountDetailsAsync(accountDetails);
        }

        public Task<AccountDetails> GetAccountDetailsAsync()
        {
            return _accountStore.GetAccountDetailsAsync();
        }

        public Task SaveOrderAsync(OrderDetails orderDetails)
        {
            return _accountStore.SaveOrderAsync(orderDetails);
        }

        public Task<OrderDetails> GetOrderAsync()
        {
            return _accountStore.GetOrderAsync();
        }

        public Task DeleteOrderAsync()
        {
            return _accountStore.DeleteOrderAsync();
        }

        public Task<ChallengeValidationStatus> GetChallengeValidationStatusAsync(string id)
        {
            return _challengeStore.GetChallengeValidationStatusAsync(id);
        }

        public Task<string> SaveChallengeValidationDetailsAsync(ChallengeDetails challengeDetails)
        {
            return _challengeStore.SaveChallengeValidationDetailsAsync(challengeDetails);
        }

        public Task<X509Certificate2> GetActiveCertificateAsync(long fromTs, long toTs)
        {
            return _certReader.GetActiveCertificateAsync(fromTs, toTs);
        }

        public Task SavePrivateKeyAsync(string orderUrl, AsymmetricCipherKeyPair privateKey, long expires)
        {
            return _certStore.SavePrivateKeyAsync(orderUrl, privateKey, expires);
        }

        public Task SaveCertificateAsync(string orderUrl, X509Certificate certificate)
        {
            return _certStore.SaveCertificateAsync(orderUrl, certificate);
        }
    }
}