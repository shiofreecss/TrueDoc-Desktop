using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Web.WebView2.Core;
using Microsoft.WindowsAPICodePack.Dialogs;
using System.Diagnostics;
using System.Drawing;
using System;
using TrueDocDesktop.App.Services;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Windows.Input;

namespace TrueDocDesktop.App;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private string? _currentFilePath;
    private bool _isPdf;
    private readonly AppSettings _settings;
    private DashScopeService? _dashScopeService;

    public MainWindow()
    {
        InitializeComponent();
        InitializeAsync();

        // Load settings
        _settings = AppSettings.Load();
        InitializeDashScopeService();

        // Add Settings option to window menu
        AddSettingsMenu();
    }

    private void AddSettingsMenu()
    {
        // Create menu
        var menu = new Menu
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 10, 10, 0)
        };

        var menuItem = new MenuItem
        {
            Header = "Settings"
        };
        menuItem.Click += SettingsMenuItem_Click;

        menu.Items.Add(menuItem);

        // Add to window
        Grid mainGrid = (Grid)Content;
        mainGrid.Children.Add(menu);
    }

    private async void InitializeAsync()
    {
        // Initialize WebView2 control for PDF viewing
        try
        {
            await PdfViewer.EnsureCoreWebView2Async(null);
            PdfViewer.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            PdfViewer.CoreWebView2.Settings.AreDevToolsEnabled = false;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to initialize WebView2: {ex.Message}\n\nPlease make sure WebView2 Runtime is installed.", 
                "Initialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SettingsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var settingsWindow = new SettingsWindow(_settings);
        settingsWindow.Owner = this;
        
        bool? result = settingsWindow.ShowDialog();
        
        if (result == true)
        {
            // Reinitialize DashScope service with the new API key
            InitializeDashScopeService();
        }
    }

    private void InitializeDashScopeService()
    {
        if (!string.IsNullOrEmpty(_settings.DashScopeApiKey))
        {
            _dashScopeService = new DashScopeService(_settings.DashScopeApiKey);
            
            // Set custom model and prompts from settings
            _dashScopeService.ModelName = _settings.ModelName;
            _dashScopeService.SystemContent = _settings.SystemPrompt;
            _dashScopeService.UserContent = _settings.UserPrompt;
            
            BtnOcr.IsEnabled = !_isPdf && _currentFilePath != null;
        }
        else
        {
            _dashScopeService = null;
            BtnOcr.IsEnabled = false;
        }
    }

    private void BtnUploadPdf_Click(object sender, RoutedEventArgs e)
    {
        OpenFileDialog openFileDialog = new OpenFileDialog
        {
            Filter = "PDF Files (*.pdf)|*.pdf",
            Title = "Select a PDF File"
        };

        if (openFileDialog.ShowDialog() == true)
        {
            LoadDocument(openFileDialog.FileName);
        }
    }

    private void BtnUploadImage_Click(object sender, RoutedEventArgs e)
    {
        OpenFileDialog openFileDialog = new OpenFileDialog
        {
            Filter = "Image Files (*.jpg; *.jpeg; *.png; *.bmp)|*.jpg;*.jpeg;*.png;*.bmp",
            Title = "Select an Image File"
        };

        if (openFileDialog.ShowDialog() == true)
        {
            LoadDocument(openFileDialog.FileName);
        }
    }

    private void LoadDocument(string filePath)
    {
        try
        {
            _currentFilePath = filePath;
            string extension = Path.GetExtension(filePath).ToLower();
            _isPdf = extension == ".pdf";

            // Hide all viewers first
            PdfViewer.Visibility = Visibility.Collapsed;
            ImageViewer.Visibility = Visibility.Collapsed;
            NoDocumentPanel.Visibility = Visibility.Collapsed;

            if (_isPdf)
            {
                // Load PDF file
                LoadPdf(filePath);
                PdfViewer.Visibility = Visibility.Visible;
            }
            else
            {
                // Load image file
                LoadImage(filePath);
                ImageViewer.Visibility = Visibility.Visible;
            }

            // Update file information
            FileInfo fileInfo = new FileInfo(filePath);
            TxtFilename.Text = Path.GetFileName(filePath);
            TxtFileType.Text = _isPdf ? "PDF Document" : "Image";
            TxtFileSize.Text = FormatFileSize(fileInfo.Length);

            // Enable action buttons
            BtnSaveAs.IsEnabled = true;
            BtnPrint.IsEnabled = true;
            
            // OCR button is only enabled for images
            BtnOcr.IsEnabled = !_isPdf && _dashScopeService != null;
            
            // Clear previous OCR results
            TxtOcrResults.Text = string.Empty;
            BtnCopyText.IsEnabled = false;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading document: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            
            // Show no document panel
            PdfViewer.Visibility = Visibility.Collapsed;
            ImageViewer.Visibility = Visibility.Collapsed;
            NoDocumentPanel.Visibility = Visibility.Visible;
        }
    }

    private void LoadPdf(string filePath)
    {
        // Use WebView2 to display PDF
        string tempHtml = Path.Combine(Path.GetTempPath(), "pdfviewer.html");
        string fileUrl = new Uri(filePath).AbsoluteUri;
        
        string html = $@"
            <!DOCTYPE html>
            <html>
            <head>
                <title>PDF Viewer</title>
                <style>
                    body, html {{ margin: 0; padding: 0; height: 100%; overflow: hidden; }}
                    #pdf-container {{ width: 100%; height: 100%; }}
                    embed {{ width: 100%; height: 100%; }}
                </style>
            </head>
            <body>
                <div id='pdf-container'>
                    <embed src='{fileUrl}' type='application/pdf' width='100%' height='100%' />
                </div>
            </body>
            </html>";

        File.WriteAllText(tempHtml, html);
        PdfViewer.Source = new Uri(tempHtml);
    }

    private void LoadImage(string filePath)
    {
        BitmapImage bitmapImage = new BitmapImage();
        bitmapImage.BeginInit();
        bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
        bitmapImage.UriSource = new Uri(filePath);
        bitmapImage.EndInit();

        ImageViewer.Source = bitmapImage;
    }

    private void BtnSaveAs_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentFilePath) || !File.Exists(_currentFilePath))
        {
            MessageBox.Show("No document is currently loaded.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        CommonSaveFileDialog saveFileDialog = new CommonSaveFileDialog
        {
            Title = "Save Document As",
            DefaultFileName = Path.GetFileName(_currentFilePath),
            AlwaysAppendDefaultExtension = true
        };

        if (_isPdf)
        {
            saveFileDialog.Filters.Add(new CommonFileDialogFilter("PDF Files", "*.pdf"));
        }
        else
        {
            string extension = Path.GetExtension(_currentFilePath).ToLower();
            saveFileDialog.Filters.Add(new CommonFileDialogFilter("Image Files", "*" + extension));
        }

        if (saveFileDialog.ShowDialog() == CommonFileDialogResult.Ok)
        {
            try
            {
                File.Copy(_currentFilePath, saveFileDialog.FileName, true);
                MessageBox.Show("File saved successfully!", "Save Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void BtnPrint_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentFilePath) || !File.Exists(_currentFilePath))
        {
            MessageBox.Show("No document is currently loaded.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        try
        {
            // Use the system's default application to print the file
            Process.Start(new ProcessStartInfo
            {
                FileName = _currentFilePath,
                Verb = "print",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error printing document: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void BtnOcr_Click(object sender, RoutedEventArgs e)
    {
        if (_dashScopeService == null)
        {
            MessageBox.Show("Please configure your DashScope API key in Settings first.", "API Key Required", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (string.IsNullOrEmpty(_currentFilePath) || !File.Exists(_currentFilePath) || _isPdf)
        {
            MessageBox.Show("Text extraction can only be performed on image files.", "Cannot Process", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            // Show progress cursor
            Mouse.OverrideCursor = Cursors.Wait;
            BtnOcr.IsEnabled = false;
            TxtOcrResults.Text = "Processing with Qwen-VL-Max...";

            // Perform OCR with Qwen-VL-Max
            string ocrResult = await _dashScopeService.PerformOcrAsync(_currentFilePath);
            
            // Display results
            TxtOcrResults.Text = ocrResult;
            BtnCopyText.IsEnabled = !string.IsNullOrWhiteSpace(ocrResult);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error extracting text: {ex.Message}", "Text Extraction Error", MessageBoxButton.OK, MessageBoxImage.Error);
            TxtOcrResults.Text = string.Empty;
        }
        finally
        {
            // Restore cursor
            Mouse.OverrideCursor = null;
            BtnOcr.IsEnabled = true;
        }
    }

    private void BtnCopyText_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(TxtOcrResults.Text))
        {
            Clipboard.SetText(TxtOcrResults.Text);
            MessageBox.Show("Text copied to clipboard!", "Copy Complete", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }

        return $"{len:0.##} {sizes[order]}";
    }
}