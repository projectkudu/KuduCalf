using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using LibGit2Sharp;
namespace SmartSync
{      
    public class GitDeploymentRepository : DeploymentRepository
    {
        class GitCommitId : SnapshotId
        {
            public ObjectId CommitId { get { return new ObjectId(AsData); } }
            public GitCommitId(ObjectId objId) : base(objId.RawId)
            {

            }
            private GitCommitId(string raw) : base(raw)
            {

            }
            public static GitCommitId FromToken(string s)
            {
                if (string.IsNullOrEmpty(s))
                {
                    return null;
                }
                return new GitCommitId(s);
            }
            public override string ToString()
            {
                return CommitId.Sha;
            }
        }

        public DirectoryInfo RepositoryDirectory { get; private set; }
        public Uri Origin { get { return _origin.Value; } }
        public bool IsChild { get { return Origin != null; } }
        private Repository GitRepo { get { return _gitRepo.Value; } }
        private Lazy<Repository> _gitRepo;
        private Lazy<Uri> _origin;
        public GitDeploymentRepository(DirectoryInfo dirInfo) : this(dirInfo,null)
        {
           
        }

        public GitDeploymentRepository(DirectoryInfo dirInfo,Uri origin)
        {
            RepositoryDirectory = dirInfo;
            if (origin == null)
            {
                _origin = new Lazy<Uri>(() => GetOrigin());
            }
            else
            {
                _origin = new Lazy<Uri>(() => origin);
            }
            _gitRepo = new Lazy<Repository>(() => new Repository(dirInfo.FullName));
        }

      
        public override bool Initialize()
        {
            if(Repository.IsValid(RepositoryDirectory.FullName))
            {
                return false;
            }
            Repository repo = null;
            if (IsChild)
            {
                var events = new GitProgressEvents();
                events.CheckoutProgressUpdate += LogCheckoutProgressUpdate;
                events.TransferProgressUpdate += LogTransferProgressUpdate;
                repo = Repository.Clone(
                    Origin.AbsoluteUri,
                    RepositoryDirectory.FullName,
                    false,
                    false,
                    events.TransferProgressHandler,
                    events.CheckoutProgressHandler);
            }
            else
            {
                repo = Repository.Init(RepositoryDirectory.FullName);
            } 
            return repo != null;
        }

        private int LogTransferProgressUpdate(TransferProgress progress)
        {
            LogEvent.TransferProgressInformation(progress.ReceivedBytes, progress.ReceivedObjects, progress.TotalObjects);
            return 0;
        }

        private void LogCheckoutProgressUpdate(string path, int completedSteps, int totalSteps)
        {
            LogEvent.CheckoutProgressInformation((double)completedSteps / (double)totalSteps);
        }

        public override SnapshotId CreateSnapshotFromFiles(IEnumerable<KeyValuePair<string, System.IO.FileInfo>> fileList, string comment)
        {
            if (IsChild)
            {
                throw new InvalidOperationException("Can't create new snapshots from child repository.");
            }
            // remove all the files from the index. 
            var files = GitRepo.Index.Select(elt => elt.Path).ToArray();
            foreach (var file in files)
            {
                GitRepo.Index.Remove(file, removeFromWorkingDirectory: false);
            }

            // add files back.
            int copiedFiles = 0;
            int totalFiles = 0;
            var stopWatch = System.Diagnostics.Stopwatch.StartNew();
            foreach (var elt in fileList)
            {
                var workingDirPath = SafeCombinePath(RepositoryDirectory, elt.Key);
                // suppress logging after the first 25 files.
                var logCopy = copiedFiles < 25;
                // copy to working dir.
                // note TryCopy handles the case of copying a file on to itself as a noop.
                if (TryCopyFile(elt.Value.FullName, workingDirPath, logCopy))
                {
                    copiedFiles++;
                }
                GitRepo.Index.Stage(workingDirPath);
                totalFiles++;
            }
            stopWatch.Stop();
            LogEvent.CopySummary(copiedFiles, totalFiles, stopWatch.ElapsedMilliseconds);

            var author = new Signature("nobody", "nobody@nowhere.invalid", DateTimeOffset.UtcNow);
            // call commit this way to avoid need on global git state
            // probably should only do this if the global configuration is not set 
            var commit = GitRepo.Commit(comment, author, author, false);
            var currId = new GitCommitId(commit.Id);
            var previousCommit = GetPreviousCommit(commit);
            if (previousCommit != null)
            {
               var previousId = new GitCommitId(previousCommit.Id);
               ProcessDeletionsBetweenSnapshot(previousId, currId, RepositoryDirectory);
            }
            return new GitCommitId(commit.Id);
        }

        public override SnapshotId GetLatestSnapshotId()
        {
            if (IsChild)
            {
                var events = new GitProgressEvents();
                events.TransferProgressUpdate += LogTransferProgressUpdate;
                events.CompletionUpdate += LogCompletionUpdate;
                GitRepo.Fetch("origin",
                    onTransferProgress: events.TransferProgressHandler,
                    onCompletion: events.CompletionHandler,
                    onProgress: x => Console.WriteLine(x));
            }
            return new GitCommitId(GitRepo.Head.Tip.Id);
        }

        int LogCompletionUpdate(RemoteCompletionType remoteCompletionType)
        {
            if (remoteCompletionType == RemoteCompletionType.Error)
            {
                LogEvent.FetchCompletionError();
                return -1;
            }
            LogEvent.FetchCompletionInformation(remoteCompletionType);
            return 0;
        }

        public override SnapshotId GetPreviousSnapshotId(SnapshotId snapshotId)
        {
            var commit = CommitFromSnapshotId(snapshotId);
            if (commit != null)
            {
                var previousCommit = GetPreviousCommit(commit);
                if (previousCommit != null)
                {
                    return new GitCommitId(previousCommit.Id);
                }
            }
            return null;
        }

        private Commit GetPreviousCommit(Commit commit)
        {
            // TODO: fail if there is more than one parent..
            var previousCommit = commit.Parents.FirstOrDefault();
            return previousCommit;
        }

        public override SnapshotInfo GetSnapshotInfo(SnapshotId snapshotId)
        {
            var commit = CommitFromSnapshotId(snapshotId);

            return new SnapshotInfo(commit.Message, commit.Author.When, snapshotId);
        }

        public override IEnumerable<SnapshotItem> GetSnapshotItems(SnapshotId snapshotId)
        {
            var commit = CommitFromSnapshotId(snapshotId);
            var tstamp = commit.Author.When;
            return FromTree("", commit.Tree, ignored => tstamp);
        }

        public override SnapshotId FromToken(string tok)
        {
            return GitCommitId.FromToken(tok);
        }

        public void ResetWorkingDirectoryTo(SnapshotId id)
        {
            var commit = CommitFromSnapshotId(id);
            GitRepo.Reset(ResetOptions.Hard, commit.Sha);
        }

        public SnapshotId CommitWorkingDirectroy(string comment)
        {
            return CreateSnapshotFromDirectory(RepositoryDirectory, comment);
        }

        private Commit CommitFromSnapshotId(SnapshotId id)
        {
            var gitCommitId = (GitCommitId)id;
            return (Commit)GitRepo.Lookup(gitCommitId.CommitId, ObjectType.Commit);
        }

        private IEnumerable<SnapshotItem> FromTree(string prefix,Tree tree,Func<string,DateTimeOffset> modTime)
        {
            foreach (var entry in tree)
            {
                var path = Path.Combine(prefix, entry.Name);
                switch (entry.TargetType)
                {
                    case TreeEntryTargetType.Blob:
                        {
                            Blob blob = (Blob)entry.Target;
                            var lastModifiedTime = modTime(path);
                            yield return new SnapshotItem(
                                path,
                                blob.Size,
                                lastModifiedTime,
                                act => act(blob.ContentStream));

                        }
                        break;
                    case TreeEntryTargetType.Tree:
                        {
                            Tree subtree = (Tree)entry.Target;
                            foreach (var item in FromTree(path, subtree, modTime))
                            {
                                yield return item;
                            }
                        }
                        break;
                }
            }
        }

        private Uri GetOrigin()
        {
            try
            {
                var origin = GitRepo.Network.Remotes["origin"];
                if (origin == null)
                {
                    return null;
                }
                return new Uri(origin.Url);
            }
            catch (RepositoryNotFoundException)
            {
                return null;
            }
          
        }
    }
}
