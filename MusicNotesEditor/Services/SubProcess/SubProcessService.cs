using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MusicNotesEditor.Services.SubProcess
{
    public class SubProcessService : ISubProcessService
    {
        public async Task<string> ProcessFilesWithPythonAsync(string[] orderedFiles, IProgress<string> progress, CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                string omrExePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "omr", "omr.exe");

                if (!File.Exists(omrExePath))
                    throw new FileNotFoundException($"OMR executable not found at: {omrExePath}");

                try
                {
                    progress?.Report("Starting OMR executable...");
                    Console.WriteLine("Starting OMR executable...");

                    // Check cancellation before starting
                    cancellationToken.ThrowIfCancellationRequested();

                    string arguments = string.Join(" ", orderedFiles.Select(f => $"\"{f}\""));

                    var processStartInfo = new ProcessStartInfo
                    {
                        FileName = omrExePath,
                        Arguments = arguments,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        WorkingDirectory = Path.GetDirectoryName(omrExePath)
                    };

                    using (var process = new Process())
                    {
                        process.StartInfo = processStartInfo;

                        var outputBuilder = new StringBuilder();
                        var errorBuilder = new StringBuilder();

                        process.OutputDataReceived += (sender, e) =>
                        {
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

                        process.ErrorDataReceived += (sender, e) =>
                        {
                            if (!string.IsNullOrEmpty(e.Data))
                                errorBuilder.AppendLine(e.Data);
                        };

                        progress?.Report("Executing OMR executable...");
                        Console.WriteLine("Executing OMR executable...");

                        process.Start();
                        process.BeginOutputReadLine();
                        process.BeginErrorReadLine();

                        bool completed = WaitForExitWithCancellation(process, 30000, cancellationToken);

                        if (cancellationToken.IsCancellationRequested)
                        {
                            try { process.Kill(); } catch { }
                            throw new OperationCanceledException("OMR execution was cancelled");
                        }

                        if (!completed)
                        {
                            process.Kill();
                            throw new TimeoutException("OMR execution timed out");
                        }

                        if (process.ExitCode != 0)
                        {
                            string errorMessage = errorBuilder.ToString();
                            throw new Exception($"OMR execution failed: {errorMessage}");
                        }

                        progress?.Report("OMR executable completed successfully");
                        Console.WriteLine("OMR executable completed successfully");
                        return outputBuilder.ToString();
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    progress?.Report($"Error: {ex.Message}");
                    throw new Exception($"Failed to execute OMR executable: {ex.Message}", ex);
                }
            }, cancellationToken);
        }

        private bool WaitForExitWithCancellation(Process process, int timeoutMilliseconds, CancellationToken cancellationToken)
        {
            var task = Task.Run(() => process.WaitForExit(timeoutMilliseconds), cancellationToken);

            try
            {
                return task.GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        public async Task<string> ExecuteJavaScriptScriptAsync(string scriptName, string arguments, IProgress<string> progress, CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            return await Task.Run(() =>
            {
                try
                {
                    // Get the Assets folder path (assuming it's in the application directory)
                    string assetsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "script");
                    string nodePath = Path.Combine(assetsPath, "node.exe");
                    string scriptPath = Path.Combine(assetsPath, scriptName);

                    // Validate paths
                    if (!File.Exists(nodePath))
                        throw new FileNotFoundException($"Node.js not found at: {nodePath}");

                    if (!File.Exists(scriptPath))
                        throw new FileNotFoundException($"JavaScript script not found at: {scriptPath}");

                    progress?.Report($"Starting Node.js script: {scriptName}...");
                    Console.WriteLine($"Starting Node.js script: {scriptName}...");

                    // Check cancellation before starting
                    cancellationToken.ThrowIfCancellationRequested();

                    // Prepare the arguments for Node.js
                    string fullArguments = $"\"{scriptPath}\" {arguments}";

                    var processStartInfo = new ProcessStartInfo
                    {
                        FileName = nodePath,
                        Arguments = fullArguments,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        WorkingDirectory = assetsPath, // Set working directory to Assets to access node_modules
                        EnvironmentVariables =
                        {
                            ["NODE_PATH"] = Path.Combine(assetsPath, "node_modules")
                        }
                    };

                    using (var process = new Process())
                    {
                        process.StartInfo = processStartInfo;

                        var outputBuilder = new StringBuilder();
                        var errorBuilder = new StringBuilder();

                        process.OutputDataReceived += (sender, e) =>
                        {
                            if (!string.IsNullOrEmpty(e.Data))
                            {
                                outputBuilder.AppendLine(e.Data);
                                progress?.Report($"Node.js: {e.Data}");
                                Console.WriteLine($"Node.js Output: {e.Data}");
                            }
                        };

                        process.ErrorDataReceived += (sender, e) =>
                        {
                            if (!string.IsNullOrEmpty(e.Data))
                            {
                                errorBuilder.AppendLine(e.Data);
                                Console.WriteLine($"Node.js Error: {e.Data}");
                            }
                        };

                        progress?.Report("Executing Node.js script...");
                        Console.WriteLine("Executing Node.js script...");

                        process.Start();
                        process.BeginOutputReadLine();
                        process.BeginErrorReadLine();

                        // Wait for exit with cancellation support
                        bool completed = WaitForExitWithCancellation(process, 60000, cancellationToken);

                        if (cancellationToken.IsCancellationRequested)
                        {
                            try
                            {
                                process.Kill();
                            }
                            catch { }
                            throw new OperationCanceledException("Node.js script execution was cancelled");
                        }

                        if (!completed)
                        {
                            process.Kill();
                            throw new TimeoutException("Node.js script execution timed out (60 seconds)");
                        }

                        if (process.ExitCode != 0)
                        {
                            string errorMessage = errorBuilder.ToString();
                            throw new Exception($"Node.js script failed with exit code {process.ExitCode}: {errorMessage}");
                        }

                        progress?.Report("Node.js script completed successfully");
                        Console.WriteLine("Node.js script completed successfully");
                        return outputBuilder.ToString();
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    progress?.Report($"Error: {ex.Message}");
                    throw new Exception($"Failed to execute JavaScript script: {ex.Message}", ex);
                }
            }, cancellationToken);
        }
    }
}