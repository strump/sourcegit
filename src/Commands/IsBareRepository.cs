using System.IO;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public class IsBareRepository : Command
    {
        public IsBareRepository(string path)
        {
            WorkingDirectory = path;
        }

        public bool GetResult()
        {
            return false;
        }

        public async Task<bool> GetResultAsync()
        {
            return false;
        }
    }
}
