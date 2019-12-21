using System.IO;
using System.Threading.Tasks;

namespace Nier.ACME.FileSystemStore
{
    public class LocalFileSystemOperations : IFileSystemOperations
    {
        private readonly string _workingDirectory;

        public LocalFileSystemOperations(string workingDirectory)
        {
            _workingDirectory = workingDirectory;
            Directory.CreateDirectory(workingDirectory);
        }

        public Task WriteStringAsync(string file, string str)
        {
            return File.WriteAllTextAsync(Path.Combine(_workingDirectory, file), str);
        }

        public Task<string> ReadStringAsync(string file)
        {
            string path = Path.Combine(_workingDirectory, file);
            if (File.Exists(path))
            {
                return File.ReadAllTextAsync(path);
            }

            return Task.FromResult<string>(null);
        }

        public Task DeleteAsync(string file)
        {
            File.Delete(Path.Combine(_workingDirectory, file));
            return Task.CompletedTask;
        }
    }
}