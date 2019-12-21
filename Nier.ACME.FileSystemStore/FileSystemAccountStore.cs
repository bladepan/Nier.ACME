using System.Text.Json;
using System.Threading.Tasks;
using Nier.ACME.Core;

namespace Nier.ACME.FileSystemStore
{
    public class FileSystemAccountStore: IAccountStore
    {
        private IFileSystemOperations _fileSystem;

        public FileSystemAccountStore(IFileSystemOperations fileSystem)
        {
            _fileSystem = fileSystem;
        }

        public Task SaveAccountDetailsAsync(AccountDetails accountDetails)
        {
            string json = JsonSerializer.Serialize(accountDetails);
            return _fileSystem.WriteStringAsync("account.json", json);
        }

        public async Task<AccountDetails> GetAccountDetailsAsync()
        {
            string json = await _fileSystem.ReadStringAsync("account.json");
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }

            AccountDetails accountDetails = JsonSerializer.Deserialize<AccountDetails>(json);
            return accountDetails;
        }

        public Task SaveOrderAsync(OrderDetails orderDetails)
        {
            string json = JsonSerializer.Serialize(orderDetails);
            return _fileSystem.WriteStringAsync("order.json", json);
        }

        public async Task<OrderDetails> GetOrderAsync()
        {
            string json = await _fileSystem.ReadStringAsync("order.json");
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }

            return JsonSerializer.Deserialize<OrderDetails>(json);
        }

        public Task DeleteOrderAsync()
        {
            return _fileSystem.DeleteAsync("order.json");
        }
    }
}