using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.Http.Headers;
using System.Diagnostics;
namespace KuduCalfWeb
{
    public static class Protocol
    {
        // see http://tools.ietf.org/html/rfc2606
        // we assume there is a shadow site listening on
        // the following invalid host header.
        public const string KuduCalfHostHeader = "kuducalf.invalid";

        public const string OperationInit = "init";
        public const string OperationUpdateNotify = "notify";
        public const string OperationUpdateNotifyAll = "notifyall";
        public const string OperationStatus = "status";

        public class CommandResult
        {
            public string Error { get; set; }
            public string Output { get; set; }
            public int ExitCode { get; set; }
        }

        public class CommandRequest
        {
            public string command { get; set; }
            public string dir { get; set; }
        }

        public class InitRequest
        {
            public string RepositoryUri { get; set; }
            public string PublicUri { get; set; }
            public string PrivateUri { get; set; }
        }

        [Serializable]
        public class KuduCalfCmdRemoteException : Exception
        {
            public KuduCalfCmdRemoteException(string msg)
                : base(msg)
            {
            }
        }

        private static NetworkCredential GetCredentailsFromUri(Uri uri)
        {
            if (uri.UserInfo == null)
            {
                return null;
            }
            var toks = uri.UserInfo.Split(':');
            var username = toks[0];
            var password = toks[1];
            return new NetworkCredential(username, password);
        }

        private static HttpClient GetClient(NetworkCredential creds = null)
        {
            var handler = new HttpClientHandler();
            handler.Credentials = creds;
            return new HttpClient(handler);
        }

        private static HttpRequestMessage KuduCalfWebRequestMessage(HttpMethod mth, Uri uri, string operation)
        {
            var requestUri = new Uri(uri, String.Format("KuduCalfWeb.ashx?comp={0}", operation));
            var req = new HttpRequestMessage(mth, requestUri);
            req.Headers.Host = KuduCalfHostHeader;
            return req;
        }

        private static string WaitResponse(Task<HttpResponseMessage> resp, out System.Net.HttpStatusCode status)
        {
            resp.Wait();
            status = resp.Result.StatusCode;
            if (resp.Result.Content == null)
            {
                return null;
            }
            var body = resp.Result.Content.ReadAsStringAsync();
            body.Wait();
            return body.Result;
        }

        private static string FormatResponse(Task<HttpResponseMessage> resp)
        {
            System.Net.HttpStatusCode status;
            var body = WaitResponse(resp, out status);
            return String.Format("Status: {0}\n{1}", status, body);
        }
        
        private static HttpContent CreateJsonContent(IEnumerable<KeyValuePair<string, string>> items)
        {
            var jsonObject = new JObject();
            foreach (KeyValuePair<string, string> kv in items)
            {
                jsonObject.Add(kv.Key, kv.Value);
            }
            var content =  new StringContent(jsonObject.ToString());
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json")
            {
                CharSet = "utf-8"
            };
            return content;
        }

        private static T GetJsonResponse<T>(Task<HttpResponseMessage> resp) where T : class
        {
            System.Net.HttpStatusCode status;
            var body = WaitResponse(resp, out status);
            if (body == null || status != System.Net.HttpStatusCode.OK)
            {
                return null;
            }
            return JsonConvert.DeserializeObject<T>(body);
        }

        static private FileVersionInfo VersionInfo
        {
            get
            {
                return FileVersionInfo.GetVersionInfo(System.Reflection.Assembly.GetExecutingAssembly().Location); 
            }
        }

        private static Uri GetDefaultPrivateUri(Uri publicUri)
        {
            var hostname = System.Net.Dns.GetHostName();
            var ipv4Address = System.Net.Dns.GetHostAddresses(hostname)
                            .Where(address => address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                            .FirstOrDefault();
            if (ipv4Address != null)
            {
                var uriBuilder = new UriBuilder(publicUri);
                uriBuilder.Host = ipv4Address.ToString();
                return uriBuilder.Uri;
            }
            return publicUri;
        }

        public static Task<HttpResponseMessage> KuduCalfWebInitAsync(Uri kuduCalfUri, Uri scmUri, string publicUri, string privateUri)
        {
            var client = GetClient();
            var repoUri = new Uri(scmUri, "Git/site/wwwroot");
            var req = KuduCalfWebRequestMessage(HttpMethod.Post, kuduCalfUri, OperationInit);
            if (String.IsNullOrEmpty(privateUri))
            {
                privateUri = GetDefaultPrivateUri(new Uri(publicUri)).AbsoluteUri;
            }
            var initreq = new InitRequest()
            {
                RepositoryUri = repoUri.AbsoluteUri,
                PublicUri = publicUri,
                PrivateUri = privateUri
            };
            var json = JsonConvert.SerializeObject(initreq);
            req.Content = new StringContent(json, System.Text.UTF8Encoding.UTF8);
            req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json")
            {
                CharSet = "utf-8"
            };
            return client.SendAsync(req);
        }
        
        public static Task<HttpResponseMessage> KuduCalfWebStatusAsync(Uri kuduCalfUri)
        {
            var client = GetClient();
            var req = KuduCalfWebRequestMessage(HttpMethod.Get, kuduCalfUri, OperationStatus);
            return client.SendAsync(req);
        }

        public static Task<HttpResponseMessage> KuduCalfWebUpdateNotifyAsync(Uri uri)
        {
            var client = GetClient();
            var req = KuduCalfWebRequestMessage(HttpMethod.Post, uri, OperationUpdateNotify);
            return client.SendAsync(req);
        }

        public static Task<HttpResponseMessage> KuduCalfWebUpdateNotifyAllAsync(Uri uri)
        {
            var client = GetClient();
            var req = KuduCalfWebRequestMessage(HttpMethod.Post, uri, OperationUpdateNotifyAll);
            return client.SendAsync(req);
        }

        public static string KuduCalfWebInit(Uri kuduCalfUri, Uri scmUri, string publicUri, string privateUri)
        {
            var resp = KuduCalfWebInitAsync(kuduCalfUri, scmUri, publicUri, privateUri);
            return FormatResponse(resp);
        }

        public static string KuduCalfWebUpdateNotify(Uri kuduCalfUri)
        {
            var resp = KuduCalfWebUpdateNotifyAsync(kuduCalfUri);
            return FormatResponse(resp);
        }

        public static string KuduCalfWebUpdateNotifyAll(Uri kuduCalfUri)
        {
            var resp = KuduCalfWebUpdateNotifyAllAsync(kuduCalfUri);
            return FormatResponse(resp);
        }

        public static string KuduCalfWebStatus(Uri kuduCalfUri)
        {
            var resp = KuduCalfWebStatusAsync(kuduCalfUri);
            return FormatResponse(resp);
        }

        // TOOD: consider using this as the host header for multi-site scenario.
        // siteId is the IIS siteid.
        public static string GetSubscriberId(string siteId)
        {
            var fqdn = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName()).HostName;
            return string.Format("site{0}.kuducalf.{1}", siteId, fqdn).ToLowerInvariant();
        }

        public static string ScmPatchFilesFromZip(Uri scmUri, string remotePath, string localPath)
        {
            var nc = GetCredentailsFromUri(scmUri);
            using (var client = GetClient(nc))
            using (var fs = System.IO.File.OpenRead(localPath))
            {
                var targetUri = new Uri(scmUri, "zip/"+remotePath.Trim('/'));
                var content = new StreamContent(fs);
                var resp = client.PutAsync(targetUri, content);
                return FormatResponse(resp);
            }
        }

        public static string ScmUpdateSetting(Uri scmUri, params KeyValuePair<string,string>[] settings)
        {
            var nc = GetCredentailsFromUri(scmUri);
            using (var client = GetClient(nc))
            {
                var targetUri = new Uri(scmUri, "settings");
                var content = CreateJsonContent(settings);
                var resp = client.PostAsync(targetUri, content);
                return FormatResponse(resp);
            }
        }

        public static string ScmUpdateKuduCalfCmd(Uri scmUri, string localPath, bool forceUpdate = false)
        {
            var currentVersion = VersionInfo.FileVersion.ToString();
            var needUpdate = !KuduCalfCmdCheckToolVersion(scmUri, currentVersion);
            var res = "No update needed";
            if (needUpdate || forceUpdate)
            {
                res = ScmPatchFilesFromZip(scmUri, "site", localPath);
                res += ScmUpdateSetting(scmUri, new KeyValuePair<string, string>("KUDU_SYNC_CMD", @"%HOME%\KuduCalfCmd\KuduCalfCmd.exe KuduSync"));
                res += KuduCalfCmdInitSyncState(scmUri);
            }
            return res;
        }
          
        public static string KuduCalfCmdRemote(Uri scmUri, string cmd)
        {
            var req = new CommandRequest()
            {
                dir = ".",
                command = @"%HOME%\KuduCalfCmd\KuduCalfCmd.exe " + cmd
            };
            var nc = GetCredentailsFromUri(scmUri);
            var client = GetClient(nc);
            var requestUri = new Uri(scmUri, "command");
            var msg = new HttpRequestMessage(HttpMethod.Post, requestUri);
            var json = JsonConvert.SerializeObject(req);
            msg.Content = new StringContent(json, System.Text.UTF8Encoding.UTF8);
            msg.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json")
                {
                    CharSet = "utf-8"
                };
            var resp = client.SendAsync(msg);
            var cresp = GetJsonResponse<CommandResult>(resp);
            if (cresp.ExitCode != 0)
            {
                throw new KuduCalfCmdRemoteException(cresp.Error);
            }
            if (cresp == null)
            {
                return String.Format("Request Failed");
            }
            if (cresp.Output == null)
            {
                return String.Format("ExitCode: {0}\nError: {1}", cresp.ExitCode, cresp.Error);
            }
            return cresp.Output;
        }

        public static void KuduCalfCmdUpdateSiteStatus(Uri scmUri, string siteId, string snapshotToken)
        {

            var subId = GetSubscriberId(siteId);
            var cmd = String.Format("Set-SubscriberSiteStatus -S {0} -T {1}", subId, snapshotToken);
            KuduCalfCmdRemote(scmUri, cmd);
        }

        public static void KuduCalfCmdRegisterSite(Uri scmUri, string siteId, Uri publicUri, Uri privateUri)
        {
            var subId = GetSubscriberId(siteId);
            var cmd = String.Format("New-Subscriber -S {0} --PublicUpdateNotifyUrl {1} --PrivateUpdateNotifyUrl {2}", subId, publicUri.AbsoluteUri, privateUri.AbsoluteUri);
            KuduCalfCmdRemote(scmUri, cmd);
        }

        public static bool KuduCalfCmdCheckToolVersion(Uri scmUri,string expectedVersion)
        {
            try
            {
                var res = KuduCalfCmdRemote(scmUri, "Get-ToolVersion -c " + expectedVersion);
                if (!String.IsNullOrEmpty(res))
                {
                    return Boolean.Parse(res);
                }
            }
            catch (KuduCalfCmdRemoteException)
            {
                return false;
            }
            return false;
        }

        public static string KuduCalfCmdInitSyncState(Uri scmUri)
        {
           return KuduCalfCmdRemote(scmUri,"Initialize-SyncState");
        }

        public static IEnumerable<Uri> KuduCalfCmdGetNeedSync(Uri scmUri, Uri publicUri)
        {
            var re = new System.Text.RegularExpressions.Regex(@"^\s*PrivateUri\s*:\s*(.+)\s*$");
            var cmd = String.Format("Watch-SyncState -T 0 -u {0}", publicUri.AbsoluteUri);
            var res = KuduCalfCmdRemote(scmUri, cmd);
            var lines = res.Split('\n');
            var ret = new List<Uri>();
            foreach (var line in lines)
            {
                var match = re.Match(line);
                if (match.Success)
                {
                    var uri = match.Groups[1].Value;
                    ret.Add(new Uri(uri, UriKind.Absolute));
                }
            }
            return ret;
        }
    }
}