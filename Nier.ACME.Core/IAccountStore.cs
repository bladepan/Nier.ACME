using System.Threading.Tasks;


namespace Nier.ACME.Core
{
    public interface IAccountStore
    {
        Task SaveAccountDetailsAsync(AccountDetails accountDetails);
        Task<AccountDetails> GetAccountDetailsAsync();

        Task SaveOrderAsync(OrderDetails orderDetails);
        Task<OrderDetails> GetOrderAsync();

        Task DeleteOrderAsync();
    }
}