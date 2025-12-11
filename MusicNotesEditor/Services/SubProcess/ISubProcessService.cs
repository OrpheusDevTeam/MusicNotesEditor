using System;
using System.Threading;
using System.Threading.Tasks;

namespace MusicNotesEditor.Services.SubProcess
{
    public interface ISubProcessService
    {
        /// <summary>
        /// Processes multiple files using a Python script asynchronously
        /// </summary>
        /// <param name="orderedFiles">Array of file paths to process in order</param>
        /// <param name="progress">Progress reporter for status updates</param>
        /// <param name="cancellationToken">Cancellation token to stop the process</param>
        /// <returns>The output from the Python script execution</returns>
        Task<string> ProcessFilesWithPythonAsync(string[] orderedFiles, IProgress<string> progress, CancellationToken cancellationToken = default);

        /// <summary>
        /// Executes a JavaScript script using Node.js from the Assets folder
        /// </summary>
        /// <param name="scriptName">Name of the JavaScript file (e.g., "process.js")</param>
        /// <param name="arguments">Arguments to pass to the JavaScript script</param>
        /// <param name="progress">Progress reporter for status updates</param>
        /// <param name="cancellationToken">Cancellation token to stop the process</param>
        /// <returns>The output from the JavaScript script execution</returns>
        Task<string> ExecuteJavaScriptScriptAsync(string scriptName, string arguments, IProgress<string> progress, CancellationToken cancellationToken = default);
    }
}