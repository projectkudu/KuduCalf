using LibGit2Sharp;
using LibGit2Sharp.Handlers;
using System;
using System.Web;
using System.Web.Hosting;
using System.Web.Configuration;
using System.IO;
namespace KuduCalfWeb
{
    /// <summary>
    /// Summary description for KuduCalf
    /// </summary>
    public class KuduCalf : IHttpHandler
    {
        public void ProcessRequest(HttpContext context)
        {
            var op = context.Request.Params["comp"];
            
            if ("init".Equals(op, StringComparison.OrdinalIgnoreCase))
            {
                DoInit(context);
                return;
            }
            if ("fetch".Equals(op, StringComparison.OrdinalIgnoreCase))
            {
                DoFetch(context);
                return;
            }
            GetStatus(context);
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
            string uri = null;
            using (var strm = new StreamReader(context.Request.InputStream))
            {
                uri = strm.ReadToEnd().Trim();
            }

            if (string.IsNullOrEmpty(uri))
            {
                SendResponse(context, 400, "No Uri data");
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

            string commitId = null;
            var handlers = GetProgressHandlers(context);
            using (var repo = Repository.Clone(uri, targetWebRoot, false, true,
                onCheckoutProgress: handlers.CheckoutProgressHandler,
                onTransferProgress: handlers.TransferProgressHandler))
            {
                if (repo != null && repo.Head != null && repo.Head.Tip != null && repo.Head.Tip.Sha != null)
                {
                    commitId = repo.Head.Tip.Sha;
                }
            }
            if (commitId == null)
            {
                SendResponse(context, 200, "Cloned empty or non-existent repo {0} ", targetWebRoot);
                return;
            }
            SendResponse(context, 200, "Cloned repo {0} head is {1}", targetWebRoot, commitId);
        }

        public void DoFetch(HttpContext context)
        {
            if (!context.Request.HttpMethod.Equals("POST", StringComparison.InvariantCulture))
            {
                SendResponse(context, 405, "Method {0} not supported.", context.Request.HttpMethod);
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

            string commitId = null;
            var handlers = GetProgressHandlers(context);
            using (var repo = new Repository(targetWebRoot))
            {
                repo.Fetch("origin",onCompletion: handlers.CompletionHandler,
                    onTransferProgress: handlers.TransferProgressHandler,
                    onProgress: msg => SendProgress(context, msg));
                repo.Reset(ResetOptions.Hard, "origin/master");
                commitId = repo.Head.Tip.Sha;
            }
            SendResponse(context, 200, "Updated {0} to {1}", targetWebRoot, commitId);
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

            string commitId = null;
            using (var repo = new Repository(targetWebRoot))
            {
                commitId = repo.Head.Tip.Sha;
            }
            SendResponse(context, 200, "Site {0} at {1}", targetWebRoot, commitId);
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


        public bool IsReusable
        {
            get
            {
                return false;
            }
        }
    }
}