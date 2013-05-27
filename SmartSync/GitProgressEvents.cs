using System;
using System.Diagnostics;
using LibGit2Sharp;
using LibGit2Sharp.Handlers;
using System.Diagnostics.CodeAnalysis;
namespace SmartSync
{
    /// <summary>
    /// Useful wrapper for dealing with git progress events.
    /// </summary>
    /// <remarks>
    /// Marked as internal as it violates some FxCop design guidelines for public apis.
    /// </remarks>
    internal class GitProgressEvents
    {
        public class UpdatePolicy
        {
            public UpdatePolicy()
            {
                ProgressStopWatch = new Stopwatch();
                UpdateOnPercentProgress = 0.05;
                UpdateOnTimeElapsed = TimeSpan.FromSeconds(15);
            }
            public double UpdateOnPercentProgress { get; set; }
            public TimeSpan UpdateOnTimeElapsed { get; set; }
            private double NextUpdatePercent { get; set; }
            private Stopwatch ProgressStopWatch { get; set; }
            private bool FirstUpdate { get; set; }
            public bool UpdateNeeded(double percentCompleted)
            {
                var doUpdate = false;
                // always update on the first call
                if (!FirstUpdate)
                {
                    doUpdate = true;
                    FirstUpdate = true;
                }
                if (ProgressStopWatch.Elapsed > UpdateOnTimeElapsed)
                {
                    doUpdate = true;
                }
                while (percentCompleted > NextUpdatePercent)
                {
                    NextUpdatePercent += UpdateOnPercentProgress;
                    doUpdate = true;
                }
                // restart the time on any update.
                if (doUpdate)
                {
                    ProgressStopWatch.Restart();
                }
                return doUpdate;
            }
        }
        public bool TransferCanceled { get; private set; }
  
        public UpdatePolicy TransferProgressUpdatePolicy { get; private set; }
        public UpdatePolicy CheckoutProgressUpdatePolicy { get; private set; }
        
        
        public event CheckoutProgressHandler CheckoutProgressUpdate;
        public event TransferProgressHandler TransferProgressUpdate;
        public event CompletionHandler CompletionUpdate;
        public GitProgressEvents()
        {
            TransferProgressUpdatePolicy = new UpdatePolicy();
            CheckoutProgressUpdatePolicy = new UpdatePolicy();
        }

        public void CheckoutProgressHandler(string path, int completedSteps, int totalSteps)
        {
            if (CheckoutProgressUpdate != null)
            {
                if (CheckoutProgressUpdatePolicy.UpdateNeeded(((double)completedSteps) / ((double)totalSteps)))
                {
                    CheckoutProgressUpdate(path, completedSteps, totalSteps);
                }
            }
        }

        public int CompletionHandler(RemoteCompletionType type)
        {
            if (CompletionUpdate != null)
            {
               var ret = CompletionHandler(type);
               if (ret < 0)
               {
                   TransferCanceled = true;
               }
               return ret;
            }
            return 0;
        }

        public int TransferProgressHandler(TransferProgress progress)
        {
            if (TransferProgressUpdate != null)
            {
                if (TransferProgressUpdatePolicy.UpdateNeeded(((double)progress.ReceivedObjects) / ((double)progress.TotalObjects)))
                {
                    var ret = TransferProgressUpdate(progress);
                    if (ret < 0)
                    {
                        TransferCanceled = true;
                    }
                    return ret;
                }
            }
            return 0;
        }
    }
}
