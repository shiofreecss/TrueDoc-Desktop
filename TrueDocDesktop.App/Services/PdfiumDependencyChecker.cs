using System;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using System.Reflection;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;
using System.IO.Compression;
using System.Windows.Controls;

namespace TrueDocDesktop.App.Services
{
    /// <summary>
    /// Utility class to check if PdfiumViewer native dependencies are installed
    /// </summary>
    public static class PdfiumDependencyChecker
    {
        private const string PdfiumDllName = "pdfium.dll";
        private const string PdfiumWin64Url = "https://github.com/bblanchon/pdfium-binaries/releases/download/chromium/5845/pdfium-windows-x64.zip";
        private const string PdfiumWin32Url = "https://github.com/bblanchon/pdfium-binaries/releases/download/chromium/5845/pdfium-windows-x86.zip";
        
        /// <summary>
        /// Checks if PdfiumViewer native dependencies are installed
        /// </summary>
        /// <returns>True if dependencies are available, otherwise false</returns>
        public static bool AreDependenciesAvailable()
        {
            try
            {
                // Check if pdfium.dll exists in the application directory
                string appPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                if (File.Exists(Path.Combine(appPath, PdfiumDllName)))
                {
                    return true;
                }
                
                // Check if pdfium.dll exists in the Libraries subdirectory
                string libPath = Path.Combine(appPath, "Libraries");
                if (Directory.Exists(libPath) && File.Exists(Path.Combine(libPath, PdfiumDllName)))
                {
                    return true;
                }
                
                // Check if pdfium.dll exists in any of the PATH directories
                string pathEnv = Environment.GetEnvironmentVariable("PATH");
                if (!string.IsNullOrEmpty(pathEnv))
                {
                    foreach (string path in pathEnv.Split(Path.PathSeparator))
                    {
                        try
                        {
                            if (File.Exists(Path.Combine(path, PdfiumDllName)))
                            {
                                return true;
                            }
                        }
                        catch
                        {
                            // Ignore any errors checking path directories
                        }
                    }
                }
                
                // Try loading PdfiumViewer to see if it can find pdfium.dll
                try
                {
                    // Create a very small PDF in memory
                    byte[] miniPdf = Convert.FromBase64String(
                        "JVBERi0xLjANCjEgMCBvYmo8PC9QYWdlcyAyIDAgUj4+ZW5kb2JqDQoyIDAgb2JqPDwvS2lkcyBbMyAwIFJdDQovQ291bnQgMT4+ZW5kb2JqDQozIDAgb2JqPDwvTWVkaWFCb3ggWzAgMCAzIDNdPj5lbmRvYmoNCnRyYWlsZXI8PC9Sb290IDEgMCBSPj4=");
                    
                    // Try to load the dummy PDF using PdfiumViewer
                    // This will throw if the native dependencies are missing
                    using (var stream = new MemoryStream(miniPdf))
                    {
                        using (var doc = PdfiumViewer.PdfDocument.Load(stream))
                        {
                            // If we get here, PdfiumViewer is working
                            return true;
                        }
                    }
                }
                catch
                {
                    return false;
                }
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Shows a dialog explaining the missing dependencies and provides instructions
        /// </summary>
        public static void ShowDependencyInstructions()
        {
            string appPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string message = "PdfiumViewer native dependencies are missing. PDF to image conversion requires these dependencies.\n\n" +
                             "Choose one of the following options:\n\n" +
                             "1. Automatically download and install (recommended)\n" +
                             "   This will download and install pdfium.dll to your application directory.\n\n" +
                             "2. Manually download and install\n" +
                             "   Download the Pdfium binary distribution and copy the DLL files to:\n" +
                             "   " + appPath + "\n\n" +
                             "Would you like to automatically install the dependencies?";
            
            var result = MessageBox.Show(message, "Missing Dependencies", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            
            if (result == MessageBoxResult.Yes)
            {
                // Automatically download and install using the PowerShell script
                RunInstallationScript();
            }
            else if (result == MessageBoxResult.No)
            {
                // Open the download page for manual installation
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "https://github.com/bblanchon/pdfium-binaries/releases",
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Unable to open browser: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        
        /// <summary>
        /// Runs the PowerShell installation script
        /// </summary>
        private static void RunInstallationScript()
        {
            try
            {
                string appPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string scriptPath = Path.Combine(appPath, "InstallPdfiumDependencies.ps1");
                
                if (!File.Exists(scriptPath))
                {
                    MessageBox.Show(
                        "The installation script was not found. Please reinstall the application or manually download the dependencies.",
                        "Script Not Found",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }
                
                // Create a window to show installation progress
                var window = new Window
                {
                    Title = "Installing PDF Dependencies",
                    Width = 450,
                    Height = 200,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    ResizeMode = ResizeMode.NoResize
                };
                
                // Create UI elements
                var panel = new StackPanel { Margin = new Thickness(20) };
                var statusText = new TextBlock { Text = "Running installation script...", Margin = new Thickness(0, 0, 0, 10) };
                var progressBar = new ProgressBar { Height = 20, IsIndeterminate = true, Margin = new Thickness(0, 0, 0, 20) };
                var cancelButton = new Button { Content = "Cancel", Width = 80, HorizontalAlignment = HorizontalAlignment.Right };
                
                // Add elements to panel
                panel.Children.Add(statusText);
                panel.Children.Add(progressBar);
                panel.Children.Add(cancelButton);
                
                // Set content
                window.Content = panel;
                
                // Process to run PowerShell script
                Process process = null;
                
                // Setup cancellation
                cancelButton.Click += (s, e) => 
                { 
                    try
                    {
                        if (process != null && !process.HasExited)
                        {
                            process.Kill();
                        }
                    }
                    catch { }
                    window.Close(); 
                };
                
                // Run the script when the window is loaded
                window.Loaded += (s, e) =>
                {
                    Task.Run(() =>
                    {
                        try
                        {
                            // Create process to run PowerShell with the installation script
                            process = new Process
                            {
                                StartInfo = new ProcessStartInfo
                                {
                                    FileName = "powershell.exe",
                                    Arguments = $"-ExecutionPolicy Bypass -File \"{scriptPath}\"",
                                    UseShellExecute = false,
                                    RedirectStandardOutput = true,
                                    RedirectStandardError = true,
                                    CreateNoWindow = true
                                },
                                EnableRaisingEvents = true
                            };
                            
                            // Handle output data
                            process.OutputDataReceived += (sender, args) =>
                            {
                                if (!string.IsNullOrEmpty(args.Data))
                                {
                                    window.Dispatcher.Invoke(() =>
                                    {
                                        statusText.Text = args.Data;
                                    });
                                }
                            };
                            
                            // Handle error data
                            process.ErrorDataReceived += (sender, args) =>
                            {
                                if (!string.IsNullOrEmpty(args.Data))
                                {
                                    window.Dispatcher.Invoke(() =>
                                    {
                                        statusText.Text = "Error: " + args.Data;
                                    });
                                }
                            };
                            
                            // Handle process exit
                            process.Exited += (sender, args) =>
                            {
                                window.Dispatcher.Invoke(() =>
                                {
                                    progressBar.IsIndeterminate = false;
                                    
                                    if (process.ExitCode == 0)
                                    {
                                        progressBar.Value = 100;
                                        statusText.Text = "Installation completed successfully!";
                                        
                                        // Change cancel button to OK
                                        cancelButton.Content = "OK";
                                        
                                        // Show success message
                                        MessageBox.Show(
                                            "PDF dependencies have been installed successfully!\n\nPlease restart the application for the changes to take effect.",
                                            "Installation Complete",
                                            MessageBoxButton.OK,
                                            MessageBoxImage.Information);
                                    }
                                    else
                                    {
                                        statusText.Text = "Installation failed.";
                                        
                                        // Show error message
                                        MessageBox.Show(
                                            "Failed to install PDF dependencies. Please try manual installation.",
                                            "Installation Failed",
                                            MessageBoxButton.OK,
                                            MessageBoxImage.Error);
                                    }
                                });
                            };
                            
                            // Start the process
                            process.Start();
                            process.BeginOutputReadLine();
                            process.BeginErrorReadLine();
                            
                            // Wait for the process to exit
                            process.WaitForExit();
                        }
                        catch (Exception ex)
                        {
                            window.Dispatcher.Invoke(() =>
                            {
                                progressBar.IsIndeterminate = false;
                                statusText.Text = "Installation failed: " + ex.Message;
                                
                                MessageBox.Show(
                                    $"Error running installation script: {ex.Message}",
                                    "Installation Error",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Error);
                            });
                        }
                    });
                };
                
                // Show the window
                window.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error running installation script: {ex.Message}", "Installation Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        /// <summary>
        /// Recursively finds a file in a directory and its subdirectories
        /// </summary>
        private static string FindFileInDirectory(string directory, string fileName)
        {
            // Check if the file exists in the current directory
            string filePath = Path.Combine(directory, fileName);
            if (File.Exists(filePath))
            {
                return filePath;
            }
            
            // Check subdirectories
            foreach (string subDir in Directory.GetDirectories(directory))
            {
                string result = FindFileInDirectory(subDir, fileName);
                if (!string.IsNullOrEmpty(result))
                {
                    return result;
                }
            }
            
            // File not found
            return null;
        }
    }
} 