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
using System.Windows.Data;  // For Binding
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Media;  // For VisualTreeHelper

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
    
    // Dictionary of document type prompts
    private readonly Dictionary<string, string> PROMPTS = new Dictionary<string, string>
    {
        {"Business Card", "I have this image and it contains one or multiple business cards, can you help me to extract the information from this image into the JSON format as: {\"0\": <bizcard_json_1>,\"1\": <bizcard_json_2> ..., \"n\": <bizcard_json_n>}. In child JSON like <bizcard_json_1> will follow this format {\"company_name\": \"\", \"full_name\": \"\", \"title\": \"\", \"email_address\": \"\", \"phone_number\": \"\", \"tel_number\": \"\", \"fax_number\": \"\", \"website\": \"\", \"address\": \"\", \"handwritting_content\": \"\"}"},
        {"Receipt", "Give me JSON as this format: {\"receipt_number\": \"\", \"document_date\": \"\", \"store_name\": \"\", \"store_address\": \"\", \"phone_number\": \"\", \"fax_number\": \"\", \"email\": \"\", \"website\": \"\", \"gst_id\": \"\", \"pax_number\": \"\", \"table_number\": \"\", \"cashier_name\": \"\", \"item_no_of_receipt_items\": [], \"item_code_of_receipt_items\": [], \"names_of_receipt_items\": [], \"quantities_of_receipt_items\": [], \"unit_prices_of_receipt_items\": [], \"gross_worth_of_receipt_items\": [], \"subtotal\": \"\", \"rounding_amount\": \"\", \"paid_amount\": \"\", \"change_amount\": \"\", \"service_charge_percent\": \"\", \"service_charge\": \"\", \"currency\": \"\", \"tax_percent\": \"\", \"tax_total\": \"\", \"total\": \"\", \"handwritting_content\": \"\"}. Note: Convert document_date to DD/MM/YYYY, Currency in Singapore Standard, All data value in string, Remove Line Break special character"},
        {"Purchase Order", "Give me JSON as this format: {\"company_name\": \"\", \"purchase_order_number\": \"\", \"document_date\": \"\", \"client_name\": \"\", \"client_address\": \"\", \"sale_order_number\": \"\", \"client_tax_id\": \"\", \"seller_name\": \"\", \"seller_address\": \"\", \"seller_tax_id\": \"\", \"iban\": \"\", \"item_no_of_invoice_items\": [], \"names_of_invoice_items\": [], \"quantities_of_invoice_items\": [], \"unit_prices_of_invoice_items\": [], \"gross_worth_of_invoice_items\": [], \"total_net_worth\": \"\", \"tax_amount\": \"\", \"tax_percent\": \"\", \"total_gross_worth\": \"\", \"handwritting_content\": \"\"}. Note: Convert document_date to DD/MM/YYYY, Currency in Singapore Standard, All data value in string format, Remove Line Break special character"},
        {"Invoice", "Give me JSON as this format: {\"company_name\": \"\", \"invoice_number\": \"\", \"purchase_order_number\": \"\", \"document_date\": \"\", \"client_name\": \"\", \"client_address\": \"\", \"sale_order_number\": \"\", \"client_tax_id\": \"\", \"seller_name\": \"\", \"seller_address\": \"\", \"seller_tax_id\": \"\", \"iban\": \"\", \"item_no_of_invoice_items\": [], \"names_of_invoice_items\": [], \"quantities_of_invoice_items\": [], \"unit_prices_of_invoice_items\": [], \"gross_worth_of_invoice_items\": [], \"total_net_worth\": \"\", \"tax_amount\": \"\", \"tax_percent\": \"\", \"total_gross_worth\": \"\", \"handwritting_content\": \"\"}. Note: Convert document_date to DD/MM/YYYY, Currency in Singapore Standard, All data value in string format, Remove Line Break special character"},
        {"Delivery Order", "Give me JSON as this format: {\"company_name\": \"\", \"invoice_number\": \"\", \"purchase_order_number\": \"\", \"delivery_order_number\": \"\", \"document_date\": \"\", \"client_name\": \"\", \"client_address\": \"\", \"sale_order_number\": \"\", \"client_tax_id\": \"\", \"seller_name\": \"\", \"seller_address\": \"\", \"seller_tax_id\": \"\", \"iban\": \"\", \"item_no_of_invoice_items\": [], \"names_of_invoice_items\": [], \"quantities_of_invoice_items\": [], \"unit_prices_of_invoice_items\": [], \"gross_worth_of_invoice_items\": [], \"total_net_worth\": \"\", \"tax_amount\": \"\", \"tax_percent\": \"\", \"total_gross_worth\": \"\", \"handwritting_content\": \"\"}. Note: Convert document_date to DD/MM/YYYY, Currency in Singapore Standard, All data value in string format, Remove Line Break special character"},
        {"Bankstatement", "Please extract all information as JSON"},
        {"General", "Please extract all information as JSON"},
        {"POSBBankStatement", "Please extract all information as JSON. All data value in string format"},
        {"OCBCBankStatement", "Please extract all information as JSON. Note: 1) Transactions item follow this format {\"date\": \"\", \"description\": \"\", \"cheque\": \"\", \"withdrawal\": \"\", \"deposit\": \"\", \"balance\": \"\"}. 2) All data value in string format"},
        {"TextDocument", "Extract the full text content from this document and return it in the following JSON format: {\"full_text\": \"Document content\"}"}
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
        if (string.IsNullOrEmpty(filePath) || !System.IO.File.Exists(filePath))
            return false;

        string extension = System.IO.Path.GetExtension(filePath).ToLower();
        
        // Check if file is PDF or a valid image type
        return extension == ".pdf" || 
               extension == ".jpg" || 
               extension == ".jpeg" || 
               extension == ".png" || 
               extension == ".bmp" || 
               extension == ".tiff" || 
               extension == ".tif" || 
               extension == ".gif";
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
            
            BtnOcr.IsEnabled = !_isPdf && _currentFilePath != null;
        }
        else
        {
            _dashScopeService = null;
            BtnOcr.IsEnabled = false;
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
                
                // Enable PDF operation buttons
                BtnConvertToImage.IsEnabled = true;
                BtnSetPassword.IsEnabled = true;
                BtnRemovePassword.IsEnabled = true;
                BtnSignPdf.IsEnabled = true;
                
                // Disable image operation buttons
                BtnConvertToPdf.IsEnabled = false;
                BtnIncreaseDpi.IsEnabled = false;
                BtnCropImage.IsEnabled = false;
            }
            else
            {
                // Load image file
                LoadImage(filePath);
                ImageViewer.Visibility = Visibility.Visible;
                
                // Show Image operations and hide PDF operations
                if (PdfOperationsGroup != null) PdfOperationsGroup.Visibility = Visibility.Collapsed;
                if (ImageOperationsGroup != null) ImageOperationsGroup.Visibility = Visibility.Visible;
                
                // Disable PDF operation buttons
                BtnConvertToImage.IsEnabled = false;
                BtnSetPassword.IsEnabled = false;
                BtnRemovePassword.IsEnabled = false;
                BtnSignPdf.IsEnabled = false;
                
                // Enable image operation buttons
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

            // Show common action buttons
            if (ActionsGroup != null) ActionsGroup.Visibility = Visibility.Visible;
            
            // Enable action buttons
            BtnSaveAs.IsEnabled = true;
            BtnPrint.IsEnabled = true;
            
            // OCR button is enabled for both images and PDFs if the API is configured
            BtnOcr.IsEnabled = _dashScopeService != null;
            
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
            }
            else
            {
                // Perform extraction with Qwen-VL-Max using the specific prompt for a single image
                string extractionResult = await _dashScopeService.PerformOcrAsync(_currentFilePath, prompt);
                
                // Pre-process the result to extract only the JSON if in markdown
                extractionResult = PreProcessExtractionResult(extractionResult);
                
                // Display the results
                TxtExtractionResults.Text = extractionResult;
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

    // Process extraction results to extract JSON from markdown
    private string PreProcessExtractionResult(string result)
    {
        if (string.IsNullOrEmpty(result))
            return result;
            
        try
        {
            // Check if result is enclosed in markdown code block
            if (result.Contains("```json") && result.Contains("```"))
            {
                int startIndex = result.IndexOf("```json") + 7;
                int endIndex = result.LastIndexOf("```");
                
                if (endIndex > startIndex)
                {
                    // Extract the JSON content between the markers
                    string jsonPart = result.Substring(startIndex, endIndex - startIndex).Trim();
                    
                    // Verify this is valid JSON before returning
                    try
                    {
                        // Try to deserialize to ensure it's valid JSON
                        var testParse = JsonConvert.DeserializeObject(jsonPart);
                        if (testParse != null)
                        {
                            return jsonPart;
                        }
                    }
                    catch
                    {
                        // If parsing fails, we'll continue to try other methods
                    }
                }
            }
            // Also check for plain code blocks
            else if (result.Contains("```") && result.Contains("```"))
            {
                int startIndex = result.IndexOf("```") + 3;
                int endIndex = result.LastIndexOf("```");
                
                if (endIndex > startIndex)
                {
                    // Extract the content between the markers
                    string jsonPart = result.Substring(startIndex, endIndex - startIndex).Trim();
                    
                    // Verify this is valid JSON before returning
                    try
                    {
                        var testParse = JsonConvert.DeserializeObject(jsonPart);
                        if (testParse != null)
                        {
                            return jsonPart;
                        }
                    }
                    catch
                    {
                        // If parsing fails, we'll continue to try other methods
                    }
                }
            }
            
            // Look for JSON objects in the text
            int braceStart = result.IndexOf('{');
            int braceEnd = result.LastIndexOf('}');
            
            if (braceStart >= 0 && braceEnd > braceStart)
            {
                // Try to extract just the JSON part
                string jsonPart = result.Substring(braceStart, braceEnd - braceStart + 1);
                
                try
                {
                    // Verify this is valid JSON by parsing it
                    var testParse = JsonConvert.DeserializeObject(jsonPart);
                    if (testParse != null)
                    {
                        // This is valid JSON, so return just this part
                        return jsonPart;
                    }
                }
                catch
                {
                    // Try to clean up common JSON issues before giving up
                    try
                    {
                        string cleanedJson = CleanJsonContent(jsonPart);
                        var testParse = JsonConvert.DeserializeObject(cleanedJson);
                        if (testParse != null)
                        {
                            return cleanedJson;
                        }
                    }
                    catch
                    {
                        // Still not valid JSON, return the original
                    }
                }
            }
            
            // As a last resort, try to clean the entire result
            try
            {
                string cleanedJson = CleanJsonContent(result);
                var testParse = JsonConvert.DeserializeObject(cleanedJson);
                if (testParse != null)
                {
                    return cleanedJson;
                }
            }
            catch
            {
                // Not valid JSON even after cleaning
            }
            
            return result;
        }
        catch
        {
            // If any processing fails, return the original
            return result;
        }
    }

    // Process PDF file for AI extraction by converting each page to image
    private async Task ProcessPdfForAIExtraction(string pdfPath, string prompt)
    {
        try
        {
            // Create output folder for temporary images
            string tempFolder = Path.Combine(Path.GetTempPath(), $"pdf_extraction_{Guid.NewGuid()}");
            Directory.CreateDirectory(tempFolder);
            
            // Set status for user
            TxtExtractionResults.Text = "Converting PDF pages to images...";
            DoEvents();
            
            // Open the PDF document
            using (PdfReader reader = new PdfReader(pdfPath))
            {
                // Get the number of pages
                int pageCount = reader.NumberOfPages;
                
                if (pageCount == 0)
                {
                    TxtExtractionResults.Text = "The PDF file does not contain any pages.";
                    return;
                }
                
                StringBuilder combinedResults = new StringBuilder();
                combinedResults.AppendLine($"PDF Analysis - {Path.GetFileName(pdfPath)} ({pageCount} pages)\n");
                
                // Process each page
                for (int pageNum = 1; pageNum <= pageCount; pageNum++)
                {
                    TxtExtractionResults.Text = $"Processing page {pageNum} of {pageCount}...";
                    DoEvents();
                    
                    // Generate image for this page with a high DPI for better OCR quality
                    string tempImagePath = Path.Combine(tempFolder, $"page_{pageNum}.png");
                    
                    using (var bitmap = GetPageImage(reader, pageNum, 300)) // 300 DPI for good quality
                    {
                        bitmap.Save(tempImagePath, System.Drawing.Imaging.ImageFormat.Png);
                    }
                    
                    // Process the image with AI extraction
                    string pagePrompt = prompt;
                    if (pageCount > 1)
                    {
                        // Modify prompt to indicate which page is being processed
                        pagePrompt = $"This is page {pageNum} of {pageCount} from a PDF document. {prompt}";
                    }
                    
                    // Perform OCR on this page
                    string pageResult = await _dashScopeService.PerformOcrAsync(tempImagePath, pagePrompt);
                    
                    // Add to combined results
                    combinedResults.AppendLine($"--- Page {pageNum} Results ---");
                    combinedResults.AppendLine(pageResult);
                    combinedResults.AppendLine();
                    
                    // Update UI with progress
                    TxtExtractionResults.Text = combinedResults.ToString();
                    DoEvents();
                }
                
                // Display final combined results
                TxtExtractionResults.Text = combinedResults.ToString();
            }
            
            // Clean up temporary folder
            try
            {
                Directory.Delete(tempFolder, true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"Error processing PDF: {ex.Message}", ex);
        }
    }

    // Copy Results button
    private void BtnCopyResults_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!string.IsNullOrEmpty(TxtExtractionResults.Text))
            {
                Clipboard.SetText(TxtExtractionResults.Text);
                MessageBox.Show("Results copied to clipboard.", "Copy Successful", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error copying text: {ex.Message}", "Copy Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // Validate Data button (previously Save Results)
    private void BtnValidateData_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (string.IsNullOrEmpty(TxtExtractionResults.Text))
            {
                MessageBox.Show("No extraction results to validate.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Parse the JSON data
            string jsonContent = TxtExtractionResults.Text.Trim();
            
            // Try to clean up the JSON content if needed
            jsonContent = CleanJsonContent(jsonContent);
            
            try
            {
                // First attempt to parse JSON as a generic JToken to determine type
                var token = JsonConvert.DeserializeObject<JToken>(jsonContent);
                
                if (token == null)
                {
                    MessageBox.Show("Invalid JSON format. The content could not be parsed.", 
                        "JSON Validation Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                JObject jsonObject = null;
                
                // Convert to JObject based on token type
                if (token is JObject obj)
                {
                    jsonObject = obj;
                }
                else if (token is JArray array)
                {
                    // Create a wrapper object to handle array
                    jsonObject = new JObject
                    {
                        ["items"] = array
                    };
                    
                    MessageBox.Show("The JSON was detected as an array and has been wrapped in an object for validation.",
                        "JSON Type Information", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else if (token is JValue value)
                {
                    // Create a wrapper object for primitive value
                    jsonObject = new JObject
                    {
                        ["value"] = value
                    };
                    
                    MessageBox.Show("The content was detected as a simple value and has been wrapped in an object for validation.",
                        "JSON Type Information", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Unsupported JSON format. The content could not be processed as a JSON object.", 
                        "JSON Validation Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                // Show document type selection dialog first
                string selectedDocType = ShowDocumentTypeSelectionDialog(jsonObject);
                
                // Only proceed if a document type was selected
                if (!string.IsNullOrEmpty(selectedDocType))
                {
                    // Show the validation panel with the selected document type
                    ShowDataValidationPanel(jsonObject, selectedDocType);
                    
                    // Update UI state
                    _isDataValidationPanelVisible = true;
                    UpdateDataValidationPanelVisibility();
                }
            }
            catch (JsonException ex)
            {
                // Show a more user-friendly error message with guidance
                string errorMessage = $"Invalid JSON format: {ex.Message}\n\nPlease check for:\n" +
                    "- Missing or extra commas\n" +
                    "- Unclosed quotes or brackets\n" +
                    "- Invalid character encoding\n" +
                    "- Properly quoted property names";
                
                MessageBox.Show(errorMessage, "JSON Validation Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error validating data: {ex.Message}", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    
    // New method to show document type selection dialog
    private string ShowDocumentTypeSelectionDialog(JObject jsonObject)
    {
        // Try to detect document type from JSON
        string detectedType = DetectDocumentType(jsonObject);
        
        // Create a window to select the document type
        var selectWindow = new Window
        {
            Title = "Select Document Type",
            Width = 350,
            Height = 200,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this,
            ResizeMode = ResizeMode.NoResize
        };
        
        var mainPanel = new StackPanel { Margin = new Thickness(20) };
        
        var docTypeLabel = new TextBlock 
        { 
            Text = "Please select a document type to continue validation:", 
            Margin = new Thickness(0, 0, 0, 15),
            TextWrapping = TextWrapping.Wrap
        };
        
        var docTypeComboBox = new ComboBox
        {
            Margin = new Thickness(0, 0, 0, 20),
            MinWidth = 200
        };
        
        // Add document type options
        docTypeComboBox.Items.Add("Invoice");
        docTypeComboBox.Items.Add("Receipt");
        docTypeComboBox.Items.Add("ID Card");
        docTypeComboBox.Items.Add("Passport");
        docTypeComboBox.Items.Add("Driver's License");
        docTypeComboBox.Items.Add("Medical Report");
        docTypeComboBox.Items.Add("Tax Form");
        docTypeComboBox.Items.Add("Contract");
        docTypeComboBox.Items.Add("Other");
        
        // Select detected document type if available
        if (!string.IsNullOrEmpty(detectedType) && detectedType != "Other")
        {
            foreach (var item in docTypeComboBox.Items)
            {
                if (item.ToString().Equals(detectedType, StringComparison.OrdinalIgnoreCase))
                {
                    docTypeComboBox.SelectedItem = item;
                    break;
                }
            }
        }
        
        // If no match was found, default to the first item
        if (docTypeComboBox.SelectedIndex < 0 && docTypeComboBox.Items.Count > 0)
        {
            docTypeComboBox.SelectedIndex = 0;
        }
        
        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        
        var cancelButton = new Button
        {
            Content = "Cancel",
            Padding = new Thickness(15, 5, 15, 5),
            Margin = new Thickness(0, 0, 10, 0)
        };
        
        var continueButton = new Button
        {
            Content = "Continue",
            Padding = new Thickness(15, 5, 15, 5),
            IsDefault = true
        };
        
        buttonPanel.Children.Add(cancelButton);
        buttonPanel.Children.Add(continueButton);
        
        mainPanel.Children.Add(docTypeLabel);
        mainPanel.Children.Add(docTypeComboBox);
        mainPanel.Children.Add(buttonPanel);
        
        selectWindow.Content = mainPanel;
        
        string selectedType = null;
        
        cancelButton.Click += (s, e) => 
        {
            selectWindow.DialogResult = false;
        };
        
        continueButton.Click += (s, e) => 
        {
            if (docTypeComboBox.SelectedItem != null)
            {
                selectedType = docTypeComboBox.SelectedItem.ToString();
                selectWindow.DialogResult = true;
            }
            else
            {
                MessageBox.Show("Please select a document type.", "Selection Required", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        };
        
        bool? result = selectWindow.ShowDialog();
        
        return result == true ? selectedType : null;
    }

    // Helper method to clean JSON content to prevent common parsing issues
    private string CleanJsonContent(string content)
    {
        if (string.IsNullOrEmpty(content))
            return content;
            
        try
        {
            // Remove markdown code block markers if present (```json and ```)
            if (content.TrimStart().StartsWith("```json"))
            {
                int startIndex = content.IndexOf("```json") + 7;
                int endIndex = content.LastIndexOf("```");
                
                if (endIndex > startIndex)
                {
                    content = content.Substring(startIndex, endIndex - startIndex).Trim();
                }
                else
                {
                    // Just remove the starting marker if ending one isn't found
                    content = content.Replace("```json", "").Trim();
                }
            }
            
            // Also handle case where just ``` is used without language specifier
            if (content.TrimStart().StartsWith("```"))
            {
                int startIndex = content.IndexOf("```") + 3;
                int endIndex = content.LastIndexOf("```");
                
                if (endIndex > startIndex)
                {
                    content = content.Substring(startIndex, endIndex - startIndex).Trim();
                }
                else
                {
                    // Just remove the starting marker if ending one isn't found
                    content = content.Replace("```", "").Trim();
                }
            }
            
            // Find the actual JSON content by identifying the outermost braces or brackets
            int firstOpenBrace = content.IndexOf('{');
            int firstOpenBracket = content.IndexOf('[');
            
            // If both exist, take the one that comes first
            int jsonStart = -1;
            if (firstOpenBrace >= 0 && firstOpenBracket >= 0)
            {
                jsonStart = Math.Min(firstOpenBrace, firstOpenBracket);
            }
            else if (firstOpenBrace >= 0)
            {
                jsonStart = firstOpenBrace;
            }
            else if (firstOpenBracket >= 0)
            {
                jsonStart = firstOpenBracket;
            }
            
            if (jsonStart >= 0)
            {
                // Extract only the JSON part by finding the matching closing brace/bracket
                char openChar = content[jsonStart];
                char closeChar = (openChar == '{') ? '}' : ']';
                
                // Count of opening and closing characters to handle nested structures
                int depth = 0;
                int jsonEnd = -1;
                
                // Scan the string to find the matching closing character
                for (int i = jsonStart; i < content.Length; i++)
                {
                    if (content[i] == openChar) depth++;
                    else if (content[i] == closeChar) depth--;
                    
                    // When we've found the matching closing character
                    if (depth == 0 && i > jsonStart)
                    {
                        jsonEnd = i;
                        break;
                    }
                }
                
                // If we found a valid JSON structure, extract it
                if (jsonEnd > jsonStart)
                {
                    content = content.Substring(jsonStart, jsonEnd - jsonStart + 1).Trim();
                }
            }
            
            // Remove BOM (Byte Order Mark) if present
            if (content.StartsWith("\ufeff"))
                content = content.Substring(1);
                
            // Remove any zero-width non-breaking spaces
            content = content.Replace("\ufeff", "");
            
            // Handle curly quotes that might have been copied from other sources
            content = content.Replace(""", "\"").Replace(""", "\"");
            content = content.Replace("'", "'").Replace("'", "'");
            
            // Remove any leading/trailing whitespace including BOM and other invisible characters
            content = new string(content.Where(c => !char.IsControl(c) || c == '\n' || c == '\r' || c == '\t').ToArray());
            
            // Try to auto-correct common issues
            content = content.Replace(",\n}", "\n}");  // Remove trailing comma in objects
            content = content.Replace(",\n]", "\n]");  // Remove trailing comma in arrays
            
            // Validate whether the result is a valid JSON
            try
            {
                JsonConvert.DeserializeObject(content);
                return content;
            }
            catch (JsonException)
            {
                // If this is not valid JSON, we might need to remove additional text
                // Try to extract just the JSON part more aggressively
                int lastCloseBrace = content.LastIndexOf('}');
                int lastCloseBracket = content.LastIndexOf(']');
                
                // Take the last closing character
                int jsonEnd = Math.Max(lastCloseBrace, lastCloseBracket);
                
                if (jsonEnd > 0 && jsonEnd < content.Length - 1)
                {
                    // There's additional text after the JSON - remove it
                    content = content.Substring(0, jsonEnd + 1).Trim();
                    
                    // Try to validate again
                    try
                    {
                        JsonConvert.DeserializeObject(content);
                        return content;
                    }
                    catch
                    {
                        // Still not valid, try the next technique
                    }
                }
            }
            
            return content;
        }
        catch
        {
            // If any cleaning process fails, return the original content
            return content;
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

    private void BtnConvertToImage_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentFilePath) || !File.Exists(_currentFilePath) || !_isPdf)
        {
            MessageBox.Show("No PDF document is currently loaded.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        
        // Create a popup to select the image format
        var formatWindow = new Window
        {
            Title = "Select Image Format",
            Width = 300,
            Height = 250,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this,
            ResizeMode = ResizeMode.NoResize
        };
        
        var stackPanel = new StackPanel { Margin = new Thickness(20) };
        var formatLabel = new TextBlock { Text = "Choose the image format:", Margin = new Thickness(0, 0, 0, 10) };
        
        var formatComboBox = new ComboBox 
        { 
            Margin = new Thickness(0, 0, 0, 20),
            SelectedIndex = 0
        };
        formatComboBox.Items.Add("JPEG (.jpg)");
        formatComboBox.Items.Add("PNG (.png)");
        
        // Add DPI selection
        var dpiLabel = new TextBlock { Text = "Select DPI:", Margin = new Thickness(0, 0, 0, 10) };
        var dpiComboBox = new ComboBox
        {
            Margin = new Thickness(0, 0, 0, 20),
            SelectedIndex = 1
        };
        dpiComboBox.Items.Add("150 DPI");
        dpiComboBox.Items.Add("300 DPI");
        dpiComboBox.Items.Add("600 DPI");
        
        var buttonsPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var cancelButton = new Button { Content = "Cancel", Padding = new Thickness(10, 5, 10, 5), Margin = new Thickness(5, 0, 0, 0) };
        var okButton = new Button { Content = "OK", Padding = new Thickness(10, 5, 10, 5), IsDefault = true };
        
        cancelButton.Click += (s, args) => { formatWindow.DialogResult = false; };
        okButton.Click += (s, args) => { formatWindow.DialogResult = true; };
        
        buttonsPanel.Children.Add(okButton);
        buttonsPanel.Children.Add(cancelButton);
        
        stackPanel.Children.Add(formatLabel);
        stackPanel.Children.Add(formatComboBox);
        stackPanel.Children.Add(dpiLabel);
        stackPanel.Children.Add(dpiComboBox);
        stackPanel.Children.Add(buttonsPanel);
        
        formatWindow.Content = stackPanel;
        
        // Show the dialog and process the result
        bool? result = formatWindow.ShowDialog();
        
        if (result == true)
        {
            string extension;
            ImageFormat imageFormat;
            
            // Determine the selected format
            if (formatComboBox.SelectedIndex == 0) // JPEG
            {
                extension = ".jpg";
                imageFormat = ImageFormat.Jpeg;
            }
            else // PNG
            {
                extension = ".png";
                imageFormat = ImageFormat.Png;
            }
            
            // Determine selected DPI
            int dpi;
            switch (dpiComboBox.SelectedIndex)
            {
                case 0:
                    dpi = 150;
                    break;
                case 2:
                    dpi = 600;
                    break;
                default:
                    dpi = 300;
                    break;
            }
            
            // Get the processing folder
            string outputFolder = GetProcessingFolder(_currentFilePath);
            if (outputFolder == null)
            {
                MessageBox.Show("Could not create processing folder. Operation cancelled.", "Folder Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            
            string baseName = Path.GetFileNameWithoutExtension(_currentFilePath);
            
            try
            {
                // Show progress cursor
                Mouse.OverrideCursor = Cursors.Wait;
                
                // Create a progress window
                var progressWindow = new Window
                {
                    Title = "Converting PDF to Images",
                    Width = 400,
                    Height = 150,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = this,
                    ResizeMode = ResizeMode.NoResize
                };
                
                var progressPanel = new StackPanel { Margin = new Thickness(20) };
                var progressLabel = new TextBlock { Text = "Initializing...", Margin = new Thickness(0, 0, 0, 10) };
                var progressBar = new ProgressBar { Minimum = 0, Maximum = 100, Height = 20 };
                
                progressPanel.Children.Add(progressLabel);
                progressPanel.Children.Add(progressBar);
                
                progressWindow.Content = progressPanel;
                progressWindow.Show();
                
                try
                {
                    // Set initial progress
                    progressBar.Value = 0;
                    progressLabel.Text = "Opening PDF document...";
                    DoEvents();
                    
                    // Open the PDF document
                    using (PdfReader reader = new PdfReader(_currentFilePath))
                    {
                        // Get the number of pages
                        int pageCount = reader.NumberOfPages;
                        progressBar.Maximum = pageCount;
                        
                        // Convert each page to an image
                        for (int i = 1; i <= pageCount; i++)
                        {
                            string outputPath = Path.Combine(outputFolder, $"{baseName}_page{i}{extension}");
                            
                            // Update progress
                            progressBar.Value = i - 1;
                            progressLabel.Text = $"Converting page {i} of {pageCount}...";
                            DoEvents();
                            
                            try
                            {
                                using (var bitmap = GetPageImage(reader, i, dpi))
                                {
                                    bitmap.Save(outputPath, imageFormat);
                                }
                            }
                            catch (Exception pageEx)
                            {
                                // Log the error but continue with other pages
                                Console.WriteLine($"Error converting page {i}: {pageEx.Message}");
                                
                                // Create an error image for this page
                                using (var errorBitmap = new Bitmap(800, 600))
                                {
                                    using (var g = Graphics.FromImage(errorBitmap))
                                    {
                                        g.Clear(System.Drawing.Color.White);
                                        g.DrawString($"Error converting page {i}: {pageEx.Message}", 
                                            new System.Drawing.Font(System.Drawing.FontFamily.GenericSansSerif, 12, System.Drawing.FontStyle.Bold), 
                                            System.Drawing.Brushes.Red, new System.Drawing.PointF(20, 20));
                                    }
                                    errorBitmap.Save(outputPath, imageFormat);
                                }
                            }
                            
                            // Update progress
                            progressBar.Value = i;
                            DoEvents();
                        }
                        
                        progressLabel.Text = "Conversion complete!";
                        progressBar.Value = progressBar.Maximum;
                        DoEvents();
                        
                        // Wait a moment before closing the progress window
                        System.Threading.Thread.Sleep(1000);
                        progressWindow.Close();
                        
                        MessageBox.Show($"Successfully converted {pageCount} pages to {(formatComboBox.SelectedIndex == 0 ? "JPEG" : "PNG")} images.\n\nFiles saved to: {outputFolder}", 
                            "Conversion Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                        
                        // Ask if the user wants to open the output folder
                        if (MessageBox.Show("Would you like to open the output folder?", "Open Folder", 
                            MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                        {
                            Process.Start("explorer.exe", outputFolder);
                        }
                    }
                }
                catch (Exception ex)
                {
                    progressWindow.Close();
                    MessageBox.Show($"Error converting PDF to images: {ex.Message}", "Conversion Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error converting PDF to images: {ex.Message}", "Conversion Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private Bitmap GetPageImage(PdfReader reader, int pageNumber, int dpi)
    {
        try
        {
            // Get the page size
            var pageSize = reader.GetPageSize(pageNumber);
            float width = pageSize.Width;
            float height = pageSize.Height;
            
            // Calculate the output size based on DPI
            int pxWidth = (int)(width * dpi / 72f);
            int pxHeight = (int)(height * dpi / 72f);
            
            // Create a bitmap to render the PDF page
            var bitmap = new Bitmap(pxWidth, pxHeight);
            
            try
            {
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    // Set high quality rendering
                    graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                    graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                    
                    // Clear the background
                    graphics.Clear(System.Drawing.Color.White);
                    
                    // Try using iTextSharp directly for basic rendering
                    try
                    {
                        // Use PdfContentByte to draw the page content
                        using (var document = new Document(pageSize))
                        {
                            // Create a temporary PDF with just this page
                            string tempPdfPath = Path.Combine(Path.GetTempPath(), $"temp_page_{pageNumber}_{Guid.NewGuid()}.pdf");
                            
                            try
                            {
                                using (FileStream fs = new FileStream(tempPdfPath, FileMode.Create))
                                {
                                    using (PdfCopy copy = new PdfCopy(document, fs))
                                    {
                                        document.Open();
                                        copy.AddPage(copy.GetImportedPage(reader, pageNumber));
                                    }
                                }
                                
                                // Now try rendering with PdfiumViewer if available
                                try
                                {
                                    using (var pdfDocument = PdfiumViewer.PdfDocument.Load(tempPdfPath))
                                    {
                                        pdfDocument.Render(0, graphics, graphics.DpiX, graphics.DpiY, 
                                            new System.Drawing.Rectangle(0, 0, pxWidth, pxHeight), 
                                            PdfiumViewer.PdfRenderFlags.CorrectFromDpi);
                                    }
                                }
                                catch (Exception pdfiumEx)
                                {
                                    // PdfiumViewer failed, try alternate approach or fallback to text rendering
                                    graphics.DrawString($"PDF Rendering Error: {pdfiumEx.Message}", 
                                        new System.Drawing.Font(System.Drawing.FontFamily.GenericSansSerif, 10, System.Drawing.FontStyle.Regular), 
                                        System.Drawing.Brushes.Black, new System.Drawing.PointF(10, 10));
                                        
                                    // Draw page information
                                    graphics.DrawString($"Page {pageNumber} - Size: {width:0.##} x {height:0.##} pts", 
                                        new System.Drawing.Font(System.Drawing.FontFamily.GenericSansSerif, 10, System.Drawing.FontStyle.Regular), 
                                        System.Drawing.Brushes.Black, new System.Drawing.PointF(10, 30));
                                }
                                finally
                                {
                                    // Clean up the temporary file
                                    try { File.Delete(tempPdfPath); } catch { /* Ignore deletion errors */ }
                                }
                            }
                            catch (Exception ex)
                            {
                                graphics.DrawString($"Error extracting page: {ex.Message}", 
                                    new System.Drawing.Font(System.Drawing.FontFamily.GenericSansSerif, 10, System.Drawing.FontStyle.Regular), 
                                    System.Drawing.Brushes.Black, new System.Drawing.PointF(10, 10));
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        graphics.DrawString($"Error rendering page: {ex.Message}", 
                            new System.Drawing.Font(System.Drawing.FontFamily.GenericSansSerif, 12, System.Drawing.FontStyle.Bold), 
                            System.Drawing.Brushes.Red, new System.Drawing.PointF(20, 20));
                    }
                }
            }
            catch (Exception ex)
            {
                // If rendering fails, add error text to the bitmap
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.Clear(System.Drawing.Color.White);
                    graphics.DrawString($"Error: {ex.Message}", 
                        new System.Drawing.Font(System.Drawing.FontFamily.GenericSansSerif, 12, System.Drawing.FontStyle.Bold), 
                        System.Drawing.Brushes.Red, new System.Drawing.PointF(20, 20));
                }
            }
            
            return bitmap;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error creating image from PDF: {ex.Message}", "PDF Conversion Error", MessageBoxButton.OK, MessageBoxImage.Error);
            
            // Return a blank image with error message if conversion fails
            var errorBitmap = new Bitmap(800, 600);
            using (var graphics = Graphics.FromImage(errorBitmap))
            {
                graphics.Clear(System.Drawing.Color.White);
                graphics.DrawString($"Error converting PDF: {ex.Message}", 
                    new System.Drawing.Font(System.Drawing.FontFamily.GenericSansSerif, 12, System.Drawing.FontStyle.Bold), 
                    System.Drawing.Brushes.Red, new System.Drawing.PointF(20, 20));
            }
            return errorBitmap;
        }
    }

    // Helper method to allow UI updates during processing
    private void DoEvents()
    {
        DispatcherFrame frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background,
            new System.Windows.Threading.DispatcherOperationCallback(delegate(object f)
            {
                ((DispatcherFrame)f).Continue = false;
                return null;
            }), frame);
        System.Windows.Threading.Dispatcher.PushFrame(frame);
    }

    private void BtnSetPassword_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentFilePath) || !File.Exists(_currentFilePath) || !_isPdf)
        {
            MessageBox.Show("No PDF document is currently loaded.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        // Create a popup to input the password
        var passwordWindow = new Window
        {
            Title = "Set PDF Password",
            Width = 350,
            Height = 220,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this,
            ResizeMode = ResizeMode.NoResize
        };

        var stackPanel = new StackPanel { Margin = new Thickness(20) };
        
        var passwordLabel = new TextBlock { Text = "Enter password:", Margin = new Thickness(0, 0, 0, 5) };
        var passwordBox = new PasswordBox { Margin = new Thickness(0, 0, 0, 15) };
        
        var confirmLabel = new TextBlock { Text = "Confirm password:", Margin = new Thickness(0, 0, 0, 5) };
        var confirmBox = new PasswordBox { Margin = new Thickness(0, 0, 0, 20) };
        
        var buttonsPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var cancelButton = new Button { Content = "Cancel", Padding = new Thickness(10, 5, 10, 5), Margin = new Thickness(5, 0, 0, 0) };
        var okButton = new Button { Content = "OK", Padding = new Thickness(10, 5, 10, 5), IsDefault = true };

        cancelButton.Click += (s, args) => { passwordWindow.DialogResult = false; };
        okButton.Click += (s, args) => 
        {
            if (string.IsNullOrEmpty(passwordBox.Password))
            {
                MessageBox.Show("Password cannot be empty.", "Password Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            if (passwordBox.Password != confirmBox.Password)
            {
                MessageBox.Show("Passwords do not match.", "Password Mismatch", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            passwordWindow.DialogResult = true;
        };

        buttonsPanel.Children.Add(okButton);
        buttonsPanel.Children.Add(cancelButton);

        stackPanel.Children.Add(passwordLabel);
        stackPanel.Children.Add(passwordBox);
        stackPanel.Children.Add(confirmLabel);
        stackPanel.Children.Add(confirmBox);
        stackPanel.Children.Add(buttonsPanel);

        passwordWindow.Content = stackPanel;

        // Show the dialog and process the result
        bool? result = passwordWindow.ShowDialog();

        if (result == true)
        {
            // Get the processing folder and output file path
            string processingFolder = GetProcessingFolder(_currentFilePath);
            if (processingFolder == null)
            {
                MessageBox.Show("Could not create processing folder. Operation cancelled.", "Folder Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            
            string outputFileName = "protected_" + Path.GetFileName(_currentFilePath);
            string outputPath = Path.Combine(processingFolder, outputFileName);

            try
            {
                // Show progress cursor
                Mouse.OverrideCursor = Cursors.Wait;
                
                // Copy the original file to a temporary location
                string tempFile = Path.GetTempFileName();
                File.Copy(_currentFilePath, tempFile, true);
                
                // Read the PDF
                using (PdfReader reader = new PdfReader(tempFile))
                {
                    // Encrypt the PDF with the provided password
                    using (FileStream fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
                    {
                        PdfEncryptor.Encrypt(reader, fs, true, passwordBox.Password, passwordBox.Password, PdfWriter.ALLOW_PRINTING);
                    }
                }
                
                // Delete the temporary file
                File.Delete(tempFile);
                
                MessageBox.Show($"Password protection added successfully!\n\nFile saved to: {outputPath}", "Protection Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                
                // Ask if user wants to open the protected PDF
                var openResult = MessageBox.Show("Do you want to open the protected PDF?", "Open File", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (openResult == MessageBoxResult.Yes)
                {
                    LoadDocument(outputPath);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error protecting PDF: {ex.Message}", "Protection Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // Restore cursor
                Mouse.OverrideCursor = null;
            }
        }
    }

    private void BtnRemovePassword_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentFilePath) || !File.Exists(_currentFilePath) || !_isPdf)
        {
            MessageBox.Show("No PDF document is currently loaded.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        
        // Create a popup to input the current password
        var passwordWindow = new Window
        {
            Title = "Remove PDF Password",
            Width = 350,
            Height = 170,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this,
            ResizeMode = ResizeMode.NoResize
        };

        var stackPanel = new StackPanel { Margin = new Thickness(20) };
        
        var passwordLabel = new TextBlock { Text = "Enter current password:", Margin = new Thickness(0, 0, 0, 5) };
        var passwordBox = new PasswordBox { Margin = new Thickness(0, 0, 0, 20) };
        
        var buttonsPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var cancelButton = new Button { Content = "Cancel", Padding = new Thickness(10, 5, 10, 5), Margin = new Thickness(5, 0, 0, 0) };
        var okButton = new Button { Content = "OK", Padding = new Thickness(10, 5, 10, 5), IsDefault = true };

        cancelButton.Click += (s, args) => { passwordWindow.DialogResult = false; };
        okButton.Click += (s, args) => 
        {
            if (string.IsNullOrEmpty(passwordBox.Password))
            {
                MessageBox.Show("Password cannot be empty.", "Password Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            passwordWindow.DialogResult = true;
        };

        buttonsPanel.Children.Add(okButton);
        buttonsPanel.Children.Add(cancelButton);

        stackPanel.Children.Add(passwordLabel);
        stackPanel.Children.Add(passwordBox);
        stackPanel.Children.Add(buttonsPanel);

        passwordWindow.Content = stackPanel;

        // Show the dialog and process the result
        bool? result = passwordWindow.ShowDialog();

        if (result == true)
        {
            // Get the processing folder and output file path
            string processingFolder = GetProcessingFolder(_currentFilePath);
            if (processingFolder == null)
            {
                MessageBox.Show("Could not create processing folder. Operation cancelled.", "Folder Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            
            string outputFileName = "unprotected_" + Path.GetFileName(_currentFilePath);
            string outputPath = Path.Combine(processingFolder, outputFileName);

            try
            {
                // Show progress cursor
                Mouse.OverrideCursor = Cursors.Wait;
                
                // Try to open the PDF with the provided password
                PdfReader reader;
                try
                {
                    reader = new PdfReader(_currentFilePath, System.Text.Encoding.ASCII.GetBytes(passwordBox.Password));
                }
                catch (Exception)
                {
                    MessageBox.Show("Incorrect password. Could not open the PDF.", "Password Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                using (reader)
                {
                    // Check if the PDF is actually encrypted
                    if (!reader.IsEncrypted())
                    {
                        MessageBox.Show("This PDF file is not password protected.", "Not Protected", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }
                    
                    // Create a new unencrypted PDF
                    using (FileStream fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
                    {
                        using (Document document = new Document())
                        {
                            PdfCopy copy = new PdfCopy(document, fs);
                            document.Open();
                            
                            int pageCount = reader.NumberOfPages;
                            for (int i = 1; i <= pageCount; i++)
                            {
                                copy.AddPage(copy.GetImportedPage(reader, i));
                            }
                            
                            document.Close();
                        }
                    }
                }
                
                MessageBox.Show($"Password protection removed successfully!\n\nFile saved to: {outputPath}", "Protection Removed", MessageBoxButton.OK, MessageBoxImage.Information);
                
                // Ask if user wants to open the unprotected PDF
                var openResult = MessageBox.Show("Do you want to open the unprotected PDF?", "Open File", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (openResult == MessageBoxResult.Yes)
                {
                    LoadDocument(outputPath);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error removing password protection: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // Restore cursor
                Mouse.OverrideCursor = null;
            }
        }
    }

    private void BtnSignPdf_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (string.IsNullOrEmpty(_currentFilePath) || !File.Exists(_currentFilePath) || !_isPdf)
            {
                MessageBox.Show("No PDF document is currently loaded.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            
            // Create a popup to get the signature information
            var signatureWindow = new Window
            {
                Title = "Sign PDF Document",
                Width = 400,
                Height = 350,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize
            };

            var stackPanel = new StackPanel { Margin = new Thickness(20) };
            
            var nameLabel = new TextBlock { Text = "Signatory Name:", Margin = new Thickness(0, 0, 0, 5) };
            var nameBox = new TextBox { Margin = new Thickness(0, 0, 0, 15) };
            
            var reasonLabel = new TextBlock { Text = "Reason for Signing:", Margin = new Thickness(0, 0, 0, 5) };
            var reasonBox = new TextBox { Margin = new Thickness(0, 0, 0, 15) };
            
            var locationLabel = new TextBlock { Text = "Location:", Margin = new Thickness(0, 0, 0, 5) };
            var locationBox = new TextBox { Margin = new Thickness(0, 0, 0, 15) };
            
            var signatureImageLabel = new TextBlock { Text = "Signature Image (optional):", Margin = new Thickness(0, 0, 0, 5) };
            var signatureImagePanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 20) };
            var signatureImagePath = new TextBox { Width = 250, IsReadOnly = true };
            var browseButton = new Button { Content = "Browse", Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(5, 0, 0, 0) };
            
            signatureImagePanel.Children.Add(signatureImagePath);
            signatureImagePanel.Children.Add(browseButton);
            
            browseButton.Click += (s, args) =>
            {
                try
                {
                    OpenFileDialog openFileDialog = new OpenFileDialog
                    {
                        Filter = "Image Files (*.jpg; *.jpeg; *.png; *.bmp)|*.jpg;*.jpeg;*.png;*.bmp",
                        Title = "Select Signature Image"
                    };

                    if (openFileDialog.ShowDialog() == true)
                    {
                        signatureImagePath.Text = openFileDialog.FileName;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error selecting image: {ex.Message}", "File Selection Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
            
            var buttonsPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var cancelButton = new Button { Content = "Cancel", Padding = new Thickness(10, 5, 10, 5), Margin = new Thickness(5, 0, 0, 0) };
            var okButton = new Button { Content = "Sign PDF", Padding = new Thickness(10, 5, 10, 5), IsDefault = true };

            cancelButton.Click += (s, args) => { signatureWindow.DialogResult = false; };
            okButton.Click += (s, args) => 
            {
                if (string.IsNullOrEmpty(nameBox.Text))
                {
                    MessageBox.Show("Signatory name is required.", "Name Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                signatureWindow.DialogResult = true;
            };

            buttonsPanel.Children.Add(okButton);
            buttonsPanel.Children.Add(cancelButton);

            stackPanel.Children.Add(nameLabel);
            stackPanel.Children.Add(nameBox);
            stackPanel.Children.Add(reasonLabel);
            stackPanel.Children.Add(reasonBox);
            stackPanel.Children.Add(locationLabel);
            stackPanel.Children.Add(locationBox);
            stackPanel.Children.Add(signatureImageLabel);
            stackPanel.Children.Add(signatureImagePanel);
            stackPanel.Children.Add(buttonsPanel);

            signatureWindow.Content = stackPanel;

            // Show the dialog and process the result
            bool? result = signatureWindow.ShowDialog();

            if (result == true)
            {
                // Get the processing folder
                string processingFolder = GetProcessingFolder(_currentFilePath);
                if (processingFolder == null)
                {
                    MessageBox.Show("Could not create processing folder. Operation cancelled.", "Folder Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                string outputFileName = "signed_" + Path.GetFileName(_currentFilePath);
                string outputPath = Path.Combine(processingFolder, outputFileName);
                
                try
                {
                    // Show progress cursor
                    Mouse.OverrideCursor = Cursors.Wait;
                    
                    // Get signature information
                    string name = nameBox.Text;
                    string reason = reasonBox.Text;
                    string location = locationBox.Text;
                    string imagePath = signatureImagePath.Text;
                    
                    // Copy original file first
                    File.Copy(_currentFilePath, outputPath, true);
                    
                    // Use a simpler approach to avoid crashes with iTextSharp
                    try
                    {
                        // Create the signature appearance
                        using (PdfReader reader = new PdfReader(_currentFilePath))
                        {
                            using (FileStream fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
                            {
                                try
                                {
                                    using (PdfStamper stamper = new PdfStamper(reader, fs))
                                    {
                                        try
                                        {
                                            // Get the first page
                                            PdfContentByte content = stamper.GetOverContent(1);
                                            
                                            // Create a signature text
                                            string signatureText = $"Signed by: {name}\n";
                                            if (!string.IsNullOrEmpty(reason))
                                                signatureText += $"Reason: {reason}\n";
                                            if (!string.IsNullOrEmpty(location))
                                                signatureText += $"Location: {location}\n";
                                            signatureText += $"Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
                                            
                                            // Add text directly to PDF
                                            iTextSharp.text.Rectangle pageSize = reader.GetPageSize(1);
                                            float x = pageSize.Right - 200;
                                            float y = pageSize.Bottom + 100;
                                            
                                            // Create a rectangle for the signature box
                                            content.Rectangle(x, y, x + 180, y + 80);
                                            content.Stroke();
                                            
                                            // Add signature text
                                            iTextSharp.text.Font font = new iTextSharp.text.Font(iTextSharp.text.Font.FontFamily.HELVETICA, 10);
                                            
                                            // Create a ColumnText for text layout
                                            iTextSharp.text.pdf.ColumnText ct = new iTextSharp.text.pdf.ColumnText(content);
                                            ct.SetSimpleColumn(x + 5, y + 5, x + 175, y + 75);
                                            ct.AddText(new iTextSharp.text.Paragraph(signatureText, font));
                                            ct.Go();
                                            
                                            // Add image if provided
                                            if (!string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
                                            {
                                                try
                                                {
                                                    iTextSharp.text.Image image = iTextSharp.text.Image.GetInstance(imagePath);
                                                    // Scale the image if needed
                                                    float maxWidth = 80;
                                                    float maxHeight = 40;
                                                    
                                                    if (image.Width > maxWidth || image.Height > maxHeight)
                                                    {
                                                        float scale = Math.Min(maxWidth / image.Width, maxHeight / image.Height);
                                                        image.ScalePercent(scale * 100);
                                                    }
                                                    
                                                    image.SetAbsolutePosition(x + 5, y + 40);
                                                    content.AddImage(image);
                                                }
                                                catch (Exception ex)
                                                {
                                                    MessageBox.Show($"Error adding signature image: {ex.Message}", "Image Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                                                }
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            MessageBox.Show($"Error formatting PDF: {ex.Message}", "Format Error", MessageBoxButton.OK, MessageBoxImage.Error);
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    MessageBox.Show($"Error creating PDF stamper: {ex.Message}", "Stamper Error", MessageBoxButton.OK, MessageBoxImage.Error);
                                }
                            }
                        }
                        
                        MessageBox.Show("PDF signed successfully!", "Signing Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                        
                        // Ask if user wants to open the signed PDF
                        var openResult = MessageBox.Show("Do you want to open the signed PDF?", "Open File", MessageBoxButton.YesNo, MessageBoxImage.Question);
                        if (openResult == MessageBoxResult.Yes)
                        {
                            LoadDocument(outputPath);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error signing PDF: {ex.Message}\n\nStack Trace: {ex.StackTrace}", "Signing Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error processing file: {ex.Message}", "Processing Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    // Restore cursor
                    Mouse.OverrideCursor = null;
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"An error occurred during the signing process: {ex.Message}\n\nStack Trace: {ex.StackTrace}", 
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            
            // Reset cursor in case of error
            Mouse.OverrideCursor = null;
        }
    }

    private void BtnConvertToPdf_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (string.IsNullOrEmpty(_currentFilePath) || !File.Exists(_currentFilePath) || _isPdf)
            {
                MessageBox.Show("No image is currently loaded.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Get the processing folder
            string processingFolder = GetProcessingFolder(_currentFilePath);
            if (processingFolder == null)
            {
                MessageBox.Show("Could not create processing folder. Operation cancelled.", "Folder Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            
            string outputFileName = Path.GetFileNameWithoutExtension(_currentFilePath) + ".pdf";
            string outputPath = Path.Combine(processingFolder, outputFileName);
            
            // Show wait cursor
            Mouse.OverrideCursor = Cursors.Wait;
            
            try
            {
                using (var document = new Document())
                {
                    // Create a FileStream to write the PDF
                    using (var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
                    {
                        // Create a PdfWriter to write to the document
                        var writer = PdfWriter.GetInstance(document, fs);
                        document.Open();

                        // Create an iTextSharp image from the loaded image
                        using (var image = System.Drawing.Image.FromFile(_currentFilePath))
                        {
                            // Create iTextSharp image
                            var pdfImage = iTextSharp.text.Image.GetInstance(image, ImageFormat.Jpeg);
                            
                            // Scale image to fit the page
                            pdfImage.ScaleToFit(document.PageSize.Width - document.LeftMargin - document.RightMargin,
                                              document.PageSize.Height - document.TopMargin - document.BottomMargin);
                            
                            // Add the image to the document
                            document.Add(pdfImage);
                        }
                        
                        document.Close();
                    }
                }
                
                MessageBox.Show($"Image successfully converted to PDF.\n\nFile saved to: {outputPath}", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                
                // Ask if user wants to open the PDF
                var result = MessageBox.Show("Do you want to open the converted PDF?", "Open File", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    LoadDocument(outputPath);
                }
            }
            finally
            {
                // Reset cursor
                Mouse.OverrideCursor = null;
            }
        }
        catch (Exception ex)
        {
            Mouse.OverrideCursor = null;
            MessageBox.Show($"Error converting image to PDF: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnIncreaseDpi_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (string.IsNullOrEmpty(_currentFilePath) || !File.Exists(_currentFilePath) || _isPdf)
            {
                MessageBox.Show("No image is currently loaded.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Create an input dialog to get the target DPI
            var dpiDialog = new InputDialog("Enter target DPI (96-600):", "Increase DPI", "300");
            if (dpiDialog.ShowDialog() == true)
            {
                if (!int.TryParse(dpiDialog.Answer, out int targetDpi) || targetDpi < 96 || targetDpi > 600)
                {
                    MessageBox.Show("Please enter a valid DPI value between 96 and 600.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Get the processing folder
                string processingFolder = GetProcessingFolder(_currentFilePath);
                if (processingFolder == null)
                {
                    MessageBox.Show("Could not create processing folder. Operation cancelled.", "Folder Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                string outputFileName = Path.GetFileNameWithoutExtension(_currentFilePath) + "_" + targetDpi + "dpi" + Path.GetExtension(_currentFilePath);
                string outputPath = Path.Combine(processingFolder, outputFileName);
                
                // Show wait cursor
                Mouse.OverrideCursor = Cursors.Wait;
                
                try
                {
                    // Load the original image
                    using (var originalImage = System.Drawing.Image.FromFile(_currentFilePath))
                    {
                        // Get original DPI
                        float originalDpiX = originalImage.HorizontalResolution;
                        float originalDpiY = originalImage.VerticalResolution;
                        
                        // Calculate size ratio
                        float ratioX = targetDpi / originalDpiX;
                        float ratioY = targetDpi / originalDpiY;
                        
                        // Calculate new dimensions
                        int newWidth = (int)(originalImage.Width * ratioX);
                        int newHeight = (int)(originalImage.Height * ratioY);
                        
                        // Create a new bitmap with the new dimensions
                        using (var newImage = new Bitmap(newWidth, newHeight))
                        {
                            // Set the resolution
                            newImage.SetResolution(targetDpi, targetDpi);
                            
                            // Draw the original image onto the new bitmap, scaling it
                            using (var g = Graphics.FromImage(newImage))
                            {
                                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                                g.DrawImage(originalImage, 0, 0, newWidth, newHeight);
                            }
                            
                            // Save the new image
                            ImageFormat format = Path.GetExtension(outputPath).ToLower() == ".png" 
                                ? ImageFormat.Png 
                                : ImageFormat.Jpeg;
                            
                            newImage.Save(outputPath, format);
                        }
                    }
                    
                    MessageBox.Show($"Image DPI increased to {targetDpi}.\n\nFile saved to: {outputPath}", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    
                    // Ask if user wants to open the new image
                    var result = MessageBox.Show("Do you want to open the new image?", "Open File", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (result == MessageBoxResult.Yes)
                    {
                        LoadDocument(outputPath);
                    }
                }
                finally
                {
                    // Reset cursor
                    Mouse.OverrideCursor = null;
                }
            }
        }
        catch (Exception ex)
        {
            Mouse.OverrideCursor = null;
            MessageBox.Show($"Error increasing image DPI: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnCropImage_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (string.IsNullOrEmpty(_currentFilePath) || !File.Exists(_currentFilePath) || _isPdf)
            {
                MessageBox.Show("No image is currently loaded.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Create a new window to perform the cropping
            var cropWindow = new Window
            {
                Title = "Crop Image",
                Width = 800,
                Height = 600,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this
            };

            // Create the layout
            var mainGrid = new Grid();
            
            // Image container (top part)
            var imageControl = new System.Windows.Controls.Image
            {
                Source = new BitmapImage(new Uri(_currentFilePath)),
                Stretch = System.Windows.Media.Stretch.Uniform,
                Margin = new Thickness(10)
            };
            
            // Canvas for drawing the selection rectangle
            var canvas = new Canvas();
            
            // Grid for overlaying the canvas on top of the image
            var imageGrid = new Grid();
            imageGrid.Children.Add(imageControl);
            imageGrid.Children.Add(canvas);
            
            // Button panel (bottom part)
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(10),
                Height = 40
            };
            
            var cropButton = new Button
            {
                Content = "Crop",
                Padding = new Thickness(20, 5, 20, 5),
                Margin = new Thickness(5)
            };
            
            var cancelButton = new Button
            {
                Content = "Cancel",
                Padding = new Thickness(20, 5, 20, 5),
                Margin = new Thickness(5)
            };
            
            buttonPanel.Children.Add(cropButton);
            buttonPanel.Children.Add(cancelButton);
            
            // Add the components to the main grid
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            
            Grid.SetRow(imageGrid, 0);
            Grid.SetRow(buttonPanel, 1);
            
            mainGrid.Children.Add(imageGrid);
            mainGrid.Children.Add(buttonPanel);
            
            cropWindow.Content = mainGrid;
            
            // Rectangle for selection
            System.Windows.Shapes.Rectangle selectionRect = null;
            System.Windows.Point startPoint = new System.Windows.Point();
            
            // Flags to track mouse state
            bool isSelecting = false;
            bool hasSelection = false;
            
            // Handler for mouse down (start selection)
            canvas.MouseLeftButtonDown += (s, args) =>
            {
                // Clear any existing selection
                if (selectionRect != null)
                {
                    canvas.Children.Remove(selectionRect);
                    hasSelection = false;
                }
                
                startPoint = args.GetPosition(canvas);
                
                selectionRect = new System.Windows.Shapes.Rectangle
                {
                    Stroke = System.Windows.Media.Brushes.Blue,
                    StrokeThickness = 2,
                    Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(50, 0, 0, 255))
                };
                
                Canvas.SetLeft(selectionRect, startPoint.X);
                Canvas.SetTop(selectionRect, startPoint.Y);
                
                canvas.Children.Add(selectionRect);
                
                isSelecting = true;
                canvas.CaptureMouse();
            };
            
            // Handler for mouse move (update selection)
            canvas.MouseMove += (s, args) =>
            {
                if (!isSelecting) return;
                
                var currentPoint = args.GetPosition(canvas);
                
                double left = Math.Min(startPoint.X, currentPoint.X);
                double top = Math.Min(startPoint.Y, currentPoint.Y);
                double width = Math.Abs(currentPoint.X - startPoint.X);
                double height = Math.Abs(currentPoint.Y - startPoint.Y);
                
                Canvas.SetLeft(selectionRect, left);
                Canvas.SetTop(selectionRect, top);
                selectionRect.Width = width;
                selectionRect.Height = height;
            };
            
            // Handler for mouse up (end selection)
            canvas.MouseLeftButtonUp += (s, args) =>
            {
                if (isSelecting)
                {
                    isSelecting = false;
                    canvas.ReleaseMouseCapture();
                    
                    if (selectionRect.Width > 5 && selectionRect.Height > 5)
                    {
                        hasSelection = true;
                    }
                    else
                    {
                        canvas.Children.Remove(selectionRect);
                        selectionRect = null;
                    }
                }
            };
            
            // Handler for the crop button
            cropButton.Click += (s, args) =>
            {
                if (!hasSelection || selectionRect == null)
                {
                    MessageBox.Show("Please select an area to crop.", "Selection Required", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                
                try
                {
                    // Get the processing folder
                    string processingFolder = GetProcessingFolder(_currentFilePath);
                    if (processingFolder == null)
                    {
                        MessageBox.Show("Could not create processing folder. Operation cancelled.", "Folder Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    
                    string outputFileName = "cropped_" + Path.GetFileName(_currentFilePath);
                    string outputPath = Path.Combine(processingFolder, outputFileName);
                    
                    // Show wait cursor
                    Mouse.OverrideCursor = Cursors.Wait;
                    
                    // Calculate the crop rectangle in terms of the original image
                    BitmapSource originalBitmap = (BitmapSource)imageControl.Source;
                    
                    double scaleX = originalBitmap.PixelWidth / imageControl.ActualWidth;
                    double scaleY = originalBitmap.PixelHeight / imageControl.ActualHeight;
                    
                    double cropLeft = Canvas.GetLeft(selectionRect) * scaleX;
                    double cropTop = Canvas.GetTop(selectionRect) * scaleY;
                    double cropWidth = selectionRect.Width * scaleX;
                    double cropHeight = selectionRect.Height * scaleY;
                    
                    // Ensure crop rectangle is within the image bounds
                    cropLeft = Math.Max(0, cropLeft);
                    cropTop = Math.Max(0, cropTop);
                    cropWidth = Math.Min(originalBitmap.PixelWidth - cropLeft, cropWidth);
                    cropHeight = Math.Min(originalBitmap.PixelHeight - cropTop, cropHeight);
                    
                    // Create a temporary file for the image data
                    string tempFile = Path.GetTempFileName();
                    try
                    {
                        // Save current image to temp file to ensure proper initialization
                        using (FileStream fs = new FileStream(tempFile, FileMode.Create))
                        {
                            BitmapEncoder encoder = new PngBitmapEncoder();
                            encoder.Frames.Add(BitmapFrame.Create(originalBitmap));
                            encoder.Save(fs);
                        }
                        
                        // Load the image and perform the crop using System.Drawing
                        using (var bitmap = new System.Drawing.Bitmap(tempFile))
                        {
                            // Create high-quality bitmap for the cropped result
                            using (var cropped = new System.Drawing.Bitmap((int)cropWidth, (int)cropHeight, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                            {
                                using (var g = System.Drawing.Graphics.FromImage(cropped))
                                {
                                    // Configure for high quality
                                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                                    g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                                    g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                                    
                                    // Draw the cropped portion
                                    g.DrawImage(bitmap, 
                                        new System.Drawing.Rectangle(0, 0, (int)cropWidth, (int)cropHeight),
                                        new System.Drawing.Rectangle((int)cropLeft, (int)cropTop, (int)cropWidth, (int)cropHeight),
                                        System.Drawing.GraphicsUnit.Pixel);
                                }
                                
                                // Save the cropped image
                                cropped.Save(outputPath, GetImageFormat(Path.GetExtension(_currentFilePath)));
                            }
                        }
                        
                        MessageBox.Show($"Image cropped successfully!\n\nFile saved to: {outputPath}", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                        
                        // Ask if user wants to open the cropped image
                        var result = MessageBox.Show("Do you want to open the cropped image?", "Open File", MessageBoxButton.YesNo, MessageBoxImage.Question);
                        if (result == MessageBoxResult.Yes)
                        {
                            LoadDocument(outputPath);
                        }
                    }
                    finally
                    {
                        // Clean up temp file on a background thread to avoid delays
                        if (File.Exists(tempFile))
                        {
                            Task.Run(() => 
                            {
                                try { File.Delete(tempFile); } 
                                catch { /* Ignore cleanup errors */ }
                            });
                        }
                        
                        // Reset cursor
                        Mouse.OverrideCursor = null;
                    }
                    
                    cropWindow.Close();
                }
                catch (Exception ex)
                {
                    Mouse.OverrideCursor = null;
                    MessageBox.Show($"Error cropping image: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
            
            // Handler for the cancel button
            cancelButton.Click += (s, args) => cropWindow.Close();
            
            // Show the window
            cropWindow.ShowDialog();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error opening crop window: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    
    private System.Drawing.Imaging.ImageFormat GetImageFormat(string extension)
    {
        switch (extension.ToLower())
        {
            case ".jpg":
            case ".jpeg":
                return System.Drawing.Imaging.ImageFormat.Jpeg;
            case ".png":
                return System.Drawing.Imaging.ImageFormat.Png;
            case ".bmp":
                return System.Drawing.Imaging.ImageFormat.Bmp;
            case ".gif":
                return System.Drawing.Imaging.ImageFormat.Gif;
            case ".tif":
            case ".tiff":
                return System.Drawing.Imaging.ImageFormat.Tiff;
            default:
                return System.Drawing.Imaging.ImageFormat.Png; // Default to PNG
        }
    }

    private void CheckPdfiumDependencies()
    {
        // Run the check on a background thread to avoid blocking the UI
        Task.Run(() => 
        {
            try
            {
                bool dependenciesAvailable = PdfiumDependencyChecker.AreDependenciesAvailable();
                
                // If dependencies are missing, show a notification in the UI thread
                if (!dependenciesAvailable)
                {
                    Dispatcher.Invoke(() => 
                    {
                        // Show a non-blocking notification to the user
                        MessageBox.Show(
                            "PDF to image conversion requires PdfiumViewer native dependencies, which appear to be missing.\n\n" +
                            "To install the required dependencies, please click on the 'Install PDF Dependencies' menu option in the top-right corner.",
                            "Missing Dependencies",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    });
                }
            }
            catch (Exception)
            {
                // Ignore any errors in the dependency check
            }
        });
    }

    // Helper method to get color depth from a PixelFormat
    private int GetPixelFormatDepth(System.Drawing.Imaging.PixelFormat format)
    {
        switch (format)
        {
            case System.Drawing.Imaging.PixelFormat.Format1bppIndexed:
                return 1;
            case System.Drawing.Imaging.PixelFormat.Format4bppIndexed:
                return 4;
            case System.Drawing.Imaging.PixelFormat.Format8bppIndexed:
                return 8;
            case System.Drawing.Imaging.PixelFormat.Format16bppArgb1555:
            case System.Drawing.Imaging.PixelFormat.Format16bppGrayScale:
            case System.Drawing.Imaging.PixelFormat.Format16bppRgb555:
            case System.Drawing.Imaging.PixelFormat.Format16bppRgb565:
                return 16;
            case System.Drawing.Imaging.PixelFormat.Format24bppRgb:
                return 24;
            case System.Drawing.Imaging.PixelFormat.Format32bppArgb:
            case System.Drawing.Imaging.PixelFormat.Format32bppPArgb:
            case System.Drawing.Imaging.PixelFormat.Format32bppRgb:
                return 32;
            case System.Drawing.Imaging.PixelFormat.Format48bppRgb:
                return 48;
            case System.Drawing.Imaging.PixelFormat.Format64bppArgb:
            case System.Drawing.Imaging.PixelFormat.Format64bppPArgb:
                return 64;
            default:
                return 0;
        }
    }

    // Helper method to get or create the processing folder for a file
    private string GetProcessingFolder(string filePath)
    {
        try
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return null;
                
            string directory = Path.GetDirectoryName(filePath);
            string filename = Path.GetFileNameWithoutExtension(filePath);
            string processingFolder = Path.Combine(directory, $"{filename}_processing");
            
            if (!Directory.Exists(processingFolder))
            {
                Directory.CreateDirectory(processingFolder);
            }
            
            return processingFolder;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error creating processing folder: {ex.Message}", "Folder Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return null;
        }
    }

    // Data Validation Panel Methods
    
    // Method to generate validation panel fields based on JSON data
    private void ShowDataValidationPanel(JObject jsonObject, string documentType)
    {
        try
        {
            // Clear existing fields
            ValidationFieldsPanel.Children.Clear();
            
            // Show the Data Validation Panel
            var grid = (Grid)this.Content;
            if (grid.ColumnDefinitions.Count > 6)
            {
                grid.ColumnDefinitions[6].Width = new GridLength(1, GridUnitType.Star);
            }
            DataValidationPanel.Visibility = Visibility.Visible;
            
            // Hide the BtnShowValidation button
            if (BtnShowValidation != null)
            {
                BtnShowValidation.Visibility = Visibility.Collapsed;
            }
            
            // Create document type selector at the top
            var docTypePanel = new StackPanel { Margin = new Thickness(0, 0, 0, 15) };
            
            var docTypeLabel = new TextBlock
            {
                Text = "Document Type:",
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 5, 0, 5)
            };
            
            var docTypeComboBox = new ComboBox
            {
                Margin = new Thickness(0, 0, 0, 10),
                MinWidth = 200
            };
            
            // Add document type options
            docTypeComboBox.Items.Add("Invoice");
            docTypeComboBox.Items.Add("Receipt");
            docTypeComboBox.Items.Add("ID Card");
            docTypeComboBox.Items.Add("Passport");
            docTypeComboBox.Items.Add("Driver's License");
            docTypeComboBox.Items.Add("Medical Report");
            docTypeComboBox.Items.Add("Tax Form");
            docTypeComboBox.Items.Add("Contract");
            docTypeComboBox.Items.Add("Other");
            
            // Select detected document type or default to Auto Detect
            docTypeComboBox.SelectedIndex = 0;
            foreach (var item in docTypeComboBox.Items)
            {
                if (item.ToString().Equals(documentType, StringComparison.OrdinalIgnoreCase))
                {
                    docTypeComboBox.SelectedItem = item;
                    break;
                }
            }
            
            // Handle document type change
            docTypeComboBox.SelectionChanged += (sender, e) =>
            {
                if (docTypeComboBox.SelectedItem != null && docTypeComboBox.SelectedItem.ToString() != "Auto Detect")
                {
                    // Reload validation panel with selected document type
                    ReloadValidationPanelForDocumentType(jsonObject, docTypeComboBox.SelectedItem.ToString());
                }
            };
            
            docTypePanel.Children.Add(docTypeLabel);
            docTypePanel.Children.Add(docTypeComboBox);
            
            // Add a separator
            var separator = new Border
            {
                BorderBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#CCCCCC")),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Margin = new Thickness(0, 0, 0, 15)
            };
            docTypePanel.Children.Add(separator);
            
            ValidationFieldsPanel.Children.Add(docTypePanel);
            
            // Create section for essential fields
            var essentialsPanel = new GroupBox
            {
                Header = "Essential Fields",
                Padding = new Thickness(10),
                Margin = new Thickness(0, 0, 0, 15),
                Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F0F8FF"))
            };
            
            var essentialsStack = new StackPanel();
            essentialsPanel.Content = essentialsStack;
            
            // Populate essential fields based on document type
            PopulateEssentialFieldsForDocumentType(essentialsStack, jsonObject, documentType);
            
            // Add to main panel
            ValidationFieldsPanel.Children.Add(essentialsPanel);
            
            // Create section for document-specific fields
            var specificFieldsPanel = new GroupBox
            {
                Header = $"{documentType} Fields",
                Padding = new Thickness(10),
                Margin = new Thickness(0, 0, 0, 15)
            };
            
            var specificFieldsStack = new StackPanel();
            specificFieldsPanel.Content = specificFieldsStack;
            
            // Populate specific fields based on document type
            PopulateSpecificFieldsForDocumentType(specificFieldsStack, jsonObject, documentType);
            
            // Add to main panel
            ValidationFieldsPanel.Children.Add(specificFieldsPanel);
            
            // Create section for all remaining JSON data
            var allDataPanel = new GroupBox
            {
                Header = "All Fields",
                Padding = new Thickness(10),
                Margin = new Thickness(0, 0, 0, 15)
            };
            
            var allDataStack = new StackPanel();
            allDataPanel.Content = allDataStack;
            
            // Create fields recursively for all the JSON data
            CreateValidationFields(jsonObject, allDataStack, "", 0);
            
            // Add to main panel
            ValidationFieldsPanel.Children.Add(allDataPanel);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error creating validation panel: {ex.Message}", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    
    // Method to detect document type from JSON content
    private string DetectDocumentType(JObject jsonObject)
    {
        // Try to get document type directly from JSON
        string documentType = GetValueOrDefault(jsonObject, "documentType", "type", "document_type");
        
        if (!string.IsNullOrEmpty(documentType))
        {
            // Clean up the document type value
            documentType = documentType.Trim();
            
            // Map common variations to standard types
            if (documentType.Contains("invoice", StringComparison.OrdinalIgnoreCase))
                return "Invoice";
                
            if (documentType.Contains("receipt", StringComparison.OrdinalIgnoreCase))
                return "Receipt";
                
            if (documentType.Contains("id", StringComparison.OrdinalIgnoreCase) || 
                documentType.Contains("identity", StringComparison.OrdinalIgnoreCase) ||
                documentType.Contains("identification", StringComparison.OrdinalIgnoreCase))
                return "ID Card";
                
            if (documentType.Contains("passport", StringComparison.OrdinalIgnoreCase))
                return "Passport";
                
            if (documentType.Contains("license", StringComparison.OrdinalIgnoreCase) || 
                documentType.Contains("driver", StringComparison.OrdinalIgnoreCase))
                return "Driver's License";
                
            if (documentType.Contains("medical", StringComparison.OrdinalIgnoreCase) || 
                documentType.Contains("health", StringComparison.OrdinalIgnoreCase))
                return "Medical Report";
                
            if (documentType.Contains("tax", StringComparison.OrdinalIgnoreCase))
                return "Tax Form";
                
            if (documentType.Contains("contract", StringComparison.OrdinalIgnoreCase) || 
                documentType.Contains("agreement", StringComparison.OrdinalIgnoreCase))
                return "Contract";
                
            // If it's a known type, return as is
            return documentType;
        }
        
        // Try to infer type from content if not explicitly specified
        if (jsonObject.TryGetValue("invoice_number", out _) || 
            jsonObject.TryGetValue("invoiceNumber", out _) ||
            jsonObject.TryGetValue("invoice_id", out _))
            return "Invoice";
            
        if (jsonObject.TryGetValue("receipt_number", out _) || 
            jsonObject.TryGetValue("receiptNumber", out _))
            return "Receipt";
            
        if (jsonObject.TryGetValue("passport_number", out _) || 
            jsonObject.TryGetValue("passportNumber", out _))
            return "Passport";
            
        if (jsonObject.TryGetValue("license_number", out _) || 
            jsonObject.TryGetValue("licenseNumber", out _))
            return "Driver's License";
            
        if (jsonObject.TryGetValue("id_number", out _) || 
            jsonObject.TryGetValue("idNumber", out _))
            return "ID Card";
            
        // Default document type
        return "Other";
    }
    
    // Method to reload validation panel based on selected document type
    private void ReloadValidationPanelForDocumentType(JObject jsonObject, string documentType)
    {
        try
        {
            // Find all GroupBox controls except the first one (document type selector)
            var children = ValidationFieldsPanel.Children.OfType<GroupBox>().ToList();
            
            foreach (var child in children)
            {
                ValidationFieldsPanel.Children.Remove(child);
            }
            
            // Create section for essential fields
            var essentialsPanel = new GroupBox
            {
                Header = "Essential Fields",
                Padding = new Thickness(10),
                Margin = new Thickness(0, 0, 0, 15),
                Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F0F8FF"))
            };
            
            var essentialsStack = new StackPanel();
            essentialsPanel.Content = essentialsStack;
            
            // Populate essential fields based on document type
            PopulateEssentialFieldsForDocumentType(essentialsStack, jsonObject, documentType);
            
            // Add to main panel
            ValidationFieldsPanel.Children.Add(essentialsPanel);
            
            // Create section for document-specific fields
            var specificFieldsPanel = new GroupBox
            {
                Header = $"{documentType} Fields",
                Padding = new Thickness(10),
                Margin = new Thickness(0, 0, 0, 15)
            };
            
            var specificFieldsStack = new StackPanel();
            specificFieldsPanel.Content = specificFieldsStack;
            
            // Populate specific fields based on document type
            PopulateSpecificFieldsForDocumentType(specificFieldsStack, jsonObject, documentType);
            
            // Add to main panel
            ValidationFieldsPanel.Children.Add(specificFieldsPanel);
            
            // Create section for all remaining JSON data
            var allDataPanel = new GroupBox
            {
                Header = "All Fields",
                Padding = new Thickness(10),
                Margin = new Thickness(0, 0, 0, 15)
            };
            
            var allDataStack = new StackPanel();
            allDataPanel.Content = allDataStack;
            
            // Create fields recursively for all the JSON data
            CreateValidationFields(jsonObject, allDataStack, "", 0);
            
            // Add to main panel
            ValidationFieldsPanel.Children.Add(allDataPanel);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error reloading validation panel: {ex.Message}", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    
    // Method to populate essential fields based on document type
    private void PopulateEssentialFieldsForDocumentType(Panel panel, JObject jsonObject, string documentType)
    {
        // Common fields for all document types
        AddEssentialField(panel, "Document Type", GetValueOrDefault(jsonObject, "documentType", "type", "document_type"));
        AddEssentialField(panel, "Date", GetValueOrDefault(jsonObject, "date", "documentDate", "issueDate", "invoice_date"));
        
        // Document-specific essential fields
        switch (documentType)
        {
            case "Invoice":
                AddEssentialField(panel, "Invoice Number", GetValueOrDefault(jsonObject, "invoiceNumber", "invoice_number", "number", "id"));
                AddEssentialField(panel, "Total Amount", GetValueOrDefault(jsonObject, "totalAmount", "total", "amount", "invoice_amount"));
                AddEssentialField(panel, "Due Date", GetValueOrDefault(jsonObject, "dueDate", "due_date", "paymentDue"));
                AddEssentialField(panel, "Vendor/Supplier", GetValueOrDefault(jsonObject, "vendor", "supplier", "company", "from"));
                AddEssentialField(panel, "Customer/Client", GetValueOrDefault(jsonObject, "customer", "client", "to", "buyer"));
                break;
                
            case "Receipt":
                AddEssentialField(panel, "Receipt Number", GetValueOrDefault(jsonObject, "receiptNumber", "receipt_number", "number", "id"));
                AddEssentialField(panel, "Total Amount", GetValueOrDefault(jsonObject, "totalAmount", "total", "amount", "payment_amount"));
                AddEssentialField(panel, "Payment Method", GetValueOrDefault(jsonObject, "paymentMethod", "payment_method", "method"));
                AddEssentialField(panel, "Merchant", GetValueOrDefault(jsonObject, "merchant", "store", "company", "seller"));
                break;
                
            case "ID Card":
                AddEssentialField(panel, "ID Number", GetValueOrDefault(jsonObject, "idNumber", "id_number", "number"));
                AddEssentialField(panel, "Full Name", GetValueOrDefault(jsonObject, "name", "fullName", "full_name"));
                AddEssentialField(panel, "Date of Birth", GetValueOrDefault(jsonObject, "dateOfBirth", "date_of_birth", "dob", "birth_date"));
                AddEssentialField(panel, "Issuing Authority", GetValueOrDefault(jsonObject, "issuingAuthority", "issuer", "authority"));
                AddEssentialField(panel, "Expiry Date", GetValueOrDefault(jsonObject, "expiryDate", "expiry", "valid_until"));
                break;
                
            case "Passport":
                AddEssentialField(panel, "Passport Number", GetValueOrDefault(jsonObject, "passportNumber", "passport_number", "number"));
                AddEssentialField(panel, "Full Name", GetValueOrDefault(jsonObject, "name", "fullName", "full_name"));
                AddEssentialField(panel, "Date of Birth", GetValueOrDefault(jsonObject, "dateOfBirth", "date_of_birth", "dob", "birth_date"));
                AddEssentialField(panel, "Nationality", GetValueOrDefault(jsonObject, "nationality", "country"));
                AddEssentialField(panel, "Issue Date", GetValueOrDefault(jsonObject, "issueDate", "issue_date"));
                AddEssentialField(panel, "Expiry Date", GetValueOrDefault(jsonObject, "expiryDate", "expiry", "valid_until"));
                break;
                
            case "Driver's License":
                AddEssentialField(panel, "License Number", GetValueOrDefault(jsonObject, "licenseNumber", "license_number", "number"));
                AddEssentialField(panel, "Full Name", GetValueOrDefault(jsonObject, "name", "fullName", "full_name"));
                AddEssentialField(panel, "Date of Birth", GetValueOrDefault(jsonObject, "dateOfBirth", "date_of_birth", "dob", "birth_date"));
                AddEssentialField(panel, "Issue Date", GetValueOrDefault(jsonObject, "issueDate", "issue_date"));
                AddEssentialField(panel, "Expiry Date", GetValueOrDefault(jsonObject, "expiryDate", "expiry", "valid_until"));
                AddEssentialField(panel, "License Class", GetValueOrDefault(jsonObject, "licenseClass", "class", "type"));
                break;
                
            case "Medical Report":
                AddEssentialField(panel, "Patient Name", GetValueOrDefault(jsonObject, "patientName", "patient", "name"));
                AddEssentialField(panel, "Doctor Name", GetValueOrDefault(jsonObject, "doctorName", "doctor", "physician"));
                AddEssentialField(panel, "Medical Facility", GetValueOrDefault(jsonObject, "facility", "hospital", "clinic"));
                AddEssentialField(panel, "Diagnosis", GetValueOrDefault(jsonObject, "diagnosis", "condition", "ailment"));
                break;
                
            case "Tax Form":
                AddEssentialField(panel, "Form Number", GetValueOrDefault(jsonObject, "formNumber", "form_number", "number"));
                AddEssentialField(panel, "Tax Year", GetValueOrDefault(jsonObject, "taxYear", "year"));
                AddEssentialField(panel, "Taxpayer Name", GetValueOrDefault(jsonObject, "taxpayerName", "name"));
                AddEssentialField(panel, "Tax ID/SSN", GetValueOrDefault(jsonObject, "taxId", "ssn", "socialSecurityNumber"));
                break;
                
            case "Contract":
                AddEssentialField(panel, "Contract Title", GetValueOrDefault(jsonObject, "title", "contractTitle", "subject"));
                AddEssentialField(panel, "Party 1", GetValueOrDefault(jsonObject, "party1", "firstParty", "from"));
                AddEssentialField(panel, "Party 2", GetValueOrDefault(jsonObject, "party2", "secondParty", "to"));
                AddEssentialField(panel, "Start Date", GetValueOrDefault(jsonObject, "startDate", "effectiveDate", "start_date"));
                AddEssentialField(panel, "End Date", GetValueOrDefault(jsonObject, "endDate", "terminationDate", "end_date"));
                break;
                
            default:
                // Default essential fields for other document types
                AddEssentialField(panel, "Document Number", GetValueOrDefault(jsonObject, "documentNumber", "number", "id"));
                AddEssentialField(panel, "Total Amount", GetValueOrDefault(jsonObject, "totalAmount", "total", "amount"));
                break;
        }
    }
    
    // Method to populate document-specific fields
    private void PopulateSpecificFieldsForDocumentType(Panel panel, JObject jsonObject, string documentType)
    {
        switch (documentType)
        {
            case "Invoice":
                // Invoice-specific fields
                AddSpecificField(panel, "Subtotal", GetValueOrDefault(jsonObject, "subtotal", "sub_total", "net"));
                AddSpecificField(panel, "Tax Amount", GetValueOrDefault(jsonObject, "taxAmount", "tax", "vat", "gst"));
                AddSpecificField(panel, "Tax Rate", GetValueOrDefault(jsonObject, "taxRate", "tax_rate", "vat_rate"));
                AddSpecificField(panel, "Currency", GetValueOrDefault(jsonObject, "currency", "currencyCode"));
                AddSpecificField(panel, "Payment Terms", GetValueOrDefault(jsonObject, "paymentTerms", "terms"));
                AddSpecificField(panel, "PO Number", GetValueOrDefault(jsonObject, "poNumber", "purchaseOrder", "po"));
                
                // Check for line items
                if (jsonObject.TryGetValue("items", out JToken itemsToken) && itemsToken is JArray itemsArray)
                {
                    CreateItemsTable(panel, itemsArray, "Line Items");
                }
                break;
                
            case "Receipt":
                // Create a comprehensive Receipt form
                var receiptForm = new Grid();
                receiptForm.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                receiptForm.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                receiptForm.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                
                // Create a scrollable container for the form
                var scrollViewer = new ScrollViewer
                {
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    MaxHeight = 500
                };
                
                var formStack = new StackPanel
                {
                    Margin = new Thickness(0, 0, 0, 10)
                };
                
                // Receipt header - Store information
                var headerGroup = new GroupBox
                {
                    Header = "Store Information",
                    Margin = new Thickness(0, 0, 0, 15)
                };
                
                var headerStack = new StackPanel();
                
                AddReceiptField(headerStack, "Receipt Number", GetValueOrDefault(jsonObject, "receipt_number"));
                AddReceiptField(headerStack, "Document Date", GetValueOrDefault(jsonObject, "document_date"));
                AddReceiptField(headerStack, "Store Name", GetValueOrDefault(jsonObject, "store_name"));
                AddReceiptField(headerStack, "Store Address", GetValueOrDefault(jsonObject, "store_address"));
                AddReceiptField(headerStack, "Phone Number", GetValueOrDefault(jsonObject, "phone_number"));
                AddReceiptField(headerStack, "Fax Number", GetValueOrDefault(jsonObject, "fax_number"));
                AddReceiptField(headerStack, "Email", GetValueOrDefault(jsonObject, "email"));
                AddReceiptField(headerStack, "Website", GetValueOrDefault(jsonObject, "website"));
                AddReceiptField(headerStack, "GST ID", GetValueOrDefault(jsonObject, "gst_id"));
                
                headerGroup.Content = headerStack;
                formStack.Children.Add(headerGroup);
                
                // Receipt details
                var detailsGroup = new GroupBox
                {
                    Header = "Receipt Details",
                    Margin = new Thickness(0, 0, 0, 15)
                };
                
                var detailsStack = new StackPanel();
                
                AddReceiptField(detailsStack, "Pax Number", GetValueOrDefault(jsonObject, "pax_number"));
                AddReceiptField(detailsStack, "Table Number", GetValueOrDefault(jsonObject, "table_number"));
                AddReceiptField(detailsStack, "Cashier Name", GetValueOrDefault(jsonObject, "cashier_name"));
                
                detailsGroup.Content = detailsStack;
                formStack.Children.Add(detailsGroup);
                
                // Create items section
                var itemsGroup = new GroupBox
                {
                    Header = "Items",
                    Margin = new Thickness(0, 0, 0, 15)
                };
                
                var itemsGrid = new Grid();
                
                // Check for item arrays
                JArray itemNumbers = GetArrayOrEmpty(jsonObject, "item_no_of_receipt_items");
                JArray itemCodes = GetArrayOrEmpty(jsonObject, "item_code_of_receipt_items");
                JArray itemNames = GetArrayOrEmpty(jsonObject, "names_of_receipt_items");
                JArray quantities = GetArrayOrEmpty(jsonObject, "quantities_of_receipt_items");
                JArray unitPrices = GetArrayOrEmpty(jsonObject, "unit_prices_of_receipt_items");
                JArray grossAmounts = GetArrayOrEmpty(jsonObject, "gross_worth_of_receipt_items");
                
                // Determine the maximum number of items
                int maxItems = Math.Max(
                    Math.Max(
                        Math.Max(itemNumbers.Count, itemCodes.Count), 
                        Math.Max(itemNames.Count, quantities.Count)
                    ), 
                    Math.Max(unitPrices.Count, grossAmounts.Count)
                );
                
                if (maxItems > 0)
                {
                    var itemsTable = new DataGrid
                    {
                        AutoGenerateColumns = false,
                        CanUserAddRows = true,
                        CanUserDeleteRows = true,
                        Margin = new Thickness(0, 10, 0, 10),
                        MinHeight = 100,
                        MaxHeight = 200,
                        Tag = "receipt_items"
                    };
                    
                    // Add columns
                    itemsTable.Columns.Add(new DataGridTextColumn 
                    { 
                        Header = "Item No.", 
                        Binding = new Binding("ItemNo"),
                        Width = new DataGridLength(1, DataGridLengthUnitType.Star)
                    });
                    
                    itemsTable.Columns.Add(new DataGridTextColumn 
                    { 
                        Header = "Item Code", 
                        Binding = new Binding("ItemCode"),
                        Width = new DataGridLength(1, DataGridLengthUnitType.Star)
                    });
                    
                    itemsTable.Columns.Add(new DataGridTextColumn 
                    { 
                        Header = "Description", 
                        Binding = new Binding("Description"),
                        Width = new DataGridLength(2, DataGridLengthUnitType.Star)
                    });
                    
                    itemsTable.Columns.Add(new DataGridTextColumn 
                    { 
                        Header = "Quantity", 
                        Binding = new Binding("Quantity"),
                        Width = new DataGridLength(1, DataGridLengthUnitType.Star)
                    });
                    
                    itemsTable.Columns.Add(new DataGridTextColumn 
                    { 
                        Header = "Unit Price", 
                        Binding = new Binding("UnitPrice"),
                        Width = new DataGridLength(1, DataGridLengthUnitType.Star)
                    });
                    
                    itemsTable.Columns.Add(new DataGridTextColumn 
                    { 
                        Header = "Amount", 
                        Binding = new Binding("Amount"),
                        Width = new DataGridLength(1, DataGridLengthUnitType.Star)
                    });
                    
                    // Create a collection for the items
                    var itemsList = new System.Collections.ObjectModel.ObservableCollection<ReceiptItem>();
                    
                    // Add items from the JSON
                    for (int i = 0; i < maxItems; i++)
                    {
                        string itemNo = i < itemNumbers.Count ? itemNumbers[i].ToString() : "";
                        string itemCode = i < itemCodes.Count ? itemCodes[i].ToString() : "";
                        string name = i < itemNames.Count ? itemNames[i].ToString() : "";
                        string quantity = i < quantities.Count ? quantities[i].ToString() : "";
                        string unitPrice = i < unitPrices.Count ? unitPrices[i].ToString() : "";
                        string amount = i < grossAmounts.Count ? grossAmounts[i].ToString() : "";
                        
                        itemsList.Add(new ReceiptItem
                        {
                            ItemNo = itemNo,
                            ItemCode = itemCode,
                            Description = name,
                            Quantity = quantity,
                            UnitPrice = unitPrice,
                            Amount = amount
                        });
                    }
                    
                    // Set the data source
                    itemsTable.ItemsSource = itemsList;
                    
                    itemsGroup.Content = itemsTable;
                }
                else
                {
                    // If no items, create empty table
                    var noItemsText = new TextBlock
                    {
                        Text = "No items found in receipt data",
                        Margin = new Thickness(5),
                        HorizontalAlignment = HorizontalAlignment.Center
                    };
                    
                    itemsGroup.Content = noItemsText;
                }
                
                formStack.Children.Add(itemsGroup);
                
                // Receipt totals
                var totalsGroup = new GroupBox
                {
                    Header = "Totals",
                    Margin = new Thickness(0, 0, 0, 15)
                };
                
                var totalsStack = new StackPanel();
                
                AddReceiptField(totalsStack, "Subtotal", GetValueOrDefault(jsonObject, "subtotal"));
                AddReceiptField(totalsStack, "Rounding Amount", GetValueOrDefault(jsonObject, "rounding_amount"));
                AddReceiptField(totalsStack, "Service Charge %", GetValueOrDefault(jsonObject, "service_charge_percent"));
                AddReceiptField(totalsStack, "Service Charge", GetValueOrDefault(jsonObject, "service_charge"));
                AddReceiptField(totalsStack, "Tax %", GetValueOrDefault(jsonObject, "tax_percent"));
                AddReceiptField(totalsStack, "Tax Total", GetValueOrDefault(jsonObject, "tax_total"));
                AddReceiptField(totalsStack, "Currency", GetValueOrDefault(jsonObject, "currency"));
                AddReceiptField(totalsStack, "Total", GetValueOrDefault(jsonObject, "total"));
                
                totalsGroup.Content = totalsStack;
                formStack.Children.Add(totalsGroup);
                
                // Payment information
                var paymentGroup = new GroupBox
                {
                    Header = "Payment Information",
                    Margin = new Thickness(0, 0, 0, 15)
                };
                
                var paymentStack = new StackPanel();
                
                AddReceiptField(paymentStack, "Paid Amount", GetValueOrDefault(jsonObject, "paid_amount"));
                AddReceiptField(paymentStack, "Change Amount", GetValueOrDefault(jsonObject, "change_amount"));
                
                paymentGroup.Content = paymentStack;
                formStack.Children.Add(paymentGroup);
                
                // Additional information
                var additionalGroup = new GroupBox
                {
                    Header = "Additional Information",
                    Margin = new Thickness(0, 0, 0, 0)
                };
                
                var additionalStack = new StackPanel();
                
                AddReceiptField(additionalStack, "Handwritten Content", GetValueOrDefault(jsonObject, "handwritting_content"));
                
                additionalGroup.Content = additionalStack;
                formStack.Children.Add(additionalGroup);
                
                scrollViewer.Content = formStack;
                panel.Children.Add(scrollViewer);
                break;
                
            // Add other document types as needed
                
            default:
                // For unknown document types, try to find and display any array data in table format
                foreach (var property in jsonObject.Properties())
                {
                    if (property.Value is JArray arrayValue && arrayValue.Count > 0)
                    {
                        CreateItemsTable(panel, arrayValue, $"{FormatFieldNameForDisplay(property.Name)}");
                    }
                }
                break;
        }
    }
    
    // Helper method for creating receipt form fields
    private void AddReceiptField(Panel panel, string label, string value)
    {
        var fieldPanel = new StackPanel { Margin = new Thickness(0, 5, 0, 5) };
        
        // Create label
        var labelTextBlock = new TextBlock
        {
            Text = label,
            FontWeight = FontWeights.Normal,
            Margin = new Thickness(0, 5, 0, 2)
        };
        
        // Create text box
        var textBox = new TextBox
        {
            Text = value,
            Tag = "receipt:" + label.Replace(" ", "_").ToLowerInvariant(), // Special tag for receipt fields
            Padding = new Thickness(5),
            Margin = new Thickness(0, 0, 0, 5)
        };
        
        fieldPanel.Children.Add(labelTextBlock);
        fieldPanel.Children.Add(textBox);
        
        panel.Children.Add(fieldPanel);
    }
    
    // Helper method to get a JArray or return an empty one if not found
    private JArray GetArrayOrEmpty(JObject jsonObject, string propertyName)
    {
        if (jsonObject.TryGetValue(propertyName, out JToken token) && token is JArray array)
        {
            return array;
        }
        return new JArray();
    }
    
    // Class to represent a receipt item in the data grid
    private class ReceiptItem
    {
        public string ItemNo { get; set; }
        public string ItemCode { get; set; }
        public string Description { get; set; }
        public string Quantity { get; set; }
        public string UnitPrice { get; set; }
        public string Amount { get; set; }
    }

    // Helper method to add a specific field with different styling
    private void AddSpecificField(Panel panel, string label, string value)
    {
        var fieldPanel = new StackPanel { Margin = new Thickness(0, 5, 0, 5) };
        
        // Create label
        var labelTextBlock = new TextBlock
        {
            Text = label,
            FontWeight = FontWeights.Normal,
            Margin = new Thickness(0, 5, 0, 2)
        };
        
        // Create text box
        var textBox = new TextBox
        {
            Text = value,
            Tag = "specific:" + label.Replace(" ", "").ToLowerInvariant(), // Special tag for specific fields
            Padding = new Thickness(5),
            Margin = new Thickness(0, 0, 0, 5)
        };
        
        fieldPanel.Children.Add(labelTextBlock);
        fieldPanel.Children.Add(textBox);
        
        panel.Children.Add(fieldPanel);
    }
    
    // Helper method to create a table for array items
    private void CreateItemsTable(Panel panel, JArray itemsArray, string tableName)
    {
        if (itemsArray.Count == 0) return;
        
        // Create a header for the table
        var tableHeader = new TextBlock
        {
            Text = tableName,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 10, 0, 5)
        };
        panel.Children.Add(tableHeader);
        
        // Create a grid for the table
        var grid = new Grid { Margin = new Thickness(0, 5, 0, 15) };
        
        // Add column definitions based on the first item's properties
        var firstItem = itemsArray[0] as JObject;
        if (firstItem == null) return;
        
        // Setup columns in the grid
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) }); // Index column
        
        int colIndex = 1;
        var propertyNames = new List<string>();
        
        foreach (var property in firstItem.Properties())
        {
            // Skip deep nested objects in table view
            if (property.Value is JObject || property.Value is JArray)
                continue;
                
            propertyNames.Add(property.Name);
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            
            // Add header
            var header = new TextBlock
            {
                Text = FormatFieldNameForDisplay(property.Name),
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(5),
                TextWrapping = TextWrapping.Wrap
            };
            
            Grid.SetRow(header, 0);
            Grid.SetColumn(header, colIndex);
            grid.Children.Add(header);
            
            colIndex++;
        }
        
        // Add index header
        var indexHeader = new TextBlock
        {
            Text = "#",
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(5),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        Grid.SetRow(indexHeader, 0);
        Grid.SetColumn(indexHeader, 0);
        grid.Children.Add(indexHeader);
        
        // Add a header row
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        
        // Add a separator after header
        var headerSeparator = new Border
        {
            BorderBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#CCCCCC")),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Margin = new Thickness(0, 25, 0, 5)
        };
        Grid.SetRow(headerSeparator, 0);
        Grid.SetColumnSpan(headerSeparator, colIndex);
        grid.Children.Add(headerSeparator);
        
        // Add rows for each item
        for (int i = 0; i < itemsArray.Count; i++)
        {
            var item = itemsArray[i] as JObject;
            if (item == null) continue;
            
            int rowIndex = i + 1;
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            
            // Add index
            var indexText = new TextBlock
            {
                Text = (i + 1).ToString(),
                Margin = new Thickness(5),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(indexText, rowIndex);
            Grid.SetColumn(indexText, 0);
            grid.Children.Add(indexText);
            
            // Add item properties
            colIndex = 1;
            foreach (var propName in propertyNames)
            {
                string value = item.TryGetValue(propName, out JToken propValue) ? propValue.ToString() : "";
                
                var textBox = new TextBox
                {
                    Text = value,
                    Tag = $"items[{i}].{propName}", // Path to this item
                    Margin = new Thickness(5, 5, 5, 5),
                    Padding = new Thickness(3),
                    TextWrapping = TextWrapping.Wrap
                };
                
                Grid.SetRow(textBox, rowIndex);
                Grid.SetColumn(textBox, colIndex);
                grid.Children.Add(textBox);
                
                colIndex++;
            }
            
            // Add separator after each row except the last
            if (i < itemsArray.Count - 1)
            {
                var rowSeparator = new Border
                {
                    BorderBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#EEEEEE")),
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    Margin = new Thickness(5, 0, 5, 0)
                };
                Grid.SetRow(rowSeparator, rowIndex);
                Grid.SetColumnSpan(rowSeparator, colIndex);
                grid.Children.Add(rowSeparator);
            }
        }
        
        panel.Children.Add(grid);
    }
    
    // Helper to get value from multiple possible field names
    private string GetValueOrDefault(JObject jsonObject, params string[] possibleFieldNames)
    {
        foreach (var fieldName in possibleFieldNames)
        {
            if (jsonObject.TryGetValue(fieldName, out JToken value))
            {
                return value.ToString();
            }
            
            // Check for case-insensitive match
            foreach (var property in jsonObject.Properties())
            {
                if (string.Equals(property.Name, fieldName, StringComparison.OrdinalIgnoreCase))
                {
                    return property.Value.ToString();
                }
            }
            
            // Try with camelCase, PascalCase, snake_case variations
            var variations = new[]
            {
                fieldName,
                char.ToLowerInvariant(fieldName[0]) + fieldName.Substring(1),
                char.ToUpperInvariant(fieldName[0]) + fieldName.Substring(1),
                string.Join("_", Regex.Split(fieldName, @"(?<!^)(?=[A-Z])")).ToLowerInvariant()
            };
            
            foreach (var variation in variations)
            {
                if (jsonObject.TryGetValue(variation, out JToken varValue))
                {
                    return varValue.ToString();
                }
            }
        }
        
        return string.Empty;
    }
    
    // Add an essential field to the panel
    private void AddEssentialField(Panel panel, string label, string value)
    {
        var fieldPanel = new StackPanel { Margin = new Thickness(0, 5, 0, 5) };
        
        // Create label
        var labelTextBlock = new TextBlock
        {
            Text = label,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 5, 0, 2)
        };
        
        // Create text box
        var textBox = new TextBox
        {
            Text = value,
            Tag = "essential:" + label.Replace(" ", "").ToLowerInvariant(), // Special tag for essential fields
            Padding = new Thickness(5),
            Margin = new Thickness(0, 0, 0, 10),
            BorderThickness = new Thickness(1),
            BorderBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#4682B4"))
        };
        
        fieldPanel.Children.Add(labelTextBlock);
        fieldPanel.Children.Add(textBox);
        
        // Add a separator
        var separator = new Border
        {
            BorderBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#DDDDDD")),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Margin = new Thickness(0, 0, 0, 5)
        };
        fieldPanel.Children.Add(separator);
        
        panel.Children.Add(fieldPanel);
    }
    
    // Recursively create fields for JSON data
    private void CreateValidationFields(JToken token, Panel parentPanel, string currentPath, int depth)
    {
        if (token is JObject jObject)
        {
            // Create a group for the object
            GroupBox groupBox = null;
            StackPanel stackPanel = null;
            
            if (depth == 0)
            {
                // Top level - use the parent panel directly
                stackPanel = parentPanel as StackPanel;
            }
            else
            {
                // Create a group box for this object
                string rawName = string.IsNullOrEmpty(currentPath) ? "Root" : System.IO.Path.GetFileName(currentPath);
                string groupName = FormatFieldNameForDisplay(rawName);
                
                groupBox = new GroupBox
                {
                    Header = groupName,
                    Padding = new Thickness(10),
                    Margin = new Thickness(0, 10, 0, 10)
                };
                
                stackPanel = new StackPanel();
                groupBox.Content = stackPanel;
                parentPanel.Children.Add(groupBox);
            }
            
            // Process each property in the object
            foreach (var property in jObject.Properties())
            {
                string newPath = string.IsNullOrEmpty(currentPath) ? property.Name : $"{currentPath}.{property.Name}";
                
                CreateValidationFields(property.Value, stackPanel, newPath, depth + 1);
            }
        }
        else if (token is JArray jArray)
        {
            // Get a user-friendly name for the array
            string rawName = string.IsNullOrEmpty(currentPath) ? "Array" : System.IO.Path.GetFileName(currentPath);
            string arrayName = FormatFieldNameForDisplay(rawName);
            
            // Create a special header for array types that should be presented nicely like invoice items
            bool isItemArray = rawName.Contains("item") || rawName.Contains("items");
            
            // Create a group for the array
            var groupBox = new GroupBox
            {
                Header = arrayName,
                Padding = new Thickness(10),
                Margin = new Thickness(0, 10, 0, 10)
            };
            
            var arrayPanel = new StackPanel();
            groupBox.Content = arrayPanel;
            parentPanel.Children.Add(groupBox);
            
            // For item arrays, create a table-like layout
            if (isItemArray && jArray.Count > 0)
            {
                // Create a grid for better layout of array items
                var grid = new Grid();
                grid.Margin = new Thickness(0, 10, 0, 0);
                
                // Add column definitions (index, value, description)
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) }); // Item #
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Value
                
                // Add headers row
                var headerIndex = new TextBlock
                {
                    Text = "#",
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(5),
                    VerticalAlignment = VerticalAlignment.Center
                };
                
                var headerValue = new TextBlock
                {
                    Text = "Value",
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(5),
                    VerticalAlignment = VerticalAlignment.Center
                };
                
                Grid.SetColumn(headerIndex, 0);
                Grid.SetColumn(headerValue, 1);
                
                grid.Children.Add(headerIndex);
                grid.Children.Add(headerValue);
                
                // Add a separator
                var headerSeparator = new Border
                {
                    BorderBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#DDDDDD")),
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    Margin = new Thickness(0, 25, 0, 5)
                };
                
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header row
                Grid.SetRow(headerIndex, 0);
                Grid.SetRow(headerValue, 0);
                Grid.SetRow(headerSeparator, 0);
                Grid.SetColumnSpan(headerSeparator, 2);
                
                grid.Children.Add(headerSeparator);
                
                // Add each array item
                for (int i = 0; i < jArray.Count; i++)
                {
                    grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    int rowIndex = i + 1; // +1 because of the header row
                    
                    var indexText = new TextBlock
                    {
                        Text = (i + 1).ToString(),
                        Margin = new Thickness(5),
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    
                    // Create text box for the array item value
                    var textBox = new TextBox
                    {
                        Text = jArray[i].ToString(),
                        Tag = $"{currentPath}[{i}]", // Store the path to find this value later
                        Margin = new Thickness(5),
                        Padding = new Thickness(5),
                        TextWrapping = TextWrapping.Wrap
                    };
                    
                    // Set row and column for the new controls
                    Grid.SetRow(indexText, rowIndex);
                    Grid.SetColumn(indexText, 0);
                    Grid.SetRow(textBox, rowIndex);
                    Grid.SetColumn(textBox, 1);
                    
                    grid.Children.Add(indexText);
                    grid.Children.Add(textBox);
                    
                    // Add a separator between items
                    if (i < jArray.Count - 1)
                    {
                        var separator = new Border
                        {
                            BorderBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#EEEEEE")),
                            BorderThickness = new Thickness(0, 0, 0, 1),
                            Margin = new Thickness(5, 0, 5, 0)
                        };
                        
                        Grid.SetRow(separator, rowIndex);
                        Grid.SetColumnSpan(separator, 2);
                        
                        grid.Children.Add(separator);
                    }
                }
                
                arrayPanel.Children.Add(grid);
            }
            else
            {
                // Standard array display for non-item arrays
                // Add array items
                for (int i = 0; i < jArray.Count; i++)
                {
                    string newPath = $"{currentPath}[{i}]";
                    
                    // Create a nested panel for each array item
                    var itemPanel = new StackPanel { Margin = new Thickness(0, 5, 0, 5) };
                    
                    // Add header for the array item
                    var itemHeader = new TextBlock
                    {
                        Text = $"Item {i + 1}",
                        FontWeight = FontWeights.SemiBold,
                        Margin = new Thickness(0, 5, 0, 5)
                    };
                    itemPanel.Children.Add(itemHeader);
                    
                    // Add a separator
                    var separator = new Border
                    {
                        BorderBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#DDDDDD")),
                        BorderThickness = new Thickness(0, 0, 0, 1),
                        Margin = new Thickness(0, 0, 0, 5)
                    };
                    itemPanel.Children.Add(separator);
                    
                    // Process the array item
                    CreateValidationFields(jArray[i], itemPanel, newPath, depth + 1);
                    
                    arrayPanel.Children.Add(itemPanel);
                }
            }
        }
        else // JValue
        {
            // Create a field for a primitive value
            string rawName = string.IsNullOrEmpty(currentPath) ? "Value" : System.IO.Path.GetFileName(currentPath);
            string fieldName = FormatFieldNameForDisplay(rawName);
            
            // Create field container
            var fieldPanel = new StackPanel { Margin = new Thickness(0, 5, 0, 5) };
            
            // Create label
            var label = new TextBlock
            {
                Text = fieldName,
                FontWeight = FontWeights.Normal,
                Margin = new Thickness(0, 5, 0, 2)
            };
            
            // Create text box
            var textBox = new TextBox
            {
                Text = token.ToString(),
                Tag = currentPath, // Store the path to find this value later
                Margin = new Thickness(0, 0, 0, 5),
                Padding = new Thickness(5),
                AcceptsReturn = token.ToString().Length > 30, // Allow multiline for longer values
                TextWrapping = TextWrapping.Wrap
            };
            
            // Set height based on content
            if (token.ToString().Length > 100)
            {
                textBox.MinHeight = 80;
                textBox.MaxHeight = 150;
                textBox.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            }
            
            // Add label and text box to field panel
            fieldPanel.Children.Add(label);
            fieldPanel.Children.Add(textBox);
            
            // Add field to parent panel
            parentPanel.Children.Add(fieldPanel);
        }
    }
    
    // Helper method to format field names for display
    private string FormatFieldNameForDisplay(string fieldName)
    {
        if (string.IsNullOrEmpty(fieldName))
            return "Value";
            
        // Check for array index pattern [0], [1], etc.
        if (fieldName.StartsWith("[") && fieldName.EndsWith("]"))
        {
            // This is an array index
            if (int.TryParse(fieldName.Substring(1, fieldName.Length - 2), out int index))
            {
                return $"Item {index + 1}";
            }
        }
        
        // Handle numeric-only field names
        if (int.TryParse(fieldName, out _))
        {
            return $"Item {fieldName}";
        }
        
        // Replace underscores with spaces and capitalize words
        string[] words = fieldName.Split('_');
        for (int i = 0; i < words.Length; i++)
        {
            if (words[i].Length > 0)
            {
                words[i] = char.ToUpper(words[i][0]) + (words[i].Length > 1 ? words[i].Substring(1) : "");
            }
        }
        
        return string.Join(" ", words);
    }
    
    // Update button to sync the data from validation panel back to JSON
    private void BtnUpdateValidation_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Get the original JSON data
            var jsonObject = JsonConvert.DeserializeObject<JObject>(TxtExtractionResults.Text);
            if (jsonObject == null)
            {
                MessageBox.Show("Cannot parse the original JSON data.", "Update Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            
            // Update the JSON object with values from the UI
            UpdateJsonFromValidationPanel(jsonObject);
            
            // Update the extraction results textbox
            TxtExtractionResults.Text = jsonObject.ToString(Formatting.Indented);
            
            MessageBox.Show("JSON data updated successfully.", "Update Complete", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error updating JSON: {ex.Message}", "Update Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    
    // Method to recursively update JSON from validation fields
    private void UpdateJsonFromValidationPanel(JObject rootObject)
    {
        try
        {
            // Find all textboxes in the validation panel
            List<TextBox> textBoxes = FindAllTextBoxes(ValidationFieldsPanel);
            
            // Process essential fields separately (they have special tags)
            foreach (var textBox in textBoxes.Where(tb => tb.Tag != null && tb.Tag.ToString().StartsWith("essential:")))
            {
                string fieldTag = textBox.Tag.ToString();
                string fieldValue = textBox.Text;
                
                // Extract the actual field name from the tag
                string fieldName = fieldTag.Substring("essential:".Length);
                
                // Map essential field tags to potential JSON property names
                switch (fieldName)
                {
                    case "documenttype":
                        TryUpdateProperty(rootObject, fieldValue, "documentType", "type", "document_type");
                        break;
                    case "date":
                        TryUpdateProperty(rootObject, fieldValue, "date", "documentDate", "issueDate", "invoice_date");
                        break;
                    case "totalamount":
                        TryUpdateProperty(rootObject, fieldValue, "totalAmount", "total", "amount", "invoice_amount");
                        break;
                    case "documentnumber":
                        TryUpdateProperty(rootObject, fieldValue, "documentNumber", "invoiceNumber", "number", "id");
                        break;
                    case "invoicenumber":
                        TryUpdateProperty(rootObject, fieldValue, "invoiceNumber", "invoice_number", "number");
                        break;
                    case "receiptnumber":
                        TryUpdateProperty(rootObject, fieldValue, "receiptNumber", "receipt_number", "number");
                        break;
                    case "idnumber":
                        TryUpdateProperty(rootObject, fieldValue, "idNumber", "id_number", "number");
                        break;
                    case "passportnumber":
                        TryUpdateProperty(rootObject, fieldValue, "passportNumber", "passport_number", "number");
                        break;
                    case "licensenumber":
                        TryUpdateProperty(rootObject, fieldValue, "licenseNumber", "license_number", "number");
                        break;
                    case "fullname":
                        TryUpdateProperty(rootObject, fieldValue, "name", "fullName", "full_name");
                        break;
                    case "dateofbirth":
                        TryUpdateProperty(rootObject, fieldValue, "dateOfBirth", "date_of_birth", "dob", "birth_date");
                        break;
                    case "duedate":
                        TryUpdateProperty(rootObject, fieldValue, "dueDate", "due_date", "paymentDue");
                        break;
                    case "vendor/supplier":
                        TryUpdateProperty(rootObject, fieldValue, "vendor", "supplier", "company", "from");
                        break;
                    case "customer/client":
                        TryUpdateProperty(rootObject, fieldValue, "customer", "client", "to", "buyer");
                        break;
                    case "paymentmethod":
                        TryUpdateProperty(rootObject, fieldValue, "paymentMethod", "payment_method", "method");
                        break;
                    case "merchant":
                        TryUpdateProperty(rootObject, fieldValue, "merchant", "store", "company", "seller");
                        break;
                    case "issuingauthority":
                        TryUpdateProperty(rootObject, fieldValue, "issuingAuthority", "issuer", "authority");
                        break;
                    case "expirydate":
                        TryUpdateProperty(rootObject, fieldValue, "expiryDate", "expiry", "valid_until");
                        break;
                    case "nationality":
                        TryUpdateProperty(rootObject, fieldValue, "nationality", "country");
                        break;
                    case "issuedate":
                        TryUpdateProperty(rootObject, fieldValue, "issueDate", "issue_date");
                        break;
                    case "licenseclass":
                        TryUpdateProperty(rootObject, fieldValue, "licenseClass", "class", "type");
                        break;
                    default:
                        // For any other essential fields, try a direct property name match
                        string propertyName = fieldName;
                        TryUpdateProperty(rootObject, fieldValue, propertyName, ConvertToCamelCase(propertyName), ConvertToSnakeCase(propertyName));
                        break;
                }
            }
            
            // Process document-specific fields
            foreach (var textBox in textBoxes.Where(tb => tb.Tag != null && tb.Tag.ToString().StartsWith("specific:")))
            {
                string fieldTag = textBox.Tag.ToString();
                string fieldValue = textBox.Text;
                
                // Extract the actual field name from the tag
                string fieldName = fieldTag.Substring("specific:".Length);
                
                // Map common document-specific fields
                switch (fieldName)
                {
                    case "subtotal":
                        TryUpdateProperty(rootObject, fieldValue, "subtotal", "sub_total", "net");
                        break;
                    case "taxamount":
                        TryUpdateProperty(rootObject, fieldValue, "taxAmount", "tax", "vat", "gst");
                        break;
                    case "taxrate":
                        TryUpdateProperty(rootObject, fieldValue, "taxRate", "tax_rate", "vat_rate");
                        break;
                    case "currency":
                        TryUpdateProperty(rootObject, fieldValue, "currency", "currencyCode");
                        break;
                    case "paymentterms":
                        TryUpdateProperty(rootObject, fieldValue, "paymentTerms", "terms");
                        break;
                    case "ponumber":
                        TryUpdateProperty(rootObject, fieldValue, "poNumber", "purchaseOrder", "po");
                        break;
                    case "discount":
                        TryUpdateProperty(rootObject, fieldValue, "discount", "discountAmount");
                        break;
                    case "cashier":
                        TryUpdateProperty(rootObject, fieldValue, "cashier", "clerk", "operator");
                        break;
                    case "register/terminal":
                        TryUpdateProperty(rootObject, fieldValue, "register", "terminal", "pos");
                        break;
                    case "location":
                        TryUpdateProperty(rootObject, fieldValue, "location", "store_location", "branch");
                        break;
                    default:
                        // For any other specific fields, try a direct property name match
                        string propertyName = fieldName;
                        TryUpdateProperty(rootObject, fieldValue, propertyName, ConvertToCamelCase(propertyName), ConvertToSnakeCase(propertyName));
                        break;
                }
            }
            
            // Process regular JSON path fields and array items
            foreach (var textBox in textBoxes.Where(tb => tb.Tag != null && 
                                                 !tb.Tag.ToString().StartsWith("essential:") && 
                                                 !tb.Tag.ToString().StartsWith("specific:")))
            {
                string path = textBox.Tag.ToString();
                string value = textBox.Text;
                
                if (!string.IsNullOrEmpty(path))
                {
                    SetJsonValueByPath(rootObject, path, value);
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error updating JSON: {ex.Message}", "Update Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    
    // Try to update a property using various possible property names
    private void TryUpdateProperty(JObject rootObject, string value, params string[] possiblePropertyNames)
    {
        // Check if any of the property names exist and update the first one found
        foreach (var propName in possiblePropertyNames)
        {
            if (rootObject.TryGetValue(propName, out _))
            {
                rootObject[propName] = value;
                return;
            }
            
            // Check for case-insensitive match
            foreach (var property in rootObject.Properties())
            {
                if (string.Equals(property.Name, propName, StringComparison.OrdinalIgnoreCase))
                {
                    rootObject[property.Name] = value;
                    return;
                }
            }
        }
        
        // If none of the properties exist, add the property with the first name
        rootObject[possiblePropertyNames[0]] = value;
    }
    
    // Helper method to find all TextBox controls in a panel
    private List<TextBox> FindAllTextBoxes(Panel panel)
    {
        var result = new List<TextBox>();
        
        foreach (var child in panel.Children)
        {
            if (child is TextBox textBox)
            {
                result.Add(textBox);
            }
            else if (child is Panel childPanel)
            {
                result.AddRange(FindAllTextBoxes(childPanel));
            }
            else if (child is ContentControl contentControl && contentControl.Content is Panel nestedPanel)
            {
                result.AddRange(FindAllTextBoxes(nestedPanel));
            }
            else if (child is GroupBox groupBox && groupBox.Content is Panel groupPanel)
            {
                result.AddRange(FindAllTextBoxes(groupPanel));
            }
        }
        
        return result;
    }
    
    // Helper method to set a value in a JObject by path
    private void SetJsonValueByPath(JObject rootObject, string path, string value)
    {
        try
        {
            if (path.Contains("[") && path.Contains("]"))
            {
                // Handle array paths like "items[0].description"
                var parts = path.Split('.');
                var current = rootObject as JToken;
                
                for (int i = 0; i < parts.Length; i++)
                {
                    var part = parts[i];
                    
                    if (part.Contains("["))
                    {
                        // Parse array access
                        var arrayName = part.Substring(0, part.IndexOf("["));
                        var indexStr = part.Substring(part.IndexOf("[") + 1, part.IndexOf("]") - part.IndexOf("[") - 1);
                        
                        if (int.TryParse(indexStr, out int index))
                        {
                            // Get the array
                            var array = current[arrayName] as JArray;
                            if (array != null && index < array.Count)
                            {
                                // Select the array item
                                current = array[index];
                            }
                            else
                            {
                                // Array or index not found
                                return;
                            }
                        }
                    }
                    else if (i < parts.Length - 1)
                    {
                        // Navigate to next property
                        current = current[part];
                        if (current == null) return;
                    }
                    else
                    {
                        // Final property - set the value
                        if (current is JObject jObj)
                        {
                            jObj[part] = value;
                        }
                    }
                }
            }
            else
            {
                // Handle simple dot notation path like "company.name"
                var parts = path.Split('.');
                var current = rootObject as JToken;
                
                for (int i = 0; i < parts.Length - 1; i++)
                {
                    current = current[parts[i]];
                    if (current == null) return;
                }
                
                if (current is JObject jObj)
                {
                    jObj[parts[parts.Length - 1]] = value;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error setting JSON value for path {path}: {ex.Message}");
        }
    }
    
    // Save validation button
    private void BtnSaveValidation_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // First update the JSON from the validation panel
            BtnUpdateValidation_Click(sender, e);
            
            // Create a default filename based on the original document
            string defaultFileName = "";
            if (!string.IsNullOrEmpty(_currentFilePath))
            {
                defaultFileName = Path.GetFileNameWithoutExtension(_currentFilePath) + "_extraction.json";
            }
            else
            {
                defaultFileName = "extraction_results.json";
            }

            // Show save dialog
            SaveFileDialog saveDialog = new SaveFileDialog
            {
                Filter = "JSON Files (*.json)|*.json|Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
                FileName = defaultFileName,
                Title = "Save Extraction Results"
            };

            if (saveDialog.ShowDialog() == true)
            {
                // Save the text to the selected file
                File.WriteAllText(saveDialog.FileName, TxtExtractionResults.Text);
                MessageBox.Show($"Results saved to {saveDialog.FileName}", "Save Successful", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error saving validated data: {ex.Message}", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    
    // Toggle validation panel
    private void BtnToggleValidation_Click(object sender, RoutedEventArgs e)
    {
        _isDataValidationPanelVisible = false;
        UpdateDataValidationPanelVisibility();
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
        
        switch (documentType)
        {
            case "Invoice":
                jsonContent = @"{
  ""company_name"": ""Cass Europe b.v."",
  ""invoice_number"": ""CEINV-000000501"",
  ""purchase_order_number"": ""PO400915054999"",
  ""document_date"": ""12/12/2019"",
  ""client_name"": ""ABC ASIA PTE LTD"",
  ""client_address"": ""1 KALLANG AVENUE\nLEVEL 1\n123456 SINGAPORE SG"",
  ""sale_order_number"": """",
  ""client_tax_id"": ""M90361999A"",
  ""seller_name"": """",
  ""seller_address"": ""Graaf Engelbertlaan 75"",
  ""seller_tax_id"": """",
  ""iban"": """",
  ""item_no_of_invoice_items"": [""22"", ""2.075"", ""2.178"", ""1""],
  ""names_of_invoice_items"": [""PARCEL PACKAGE FEES"", ""Freight Billing File With Resolution"", ""IMACHING - EDI"", ""MONTHLY FEE""],
  ""quantities_of_invoice_items"": [""22"", ""2.075"", ""2.178"", ""1""],
  ""unit_prices_of_invoice_items"": [""€0,06"", ""€0,55"", ""€0,10"", ""€285,00""],
  ""gross_worth_of_invoice_items"": [""€1,32"", ""€1.141,25"", ""€217,80"", ""€285,00""],
  ""total_net_worth"": ""€1.645,37"",
  ""tax_amount"": ""€0,00"",
  ""tax_percent"": """",
  ""total_gross_worth"": ""€1.645,37"",
  ""handwritting_content"": """"
}";
                break;
                
            case "Receipt":
                jsonContent = @"{
  ""receipt_number"": ""R328794"",
  ""document_date"": ""15/04/2023"",
  ""store_name"": ""FAIRPRICE FINEST"",
  ""store_address"": ""290 Orchard Rd, #B1-03/04, Singapore 238859"",
  ""phone_number"": ""+65 6737 1516"",
  ""fax_number"": """",
  ""email"": """",
  ""website"": ""www.fairprice.com.sg"",
  ""gst_id"": ""M4-0000042-2"",
  ""pax_number"": ""2"",
  ""table_number"": """",
  ""cashier_name"": ""Sophia"",
  ""item_no_of_receipt_items"": [""1"", ""2"", ""3"", ""4"", ""5""],
  ""item_code_of_receipt_items"": [""APL001"", ""ORG002"", ""BRD003"", ""MLK004"", ""WTR005""],
  ""names_of_receipt_items"": [""Apple Fuji"", ""Organic Spinach"", ""Whole Grain Bread"", ""Low Fat Milk 1L"", ""Mineral Water 500ml""],
  ""quantities_of_receipt_items"": [""4"", ""1"", ""2"", ""1"", ""6""],
  ""unit_prices_of_receipt_items"": [""$0.80"", ""$3.50"", ""$3.95"", ""$2.95"", ""$0.80""],
  ""gross_worth_of_receipt_items"": [""$3.20"", ""$3.50"", ""$7.90"", ""$2.95"", ""$4.80""],
  ""subtotal"": ""$22.35"",
  ""rounding_amount"": ""$0.00"",
  ""paid_amount"": ""$25.00"",
  ""change_amount"": ""$2.65"",
  ""service_charge_percent"": """",
  ""service_charge"": """",
  ""currency"": ""SGD"",
  ""tax_percent"": ""7%"",
  ""tax_total"": ""$1.46"",
  ""total"": ""$23.81"",
  ""handwritting_content"": """"
}";
                break;
                
            case "Purchase Order":
                jsonContent = @"{
  ""company_name"": ""JVD TECHNOLOGIES (ASIA) PTE LTD"",
  ""purchase_order_number"": ""ASPO25000807"",
  ""document_date"": ""03/03/2025"",
  ""client_name"": ""MOMENTUS HOTEL ALEXANDRA"",
  ""client_address"": ""323 Alexandra Road, Singapore 159972"",
  ""sale_order_number"": ""LSO25-022301"",
  ""client_tax_id"": """",
  ""seller_name"": ""JVD TECHNOLOGIES (ASIA) PTE LTD"",
  ""seller_address"": ""No. 56 Loyang Way #06-04 Loyang Enterprise, Singapore 508775"",
  ""seller_tax_id"": ""199906280E"",
  ""iban"": """",
  ""item_no_of_invoice_items"": [""LTW-300549-UB-V2""],
  ""names_of_invoice_items"": [""Linen trolley, H820/L1000/W700mm, steel 25mm tube, 2 solid & 2 swivel wheels, Black bag, Black""],
  ""quantities_of_invoice_items"": [""5""],
  ""unit_prices_of_invoice_items"": [""""],
  ""gross_worth_of_invoice_items"": [""""],
  ""total_net_worth"": """",
  ""tax_amount"": """",
  ""tax_percent"": """",
  ""total_gross_worth"": """",
  ""handwritting_content"": """"
}";
                break;
                
            case "Delivery Order":
                jsonContent = @"{
  ""company_name"": ""JVD TECHNOLOGIES (ASIA) PTE LTD"",
  ""invoice_number"": ""INV25-0330005"",
  ""purchase_order_number"": ""ASPO25000807"",
  ""delivery_order_number"": ""DO25-0018234"",
  ""document_date"": ""03/03/2025"",
  ""client_name"": ""MOMENTUS HOTEL ALEXANDRA"",
  ""client_address"": ""323 Alexandra Road, Singapore 159972"",
  ""sale_order_number"": ""LSO25-022301"",
  ""client_tax_id"": """",
  ""seller_name"": ""JVD TECHNOLOGIES (ASIA) PTE LTD"",
  ""seller_address"": ""No. 56 Loyang Way #06-04 Loyang Enterprise, Singapore 508775"",
  ""seller_tax_id"": ""199906280E"",
  ""iban"": """",
  ""item_no_of_invoice_items"": [""LTW-300549-UB-V2""],
  ""names_of_invoice_items"": [""Linen trolley, H820/L1000/W700mm, steel 25mm tube, 2 solid & 2 swivel wheels, Black bag, Black""],
  ""quantities_of_invoice_items"": [""5""],
  ""unit_prices_of_invoice_items"": [""""],
  ""gross_worth_of_invoice_items"": [""""],
  ""total_net_worth"": """",
  ""tax_amount"": """",
  ""tax_percent"": """",
  ""total_gross_worth"": """",
  ""handwritting_content"": """"
}";
                break;
                
            case "Business Card":
                jsonContent = @"{
  ""0"": {
    ""company_name"": ""ABC CONSULTING PTE LTD"",
    ""full_name"": ""John Smith"",
    ""title"": ""Senior Consultant"",
    ""email_address"": ""john.smith@abcconsulting.com"",
    ""phone_number"": ""+65 9123 4567"",
    ""tel_number"": ""+65 6789 1234"",
    ""fax_number"": ""+65 6789 1235"",
    ""website"": ""www.abcconsulting.com"",
    ""address"": ""1 Raffles Place, #20-01, Singapore 048616"",
    ""handwritting_content"": """"
  }
}";
                break;
            
            default:
                MessageBox.Show($"No sample data available for {documentType} type.", "Sample Data", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
        }
        
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
                Height = 250,
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
            
            var rbInvoice = new RadioButton 
            { 
                Content = "Invoice", 
                IsChecked = true, 
                Margin = new Thickness(0, 5, 0, 5),
                GroupName = "DocType"
            };
            
            var rbReceipt = new RadioButton 
            { 
                Content = "Receipt", 
                Margin = new Thickness(0, 5, 0, 5),
                GroupName = "DocType"
            };
            
            var rbPO = new RadioButton 
            { 
                Content = "Purchase Order", 
                Margin = new Thickness(0, 5, 0, 5),
                GroupName = "DocType"
            };
            
            var rbDO = new RadioButton 
            { 
                Content = "Delivery Order", 
                Margin = new Thickness(0, 5, 0, 5),
                GroupName = "DocType"
            };
            
            var rbBC = new RadioButton 
            { 
                Content = "Business Card", 
                Margin = new Thickness(0, 5, 0, 5),
                GroupName = "DocType"
            };
            
            radioButtonPanel.Children.Add(rbInvoice);
            radioButtonPanel.Children.Add(rbReceipt);
            radioButtonPanel.Children.Add(rbPO);
            radioButtonPanel.Children.Add(rbDO);
            radioButtonPanel.Children.Add(rbBC);
            
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
                string selectedDocType = "Invoice"; // Default
                
                if (rbInvoice.IsChecked == true) selectedDocType = "Invoice";
                else if (rbReceipt.IsChecked == true) selectedDocType = "Receipt";
                else if (rbPO.IsChecked == true) selectedDocType = "Purchase Order";
                else if (rbDO.IsChecked == true) selectedDocType = "Delivery Order";
                else if (rbBC.IsChecked == true) selectedDocType = "Business Card";
                
                // Load the selected document type for validation
                LoadSampleJsonForValidation(selectedDocType);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error testing validation: {ex.Message}", "Test Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // Fix JSON button - attempt to automatically repair JSON formatting issues
    private void BtnFixJson_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (string.IsNullOrEmpty(TxtExtractionResults.Text))
            {
                MessageBox.Show("No JSON content to fix.", "JSON Fix", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Get the original text
            string originalText = TxtExtractionResults.Text;
            bool wasMarkdownFormatted = false;
            
            // Check for markdown code block pattern
            if (originalText.TrimStart().StartsWith("```") && originalText.TrimEnd().EndsWith("```"))
            {
                wasMarkdownFormatted = true;
            }
            
            // Apply basic cleaning
            string cleanedText = CleanJsonContent(originalText);
            
            try
            {
                // Try to parse and reformat the JSON
                var jsonObject = JsonConvert.DeserializeObject(cleanedText);
                if (jsonObject != null)
                {
                    // If it parses successfully, reformat it with proper indentation
                    string formattedJson = JsonConvert.SerializeObject(jsonObject, Formatting.Indented);
                    TxtExtractionResults.Text = formattedJson;
                    
                    string message = "JSON formatting has been fixed and beautified.";
                    if (wasMarkdownFormatted)
                    {
                        message += "\n\nMarkdown code block markers (```json, ```) were also removed.";
                    }
                    
                    MessageBox.Show(message, "JSON Fixed", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
            }
            catch (JsonException ex)
            {
                // If standard parsing fails, try more advanced repair techniques
                string repairedJson = AttemptAdvancedJsonRepair(cleanedText);
                
                // Check if the repaired JSON is valid
                try
                {
                    var testObject = JsonConvert.DeserializeObject<JObject>(repairedJson);
                    if (testObject != null)
                    {
                        // Successfully repaired!
                        TxtExtractionResults.Text = JsonConvert.SerializeObject(testObject, Formatting.Indented);
                        
                        string message = "JSON has been repaired and formatted.";
                        if (wasMarkdownFormatted)
                        {
                            message += "\n\nMarkdown code block markers (```json, ```) were also removed.";
                        }
                        
                        MessageBox.Show(message, "JSON Fixed", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }
                }
                catch
                {
                    // Advanced repair failed too
                }
                
                // If we got here, provide guidance based on the original error
                string errorMessage = $"Couldn't automatically fix JSON: {ex.Message}\n\nPlease manually check for:";
                
                // Add specific guidance based on error message patterns
                if (ex.Message.Contains("Unexpected character"))
                {
                    errorMessage += "\n- Remove markdown formatting (```json, ```)";
                    errorMessage += "\n- Remove non-standard quotes or characters";
                    errorMessage += "\n- Ensure property names are in double quotes";
                }
                else if (ex.Message.Contains("after an array element"))
                {
                    errorMessage += "\n- Check for missing commas between array elements";
                    errorMessage += "\n- Check for extra commas at the end of arrays";
                }
                else if (ex.Message.Contains("after a value"))
                {
                    errorMessage += "\n- Check for missing commas between properties";
                    errorMessage += "\n- Check for extra commas at the end of objects";
                }
                
                MessageBox.Show(errorMessage, "JSON Repair Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error fixing JSON: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
        
