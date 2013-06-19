using LibGit2Sharp;
using LibGit2Sharp.Handlers;
using System;
using System.Web;
using System.Web.Hosting;
using System.Web.Configuration;
using System.IO;
using Newtonsoft.Json;
namespace KuduCalfWeb
{
    /// <summary>
    /// Summary description for KuduCalf
    /// </summary>
    public class KuduCalfWebHttpHandler : IHttpHandler
    {
        static System.Diagnostics.Stopwatch stopWatch = System.Diagnostics.Stopwatch.StartNew();
        static int requestCounter = 0;
        const int throttleIntervalInSeconds = 10;
        const int maxRequestPerInterval = 10;
        const string publicUriSettingName = "kuducalfweb.publicuri";
        public void ProcessRequest(HttpContext context)
        {
            try
            {
                var op = context.Request.Params["comp"];

                if (Protocol.OperationInit.Equals(op, StringComparison.OrdinalIgnoreCase))
                {
                    DoInit(context);
                    return;
                }
                if (Protocol.OperationUpdateNotifyAll.Equals(op, StringComparison.OrdinalIgnoreCase))
                {
                    DoUpdateNotifyAll(context);
                    return;
                }
                if (Protocol.OperationUpdateNotify.Equals(op, StringComparison.OrdinalIgnoreCase))
                {
                    DoUpdateNotify(context);
                    return;
                }
                if (Protocol.OperationStatus.Equals(op, StringComparison.OrdinalIgnoreCase))
                {
                    GetStatus(context);
                    return;
                }
                SendResponse(context, 400, "Bad request");
            }
            catch (Exception ex)
            {
                SendResponseBody(context, 500, ex.ToString());
            }
        }

        public void DoInit(HttpContext context)
        {
            if (!context.Request.HttpMethod.Equals("POST", StringComparison.InvariantCulture))
            {
                SendResponse(context, 405, "Method {0} not supported.", context.Request.HttpMethod);
            }

            if (!context.Request.IsLocal)
            {
                SendResponse(context, 403, "Remote init not supported.");
                return;
            }

            var targetWebRoot = GetTargetWebRoot();
            string body = null;
            Protocol.InitRequest initReq = null;
            using (var strm = new StreamReader(context.Request.InputStream))
            {
               body = strm.ReadToEnd();
               initReq = JsonConvert.DeserializeObject<Protocol.InitRequest>(body);
            }

            if (initReq == null || 
                String.IsNullOrEmpty(initReq.PublicUri) ||
                String.IsNullOrEmpty(initReq.RepositoryUri) ||
                String.IsNullOrEmpty(initReq.PrivateUri)
                )
            {
                SendResponse(context, 400, "Bad request: {0}", body);
                return;
            }

            if (String.IsNullOrEmpty(targetWebRoot))
            {
                SendResponse(context, 404, "No web root target.");
                return;
            }

            if (Repository.IsValid(targetWebRoot))
            {
                SendResponse(context, 409, "Repository already created at: {0}", targetWebRoot);
                return;
            }

            string snapshotId = null;
            string siteId = null;
            var handlers = GetProgressHandlers(context);
            using (var repo = Repository.Clone(initReq.RepositoryUri, targetWebRoot, false, false,
                onCheckoutProgress: handlers.CheckoutProgressHandler,
                onTransferProgress: handlers.TransferProgressHandler))
            {
                snapshotId = GetLatestSnapShotId(repo);
                var scmUri = GetScmUri(repo);
                var publicUri = new Uri(initReq.PublicUri);
                var privateUri = new Uri(initReq.PrivateUri);
                siteId = HostingEnvironment.ApplicationHost.GetSiteID();
                repo.Config.Set(publicUriSettingName, publicUri.AbsoluteUri, ConfigurationLevel.Local);
                Protocol.KuduCalfCmdRegisterSite(scmUri, siteId, publicUri, privateUri);
            }
 
            if (snapshotId == null)
            {
                SendResponse(context, 200, "Cloned empty or non-existent repo {0} ", targetWebRoot);
                return;
            }
            SendResponse(context, 200, "Cloned repo {0} head is {1}", targetWebRoot, snapshotId);
        }

        public void DoUpdateNotifyAll(HttpContext context)
        {
            if (!context.Request.HttpMethod.Equals("POST", StringComparison.InvariantCulture))
            {
                SendResponse(context, 405, "Method {0} not supported.", context.Request.HttpMethod);
            }
            var count = System.Threading.Interlocked.Increment(ref requestCounter);

            if (ThrottleRequest())
            {
                SendResponse(context, 503, "Server busy");
                return;
            }

            var targetWebRoot = GetTargetWebRoot();
            if (String.IsNullOrEmpty(targetWebRoot))
            {
                SendResponse(context, 404, "No web root target.");
                return;
            }

            if (!Repository.IsValid(targetWebRoot))
            {
                SendResponse(context, 404, "No repository configured at: {0}", targetWebRoot);
                return;
            }
            Uri scmUri = null;
            Uri publicUri = null;
            using (var repo = new Repository(targetWebRoot))
            {
                scmUri = GetScmUri(repo);
                var uriFromConfig = repo.Config.Get<string>(publicUriSettingName).Value;
                if (String.IsNullOrEmpty(uriFromConfig))
                {
                    SendResponse(context, 500, "Unable to read repository configuration data.");
                    return;
                }
                publicUri = new Uri(uriFromConfig);
            }
            var uris = Protocol.KuduCalfCmdGetNeedSync(scmUri, publicUri);
            var resps = new System.Text.StringBuilder();
            foreach (var privateUri in uris)
            {
                resps.AppendFormat("Response from: {0}\n", privateUri);
                var resp = Protocol.KuduCalfWebUpdateNotify(privateUri);
                resps.AppendLine(resp);
            }
            SendResponseBody(context, 202, resps.ToString());
        }

        public void DoUpdateNotify(HttpContext context)
        {
            if (!context.Request.HttpMethod.Equals("POST", StringComparison.InvariantCulture))
            {
                SendResponse(context, 405, "Method {0} not supported.", context.Request.HttpMethod);
            }
            if (ThrottleRequest())
            {
                SendResponse(context, 503, "Server busy");
                return;
            }

            TryUpdateRepository(context);
        }
       
        static public  Uri GetScmUri(Repository repo)
        {
           var repoUri = new UriBuilder(repo.Network.Remotes["origin"].Url);
           repoUri.Query = null;
           repoUri.Path = null;
           return repoUri.Uri;
        }

        public void TryUpdateRepository(HttpContext context)
        {
            var targetWebRoot = GetTargetWebRoot();
            if (String.IsNullOrEmpty(targetWebRoot))
            {
                SendResponse(context, 404, "No web root target.");
                return;
            }

            if (!Repository.IsValid(targetWebRoot))
            {
                SendResponse(context, 404, "No repository configured at: {0}", targetWebRoot);
                return;
            }

            string snapshotId = null;
            string siteId = null;
            var handlers = GetProgressHandlers(context);
            using (var repo = new Repository(targetWebRoot))
            {
                repo.Fetch("origin", onCompletion: handlers.CompletionHandler,
                    onTransferProgress: handlers.TransferProgressHandler,
                    onProgress: msg => SendProgress(context, msg));
                repo.Reset(ResetOptions.Hard, "origin/master");
                snapshotId = GetLatestSnapShotId(repo);
                siteId = HostingEnvironment.ApplicationHost.GetSiteID();
                var url = GetScmUri(repo);
                Protocol.KuduCalfCmdUpdateSiteStatus(url, siteId, snapshotId);
            }
            SendResponse(context, 200, "Updated root {0} of subscriber {1} to {2}", targetWebRoot, siteId, snapshotId);
        }

        public void GetStatus(HttpContext context)
        {
            var targetWebRoot = GetTargetWebRoot();
            if (String.IsNullOrEmpty(targetWebRoot))
            {
                SendResponse(context, 404, "No web root target.");
                return;
            }

            if (!Repository.IsValid(targetWebRoot))
            {
                SendResponse(context, 404, "No repository configured at: {0}", targetWebRoot);
                return;
            }

            string snapshotId = null;
            using (var repo = new Repository(targetWebRoot))
            {
                snapshotId = GetLatestSnapShotId(repo);
            }
            SendResponse(context, 200, "Site {0} at {1}", targetWebRoot, snapshotId);
        }

        static string GetLatestSnapShotId(Repository repo)
        {
            string commitId = null;
            if (repo != null && repo.Head != null && repo.Head.Tip != null && repo.Head.Tip.Id != null)
            {
                commitId = Convert.ToBase64String(repo.Head.Tip.Id.RawId);
            }
            return commitId;
        }
        static SmartSync.GitProgressEvents GetProgressHandlers(HttpContext ctxt)
        {
            var handlers = new SmartSync.GitProgressEvents();
            handlers.CheckoutProgressUpdate += (path, completed, total) => 
            {
                SendProgress(ctxt, "[Checkout {0} of {1} steps complete]\n", completed, total);
                return;
            };

            handlers.TransferProgressUpdate += progress =>
            {
                SendProgress(ctxt, "[Transferred {0} bytes and {1} out of {2} objects]\n", 
                    progress.ReceivedBytes, 
                    progress.ReceivedObjects, 
                    progress.TotalObjects);
                return 0;
            };
            return handlers;
        }

        static string GetTargetWebRoot()
        {
            string targetWebRoot = HostingEnvironment.MapPath("~/App");
            return targetWebRoot;
        }

        static void SendResponse(HttpContext context, int statusCode, string fmt, params object[] args)
        {
            var msg = String.Format(fmt, args);
            if (IsStreamingModeEnable(context))
            {
                context.Response.Write(msg);
                // add what would have been the final status code for good measure.
               // context.Response.Headers["X-Status"] = statusCode.ToString();
            }
            else
            {
                context.Response.StatusCode = statusCode;
                context.Response.StatusDescription = msg;
                context.Response.ContentType = "text/plain";
                context.Response.Write(msg);
            }
          
        }

        static void SendResponseBody(HttpContext context, int statusCode, string body)
        {
                context.Response.StatusCode = statusCode;
                context.Response.ContentType = "text/plain";
                context.Response.Write(body);
        }

        static bool IsStreamingModeEnable(HttpContext context)
        {
            return !context.Response.BufferOutput && !context.Response.Buffer; 
        }

        static void EnableStreamingMode(HttpContext context)
        {
            if (!IsStreamingModeEnable(context))
            {
                // 202 a hint we are doing a long operation and streaming incremental progress.
                // which also is a hint we sent the headers already.
                context.Response.Buffer = false;
                context.Response.BufferOutput = false;
                context.Response.ContentType = "text/plain";
                context.Response.StatusCode = 202;
            }
        }

        static void SendProgress(HttpContext context, string msg)
        {
            EnableStreamingMode(context); 
            context.Response.Write(msg);
           
        }

        static void SendProgress(HttpContext context,string fmt, params object[] args)
        {
            var msg = String.Format(fmt, args);
            SendProgress(context, msg);
        }

        // simple rate throttle.
        private bool ThrottleRequest()
        {
            var count = System.Threading.Interlocked.Increment(ref requestCounter);
            if (stopWatch.Elapsed > TimeSpan.FromSeconds(throttleIntervalInSeconds))
            {
                System.Threading.Interlocked.Exchange(ref requestCounter, 0);
                stopWatch.Reset();
            }
            return (count > maxRequestPerInterval);
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