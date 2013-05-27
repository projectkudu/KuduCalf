using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
namespace SmartSync
{
    public interface ILogEvent
    {
        [LogEvent(TraceEventType.Error, "The combination of {0} and {1} does not result in a path under {0}.")]
        void CombinedPathNotUnderExpectedDirectory(string dirRoot, string path);

        [LogEvent(TraceEventType.Warning, "Skipped update of snapshot item {0} because or previous errors.")]
        void UpdateOfItemSkipedBecauseOfError(string relPath);

        [LogEvent(TraceEventType.Warning, "Did not find {0} in repository.")]
        void SnapshotIdNotFound(SnapshotId token);

        [LogEvent(TraceEventType.Error, "File: {0}. Exception: {1}")]
        void UnexpectedExceptionDuringPopulate(string fullPath, Exception ex);

        [LogEvent(TraceEventType.Warning, "Skipped deletion of {0} because of previous error.")]
        void DeleteOfItemSkipedBecauseOfError(string relPath);

        [LogEvent(TraceEventType.Information, "File: {0} Size: {1} LastModified:{2}")]
        void ExtractedSnapshotContentTo(string fullPath, long size, DateTimeOffset dateTimeOffset);

        [LogEvent(TraceEventType.Verbose, "Deleted File:{0}")]
        void DeletedFile(string fullPath);

        [LogEvent(TraceEventType.Warning, "Unexpected while garbage collecting agents: {0}.")]
        void UnexpectedExceptionAgentGarabeCollect(Exception ex);

        [LogEvent(TraceEventType.Error, "Unexpected exception while copying file {0} to {1}. Exception {2}")]
        void UnexpectedExceptionDuringFileCopy(string src, string dst,Exception ex);

        [LogEvent(TraceEventType.Information, "Copied file from {0} to {1}.")]
        void CopiedFile(string src, string dst);
        
        [LogEvent(TraceEventType.Verbose, "Skip copied of file from {0} to {1} no change found.")]
        void SkipFileCopyNoChange(string src, string dst);

        [LogEvent(TraceEventType.Information, "Total of {0} files out of a total set of {1} were copied in {2} milliseconds. (Note only first 25 copies logged.)")]
        void CopySummary(int copiedFiles, int totalFiles, long milliseconds);

        [LogEvent(TraceEventType.Information, "Received {0} bytes and {1} objects for {2} total objects.")]
        void TransferProgressInformation(long receivedBytes, int receivedObjects, int totalObjects);

        [LogEvent(TraceEventType.Information, "Checkout in progress {0:P} complete.")]
        void CheckoutProgressInformation(double percent);

        [LogEvent(TraceEventType.Information, "Fetched completed {0}")]
        void FetchCompletionInformation(LibGit2Sharp.RemoteCompletionType remoteCompletionType);

        [LogEvent(TraceEventType.Error, "Fetch completed with error")]
        void FetchCompletionError();
    }
}
