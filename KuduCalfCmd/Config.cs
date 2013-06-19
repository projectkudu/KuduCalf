using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
using System.IO;
using System.Reflection;
using System.Diagnostics;
namespace KuduCalfCmd
{
    class Config
    {
        public FileVersionInfo VersionInfo 
        {
            get
            {
                if (_fileVersionInfo == null)
                {
                   _fileVersionInfo =  FileVersionInfo.GetVersionInfo(ExecutingProgramLocation.FullName);
                }
                return _fileVersionInfo;
            }
        }

        public DirectoryInfo GitRepositoryDirectory
        {
            get
            {
                if (_gitRepositoryDirectory == null)
                {
                    var dir = ConfigurationManager.AppSettings["GitRepositoryDirectory"];
                    if (string.IsNullOrEmpty(dir))
                    {
                        dir = Environment.GetEnvironmentVariable("DEPLOYMENT_TARGET");
                    }
                    if (string.IsNullOrEmpty(dir))
                    {
                        dir = Environment.GetEnvironmentVariable("WEBROOT_PATH");
                    }
                    if (string.IsNullOrEmpty(dir))
                    {
                        dir = Path.Combine(ExecutingProgramLocation.DirectoryName, "KuduCalfCmd.repo");
                    }
                    _gitRepositoryDirectory = new DirectoryInfo(dir);
                    _gitRepositoryDirectory.Create();
                }
                return _gitRepositoryDirectory;
            }
            set
            {
                _gitRepositoryDirectory = value;
            }
        }

        public string DefaultPublisherId
        {
            get
            {   var id = Environment.GetEnvironmentVariable("APP_POOL_ID");
                if (string.IsNullOrEmpty(id))
                {
                    id = Path.GetFileNameWithoutExtension(ExecutingProgramLocation.Name);  
                }
                return id;
            }
        }

        public DirectoryInfo SubscriberStateDirectory
        {
            get
            {
                if (_subscriberStateDirectory == null)
                {
                    var dir = ConfigurationManager.AppSettings["SubscriberStateDirectory"];
                    if (string.IsNullOrEmpty(dir))
                    {
                        dir = Path.Combine(ExecutingProgramLocation.DirectoryName, "KuduCalfCmd.db");
                    }
                    _subscriberStateDirectory = new DirectoryInfo(dir);
                    _subscriberStateDirectory.Create();
                }
                return _subscriberStateDirectory;
            }
        }
       
        private FileInfo ExecutingProgramLocation
        {
            get
            {
                if (_executingProgramLocation == null)
                {
                    var asm = Assembly.GetExecutingAssembly();
                    var uri = new Uri(asm.CodeBase);
                    _executingProgramLocation = new FileInfo(uri.LocalPath);
                }
                return _executingProgramLocation;
            }
        }
        private FileVersionInfo _fileVersionInfo;
        private FileInfo _executingProgramLocation;
        private DirectoryInfo _gitRepositoryDirectory;
        private DirectoryInfo _subscriberStateDirectory;
        
       
    }
}
