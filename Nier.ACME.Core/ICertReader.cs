using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace Nier.ACME.Core
{
    public interface ICertReader
    {
        /// <summary>
        /// Find any cert that has overlap with [fromTimeStamp, toTimeStamp), if
        /// multiple certs are found, return the one with the greatest NotAfter
        /// </summary>
        /// <param name="fromTimeStamp"></param>
        /// <param name="toTimeStamp"></param>
        /// <returns></returns>
        Task<X509Certificate2> GetActiveCertificateAsync(long fromTimeStamp, long toTimeStamp);
    }
}