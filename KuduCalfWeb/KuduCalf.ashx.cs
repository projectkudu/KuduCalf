using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Web;
using LibGit2Sharp;

namespace KuduCalfWeb
{
    /// <summary>
    /// Summary description for KuduCalf
    /// </summary>
    public class KuduCalf : IHttpHandler
    {
        public void ProcessRequest(HttpContext context)
        {
            context.Response.ContentType = "text/plain";

            string targetWebRoot = ConfigurationManager.AppSettings["WebRoot"];
            targetWebRoot = Path.Combine(HttpRuntime.AppDomainAppPath, targetWebRoot);

            string repoUri = ConfigurationManager.AppSettings["RemoteRepository"];

            var creds = new Credentials
            {
                Username = ConfigurationManager.AppSettings["RemoteRepositoryUsername"],
                Password = ConfigurationManager.AppSettings["RemoteRepositoryPassword"]
            };

            if (!Repository.IsValid(targetWebRoot))
            {
                using (var repo = Repository.Clone(repoUri, targetWebRoot, credentials: creds)) { }
            }
            else
            {
                using (var repo = new Repository(targetWebRoot))
                {
                    // TODO: is there a cleaner way of doing a pull?
                    repo.Fetch("origin", credentials: creds);
                    repo.Reset(ResetOptions.Hard, "origin/master");
                }
            }

            context.Response.Write(targetWebRoot);
        }

        public bool IsReusable
        {
            get
            {
                return false;
            }
        }
    }
}