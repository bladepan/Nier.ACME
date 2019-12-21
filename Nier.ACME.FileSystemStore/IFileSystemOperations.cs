using System.Threading.Tasks;

namespace Nier.ACME.FileSystemStore
{
    public interface IFileSystemOperations
    {
        Task WriteStringAsync(string file, string str);

        /// <summary>
        /// return null if file does not exist
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        Task<string> ReadStringAsync(string file);

        Task DeleteAsync(string file);
    }
}