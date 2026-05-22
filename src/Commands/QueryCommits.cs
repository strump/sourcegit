using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public class QueryCommits : Command
    {
        public QueryCommits(string repo, string limits, bool markMerged = true)
        {
            WorkingDirectory = repo;
            Context = repo;
            Args = "log --template \"{separate('\\x00', " + 
                   "node, " + // Commit Hash
                   "revset('parents(%d)', rev) % '{node}', " + // List of parent hashes
                   "ifcontains('tip', tags, branch, ' '), " + // Branch name if commit is a tip of a branch. In Mercurial all commits have a branch associated.
                   "separate('±', person(author), email(author)), " + // Author name with email separated with '±' symbol
                   "date(localdate(date), '%s'), " + // Commit timestamp in local timezome
                   "firstline(desc)" + // First line of commit message
                   ")}\\n\" " +
                   limits;
            _markMerged = markMerged;
        }

        public QueryCommits(string repo, string filter, Models.CommitSearchMethod method, bool onlyCurrentBranch)
        {
            var builder = new StringBuilder();
            builder.Append("log -1000 --date-order --no-show-signature --decorate=full --format=%H%x00%P%x00%D%x00%aN±%aE%x00%at%x00%cN±%cE%x00%ct%x00%s ");

            if (!onlyCurrentBranch)
                builder.Append("--branches --remotes ");

            if (method == Models.CommitSearchMethod.ByAuthor)
            {
                builder.Append("-i --author=").Append(filter.Quoted());
            }
            else if (method == Models.CommitSearchMethod.ByCommitter)
            {
                builder.Append("-i --committer=").Append(filter.Quoted());
            }
            else if (method == Models.CommitSearchMethod.ByMessage)
            {
                var words = filter.Split([' ', '\t', '\r'], StringSplitOptions.RemoveEmptyEntries);
                foreach (var word in words)
                    builder.Append("--grep=").Append(word.Trim().Quoted()).Append(' ');
                builder.Append("--all-match -i");
            }
            else if (method == Models.CommitSearchMethod.ByPath)
            {
                builder.Append("-- ").Append(filter.Quoted());
            }
            else
            {
                builder.Append("-G").Append(filter.Quoted());
            }

            WorkingDirectory = repo;
            Context = repo;
            Args = builder.ToString();
            _markMerged = false;
        }

        public async Task<List<Models.Commit>> GetResultAsync()
        {
            var commits = new List<Models.Commit>();
            try
            {
                using var proc = new Process();
                proc.StartInfo = CreateGitStartInfo(true);
                proc.Start();

                var findHead = false;
                while (await proc.StandardOutput.ReadLineAsync().ConfigureAwait(false) is { } line)
                {
                    var parts = line.Split('\0');
                    if (parts.Length != 6)
                        continue;

                    var commit = new Models.Commit() { SHA = parts[0] };
                    commit.ParseParents(parts[1]);
                    commit.ParseBranch(parts[2]);
                    commit.Author = Models.User.FindOrAdd(parts[3]);
                    commit.AuthorTime = ulong.Parse(parts[4]);
                    // There is not such thing as committer in Mercurial. So copy author
                    // and commit timestamp here.
                    commit.Committer = commit.Author;
                    commit.CommitterTime = commit.AuthorTime;
                    commit.Subject = parts[5];
                    commits.Add(commit);

                    findHead |= commit.IsMerged;
                }

                await proc.WaitForExitAsync().ConfigureAwait(false);

                if (_markMerged && !findHead && commits.Count > 0)
                {
                    var set = await new QueryCurrentBranchCommitHashes(WorkingDirectory, commits[^1].CommitterTime)
                        .GetResultAsync()
                        .ConfigureAwait(false);

                    foreach (var c in commits)
                    {
                        if (set.Contains(c.SHA))
                        {
                            c.IsMerged = true;
                            break;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                RaiseException($"Failed to query commits. Reason: {e.Message}");
            }

            return commits;
        }

        private bool _markMerged = false;
    }
}
