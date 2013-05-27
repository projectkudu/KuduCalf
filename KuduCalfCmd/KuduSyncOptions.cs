using CommandLine;
using CommandLine.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KuduCalfCmd
{
    // copied from KuduSync.NET so we can get the same argument parsing.
    public class KuduSyncOptions 
    {
        [Option('f', "from", Required = true, HelpText = "Source directory to sync")]
        public string From { get; set; }

        [Option('t', "to", Required = false, HelpText = "Destination directory to sync")]
        public string To { get; set; }

        [Option('n', "nextManifest", Required = false, HelpText = "Next manifest file path")]
        public string NextManifestFilePath { get; set; }

        [Option('p', "previousManifest", Required = false, HelpText = "Previous manifest file path")]
        public string PreviousManifestFilePath { get; set; }

        [Option('i', "ignore", Required = false, HelpText = "List of files/directories to ignore and not sync, delimited by ;")]
        public string Ignore { get; set; }

        [Option('q', "quiet", Required = false, HelpText = "No logging")]
        public bool Quiet { get; set; }

        [Option('v', "verbose", Required = false, HelpText = "Verbose logging with maximum number of output lines")]
        public int? Verbose { get; set; }

        [Option('w', "whatIf", Required = false, HelpText = "Only log without actual copy/remove of files")]
        public bool WhatIf { get; set; }

        [Option("perf", Required = false, HelpText = "Print out the time it took to complete KuduSync operation")]
        public bool Perf { get; set; }

        [HelpOption]
        public string GetUsage()
        {
            return HelpText.AutoBuild(this, (HelpText current) => HelpText.DefaultParsingErrorsHandler(this, current));
        }
    }
}
