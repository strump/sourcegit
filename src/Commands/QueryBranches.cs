using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public class QueryBranches : Command
    {
        public QueryBranches(string repo)
        {
            WorkingDirectory = repo;
            Context = repo;
            //Args = "branch -l --all -v --format=\"%(refname)%00%(committerdate:unix)%00%(objectname)%00%(HEAD)%00%(upstream)%00%(upstream:trackshort)%00%(worktreepath)\"";
            Args = "branches -T \"{branch}\\x00{date(date, '%s')}\\x00{node}\\x00{ifcontains(tags, 'tip', '*', ' ')}\"";
        }

        public async Task<List<Models.Branch>> GetResultAsync()
        {
            var branches = new List<Models.Branch>();
            var rs = await ReadToEndAsync().ConfigureAwait(false);
            if (!rs.IsSuccess)
                return branches;

            var lines = rs.StdOut.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
            var mismatched = new HashSet<string>();
            var remotes = new Dictionary<string, Models.Branch>();
            foreach (var line in lines)
            {
                var b = ParseLine(line, mismatched);
                if (b != null)
                {
                    branches.Add(b);
                    if (!b.IsLocal)
                        remotes.Add(b.FullName, b);
                }
            }

            foreach (var b in branches)
            {
                if (b.IsLocal && !string.IsNullOrEmpty(b.Upstream))
                {
                    if (remotes.TryGetValue(b.Upstream, out var upstream))
                    {
                        b.IsUpstreamGone = false;

                        if (mismatched.Contains(b.FullName))
                            await new QueryTrackStatus(WorkingDirectory).GetResultAsync(b, upstream).ConfigureAwait(false);
                    }
                    else
                    {
                        b.IsUpstreamGone = true;
                    }
                }
            }

            return branches;
        }

        private Models.Branch ParseLine(string line, HashSet<string> mismatched)
        {
            var parts = line.Split('\0');
            if (parts.Length < 4)
                return null;

            var branch = new Models.Branch();
            branch.Name = parts[0];
            branch.IsLocal = true;

            ulong.TryParse(parts[1], out var committerDate);

            branch.FullName = parts[0];
            branch.CommitterDate = committerDate;
            branch.Head = parts[2];
            branch.IsCurrent = parts[3] == "*";
            //branch.Upstream = parts[4];
            branch.IsUpstreamGone = false;

            /*
            if (branch.IsLocal &&
                !string.IsNullOrEmpty(branch.Upstream) &&
                !string.IsNullOrEmpty(parts[5]) &&
                !parts[5].Equals("=", StringComparison.Ordinal))
                mismatched.Add(branch.FullName);
            */

            //branch.WorktreePath = parts[6];
            return branch;
        }
    }
}
