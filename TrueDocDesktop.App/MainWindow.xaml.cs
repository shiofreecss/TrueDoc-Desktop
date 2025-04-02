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
    
    // Dictionary of document type prompts
    private readonly Dictionary<string, string> PROMPTS = new Dictionary<string, string>
    {
        {"Business Card", "I have this image and it contains one or multiple business cards, can you help me to extract the information from this image into the JSON format as: {\"0\": <bizcard_json_1>,\"1\": <bizcard_json_2> ..., \"n\": <bizcard_json_n>}. In child JSON like <bizcard_json_1> will follow this format {\"company_name\": \"\", \"full_name\": \"\", \"title\": \"\", \"email_address\": \"\", \"phone_number\": \"\", \"tel_number\": \"\", \"fax_number\": \"\", \"website\": \"\", \"address\": \"\", \"handwritting_content\": \"\"}"},
        {"Receipt", "Give me JSON as this format: {\"receipt_number\": \"\", \"document_date\": \"\", \"store_name\": \"\", \"store_address\": \"\", \"phone_number\": \"\", \"fax_number\": \"\", \"email\": \"\", \"website\": \"\", \"gst_id\": \"\", \"pax_number\": \"\", \"table_number\": \"\", \"cashier_name\": \"\", \"item_no_of_receipt_items\": [], \"item_code_of_receipt_items\": [], \"names_of_receipt_items\": [], \"quantities_of_receipt_items\": [], \"unit_prices_of_receipt_items\": [], \"gross_worth_of_receipt_items\": [], \"subtotal\": \"\", \"rounding_amount\": \"\", \"paid_amount\": \"\", \"change_amount\": \"\", \"service_charge_percent\": \"\", \"service_charge\": \"\", currency\": \"\", \"tax_percent\": \"\", \"tax_total\": \"\", \"total\": \"\", \"handwritting_content\": \"\"}. Note: Convert document_date to DD/MM/YYYY, Currency in Singapore Standard, All data value in string, Remove Line Break special character"},
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
            
            // OCR button is only enabled for images
            BtnOcr.IsEnabled = !_isPdf && _dashScopeService != null;
            
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
            // Show progress cursor
            Mouse.OverrideCursor = Cursors.Wait;
            BtnExtractData.IsEnabled = false;
            TxtExtractionResults.Text = "Processing...";
            
            // Get the selected document type
            ComboBoxItem selectedItem = (ComboBoxItem)CmbDocumentType.SelectedItem;
            string documentType = selectedItem.Content.ToString();
            
            // Get the appropriate prompt for the document type
            string prompt = PROMPTS[documentType];
            
            // Perform extraction with Qwen-VL-Max using the specific prompt
            string extractionResult = await _dashScopeService.PerformOcrAsync(_currentFilePath, prompt);
            
            // Display the results
            TxtExtractionResults.Text = extractionResult;
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

    // Copy Results button
    private void BtnCopyResults_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(TxtExtractionResults.Text))
        {
            Clipboard.SetText(TxtExtractionResults.Text);
            MessageBox.Show("Results copied to clipboard!", "Copy Complete", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    // Save Results button
    private void BtnSaveResults_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(TxtExtractionResults.Text))
        {
            MessageBox.Show("No results to save.", "Empty Results", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        SaveFileDialog saveFileDialog = new SaveFileDialog
        {
            Filter = "JSON Files (*.json)|*.json",
            Title = "Save Extraction Results",
            FileName = Path.GetFileNameWithoutExtension(_currentFilePath) + "_extraction.json"
        };

        if (saveFileDialog.ShowDialog() == true)
        {
            try
            {
                File.WriteAllText(saveFileDialog.FileName, TxtExtractionResults.Text);
                MessageBox.Show("Results saved successfully!", "Save Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving results: {ex.Message}", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
}