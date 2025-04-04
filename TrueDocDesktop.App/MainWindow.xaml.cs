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
using TrueDocDesktop.App.Dialogs;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Windows.Input;
using iTextSharp.text.pdf;
using iTextSharp.text;
using System.Collections.Generic;
using System.Drawing.Imaging;
using PdfiumViewer;
using System.Windows.Threading;
using System.Collections.ObjectModel;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;

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
    private bool _isToolsPanelVisible = true; // Track Tools panel visibility
    private bool _isExtractPanelVisible = false; // Set to false by default to hide the extraction panel
    private bool _isDataValidationPanelVisible = false; // Set to false by default to hide the data validation panel
    
    // PDF page navigation variables
    private int _currentPdfPage = 1;
    private int _totalPdfPages = 1;
    private string _processingFolder = string.Empty;
    private string _pdfBaseName = string.Empty;
    private Dictionary<int, string> _pdfPageResults = new Dictionary<int, string>();
    
    // Dictionary of document type prompts
    private readonly Dictionary<string, string> PROMPTS = new Dictionary<string, string>
    {
        {"General", "Please extract all information as JSON"}
    };

    public MainWindow()
    {
        try
        {
            // Add global exception handler
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            Application.Current.DispatcherUnhandledException += Current_DispatcherUnhandledException;
            
            InitializeComponent();
            InitializeAsync();

            // Load settings
            _settings = AppSettings.Load();
            InitializeDashScopeService();

            // Add Settings option to window menu
            AddSettingsMenu();
            
            // Initialize drag and drop functionality
            InitializeDragAndDrop();
            
            // Initialize panel visibility
            _isToolsPanelVisible = true;
            _isExtractPanelVisible = false;
            
            // Apply initial panel visibility with a delay to ensure UI is loaded
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() =>
            {
                try
                {
                    // Initialize tools panel (should be visible)
                    var grid = (Grid)this.Content;
                    
                    // Ensure extraction panel is hidden initially
                    ExtractPanel.Visibility = Visibility.Collapsed;
                    
                    // Hide extraction column to start with
                    if (grid.ColumnDefinitions.Count > 4)
                    {
                        grid.ColumnDefinitions[4].Width = new GridLength(0);
                    }
                    
                    // Initialize floating buttons visibility
                    if (BtnShowTools != null)
                    {
                        BtnShowTools.Visibility = Visibility.Collapsed; // Hidden initially as tools panel is visible
                    }
                    
                    if (FindName("BtnShowExtract") is Button showExtractBtn)
                    {
                        showExtractBtn.Visibility = Visibility.Visible; // Visible initially as extract panel is hidden
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error initializing panel layout: {ex.Message}");
                }
            }));
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error initializing application: {ex.Message}\n\nStack Trace: {ex.StackTrace}", 
                "Critical Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Current_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        // Log the exception
        string errorMessage = $"Unhandled UI exception: {e.Exception.Message}\n\nStack Trace: {e.Exception.StackTrace}";
        
        // Show error message to user
        MessageBox.Show(errorMessage, "Application Error", MessageBoxButton.OK, MessageBoxImage.Error);
        
        // Mark as handled so application doesn't crash
        e.Handled = true;
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        // Log the exception
        Exception ex = e.ExceptionObject as Exception;
        string errorMessage = ex != null 
            ? $"Unhandled exception: {ex.Message}\n\nStack Trace: {ex.StackTrace}" 
            : "Unknown unhandled exception occurred";
        
        // Show error message to user
        MessageBox.Show(errorMessage, "Critical Error", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private void InitializeDragAndDrop()
    {
        // Enable drag and drop for the main window
        this.AllowDrop = true;
        this.DragEnter += MainWindow_DragEnter;
        this.DragOver += MainWindow_DragOver;
        this.Drop += MainWindow_Drop;
        this.DragLeave += MainWindow_DragLeave;
        
        // Enable drag and drop for the preview container
        PreviewContainer.AllowDrop = true;
        PreviewContainer.DragOver += PreviewContainer_DragOver;
        PreviewContainer.Drop += PreviewContainer_Drop;
    }

    private void MainWindow_DragEnter(object sender, DragEventArgs e)
    {
        Console.WriteLine("MainWindow DragEnter triggered");
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files.Length > 0 && IsValidFileType(files[0]))
            {
                e.Effects = DragDropEffects.Copy;
                // Highlight drop area
                PreviewBorder.BorderBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#4285F4"));
                PreviewBorder.BorderThickness = new Thickness(2);
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void MainWindow_DragOver(object sender, DragEventArgs e)
    {
        Console.WriteLine("MainWindow DragOver triggered");
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files.Length > 0 && IsValidFileType(files[0]))
            {
                e.Effects = DragDropEffects.Copy;
                // Keep highlight
                PreviewBorder.BorderBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#4285F4"));
                PreviewBorder.BorderThickness = new Thickness(2);
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void MainWindow_Drop(object sender, DragEventArgs e)
    {
        Console.WriteLine("MainWindow Drop triggered");
        
        // Reset border
        PreviewBorder.BorderBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#DDDDDD"));
        PreviewBorder.BorderThickness = new Thickness(1);
        
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files.Length > 0 && IsValidFileType(files[0]))
            {
                LoadDocument(files[0]);
                e.Handled = true;
            }
        }
    }

    private void MainWindow_DragLeave(object sender, DragEventArgs e)
    {
        Console.WriteLine("MainWindow DragLeave triggered");
        
        // Reset border
        PreviewBorder.BorderBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#DDDDDD"));
        PreviewBorder.BorderThickness = new Thickness(1);
        
        e.Handled = true;
    }

    private void PreviewContainer_DragOver(object sender, DragEventArgs e)
    {
        // Check if the dragged data contains file(s)
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            // Get the files from the dragged data
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            
            // Check if there's at least one file and if it's a PDF or image
            if (files.Length > 0 && IsValidFileType(files[0]))
            {
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
                // Debug info
                Console.WriteLine($"Valid file being dragged: {files[0]}");
                return;
            }
            else
            {
                // Debug info about invalid files
                if (files.Length > 0)
                {
                    Console.WriteLine($"Invalid file type: {files[0]}, extension: {Path.GetExtension(files[0])}");
                }
            }
        }
        else
        {
            // Debug what formats are available
            string[] formats = e.Data.GetFormats();
            Console.WriteLine($"Available formats: {string.Join(", ", formats)}");
        }
        
        // If we get here, the data is not acceptable
        e.Effects = DragDropEffects.None;
        e.Handled = true;
    }

    private void PreviewContainer_Drop(object sender, DragEventArgs e)
    {
        Console.WriteLine("Drop event triggered");
        
        // Check if the dropped data contains file(s)
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            // Get the files from the dropped data
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            
            Console.WriteLine($"Files dropped: {files.Length}");
            
            // If there's at least one file and it's a valid type, load it
            if (files.Length > 0 && IsValidFileType(files[0]))
            {
                Console.WriteLine($"Loading document: {files[0]}");
                LoadDocument(files[0]);
                e.Handled = true;
            }
            else if (files.Length > 0)
            {
                Console.WriteLine($"Invalid file dropped: {files[0]}");
            }
        }
        else
        {
            Console.WriteLine("Drop data doesn't contain file drop format");
        }
    }

    private bool IsValidFileType(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return false;

        string extension = Path.GetExtension(filePath).ToLower();
        
        // Check if file is PDF or a valid image type
        return extension == ".pdf" || 
               extension == ".jpg" || 
               extension == ".jpeg" || 
               extension == ".png" || 
               extension == ".bmp";
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
        try
        {
            // Initialize WebView2 environment if needed
            if (PdfViewer.CoreWebView2 == null)
            {
                var webView2Environment = await CoreWebView2Environment.CreateAsync(null, Path.GetTempPath(), null);
                await PdfViewer.EnsureCoreWebView2Async(webView2Environment);
            }
            
            // Set default visibility
            PdfViewer.Visibility = Visibility.Collapsed;
            ImageViewer.Visibility = Visibility.Collapsed;
            NoDocumentPanel.Visibility = Visibility.Visible;
            
            // Collapse tool-specific panels
            if (PdfOperationsGroup != null) PdfOperationsGroup.Visibility = Visibility.Collapsed;
            if (ImageOperationsGroup != null) ImageOperationsGroup.Visibility = Visibility.Collapsed;
            if (ActionsGroup != null) ActionsGroup.Visibility = Visibility.Collapsed;
            if (DocumentInfoGroup != null) DocumentInfoGroup.Visibility = Visibility.Collapsed;
            
            // Set WebView2 settings
            if (PdfViewer.CoreWebView2 != null)
            {
                // Disable default context menu
                PdfViewer.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                
                // Handle new window requests to open them in the default browser
                PdfViewer.CoreWebView2.NewWindowRequested += (s, e) =>
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = e.Uri,
                        UseShellExecute = true
                    });
                    e.Handled = true;
                };
            }
            
            // Check for PDF dependencies
            CheckPdfiumDependencies();
            
            // Make sure Data Validation panel is hidden by default
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() =>
            {
                _isDataValidationPanelVisible = false;
                var grid = (Grid)this.Content;
                
                if (grid.ColumnDefinitions.Count > 6)
                {
                    grid.ColumnDefinitions[6].Width = new GridLength(0);
                }
                
                if (DataValidationPanel != null)
                {
                    DataValidationPanel.Visibility = Visibility.Collapsed;
                }
                
                // Make sure BtnShowValidation is visible
                if (BtnShowValidation != null)
                {
                    BtnShowValidation.Visibility = Visibility.Visible;
                }
            }));
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error initializing web view: {ex.Message}", "Initialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
            
            // Enable buttons based on document type
            if (_currentFilePath != null)
            {
                if (_isPdf)
                {
                    BtnPdfExtract.IsEnabled = true;
                }
                else
                {
                    BtnImageExtract.IsEnabled = true;
                }
            }
        }
        else
        {
            _dashScopeService = null;
            if (BtnPdfExtract != null) BtnPdfExtract.IsEnabled = false;
            if (BtnImageExtract != null) BtnImageExtract.IsEnabled = false;
        }
    }

    private void DependenciesMenuItem_Click(object sender, RoutedEventArgs e)
    {
        PdfiumDependencyChecker.ShowDependencyInstructions();
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

    private async Task<bool> EnsureWebView2Initialized()
    {
        try
        {
            if (PdfViewer.CoreWebView2 == null)
            {
                // Create and initialize the WebView2 environment
                var webView2Environment = await CoreWebView2Environment.CreateAsync(null, Path.GetTempPath(), null);
                await PdfViewer.EnsureCoreWebView2Async(webView2Environment);
                
                // Configure WebView2 settings
                if (PdfViewer.CoreWebView2 != null)
                {
                    // Disable context menu
                    PdfViewer.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                    
                    // Handle new window requests
                    PdfViewer.CoreWebView2.NewWindowRequested += (s, e) =>
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = e.Uri,
                            UseShellExecute = true
                        });
                        e.Handled = true;
                    };
                    
                    return true;
                }
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error initializing WebView2: {ex.Message}", "WebView2 Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    private async void LoadDocument(string filePath)
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
                // Ensure WebView2 is initialized before loading a PDF
                bool webViewInitialized = await EnsureWebView2Initialized();
                if (!webViewInitialized)
                {
                    MessageBox.Show("Failed to initialize PDF viewer. Please try again or restart the application.", 
                                   "PDF Viewer Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    NoDocumentPanel.Visibility = Visibility.Visible;
                    return;
                }
                
                // Load PDF file
                LoadPdf(filePath);
                PdfViewer.Visibility = Visibility.Visible;
                
                // Show PDF operations and hide Image operations
                if (PdfOperationsGroup != null) PdfOperationsGroup.Visibility = Visibility.Visible;
                if (ImageOperationsGroup != null) ImageOperationsGroup.Visibility = Visibility.Collapsed;
                
                // Show Actions group for PDFs
                if (ActionsGroup != null) ActionsGroup.Visibility = Visibility.Visible;
                
                // Enable PDF operation buttons
                BtnPdfExtract.IsEnabled = _dashScopeService != null;
                BtnConvertToImage.IsEnabled = true;
                BtnSetPassword.IsEnabled = true;
                BtnRemovePassword.IsEnabled = true;
                BtnSignPdf.IsEnabled = true;
                
                // Enable action buttons
                BtnSaveAs.IsEnabled = true;
                BtnPrint.IsEnabled = true;
            }
            else
            {
                // Load image file
                LoadImage(filePath);
                ImageViewer.Visibility = Visibility.Visible;
                
                // Show Image operations and hide PDF operations
                if (PdfOperationsGroup != null) PdfOperationsGroup.Visibility = Visibility.Collapsed;
                if (ImageOperationsGroup != null) ImageOperationsGroup.Visibility = Visibility.Visible;
                
                // Hide Actions group for images
                if (ActionsGroup != null) ActionsGroup.Visibility = Visibility.Collapsed;
                
                // Enable image operation buttons
                BtnImageExtract.IsEnabled = _dashScopeService != null;
                BtnConvertToPdf.IsEnabled = true;
                BtnIncreaseDpi.IsEnabled = true;
                BtnCropImage.IsEnabled = true;
            }

            // Show and update file information
            if (DocumentInfoGroup != null) DocumentInfoGroup.Visibility = Visibility.Visible;
            
            // Update basic file information
            FileInfo fileInfo = new FileInfo(filePath);
            if (TxtFilename != null) TxtFilename.Text = Path.GetFileName(filePath);
            if (TxtFileType != null) TxtFileType.Text = _isPdf ? "PDF Document" : "Image";
            if (TxtFileSize != null) TxtFileSize.Text = FormatFileSize(fileInfo.Length);
            
            // Update additional metadata
            if (TxtCreatedDate != null) TxtCreatedDate.Text = fileInfo.CreationTime.ToString("yyyy-MM-dd HH:mm:ss");
            if (TxtModifiedDate != null) TxtModifiedDate.Text = fileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss");
            
            // Get format-specific metadata
            if (_isPdf)
            {
                // Get PDF-specific metadata
                try
                {
                    using (PdfReader reader = new PdfReader(filePath))
                    {
                        // Get PDF info
                        if (TxtDimensions != null) TxtDimensions.Text = "N/A";
                        if (TxtPageCount != null) TxtPageCount.Text = reader.NumberOfPages.ToString();
                        
                        // Check if PDF has a title
                        var info = reader.Info;
                        if (TxtTitle != null) 
                            TxtTitle.Text = info.ContainsKey("Title") && !string.IsNullOrEmpty(info["Title"]) 
                                ? info["Title"] 
                                : "N/A";
                        
                        // Check if PDF is encrypted
                        if (TxtEncryption != null) TxtEncryption.Text = reader.IsEncrypted() ? "Yes" : "No";

                        // Hide image-specific fields
                        if (LabelResolution != null) LabelResolution.Visibility = Visibility.Collapsed;
                        if (TxtResolution != null) TxtResolution.Visibility = Visibility.Collapsed;
                        if (LabelColorDepth != null) LabelColorDepth.Visibility = Visibility.Collapsed;
                        if (TxtColorDepth != null) TxtColorDepth.Visibility = Visibility.Collapsed;
                    }
                }
                catch
                {
                    // If reading PDF metadata fails, set default values
                    if (TxtDimensions != null) TxtDimensions.Text = "N/A";
                    if (TxtPageCount != null) TxtPageCount.Text = "N/A";
                    if (TxtTitle != null) TxtTitle.Text = "N/A";
                    if (TxtEncryption != null) TxtEncryption.Text = "N/A";
                }
            }
            else
            {
                // Get image-specific metadata
                try
                {
                    using (var image = System.Drawing.Image.FromFile(filePath))
                    {
                        if (TxtDimensions != null) TxtDimensions.Text = $"{image.Width} x {image.Height} px";
                        if (TxtPageCount != null) TxtPageCount.Text = "1";
                        if (TxtTitle != null) TxtTitle.Text = "N/A";
                        if (TxtEncryption != null) TxtEncryption.Text = "No";
                        
                        // Get resolution
                        if (LabelResolution != null) LabelResolution.Visibility = Visibility.Visible;
                        if (TxtResolution != null)
                        {
                            TxtResolution.Visibility = Visibility.Visible;
                            TxtResolution.Text = $"{image.HorizontalResolution:0.##} x {image.VerticalResolution:0.##} DPI";
                        }
                        
                        // Get color depth
                        if (LabelColorDepth != null) LabelColorDepth.Visibility = Visibility.Visible;
                        if (TxtColorDepth != null)
                        {
                            TxtColorDepth.Visibility = Visibility.Visible;
                            TxtColorDepth.Text = $"{GetPixelFormatDepth(image.PixelFormat)} bit";
                        }
                    }
                }
                catch
                {
                    // If reading image metadata fails, set default values
                    if (TxtDimensions != null) TxtDimensions.Text = "N/A";
                    if (TxtPageCount != null) TxtPageCount.Text = "N/A";
                    if (TxtTitle != null) TxtTitle.Text = "N/A";
                    if (TxtEncryption != null) TxtEncryption.Text = "N/A";
                    if (TxtResolution != null) TxtResolution.Text = "N/A";
                    if (TxtColorDepth != null) TxtColorDepth.Text = "N/A";
                }
            }
            
            // OCR button is enabled for both images and PDFs if the API is configured
            // BtnOcr.IsEnabled = _dashScopeService != null;
            
            // Remove references to OcrResultsGroup which has been deleted
            // Show OCR results section for images if the API is configured
            // if (OcrResultsGroup != null) 
            //     OcrResultsGroup.Visibility = !_isPdf && _dashScopeService != null ? Visibility.Visible : Visibility.Collapsed;
            
            // Clear previous OCR results
            // TxtOcrResults.Text = string.Empty;
            // BtnCopyText.IsEnabled = false;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading document: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            
            // Show no document panel
            PdfViewer.Visibility = Visibility.Collapsed;
            ImageViewer.Visibility = Visibility.Collapsed;
            NoDocumentPanel.Visibility = Visibility.Visible;
            
            // Hide operation groups
            if (PdfOperationsGroup != null) PdfOperationsGroup.Visibility = Visibility.Collapsed;
            if (ImageOperationsGroup != null) ImageOperationsGroup.Visibility = Visibility.Collapsed;
            if (ActionsGroup != null) ActionsGroup.Visibility = Visibility.Collapsed;
            // Remove reference to OcrResultsGroup which has been deleted
            // if (OcrResultsGroup != null) OcrResultsGroup.Visibility = Visibility.Collapsed;
            if (DocumentInfoGroup != null) DocumentInfoGroup.Visibility = Visibility.Collapsed;
        }
    }

    private void LoadPdf(string filePath)
    {
        try
        {
            // Use WebView2 to display PDF
            string tempHtml = Path.Combine(Path.GetTempPath(), $"pdfviewer_{Guid.NewGuid()}.html");
            string absolutePath = Path.GetFullPath(filePath);
            string fileUrl = new Uri(absolutePath).AbsoluteUri;
            
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
            
            // Handle WebView2 loading
            if (PdfViewer.CoreWebView2 != null)
            {
                PdfViewer.CoreWebView2.Navigate(new Uri(tempHtml).AbsoluteUri);
            }
            else
            {
                PdfViewer.Source = new Uri(tempHtml);
            }
            
            // Ensure WebView2 is visible
            PdfViewer.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading PDF: {ex.Message}", "PDF Error", MessageBoxButton.OK, MessageBoxImage.Error);
            NoDocumentPanel.Visibility = Visibility.Visible;
            PdfViewer.Visibility = Visibility.Collapsed;
        }
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

        // Get the processing folder
        string processingFolder = GetProcessingFolder(_currentFilePath);
        if (processingFolder == null)
        {
            MessageBox.Show("Could not create processing folder. Operation cancelled.", "Folder Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        
        string fileName = "copy_" + Path.GetFileName(_currentFilePath);
        string outputPath = Path.Combine(processingFolder, fileName);
        
        try
        {
            // Show wait cursor
            Mouse.OverrideCursor = Cursors.Wait;
            
            File.Copy(_currentFilePath, outputPath, true);
            
            MessageBox.Show($"File saved successfully!\n\nFile saved to: {outputPath}", "Save Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            
            // Ask if user wants to open the file
            var result = MessageBox.Show("Do you want to open the saved file?", "Open File", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                LoadDocument(outputPath);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error saving file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            // Reset cursor
            Mouse.OverrideCursor = null;
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

    private void BtnToggleTools_Click(object sender, RoutedEventArgs e)
    {
        // Hide tools panel
        _isToolsPanelVisible = false;
        
        // In the new layout, we're using column definitions instead
        var grid = (Grid)this.Content;
        var columnDefinitions = grid.ColumnDefinitions;
        
        // Hide the tools column
        if (columnDefinitions.Count > 2)
        {
            columnDefinitions[2].Width = new GridLength(0);
        }
        
        // Show the floating button
        if (BtnShowTools != null)
        {
            BtnShowTools.Visibility = Visibility.Visible;
        }
    }
    
    private void BtnShowTools_Click(object sender, RoutedEventArgs e)
    {
        // Show tools panel
        _isToolsPanelVisible = true;
        
        // In the new layout, we're using column definitions
        var grid = (Grid)this.Content;
        var columnDefinitions = grid.ColumnDefinitions;
        
        // Show the tools column
        if (columnDefinitions.Count > 2)
        {
            columnDefinitions[2].Width = new GridLength(1, GridUnitType.Star);
        }
        
        // Hide the floating button
        if (BtnShowTools != null)
        {
            BtnShowTools.Visibility = Visibility.Collapsed;
        }
    }
    
    private void BtnOcr_Click(object sender, RoutedEventArgs e)
    {
        if (_dashScopeService == null)
        {
            MessageBox.Show("Please configure your DashScope API key in Settings first.", "API Key Required", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (string.IsNullOrEmpty(_currentFilePath) || !File.Exists(_currentFilePath))
        {
            MessageBox.Show("Please load a document first.", "No Document", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Toggle extraction panel visibility
        _isExtractPanelVisible = !_isExtractPanelVisible;
        
        try
        {
            // Get the main grid
            var grid = (Grid)this.Content;
            
            // Update the layout based on the extraction panel visibility state
            if (_isExtractPanelVisible)
            {
                // Show extraction panel
                if (grid.ColumnDefinitions.Count > 4)
                {
                    grid.ColumnDefinitions[4].Width = new GridLength(1, GridUnitType.Star);
                }
                ExtractPanel.Visibility = Visibility.Visible;
                
                // Hide the BtnShowExtract button
                var showExtractBtn = FindName("BtnShowExtract") as Button;
                if (showExtractBtn != null)
                {
                    showExtractBtn.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                // Hide extraction panel
                if (grid.ColumnDefinitions.Count > 4)
                {
                    grid.ColumnDefinitions[4].Width = new GridLength(0);
                }
                ExtractPanel.Visibility = Visibility.Collapsed;
                
                // Show the BtnShowExtract button
                var showExtractBtn = FindName("BtnShowExtract") as Button;
                if (showExtractBtn != null)
                {
                    showExtractBtn.Visibility = Visibility.Visible;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error toggling extraction panel: {ex.Message}");
        }
    }

    // Update this method to match our new approach
    private void UpdateExtractPanelVisibility()
    {
        try
        {
            var grid = (Grid)this.Content;
            
            // Show/hide the extraction panel column
            if (grid.ColumnDefinitions.Count > 4)
            {
                if (_isExtractPanelVisible)
                {
                    grid.ColumnDefinitions[4].Width = new GridLength(1, GridUnitType.Star);
                    ExtractPanel.Visibility = Visibility.Visible;
                    
                    // Update button visibility
                    var showExtractBtn = FindName("BtnShowExtract") as Button;
                    if (showExtractBtn != null)
                    {
                        showExtractBtn.Visibility = Visibility.Collapsed;
                    }
                    
                    // When showing extract panel, hide tools panel
                    _isToolsPanelVisible = false;
                    if (grid.ColumnDefinitions.Count > 2)
                    {
                        grid.ColumnDefinitions[2].Width = new GridLength(0);
                    }
                    
                    // Show the floating button for tools
                    if (BtnShowTools != null)
                    {
                        BtnShowTools.Visibility = Visibility.Visible;
                    }
                }
                else
                {
                    grid.ColumnDefinitions[4].Width = new GridLength(0);
                    ExtractPanel.Visibility = Visibility.Collapsed;
                    
                    // Update button visibility
                    var showExtractBtn = FindName("BtnShowExtract") as Button;
                    if (showExtractBtn != null)
                    {
                        showExtractBtn.Visibility = Visibility.Visible;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating extraction panel visibility: {ex.Message}");
        }
    }

    // Extract Data button in the new panel
    private async void BtnExtractData_Click(object sender, RoutedEventArgs e)
    {
        if (_dashScopeService == null)
        {
            MessageBox.Show("Please configure your DashScope API key in Settings first.", "API Key Required", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (string.IsNullOrEmpty(_currentFilePath) || !File.Exists(_currentFilePath))
        {
            MessageBox.Show("No document is currently loaded.", "No Document", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            // Check if JSON file already exists in the processing folder
            string processingFolder = GetProcessingFolder(_currentFilePath);
            string originalFileName = Path.GetFileNameWithoutExtension(_currentFilePath);
            
            // Different handling for PDF vs single image
            if (_isPdf)
            {
                // For PDFs, check for page-specific JSON files
                bool hasExistingData = false;
                
                // Get PDF page count
                int pageCount = 1;
                try 
                {
                    using (PdfReader reader = new PdfReader(_currentFilePath))
                    {
                        pageCount = reader.NumberOfPages;
                        }
                    }
                    catch
                    {
                    // If we can't read the PDF, assume single page
                    pageCount = 1;
                }
                
                // Check if page 1 data exists
                string page1JsonPath = Path.Combine(processingFolder, $"{originalFileName}_page1_data.json");
                hasExistingData = File.Exists(page1JsonPath);
                
                if (hasExistingData)
                {
                    // Ask user if they want to load existing JSON
                    var result = MessageBox.Show(
                        $"Found existing PDF extraction data. Would you like to load it?\n\nPath: {page1JsonPath}\n\nClick 'Yes' to load existing data or 'No' to perform a new extraction.",
                        "Existing PDF Data Found",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);
                    
                    if (result == MessageBoxResult.Yes)
                    {
                        // Initialize navigation variables
                        _processingFolder = processingFolder;
                        _pdfBaseName = originalFileName;
                        _totalPdfPages = pageCount;
                        _currentPdfPage = 1;
                        _pdfPageResults.Clear();
                        
                        // Load the first page data
                        string jsonContent = File.ReadAllText(page1JsonPath);
                        _pdfPageResults[1] = jsonContent;
                        
                        // Show extract panel and update UI
                        _isExtractPanelVisible = true;
                        UpdateExtractPanelVisibility();
                        
                        // Hide data validation panel
                        _isDataValidationPanelVisible = false;
                        UpdateDataValidationPanelVisibility();
                        
                        // Show PDF navigation for multi-page PDFs
                        if (pageCount > 1)
                        {
                            PdfNavigationPanel.Visibility = Visibility.Visible;
                        }
                        else
                        {
                            PdfNavigationPanel.Visibility = Visibility.Collapsed;
                        }
                        
                        // Display the loaded JSON
                        TxtExtractionResults.Text = jsonContent;
                        UpdatePageNavigationInfo();
                        
                        MessageBox.Show("Existing PDF extraction data has been loaded.", "Load Successful", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }
                    // If No, proceed with new extraction
                }
            }
            else
            {
                // Regular single file check for non-PDF files
                string jsonFilePath = Path.Combine(processingFolder, $"{originalFileName}_data.json");
                
                if (File.Exists(jsonFilePath))
                {
                    // Ask user if they want to load existing JSON instead of re-extracting
                    var result = MessageBox.Show(
                        $"Found existing extraction data for this document. Would you like to load it?\n\nPath: {jsonFilePath}\n\nClick 'Yes' to load existing data or 'No' to perform a new extraction.",
                        "Existing Data Found",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);
                    
                    if (result == MessageBoxResult.Yes)
                    {
                        // Load the existing JSON file
                        string jsonContent = File.ReadAllText(jsonFilePath);
                        
                        // Show extract panel and update UI
                        _isExtractPanelVisible = true;
                        UpdateExtractPanelVisibility();
                        
                        // Hide data validation panel
                        _isDataValidationPanelVisible = false;
                        UpdateDataValidationPanelVisibility();
                        
                        // Make sure PDF navigation is hidden for single images
                        PdfNavigationPanel.Visibility = Visibility.Collapsed;
                        
                        // Display the loaded JSON
                        TxtExtractionResults.Text = jsonContent;
                        
                        MessageBox.Show("Existing extraction data has been loaded.", "Load Successful", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }
                    // If No, proceed with new extraction
                }
            }

            // Show extract panel and hide tools panel
            _isExtractPanelVisible = true;
            UpdateExtractPanelVisibility();
            
            // Make sure data validation panel is hidden
            _isDataValidationPanelVisible = false;
            UpdateDataValidationPanelVisibility();
            
            // Show progress cursor
            Mouse.OverrideCursor = Cursors.Wait;
            BtnExtractData.IsEnabled = false;
            TxtExtractionResults.Text = "Processing...";
            
            // Get the selected document type
            ComboBoxItem selectedItem = (ComboBoxItem)CmbDocumentType.SelectedItem;
            string documentType = selectedItem.Content.ToString();
            
            // Get the appropriate prompt for the document type
            string prompt = PROMPTS[documentType];
            
            if (_isPdf)
            {
                // For PDF files, extract each page as image and process
                await ProcessPdfForAIExtraction(_currentFilePath, prompt);
                
                // Show a message about where files have been saved
                if (_totalPdfPages > 1)
                {
                    MessageBox.Show($"PDF extraction complete! Individual page data has been saved to:\n{processingFolder}", 
                        "PDF Extraction Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            else
            {
                // Perform extraction with Qwen-VL-Max using the specific prompt for a single image
                string extractionResult = await _dashScopeService.PerformOcrAsync(_currentFilePath, prompt);
                
                // Pre-process the result to extract only the JSON if in markdown
                extractionResult = PreProcessExtractionResult(extractionResult);
                
                // Display the results
                TxtExtractionResults.Text = extractionResult;
                
                // Hide PDF navigation for single images
                PdfNavigationPanel.Visibility = Visibility.Collapsed;
                
                // Automatically fix and save the JSON
                try
                {
                    // Clean the JSON content
                    string jsonContent = CleanJsonContent(TxtExtractionResults.Text);
                    
                    // Try to format and fix the JSON
                    try
                    {
                        var jsonObject = JsonConvert.DeserializeObject(jsonContent);
                        if (jsonObject != null)
                        {
                            // Reformat with proper indentation
                            string formattedJson = JsonConvert.SerializeObject(jsonObject, Formatting.Indented);
                            
                            // Update the text box with the formatted JSON
                            TxtExtractionResults.Text = formattedJson;
                            
                            // Save to file in the processing folder
                            if (processingFolder != null)
                            {
                                string jsonFilePath = Path.Combine(processingFolder, $"{originalFileName}_data.json");
                                
                                // Save the formatted JSON to file
                                File.WriteAllText(jsonFilePath, formattedJson);
                                
                                // Inform the user that the JSON has been saved
                                MessageBox.Show($"Extraction results have been formatted and saved to:\n{jsonFilePath}", 
                                    "JSON Saved", MessageBoxButton.OK, MessageBoxImage.Information);
                            }
                        }
                    }
                    catch (JsonException)
                    {
                        // If standard parsing fails, try more advanced repair
                        string repairedJson = AttemptAdvancedJsonRepair(jsonContent);
                        
                        try
                        {
                            var testObject = JsonConvert.DeserializeObject(repairedJson);
                            if (testObject != null)
                            {
                                // Successfully repaired
                                string formattedJson = JsonConvert.SerializeObject(testObject, Formatting.Indented);
                                TxtExtractionResults.Text = formattedJson;
                                
                                // Save to file in the processing folder
                                if (processingFolder != null)
                                {
                                    string jsonFilePath = Path.Combine(processingFolder, $"{originalFileName}_data.json");
                                    
                                    // Save the formatted JSON to file
                                    File.WriteAllText(jsonFilePath, formattedJson);
                                    
                                    // Inform the user that the JSON has been saved
                                    MessageBox.Show($"Extraction results have been repaired, formatted and saved to:\n{jsonFilePath}", 
                                        "JSON Saved", MessageBoxButton.OK, MessageBoxImage.Information);
                                }
                            }
                        }
                        catch
                        {
                            // Advanced repair also failed
                            MessageBox.Show("The extracted data couldn't be automatically converted to valid JSON format.",
                                "JSON Fix Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    }
                }
                catch (Exception jsonEx)
                {
                    MessageBox.Show($"Error processing JSON: {jsonEx.Message}", "JSON Processing Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error extracting data: {ex.Message}", "Extraction Error", MessageBoxButton.OK, MessageBoxImage.Error);
            TxtExtractionResults.Text = "Error occurred during extraction.";
        }
        finally
        {
            // Restore cursor
            Mouse.OverrideCursor = null;
            BtnExtractData.IsEnabled = true;
        }
    }

    // Show validation panel button click
    private void BtnShowValidation_Click(object sender, RoutedEventArgs e)
    {
        _isDataValidationPanelVisible = true;
        UpdateDataValidationPanelVisibility();
    }
    
    // Update validation panel visibility
    private void UpdateDataValidationPanelVisibility()
    {
        try
        {
            var grid = (Grid)this.Content;
            
            // Show/hide the data validation panel column
            if (grid.ColumnDefinitions.Count > 6)
            {
                if (_isDataValidationPanelVisible)
                {
                    grid.ColumnDefinitions[6].Width = new GridLength(1, GridUnitType.Star);
                    DataValidationPanel.Visibility = Visibility.Visible;
                    
                    // Update button visibility
                    if (BtnShowValidation != null)
                    {
                        BtnShowValidation.Visibility = Visibility.Collapsed;
                    }
            }
            else
            {
                    grid.ColumnDefinitions[6].Width = new GridLength(0);
                    DataValidationPanel.Visibility = Visibility.Collapsed;
                    
                    // Update button visibility
                    if (BtnShowValidation != null)
                    {
                        BtnShowValidation.Visibility = Visibility.Visible;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating validation panel visibility: {ex.Message}");
        }
    }
    
    // Test method to demonstrate validation with sample data
    private void LoadSampleJsonForValidation(string documentType)
    {
        string jsonContent = "";
        
        // Only supporting General type now
        jsonContent = @"{
  ""extracted_text"": ""This is a sample document with extracted content"",
  ""document_type"": ""General"",
  ""page_count"": ""1"",
  ""extraction_date"": ""03/04/2023"",
  ""confidence_score"": ""0.95"",
  ""elements"": [
    {
      ""type"": ""paragraph"",
      ""content"": ""Sample paragraph content from the document""
    },
    {
      ""type"": ""table"",
      ""rows"": ""3"",
      ""columns"": ""4"",
      ""headers"": [""Column 1"", ""Column 2"", ""Column 3"", ""Column 4""]
    }
  ]
}";
        
        try
        {
            // Set the sample JSON to the extraction results textbox
            TxtExtractionResults.Text = jsonContent;
            
            // Parse the JSON
            var jsonObject = JsonConvert.DeserializeObject<JObject>(jsonContent);
            
            // Show validation panel directly with selected document type
            if (jsonObject != null)
            {
                ShowDataValidationPanel(jsonObject, documentType);
                
                // Update UI state
                _isDataValidationPanelVisible = true;
                UpdateDataValidationPanelVisibility();
                
                // Update the document type dropdown
                foreach (ComboBoxItem item in CmbDocumentType.Items)
                {
                    if (item.Content.ToString() == documentType)
                    {
                        CmbDocumentType.SelectedItem = item;
                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading sample data: {ex.Message}", "Sample Data Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // Test validation button click
    private void BtnTestValidation_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Create a window to select the document type
            var selectWindow = new Window
            {
                Title = "Select Document Type",
                Width = 300,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize
            };
        
            var mainPanel = new StackPanel { Margin = new Thickness(20) };
            
            var docTypeLabel = new TextBlock 
            { 
                Text = "Choose a document type to test:", 
                Margin = new Thickness(0, 0, 0, 10),
                FontWeight = FontWeights.SemiBold
            };
            
            var radioButtonPanel = new StackPanel();
            
            var rbGeneral = new RadioButton 
            { 
                Content = "General", 
                IsChecked = true, 
                Margin = new Thickness(0, 5, 0, 5),
                GroupName = "DocType"
            };
            
            radioButtonPanel.Children.Add(rbGeneral);
            
            var buttonsPanel = new StackPanel 
            { 
                Orientation = Orientation.Horizontal, 
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 20, 0, 0)
            };
            
            var cancelButton = new Button 
            { 
                Content = "Cancel", 
                Padding = new Thickness(10, 5, 10, 5), 
                Margin = new Thickness(5, 0, 0, 0) 
            };
            
            var okButton = new Button 
            { 
                Content = "OK", 
                Padding = new Thickness(10, 5, 10, 5), 
                IsDefault = true 
            };
            
            cancelButton.Click += (s, args) => { selectWindow.DialogResult = false; };
            okButton.Click += (s, args) => { selectWindow.DialogResult = true; };
            
            buttonsPanel.Children.Add(okButton);
            buttonsPanel.Children.Add(cancelButton);
            
            mainPanel.Children.Add(docTypeLabel);
            mainPanel.Children.Add(radioButtonPanel);
            mainPanel.Children.Add(buttonsPanel);
            
            selectWindow.Content = mainPanel;
            
            // Show the window and process results
            if (selectWindow.ShowDialog() == true)
            {
                // Load the selected document type for validation
                LoadSampleJsonForValidation("General");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error testing validation: {ex.Message}", "Test Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // Attempt to repair seriously malformed JSON with common patterns
    private string AttemptAdvancedJsonRepair(string input)
    {
        if (string.IsNullOrEmpty(input))
            return "{}";
            
        string result = input;
        
        try
        {
            // Add missing braces if needed
            if (!result.TrimStart().StartsWith("{"))
                result = "{" + result;
                
            if (!result.TrimEnd().EndsWith("}"))
                result = result + "}";
                
            // Fix property names without quotes
            result = System.Text.RegularExpressions.Regex.Replace(
                result,
                @"(\s*)(\w+)(\s*):", 
                m => $"{m.Groups[1].Value}\"{m.Groups[2].Value}\"{m.Groups[3].Value}:");
                
            // Fix missing quotes around string values 
            result = System.Text.RegularExpressions.Regex.Replace(
                result,
                @":(\s*)([^""\d\s\[\]{},][^,\[\]{}]*?)(\s*)(,|$|]|})", 
                m => $":{m.Groups[1].Value}\"{m.Groups[2].Value.Trim()}\"{m.Groups[3].Value}{m.Groups[4].Value}");
                
            // Fix missing commas between properties
            result = System.Text.RegularExpressions.Regex.Replace(
                result, 
                @"}(\s*)""", 
                "},\"");
                
            // Fix trailing commas
            result = result.Replace(",}", "}").Replace(",]", "]");
            
            // Fix repeated commas
            result = result.Replace(",,", ",");
            
            return result;
        }
        catch
        {
            // If repair process fails, return the original input
            return input;
        }
    }
    
    // Helper method to convert a string to camelCase
    private string ConvertToCamelCase(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;
            
        string[] words = input.Split(new[] { ' ', '_', '-' }, StringSplitOptions.RemoveEmptyEntries);
        string result = words[0].ToLower();
        
        for (int i = 1; i < words.Length; i++)
        {
            result += char.ToUpper(words[i][0]) + words[i].Substring(1).ToLower();
        }
        
        return result;
    }
    
    // Helper method to convert a string to snake_case
    private string ConvertToSnakeCase(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;
            
        // Replace spaces, hyphens with underscores
        string result = input.Replace(' ', '_').Replace('-', '_');
        
        // Handle camelCase or PascalCase
        result = Regex.Replace(result, "([a-z])([A-Z])", "$1_$2");
        
        return result.ToLower();
    }

    // Update page navigation display
    private void UpdatePageNavigationInfo()
    {
        TxtPageInfo.Text = $"Page {_currentPdfPage} of {_totalPdfPages}";
        
        // Enable/disable navigation buttons
        BtnPrevPage.IsEnabled = _currentPdfPage > 1;
        BtnNextPage.IsEnabled = _currentPdfPage < _totalPdfPages;
    }
    
    // Handle Previous Page button click
    private void BtnPrevPage_Click(object sender, RoutedEventArgs e)
    {
        if (_currentPdfPage > 1)
        {
            _currentPdfPage--;
            LoadPageData(_currentPdfPage);
            UpdatePageNavigationInfo();
        }
    }
    
    // Handle Next Page button click
    private void BtnNextPage_Click(object sender, RoutedEventArgs e)
    {
        if (_currentPdfPage < _totalPdfPages)
        {
            _currentPdfPage++;
            LoadPageData(_currentPdfPage);
            UpdatePageNavigationInfo();
        }
    }
    
    // Load data for a specific page
    private void LoadPageData(int pageNum)
    {
        // Check if we already have the result in memory
        if (_pdfPageResults.ContainsKey(pageNum))
        {
            TxtExtractionResults.Text = _pdfPageResults[pageNum];
                return;
            }
            
        // Otherwise, try to load from file
        try
        {
            string jsonFilePath = Path.Combine(_processingFolder, $"{_pdfBaseName}_page{pageNum}_data.json");
            if (File.Exists(jsonFilePath))
            {
                string jsonContent = File.ReadAllText(jsonFilePath);
                _pdfPageResults[pageNum] = jsonContent;
                TxtExtractionResults.Text = jsonContent;
            }
            else
            {
                TxtExtractionResults.Text = $"Data for page {pageNum} is not available.";
                }
            }
            catch (Exception ex)
            {
            TxtExtractionResults.Text = $"Error loading page {pageNum} data: {ex.Message}";
        }
    }

    // Process PDF for AI extraction - saves individual JSON files for each page
    private async Task ProcessPdfForAIExtraction(string pdfPath, string prompt)
    {
        try
        {
            // Get the processing folder
            string processingFolder = GetProcessingFolder(pdfPath);
            string originalFileName = Path.GetFileNameWithoutExtension(pdfPath);
            
            // Initialize PDF page navigation variables
            _processingFolder = processingFolder;
            _pdfBaseName = originalFileName;
            _pdfPageResults.Clear();
            
            // Open the PDF file
            using (PdfReader reader = new PdfReader(pdfPath))
            {
                // Get total pages 
                _totalPdfPages = reader.NumberOfPages;
                _currentPdfPage = 1;
                
                // Show PDF navigation for multi-page PDFs
                if (_totalPdfPages > 1)
                {
                    PdfNavigationPanel.Visibility = Visibility.Visible;
                }
                else
                {
                    PdfNavigationPanel.Visibility = Visibility.Collapsed;
                }
                
                // Process each page
                for (int pageNum = 1; pageNum <= _totalPdfPages; pageNum++)
                {
                    // Show progress
                    TxtExtractionResults.Text = $"Processing page {pageNum} of {_totalPdfPages}...";
                    await Task.Delay(50); // Allow UI to update
                    
                    // Generate a temporary image file for this page
                    string tempImagePath = Path.Combine(processingFolder, $"{originalFileName}_page{pageNum}.png");
                    
                    // Convert PDF page to image
                    using (var pdfDocument = PdfiumViewer.PdfDocument.Load(pdfPath))
                    {
                        using (var bitmap = pdfDocument.Render(pageNum - 1, 300, 300, true))
                        {
                            bitmap.Save(tempImagePath, ImageFormat.Png);
                        }
                    }
                    
                    // Perform extraction on the page image
                    string extractionResult = await _dashScopeService.PerformOcrAsync(tempImagePath, prompt);
                    
                    // Pre-process the result to extract only the JSON
                    extractionResult = PreProcessExtractionResult(extractionResult);
                    
                    // Store the result in memory
                    _pdfPageResults[pageNum] = extractionResult;
                    
                    // Clean and format the JSON
                    try
                    {
                        string jsonContent = CleanJsonContent(extractionResult);
                        
                        try
                        {
                            var jsonObject = JsonConvert.DeserializeObject(jsonContent);
                            if (jsonObject != null)
                            {
                                // Format with proper indentation
                                string formattedJson = JsonConvert.SerializeObject(jsonObject, Formatting.Indented);
                                
                                // Update the stored result with the formatted version
                                _pdfPageResults[pageNum] = formattedJson;
                                
                                // Save to file with page number in filename
                                string jsonFilePath = Path.Combine(processingFolder, $"{originalFileName}_page{pageNum}_data.json");
                                File.WriteAllText(jsonFilePath, formattedJson);
                            }
                        }
                        catch (JsonException)
                        {
                            // If standard parsing fails, try advanced repair
                            string repairedJson = AttemptAdvancedJsonRepair(jsonContent);
                            
                            try
                            {
                                var testObject = JsonConvert.DeserializeObject(repairedJson);
                                if (testObject != null)
                                {
                                    // Successfully repaired
                                    string formattedJson = JsonConvert.SerializeObject(testObject, Formatting.Indented);
                                    
                                    // Update the stored result with the repaired version
                                    _pdfPageResults[pageNum] = formattedJson;
                                    
                                    // Save to file with page number in filename
                                    string jsonFilePath = Path.Combine(processingFolder, $"{originalFileName}_page{pageNum}_data.json");
                                    File.WriteAllText(jsonFilePath, formattedJson);
                                }
                            }
                            catch
                            {
                                // Even the advanced repair failed, save the raw result
                                string jsonFilePath = Path.Combine(processingFolder, $"{originalFileName}_page{pageNum}_data.json");
                                File.WriteAllText(jsonFilePath, extractionResult);
                            }
                        }
                    }
                    catch (Exception jsonEx)
                    {
                        // Log but continue with next page
                        Console.WriteLine($"Error processing JSON for page {pageNum}: {jsonEx.Message}");
                    }
                    
                    // Clean up the temporary image file
                    try
                    {
                        if (File.Exists(tempImagePath))
                        {
                            File.Delete(tempImagePath);
                        }
                    }
                    catch 
                    {
                        // Ignore cleanup errors
                    }
                }
            }
            
            // Display the results of the current page
            UpdatePageNavigationInfo();
            if (_pdfPageResults.ContainsKey(_currentPdfPage))
            {
                TxtExtractionResults.Text = _pdfPageResults[_currentPdfPage];
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"Error processing PDF: {ex.Message}", ex);
        }
    }
    
    // Helper method to get the processing folder
    private string GetProcessingFolder(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            return null;
        
        try
        {
            // Get the directory where the file is located
            string parentDirectory = Path.GetDirectoryName(filePath);
            string fileName = Path.GetFileNameWithoutExtension(filePath);
            
            // Create a processing folder next to the file
            string processingFolder = Path.Combine(parentDirectory, $"{fileName}_processing");
            
            // Create the folder if it doesn't exist
            if (!Directory.Exists(processingFolder))
            {
                Directory.CreateDirectory(processingFolder);
            }
            
            return processingFolder;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating processing folder: {ex.Message}");
            return null;
        }
    }
    
    // Helper method to compute a hash for a file
    private string ComputeFileHash(string filePath)
    {
        try
        {
            using (var md5 = MD5.Create())
            {
                using (var stream = File.OpenRead(filePath))
                {
                    byte[] hash = md5.ComputeHash(stream);
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }
        }
        catch
        {
            // Fallback to a simpler approach if hashing fails
            return Path.GetFileNameWithoutExtension(filePath) + "_" + 
                   new FileInfo(filePath).Length.ToString();
        }
    }
    
    // Helper to format file size in human-readable format
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
    
    // Helper method to get pixel format depth
    private int GetPixelFormatDepth(PixelFormat format)
    {
        switch (format)
        {
            case PixelFormat.Format1bppIndexed:
                return 1;
            case PixelFormat.Format4bppIndexed:
                return 4;
            case PixelFormat.Format8bppIndexed:
                return 8;
            case PixelFormat.Format16bppGrayScale:
            case PixelFormat.Format16bppRgb555:
            case PixelFormat.Format16bppRgb565:
            case PixelFormat.Format16bppArgb1555:
                return 16;
            case PixelFormat.Format24bppRgb:
                return 24;
            case PixelFormat.Format32bppRgb:
            case PixelFormat.Format32bppArgb:
            case PixelFormat.Format32bppPArgb:
                return 32;
            case PixelFormat.Format48bppRgb:
                return 48;
            case PixelFormat.Format64bppArgb:
            case PixelFormat.Format64bppPArgb:
                return 64;
            default:
                return 0;
        }
    }

    // Helper method to clean JSON content
    private string CleanJsonContent(string jsonText)
    {
        if (string.IsNullOrEmpty(jsonText))
            return "{}";
            
        // Remove common markdown formatting
        string cleaned = jsonText;
        
        // Remove codeblocks
        cleaned = Regex.Replace(cleaned, @"```(?:json)?([^`]+)```", "$1", RegexOptions.Singleline);
        
        // Remove any random text before the first { or after the last }
        int firstBrace = cleaned.IndexOf('{');
        int lastBrace = cleaned.LastIndexOf('}');
        
        if (firstBrace >= 0 && lastBrace > firstBrace)
        {
            cleaned = cleaned.Substring(firstBrace, lastBrace - firstBrace + 1);
        }
        
        return cleaned;
    }
    
    // Helper method to pre-process extraction result to extract the JSON
    private string PreProcessExtractionResult(string result)
    {
        if (string.IsNullOrEmpty(result))
            return "{}";
            
        // Extract JSON from markdown code blocks if present
        Match jsonMatch = Regex.Match(result, @"```(?:json)?([^`]+)```", RegexOptions.Singleline);
        if (jsonMatch.Success)
        {
            return jsonMatch.Groups[1].Value.Trim();
        }
        
        // If not in code blocks, try to extract JSON using braces
        int firstBrace = result.IndexOf('{');
        int lastBrace = result.LastIndexOf('}');
        
        if (firstBrace >= 0 && lastBrace > firstBrace)
        {
            return result.Substring(firstBrace, lastBrace - firstBrace + 1);
        }
        
        // Return the original if no json-like structure found
        return result;
    }
    
    // Helper method to show data validation panel
    private void ShowDataValidationPanel(object jsonData, string documentType)
    {
        try
        {
            // Clear existing fields
            ValidationFieldsPanel.Children.Clear();
            
            // Convert to JObject
            JObject dataObject;
            
            if (jsonData is JObject obj)
            {
                dataObject = obj;
            }
            else
            {
                string jsonString = jsonData.ToString();
                dataObject = JObject.Parse(jsonString);
            }
            
            // For General document type, create fields based on all properties
            AddValidationFieldsFromJObject(dataObject);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error setting up validation panel: {ex.Message}", 
                "Validation Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    
    // Helper method to add validation fields based on a list of field names
    private void AddValidationFields(JObject data, string[] fieldNames)
    {
        foreach (string fieldName in fieldNames)
        {
            // Create field label
            var fieldLabel = new TextBlock
            {
                Text = fieldName.Replace("_", " ").ToUpper(),
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 10, 0, 2)
            };
            
            ValidationFieldsPanel.Children.Add(fieldLabel);
            
            // Create field value text box
            var fieldValue = new TextBox
            {
                Text = data[fieldName]?.ToString() ?? "",
                Padding = new Thickness(8, 5, 8, 5),
                Margin = new Thickness(0, 0, 0, 10),
                Tag = fieldName // Store the field name for later reference
            };
            
            ValidationFieldsPanel.Children.Add(fieldValue);
        }
    }
    
    // Helper method to dynamically add validation fields based on JObject properties
    private void AddValidationFieldsFromJObject(JObject data)
    {
        foreach (var property in data.Properties())
        {
            // Create field label
            var fieldLabel = new TextBlock
            {
                Text = property.Name.Replace("_", " ").ToUpper(),
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 10, 0, 2)
            };
            
            ValidationFieldsPanel.Children.Add(fieldLabel);
            
            // Create field value text box
            var fieldValue = new TextBox
            {
                Text = property.Value?.ToString() ?? "",
                Padding = new Thickness(8, 5, 8, 5),
                Margin = new Thickness(0, 0, 0, 10),
                Tag = property.Name // Store the field name for later reference
            };
            
            ValidationFieldsPanel.Children.Add(fieldValue);
        }
    }

    private void CheckPdfiumDependencies()
    {
        try
        {
            // Test loading a minimal PDF document to verify PDFium is installed
            using (var stream = new MemoryStream())
            {
                // Create minimal valid PDF
                var bytes = Encoding.ASCII.GetBytes("%PDF-1.4\n%âãÏÓ\n1 0 obj\n<</Type/Catalog/Pages 2 0 R>>\nendobj\n2 0 obj\n<</Type/Pages/Kids[]/Count 0>>\nendobj\nxref\n0 3\n0000000000 65535 f\n0000000015 00000 n\n0000000060 00000 n\ntrailer\n<</Size 3/Root 1 0 R>>\nstartxref\n110\n%%EOF\n");
                stream.Write(bytes, 0, bytes.Length);
                stream.Position = 0;

                // Try to load the PDF with PdfiumViewer
                using (var pdfDocument = PdfiumViewer.PdfDocument.Load(stream))
                {
                    // If it loads successfully, PDFium is properly installed
                }
            }
        }
        catch (DllNotFoundException)
        {
            MessageBox.Show(
                "PDF processing libraries are missing. Please reinstall the application or contact support.",
                "Missing Dependencies",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        catch (Exception ex)
        {
            // Other errors can be logged but may not require user notification
            Console.WriteLine($"Error checking PDF dependencies: {ex.Message}");
        }
    }

    // PDF operations button handlers
    private void BtnConvertToImage_Click(object sender, RoutedEventArgs e)
    {
        // Stub implementation - to be implemented later
        MessageBox.Show("Convert to Image functionality will be implemented in a future update.", "Not Implemented", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BtnSetPassword_Click(object sender, RoutedEventArgs e)
    {
        // Stub implementation - to be implemented later
        MessageBox.Show("Set Password functionality will be implemented in a future update.", "Not Implemented", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BtnRemovePassword_Click(object sender, RoutedEventArgs e)
    {
        // Stub implementation - to be implemented later
        MessageBox.Show("Remove Password functionality will be implemented in a future update.", "Not Implemented", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BtnSignPdf_Click(object sender, RoutedEventArgs e)
    {
        // Stub implementation - to be implemented later
        MessageBox.Show("Sign PDF functionality will be implemented in a future update.", "Not Implemented", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    // Image operations button handlers
    private void BtnConvertToPdf_Click(object sender, RoutedEventArgs e)
    {
        // Stub implementation - to be implemented later
        MessageBox.Show("Convert to PDF functionality will be implemented in a future update.", "Not Implemented", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BtnIncreaseDpi_Click(object sender, RoutedEventArgs e)
    {
        // Stub implementation - to be implemented later
        MessageBox.Show("Increase DPI functionality will be implemented in a future update.", "Not Implemented", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BtnCropImage_Click(object sender, RoutedEventArgs e)
    {
        // Stub implementation - to be implemented later
        MessageBox.Show("Crop Image functionality will be implemented in a future update.", "Not Implemented", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    // Extraction panel button handlers
    private void BtnCopyResults_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(TxtExtractionResults?.Text))
    {
        try
        {
                Clipboard.SetText(TxtExtractionResults.Text);
                MessageBox.Show("Results copied to clipboard.", "Copy Successful", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
                MessageBox.Show($"Error copying to clipboard: {ex.Message}", "Copy Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void BtnValidateData_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Parse the JSON from the extraction results
            if (!string.IsNullOrEmpty(TxtExtractionResults?.Text))
            {
                var jsonObject = JsonConvert.DeserializeObject<JObject>(TxtExtractionResults.Text);
                if (jsonObject != null)
                {
                    // Get the selected document type
                    ComboBoxItem selectedItem = (ComboBoxItem)CmbDocumentType?.SelectedItem;
                    string documentType = selectedItem?.Content.ToString() ?? "General";
                    
                    // Show the validation panel
                    ShowDataValidationPanel(jsonObject, documentType);
                    
                    // Show the validation panel
                    _isDataValidationPanelVisible = true;
                    UpdateDataValidationPanelVisibility();
                            }
                            else
                            {
                    MessageBox.Show("Failed to parse JSON data. Please check the format of the extracted data.", 
                        "Invalid JSON", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                    }
                    else
                    {
                MessageBox.Show("No extraction data to validate. Please extract data first.", 
                    "No Data", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error validating data: {ex.Message}", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // Validation panel button handlers
    private void BtnToggleValidation_Click(object sender, RoutedEventArgs e)
    {
        // Hide validation panel
        _isDataValidationPanelVisible = false;
        UpdateDataValidationPanelVisibility();
    }
    
    private void BtnUpdateValidation_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // First, build a new JSON object from the validation fields
            JObject updatedData = new JObject();
            
            // Iterate through all TextBox controls in the ValidationFieldsPanel
            foreach (var child in ValidationFieldsPanel.Children)
            {
                if (child is TextBox textBox && textBox.Tag != null)
                {
                    string fieldName = textBox.Tag.ToString();
                    string fieldValue = textBox.Text;
                    
                    // Add the field to the updated JSON object
                    updatedData[fieldName] = fieldValue;
                }
            }
            
            // Format the JSON for display
            string jsonText = JsonConvert.SerializeObject(updatedData, Formatting.Indented);
            
            // Update the extraction results text box
            TxtExtractionResults.Text = jsonText;
            
            MessageBox.Show("JSON data has been updated with your changes.", "Update Successful", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error updating JSON: {ex.Message}", "Update Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnSaveValidation_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (string.IsNullOrEmpty(_currentFilePath))
            {
                MessageBox.Show("No document is currently loaded.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            
            // Get the processing folder
            string processingFolder = GetProcessingFolder(_currentFilePath);
            if (processingFolder == null)
            {
                MessageBox.Show("Could not create processing folder. Operation cancelled.", "Folder Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Get current JSON from extraction results
            string jsonContent = TxtExtractionResults.Text;
            if (string.IsNullOrEmpty(jsonContent))
            {
                MessageBox.Show("No data to save. Please extract data first.", "No Data", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            
            // Define the file name based on whether this is a PDF page or a single image
            string originalFileName = Path.GetFileNameWithoutExtension(_currentFilePath);
            string jsonFilePath;
            
            if (_isPdf && _totalPdfPages > 1)
            {
                // For multi-page PDFs, we save page-specific JSON files
                jsonFilePath = Path.Combine(processingFolder, $"{originalFileName}_page{_currentPdfPage}_data.json");
                
                // Update the in-memory collection with the current content
                _pdfPageResults[_currentPdfPage] = jsonContent;
            }
            else
            {
                // For single image or single-page PDF
                jsonFilePath = Path.Combine(processingFolder, $"{originalFileName}_data.json");
            }
            
            // Save the JSON to file
            File.WriteAllText(jsonFilePath, jsonContent);
            
            MessageBox.Show($"Validated data has been saved to:\n{jsonFilePath}", "Save Successful", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error saving validated data: {ex.Message}", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}