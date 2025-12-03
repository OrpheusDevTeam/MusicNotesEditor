using System;
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
        /// <returns>The output from the Python script execution</returns>
        /// <exception cref="TimeoutException">When the Python script execution times out</exception>
        /// <exception cref="Exception">When the Python script fails or encounters an error</exception>
        Task<string> ProcessFilesWithPythonAsync(string[] orderedFiles, IProgress<string> progress);
    }
}