using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public class QueryRepositoryRootPath : Command
    {
        public QueryRepositoryRootPath(string path)
        {
            WorkingDirectory = path;
            Args = "root";
        }

        public Result GetResult()
        {
            return ReadToEnd();
        }

        public async Task<Result> GetResultAsync()
        {
            return await ReadToEndAsync().ConfigureAwait(false);
        }
    }
}
