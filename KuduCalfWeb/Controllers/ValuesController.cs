﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web;
using System.Web.Http;
using LibGit2Sharp;

namespace KuduCalfWeb.Controllers
{
    public class ValuesController : ApiController
    {
        // GET api/values
        public string Get()
        {
            string targetWebRoot = ConfigurationManager.AppSettings["WebRoot"];
            targetWebRoot = Path.Combine(HttpRuntime.AppDomainAppPath, targetWebRoot);

            string repoUri = ConfigurationManager.AppSettings["RemoteRepository"];

            if (!Repository.IsValid(targetWebRoot))
            {
                using (var repo = Repository.Clone(repoUri, targetWebRoot)) { }
            }
            else
            {
                using (var repo = new Repository(targetWebRoot))
                {
                    // TODO: is there a cleaner way of doing a pull?
                    repo.Fetch("origin");
                    repo.Reset(ResetOptions.Hard, "origin/master");
                }
            }

            return targetWebRoot;
        }
    }
}