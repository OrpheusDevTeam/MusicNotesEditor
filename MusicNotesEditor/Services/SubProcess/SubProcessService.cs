using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MusicNotesEditor.Services.SubProcess
{
    public class SubProcessService : ISubProcessService
    {

        public async Task<string> ProcessFilesWithPythonAsync(string[] orderedFiles, IProgress<string> progress)
        {
            string pythonScriptPath = @"C:\Users\jmosz\Desktop\Studia\ZPI Team Project\OMR\main.py";
            string pythonExecutable = "python";

            return await Task.Run(() =>
            {
                try
                {
                    progress?.Report("Starting Python script...");
                    Console.WriteLine("Starting Python script...");

                    string arguments = $"\"{pythonScriptPath}\" {string.Join(" ", orderedFiles.Select(f => $"\"{f}\""))}";

                    var processStartInfo = new ProcessStartInfo
                    {
                        FileName = pythonExecutable,
                        Arguments = arguments,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        WorkingDirectory = Path.GetDirectoryName(pythonScriptPath)
                    };

                    using (var process = new Process())
                    {
                        process.StartInfo = processStartInfo;

                        var outputBuilder = new StringBuilder();
                        var errorBuilder = new StringBuilder();

                        process.OutputDataReceived += (sender, e) => {
                            if (!string.IsNullOrEmpty(e.Data))
                            {
                                outputBuilder.AppendLine(e.Data);
                                // Only show non-JSON messages in UI
                                if (!e.Data.Trim().StartsWith("[") && !e.Data.Trim().StartsWith("{"))
                                {
                                    progress?.Report($"Processing: {e.Data}");
                                }
                                Console.WriteLine($"Output: {e.Data}");
                            }
                        };

                        process.ErrorDataReceived += (sender, e) => {
                            if (!string.IsNullOrEmpty(e.Data))
                                errorBuilder.AppendLine(e.Data);
                        };

                        progress?.Report("Executing Python script...");
                        Console.WriteLine("Executing Python script...");
                        process.Start();
                        process.BeginOutputReadLine();
                        process.BeginErrorReadLine();

                        bool completed = process.WaitForExit(30000);

                        if (!completed)
                        {
                            process.Kill();
                            throw new TimeoutException("Python script execution timed out");
                        }

                        if (process.ExitCode != 0)
                        {
                            string errorMessage = errorBuilder.ToString();
                            throw new Exception($"Python script failed: {errorMessage}");
                        }

                        progress?.Report("Python script completed successfully");
                        Console.WriteLine("Python script completed successfully");
                        return outputBuilder.ToString();
                    }
                }
                catch (Exception ex)
                {
                    progress?.Report($"Error: {ex.Message}");
                    throw new Exception($"Failed to execute Python script: {ex.Message}", ex);
                }
            });
        }


    }
}
