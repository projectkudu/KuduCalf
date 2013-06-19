using CommandLine;
using CommandLine.Text;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Http;
using SmartSync;
using System.Threading.Tasks;

// TODO: Quick and dirty stop gap till we have a proper powershell module. 
namespace KuduCalfCmd
{
    class Cmds
    {
        public static ILogEvent LogEvent { get; set; }
        public Cmds(Config config)
        {
            Config = config;
        }
        public Config Config { get; set; }
       
        public class CmdArgsCommon
        {
            [Option('v', "Verbose", Required = false, HelpText = "Enable Verbose logging.")]
            public bool Verbose { get; set; }
        }

        public class CmdArgsGetToolVersion : CmdArgsCommon
        {
            [Option('c', "CompatibleWith", Required = false, HelpText = "Check if current version is forward compatible with passed in version.")]
            public string CompatibleWith { get; set; }
        }

        public class CmdArgsSubIdReq : CmdArgsCommon
        {
            [Option('s',"SubscriberId",Required = true, HelpText = "Id of subscriber.")]
            public string Id { get; set; }
        }

        public class CmdArgsSetSubscriberSiteStatus : CmdArgsSubIdReq
        {
            [Option('T', "Token", Required = true, HelpText = "Token of Snapshot")]
            public string Token { get; set; }
        }

        public class CmdArgsNewSub : CmdArgsSubIdReq
        {
            [Option('u', "PublicUpdateNotifyUrl", Required = true, HelpText = "The public update notification  url of the subscriber.")]
            public string PublicUpdateNotifyUrl { get; set; }
            [Option('d', "PrivateUpdateNotifyUrl", Required = false, HelpText = "Optional private update notification url of the subscriber to use when behind a loadbalancer.")]
            public string PrivateUpdateNotifyUrl { get; set; }
        }

        public class CmdArgsSubIdOpt : CmdArgsCommon
        {
            [Option('s',"SubscriberId", Required = false, HelpText = "Id of subscriber.")]
            public string Id { get; set; }
        }

        public class CmdArgsInitalizeSyncState: CmdArgsCommon
        {
            [Option('u', "ParentUrl", Required = false, HelpText = "Url of parent repository")]
            public string ParentUri { get; set; }

            [Option('f', "Force", Required = false, HelpText = "Reset the state if already created.")]
            public bool Force { get; set; }
        }

        public class CmdArgsSyncDirectory : CmdArgsCommon
        {
            [Option('d', "LitteralPath", Required = true)]
            public string Path { get; set; }
        }

        public class CmdArgsPublishDirectory : CmdArgsCommon
        {
            [Option('i', "ignore", Required = false, HelpText = "List of files/directories to ignore and not sync, delimited by ;")]
            public string Ignore { get; set; }

            [Option('d',"LitteralPath", Required = true)]
            public string Path { get; set; }

            [Option('c',"Comment", Required = false)]
            public string Comment { get; set; }
        }

        public class CmdArgsGetHelpCommand : CmdArgsCommon
        {
            [ValueOption(0)]
            public string Command { get; set; }
        }

        public class CmdArgsWatchSyncState : CmdArgsCommon
        {
            [Option('t', "PollInterval", Required = false, HelpText = "Polling interval to refresh at.",
                DefaultValue = 0)]
            public int PollInterval { get; set; }

            [Option('u', "PublicUpdateNotifyUrl", Required = false, HelpText = "The public update notification url of the subscriber(s).")]
            public string PublicUpdateNotifyUrl { get; set; }
        }

        public class CmdVerbs 
        {
            public const string GetToolVersionVerb = "Get-ToolVersion";
            public const string NewSubscriberVerb = "New-Subscriber";
            public const string GetSubscriberVerb = "Get-Subscriber";
            public const string SetSubscriberSiteStatusVerb = "Set-SubscriberSiteStatus";
            public const string RemoveSubscriberVerb = "Remove-Subscriber";
            public const string SyncSubscriberVerb = "Sync-Subscriber";
            public const string PublishDirectoryVerb = "Publish-Directory";
            public const string SyncDirectoryVerb = "Sync-Directory";
            public const string InitializeSyncStateVerb = "Initialize-SyncState";
            public const string WatchSyncStateVerb = "Watch-SyncState";
            public const string KuduSyncVerb = "KuduSync";
            public const string GetHelpVerb = "Get-Help";

            public CmdVerbs()
            {
                GetToolVersion = new CmdArgsGetToolVersion();
                NewSubscriber = new CmdArgsNewSub();
                GetSubscriber = new CmdArgsSubIdOpt();
                RemoveSubscriber = new CmdArgsSubIdReq();
                SyncSubscriber = new CmdArgsSubIdOpt();
                PublishDirectory = new CmdArgsPublishDirectory();
                SyncDirectory = new CmdArgsSyncDirectory();
                InitalizeSyncState  = new CmdArgsInitalizeSyncState();
                KuduSync = new KuduSyncOptions();
                GetHelp = new CmdArgsGetHelpCommand();
                SetSubscriberSiteStatus = new CmdArgsSetSubscriberSiteStatus();
                WatchSyncState = new CmdArgsWatchSyncState();
                
            }

            [VerbOption(GetToolVersionVerb)]
            public CmdArgsGetToolVersion GetToolVersion { get; set; }

            [VerbOption(NewSubscriberVerb)]
            public CmdArgsSubIdReq NewSubscriber { get; set; }

            [VerbOption(GetSubscriberVerb)]
            public CmdArgsSubIdOpt GetSubscriber { get; set; }

            [VerbOption(RemoveSubscriberVerb)]
            public CmdArgsSubIdReq RemoveSubscriber { get; set; }

            [VerbOption(SyncSubscriberVerb)]
            public CmdArgsSubIdOpt SyncSubscriber { get; set; }

            [VerbOption(PublishDirectoryVerb)]
            public CmdArgsPublishDirectory PublishDirectory { get; set; }

            [VerbOption(SyncDirectoryVerb)]
            public CmdArgsSyncDirectory SyncDirectory { get; set; }

            [VerbOption(InitializeSyncStateVerb)]
            public CmdArgsInitalizeSyncState InitalizeSyncState { get; set; }

            [VerbOption(KuduSyncVerb)]
            public KuduSyncOptions KuduSync { get; set; }

            [VerbOption(GetHelpVerb)]
            public CmdArgsGetHelpCommand GetHelp { get; set; }

            [VerbOption(SetSubscriberSiteStatusVerb)]
            public CmdArgsSetSubscriberSiteStatus SetSubscriberSiteStatus { get; set; }

            [VerbOption(WatchSyncStateVerb)]
            public CmdArgsWatchSyncState WatchSyncState { get; set; }

            [HelpVerbOption]
            public string GetUsage(string verb)
            {
                if (!String.IsNullOrEmpty(verb))
                {
                    return "Incorrect command syntax\n" +
                           "    KuduCalfCmd Get-Help " + verb + "\n" +
                           "for command usage usage.\n";
                }
                else
                {
                    return "Unknown command\n" +
                         "    KuduCalfCmd Get-Help * \n" +
                         "for a list of valid commands and their usage.\n";
                }
            }

        }
        public void GetHelp(CmdVerbs cmdv, CmdArgsGetHelpCommand args)
        {
            var s = args.Command;
            string txt;
            if (s == null)
            {
                txt = cmdv.GetUsage("");
            } 
            else if (s.Equals("*"))
            {
                txt = HelpText.AutoBuild(cmdv, "");
            }
            else
            {
                txt = HelpText.AutoBuild(cmdv, s);
            }
            Console.WriteLine(txt);
        }

        public  string GetToolVersion(CmdArgsGetToolVersion args)
        {
            var currentVersion = Config.VersionInfo.FileVersion;
            if (String.IsNullOrEmpty(args.CompatibleWith))
            {
                return Config.VersionInfo.FileVersion;
            }
            else
            {
                if (args.CompatibleWith.Equals("0.0.0.0"))
                {
                    return "False"; // unversioned dev build 
                }
                bool compatibleWith = args.CompatibleWith.Equals(Config.VersionInfo.FileVersion.ToString());
                return compatibleWith.ToString();
            }
        }

        public IEnumerable<SubscriberState> GetSubscriber(CmdArgsSubIdOpt args)
        {
            var ops = GetOperations();
            if (String.IsNullOrEmpty(args.Id))
            {
                return ops.GetSubcribers();
            }
            else
            {
                return SingleOrEmpty(ops.GetSubscriber(args.Id));
            }
        }

        public IEnumerable<SubscriberState> RemoveSubscriber(CmdArgsSubIdReq args)
        {
            var ops = GetOperations();
            var sub = ops.GetSubscriber(args.Id);
            ops.Delete(args.Id);
            yield return sub;
        }

        public SubscriberState NewSubscriber(CmdArgsNewSub args)
        {
            var ops = GetOperations();
            var sub = args.Id;
            var publicUpdateNotifyUri = new Uri(args.PublicUpdateNotifyUrl, UriKind.Absolute);
            var privateUpdateNotifyUri = !String.IsNullOrEmpty(args.PrivateUpdateNotifyUrl) ? new Uri(args.PrivateUpdateNotifyUrl, UriKind.Absolute) : null;
           
            var init = new SubscriberState()
            {
                Id = args.Id,
                PrivateUpdateNotifyUri = privateUpdateNotifyUri,
                PublicUpdateNotifyUri = publicUpdateNotifyUri,
                SubscribedToPublisher = Config.DefaultPublisherId
            };

            if (!ops.CreateSubscriber(init))
            {
                return null;
            }
            return init;
        }

        public bool KuduSync(KuduSyncOptions opts)
        {
            string id = Config.DefaultPublisherId;
            string comment;

            var kuduUser = Environment.GetEnvironmentVariable("KUDU_DEPLOYER");
            if (!String.IsNullOrEmpty(kuduUser))
            {
                var deploymentBranch = Environment.GetEnvironmentVariable("deployment_branch");
                comment = String.Format("From branch {0} by user {1}", deploymentBranch, kuduUser);
            }
            else
            {
                id = "MachineName-" + Environment.MachineName;
                comment = String.Format("Deployed by {0} on machine {1}", Environment.UserName, Environment.MachineName);
            }

            Config.GitRepositoryDirectory = new DirectoryInfo(opts.To);
            InitializeSyncState(new CmdArgsInitalizeSyncState());
            PublishDirectory(new CmdArgsPublishDirectory()
            {
                Path = opts.From,
                Ignore = opts.Ignore,
                Comment = comment
            });
            SyncSubscriber(new CmdArgsSubIdOpt());
            WatchSyncState(new CmdArgsWatchSyncState() { PollInterval = 15 });
            return true;
        }

        private HashSet<string> BuildIgnoreList(string ignore)
        {
            if (!String.IsNullOrEmpty(ignore))
            {
                var ignoreList = ignore.Split(';').Select(s => s.Trim());

                if (ignoreList.Any(s => s.Contains('*') || s.Contains('/') || s.Contains('\\')))
                {
                    throw new NotSupportedException("Wildcard matching (or \\) is not supported");
                }

                return new HashSet<string>(ignoreList, StringComparer.OrdinalIgnoreCase);
            }

            return new HashSet<string>();
        }

        static private bool IsTopLevelGitRepo(FileSystemInfo item, DirectoryInfo root)
        {
            var dirItem = item as DirectoryInfo;
            if (dirItem == null)
            {
                return false;
            }
            if (!dirItem.Name.Equals(".git", StringComparison.InvariantCultureIgnoreCase))
            {
                return false;
            }
            if (!dirItem.Parent.FullName.Equals(root.FullName, StringComparison.InvariantCultureIgnoreCase))
            {
                return false;
            }
            LogEvent.IgnoringTopLevelGitRepositoryInSourceDirectory(item.FullName);
            return true;
        }

        public void WatchSyncState(CmdArgsWatchSyncState args)
        {
            var ops = GetOperations();
            var stopWatch = System.Diagnostics.Stopwatch.StartNew();
            HashSet<string> lastWaitSet = new HashSet<string>();
            Func<SubscriberState, bool> filter = ignored => true;
            if (args.PublicUpdateNotifyUrl != null)
            {
                var pubUri = new Uri(args.PublicUpdateNotifyUrl);
                filter = (s) =>
                    {
                        return pubUri.Equals(s.PublicUpdateNotifyUri);
                    };
            }
            IEnumerable<SubscriberState> outOfSync = ops.NotAtTargetState().Where(filter);
            while (outOfSync.Any())
            {
                var ids = outOfSync.Select(sub => sub.Id);
                var currWaitSet = new HashSet<string>(ids);
                if (!currWaitSet.SequenceEqual(lastWaitSet))
                {
                    WriteToOutput("Waiting for the following subscribers to catch up.");
                    WriteToOutput("===================================================");
                    foreach (var sub in outOfSync)
                    {
                        WriteToOutput("Id : {0}", sub.Id);
                        WriteToOutput("PublicUri : {0}", sub.PublicUpdateNotifyUri);
                        WriteToOutput("PrivateUri : {0}", sub.PrivateUpdateNotifyUri);
                    }
                    WriteToOutput("===================================================");
                    lastWaitSet = currWaitSet;
                }
                if (args.PollInterval <= 0)
                {
                    return;
                }
                ops.WaitForStateChange(TimeSpan.FromSeconds(args.PollInterval));
                outOfSync = ops.NotAtTargetState().Where(filter);
            }
            WriteToOutput("Subscribers all up to date.");
        }

        public SnapshotId PublishDirectory(CmdArgsPublishDirectory args)
        {
            var ops = GetOperations();
            var pubId = Config.DefaultPublisherId;
            var pub = ops.GetPublisher(pubId);
            var repo = GetGitDeploymentRepo();
            var dirRoot = new DirectoryInfo(args.Path);
            var comment = args.Comment;
            if (string.IsNullOrEmpty(comment))
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendFormat("  Machine: {0}",Environment.MachineName);
                sb.AppendLine();
                sb.AppendFormat("Directory: {0}",dirRoot.FullName);
                sb.AppendLine();
                sb.AppendFormat(" DateTime: {0}",DateTimeOffset.UtcNow);
                sb.AppendLine();
                sb.AppendFormat("     User: {0}\\{1}",Environment.UserDomainName, Environment.UserName);
                sb.AppendLine();
                comment = sb.ToString();
            }
            var ignoreList = BuildIgnoreList(args.Ignore);

            Func<FileSystemInfo, bool> filter = fsi => !ignoreList.Contains(fsi.Name) && !IsTopLevelGitRepo(fsi, dirRoot) ;
            var snapId =  repo.CreateSnapshotFromDirectory(dirRoot, comment, filter);
            WriteToOutput("Publishing directory to: {0}", pubId);
            ops.UpdatePublisher(pubId, pubState => {
                    // note that the snapId maybe out of date so we always fetch the latest for the repo.
                    pubState.LatestSnapshotIdToken = repo.GetLatestSnapshotId().Token;
            });
            return snapId;
        }

        public IEnumerable<SubscriberState> SyncSubscriber(CmdArgsSubIdOpt args)
        {
            var allSubsWithNotifyUris =  GetSubscriber(args).Where(sub => sub.PublicUpdateNotifyUri != null); 
            var subs = new Dictionary<string, SubscriberState>();
            foreach(var sub in allSubsWithNotifyUris)
            {
                if(!subs.ContainsKey(sub.PublicUpdateNotifyUri.AbsoluteUri))
                {
                    subs.Add(sub.PublicUpdateNotifyUri.AbsoluteUri, sub);
                }
            }
            var subsToPing = subs.Values;
            var resp  =  PingSubscribers(subsToPing, TimeSpan.FromMinutes(5));
            var succeded = from ret in resp
                           where ret.Value == HttpStatusCode.Accepted
                           select ret.Key;

            var failed = from ret in resp
                         where ret.Value != HttpStatusCode.Accepted
                         select ret;

            foreach (var kv in failed)
            {
                LogEvent.SubscriberSyncWrongStatus(kv.Key.Id, kv.Key.PublicUpdateNotifyUri, (int)kv.Value);
            }

            return succeded;
        }

        public void SyncDirectory(CmdArgsSyncDirectory args)
        {
            var repo = GetGitDeploymentRepo();
            var snapid = repo.GetLatestSnapshotId();
            var targetDir = new DirectoryInfo(args.Path);
            repo.PopulateDirectoryFromSnapshot(snapid, targetDir);
        }

        public bool InitializeSyncState(CmdArgsInitalizeSyncState args)
        {
            bool ret = true;
            Uri origin = null;
            if (!String.IsNullOrEmpty(args.ParentUri))
            {
                origin = new Uri(args.ParentUri);
            }
            var repo = GetGitDeploymentRepo(origin);
            var ops = GetOperations();
            ret &= repo.Initialize();
            ret &= ops.Initialize(args.Force);
            ops.CreatePublisher(new PublisherState() {
                    Created = DateTime.UtcNow,
                    Id = Config.DefaultPublisherId
                }, args.Force);
            return ret;
        }

        public void SetSubscriberSiteStatus(CmdArgsSetSubscriberSiteStatus args)
        {
            var ops = GetOperations();
            var sub = args.Id;
            var token = args.Token;
            ops.UpdateSubscriber(sub, subState => {
                subState.LastSyncedSnaphostIdToken =  token;
            });                           
        }

        private void DoCommand(CmdVerbs cmds, string verb, Object args)
        {
            if (args == null)
            {
                return;
            }
            var verbCmds = new Dictionary<string, Action>(StringComparer.OrdinalIgnoreCase);

            verbCmds.Add(CmdVerbs.GetToolVersionVerb, () =>
            {
                var ret = GetToolVersion((CmdArgsGetToolVersion)args);
                WriteToOutput(ret);
            });
            verbCmds.Add(CmdVerbs.GetSubscriberVerb, () =>
            {
                var subs = GetSubscriber((CmdArgsSubIdOpt)args);
                WriteToOutput(subs);
            });
            verbCmds.Add(CmdVerbs.NewSubscriberVerb, () =>
            {
                var sub = NewSubscriber((CmdArgsNewSub)args);
                WriteToOutput(sub);
            });
            verbCmds.Add(CmdVerbs.RemoveSubscriberVerb, () =>
            {
                var sub = RemoveSubscriber((CmdArgsSubIdReq)args);
                WriteToOutput(sub);
            });
            verbCmds.Add(CmdVerbs.SyncSubscriberVerb, () =>
            {
                var sub = SyncSubscriber((CmdArgsSubIdOpt)args);
                WriteToOutput(sub);
            });
            verbCmds.Add(CmdVerbs.PublishDirectoryVerb, () =>
            {
                var snapId = PublishDirectory((CmdArgsPublishDirectory)args);
                WriteToOutput(snapId);
            });
            verbCmds.Add(CmdVerbs.SyncDirectoryVerb, () =>
            {
                SyncDirectory((CmdArgsSyncDirectory)args);
            });
            verbCmds.Add(CmdVerbs.InitializeSyncStateVerb, () =>
            {
                InitializeSyncState((CmdArgsInitalizeSyncState)args);
            });
            verbCmds.Add(CmdVerbs.KuduSyncVerb, () =>
            {
                KuduSync((KuduSyncOptions)args);
            });
            verbCmds.Add(CmdVerbs.WatchSyncStateVerb, () => 
            {
                WatchSyncState((CmdArgsWatchSyncState)args);
            });
            verbCmds.Add(CmdVerbs.SetSubscriberSiteStatusVerb, () =>
            {
                SetSubscriberSiteStatus((CmdArgsSetSubscriberSiteStatus)args);
            });
            verbCmds.Add(CmdVerbs.GetHelpVerb, () =>
            {
                var opts =   (CmdArgsGetHelpCommand)args;
                var normalizedVerb = verbCmds.Keys.Where(key => key.Equals(opts.Command, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
                if (!String.IsNullOrEmpty(normalizedVerb))
                {
                    opts.Command = normalizedVerb;
                }
                GetHelp(cmds, opts);
            });
            verbCmds[verb].Invoke();
        }

        public bool Run(string[] args)
        {

            var cmdVerbs = new CmdVerbs();
            var parser = new Parser(settings =>
                {
                    settings.CaseSensitive = false;
                    settings.HelpWriter = Console.Out;
                    settings.IgnoreUnknownArguments = false;
                 
                });
            var ret = parser.ParseArgumentsStrict(args, 
                cmdVerbs,
                (verb,opts) => DoCommand(cmdVerbs, verb, opts));
            return ret;
        }

        #region Private Helpers
        static Cmds()
        {
            var ts = new TraceSource("KuduCalfCmd",
                SourceLevels.Critical |
                SourceLevels.Error |
                SourceLevels.Warning |
                SourceLevels.Verbose);
            LogEvent = EventLoggerFactory.CreateEventLogger<ILogEvent>(ts);
        }
        static IEnumerable<T> SingleOrEmpty<T>(T obj)
        {
            if (obj == null)
            {
                yield break;
            }
            yield return obj;
        }
        private void WriteToOutput(string fmt,params object[] args)
        {
            Console.WriteLine(fmt, args);
        }
 
        private void WriteToOutput(Object obj)
        {
            if (obj != null)
            {
                foreach (System.ComponentModel.PropertyDescriptor p in System.ComponentModel.TypeDescriptor.GetProperties(obj))
                {
                    Console.WriteLine("{0} : {1}", p.DisplayName, p.GetValue(obj));
                }
            }
            Console.WriteLine();
        }
       
        private void WriteToOutput<T>(IEnumerable<T> elts)
        {
            foreach (var elt in elts)
            {
                WriteToOutput(elt);
            }
        }

        private SmartSyncStateOperations GetOperations()
        {
            var stateDir = Config.SubscriberStateDirectory;
            var ops = new LocalFileSmartSyncStateOperations(stateDir);
            return ops;
        }

        private IEnumerable<KeyValuePair<SubscriberState, HttpStatusCode>> 
            PingSubscribers(IEnumerable<SubscriberState> subs, TimeSpan timeout)
        {
            var tasks = subs.Select(PingNotifyAll).ToArray();
            Task.WaitAll(tasks, timeout);
            var failed = from task in tasks
                         where task.Exception != null
                         select task.Exception;
            foreach (var ex in failed)
            {
                LogEvent.UnexpectedErrorDuringSitePing(ex);
            } 
            var succeded =  from task in tasks
                   where !task.IsFaulted && task.IsCompleted
                   select task.Result;
            return succeded;
        }
        
        private async Task<KeyValuePair<SubscriberState, HttpStatusCode>> PingNotifyAll(SubscriberState sub)
        {
            var rsp = await KuduCalfWeb.Protocol.KuduCalfWebUpdateNotifyAllAsync(sub.PublicUpdateNotifyUri);
            HttpStatusCode statusCode = HttpStatusCode.Unused;
            statusCode = rsp.StatusCode;
            WarmupSite(sub);
            return new KeyValuePair<SubscriberState,HttpStatusCode>(sub, statusCode);
        }

        // make a real request to warm up the site as well
        // fire and forget
        private static void WarmupSite(SubscriberState sub)
        {
            var client = new HttpClient();
            client.GetAsync(sub.PublicUpdateNotifyUri);
        }

        // A bit of a hack, probably there is a prettier way to do this.
        private bool StreamProgress(HttpContent body)
        {
            //Can't seem to get this to work reliably  punt for now.
#if false

            var task = body.ReadAsStreamAsync();
            task.Wait();
            if (task.Exception != null)
            {
                return false;
            }
            var strm = task.Result;
            // create text stream with a small look ahead buffer.
            using (var txtstrm = new StreamReader(strm, System.Text.UTF8Encoding.UTF8, false, 256))
            {
                var line = txtstrm.ReadLine();
                while (line != null)
                {
                    Console.WriteLine(line);
                    line = txtstrm.ReadLine();
                }
            }
#endif 
            return true;
        }
        private GitDeploymentRepository GetGitDeploymentRepo(Uri origin = null)
        {
            var repo = new GitDeploymentRepository(Config.GitRepositoryDirectory, origin);
            return repo;
        }
        #endregion     

    }
}
