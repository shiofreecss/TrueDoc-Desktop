using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Drawing;
using System.IO;
using System.Windows.Threading;
using System.Threading.Tasks;

namespace TrueDocDesktop.App.Dialogs
{
    /// <summary>
    /// Dialog for cropping images
    /// </summary>
    public partial class ImageCropDialog : Window
    {
        // Original image path
        private readonly string _imagePath;
        
        // Transformed image for display
        private TransformedBitmap _transformedBitmap;
        
        // Drag operation state
        private bool _isDragging = false;
        private System.Windows.Point _startPoint;
        private System.Windows.Rect _originalRect;
        private string _dragMode = string.Empty;
        
        // Scaling factors between UI and actual image
        private double _scaleX = 1.0;
        private double _scaleY = 1.0;
        
        // Original image size
        private int _originalWidth;
        private int _originalHeight;
        
        // The cropped result
        public Bitmap CroppedImage { get; private set; }

        public ImageCropDialog(string imagePath)
        {
            InitializeComponent();
            
            _imagePath = imagePath;
            Loaded += ImageCropDialog_Loaded;
        }

        private void ImageCropDialog_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Load the image
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(_imagePath);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                
                // Store original dimensions
                _originalWidth = bitmap.PixelWidth;
                _originalHeight = bitmap.PixelHeight;
                
                // Apply to image source
                ImageSource.Source = bitmap;
                
                // Handle image loaded and calculate scaling
                ImageSource.Loaded += (s, args) =>
                {
                    // Calculate the scaling factors
                    _scaleX = _originalWidth / ImageSource.ActualWidth;
                    _scaleY = _originalHeight / ImageSource.ActualHeight;
                    
                    // Set up canvas
                    CropCanvas.Width = ImageSource.ActualWidth;
                    CropCanvas.Height = ImageSource.ActualHeight;
                    
                    // Initialization delay to ensure UI is fully loaded
                    var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
                    timer.Tick += (_, _) =>
                    {
                        timer.Stop();
                        // Default initial selection to 50% of the image
                        double selWidth = ImageSource.ActualWidth * 0.5;
                        double selHeight = ImageSource.ActualHeight * 0.5;
                        
                        // Position in the center
                        double left = (ImageSource.ActualWidth - selWidth) / 2;
                        double top = (ImageSource.ActualHeight - selHeight) / 2;
                        
                        // Set the initial crop selection
                        SetCropSelection(left, top, selWidth, selHeight);
                    };
                    timer.Start();
                };
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading image: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                DialogResult = false;
                Close();
            }
        }

        private void SetCropSelection(double left, double top, double width, double height)
        {
            // Update crop selection rectangle
            Canvas.SetLeft(CropSelection, left);
            Canvas.SetTop(CropSelection, top);
            CropSelection.Width = width;
            CropSelection.Height = height;
            
            // Update the resize handles
            UpdateResizeHandles();
            
            // Update selection info text
            int selWidth = (int)(width * _scaleX);
            int selHeight = (int)(height * _scaleY);
            SelectedAreaText.Text = $"{selWidth} x {selHeight}";
            
            // Enable crop button if we have a valid selection
            BtnCrop.IsEnabled = width > 10 && height > 10;
        }

        private void UpdateResizeHandles()
        {
            double left = Canvas.GetLeft(CropSelection);
            double top = Canvas.GetTop(CropSelection);
            double right = left + CropSelection.Width;
            double bottom = top + CropSelection.Height;
            
            // Position the handles at the corners
            Canvas.SetLeft(TopLeftHandle, left - 5);
            Canvas.SetTop(TopLeftHandle, top - 5);
            
            Canvas.SetLeft(TopRightHandle, right - 5);
            Canvas.SetTop(TopRightHandle, top - 5);
            
            Canvas.SetLeft(BottomLeftHandle, left - 5);
            Canvas.SetTop(BottomLeftHandle, bottom - 5);
            
            Canvas.SetLeft(BottomRightHandle, right - 5);
            Canvas.SetTop(BottomRightHandle, bottom - 5);
            
            // Make handles visible
            TopLeftHandle.Visibility = Visibility.Visible;
            TopRightHandle.Visibility = Visibility.Visible;
            BottomLeftHandle.Visibility = Visibility.Visible;
            BottomRightHandle.Visibility = Visibility.Visible;
        }

        #region Mouse Event Handlers
        
        private void CropSelection_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isDragging = true;
            _startPoint = e.GetPosition(CropCanvas);
            _originalRect = new System.Windows.Rect(
                Canvas.GetLeft(CropSelection),
                Canvas.GetTop(CropSelection),
                CropSelection.Width,
                CropSelection.Height);
            _dragMode = "Move";
            CropSelection.CaptureMouse();
        }

        private void CropSelection_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging && _dragMode == "Move")
            {
                var currentPoint = e.GetPosition(CropCanvas);
                double deltaX = currentPoint.X - _startPoint.X;
                double deltaY = currentPoint.Y - _startPoint.Y;
                
                // Calculate new position
                double newLeft = _originalRect.Left + deltaX;
                double newTop = _originalRect.Top + deltaY;
                
                // Constrain to canvas bounds
                newLeft = Math.Max(0, Math.Min(newLeft, CropCanvas.ActualWidth - _originalRect.Width));
                newTop = Math.Max(0, Math.Min(newTop, CropCanvas.ActualHeight - _originalRect.Height));
                
                // Update position
                Canvas.SetLeft(CropSelection, newLeft);
                Canvas.SetTop(CropSelection, newTop);
                
                // Update handles
                UpdateResizeHandles();
                
                // No need to update size info as it hasn't changed
            }
        }

        private void CropSelection_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                _dragMode = string.Empty;
                CropSelection.ReleaseMouseCapture();
            }
        }

        private void ResizeHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isDragging = true;
            _startPoint = e.GetPosition(CropCanvas);
            _originalRect = new System.Windows.Rect(
                Canvas.GetLeft(CropSelection),
                Canvas.GetTop(CropSelection),
                CropSelection.Width,
                CropSelection.Height);
            
            // Determine which handle is being dragged
            var handle = sender as System.Windows.Shapes.Rectangle;
            if (handle == TopLeftHandle) _dragMode = "TopLeft";
            else if (handle == TopRightHandle) _dragMode = "TopRight";
            else if (handle == BottomLeftHandle) _dragMode = "BottomLeft";
            else if (handle == BottomRightHandle) _dragMode = "BottomRight";
            
            handle.CaptureMouse();
        }

        private void ResizeHandle_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging && !string.IsNullOrEmpty(_dragMode))
            {
                var currentPoint = e.GetPosition(CropCanvas);
                double deltaX = currentPoint.X - _startPoint.X;
                double deltaY = currentPoint.Y - _startPoint.Y;
                
                double newLeft = _originalRect.Left;
                double newTop = _originalRect.Top;
                double newWidth = _originalRect.Width;
                double newHeight = _originalRect.Height;
                
                // Handle resize based on which corner is being dragged
                switch (_dragMode)
                {
                    case "TopLeft":
                        newLeft = Math.Min(_originalRect.Right - 10, _originalRect.Left + deltaX);
                        newTop = Math.Min(_originalRect.Bottom - 10, _originalRect.Top + deltaY);
                        newWidth = _originalRect.Right - newLeft;
                        newHeight = _originalRect.Bottom - newTop;
                        break;
                    
                    case "TopRight":
                        newTop = Math.Min(_originalRect.Bottom - 10, _originalRect.Top + deltaY);
                        newWidth = Math.Max(10, _originalRect.Width + deltaX);
                        newHeight = _originalRect.Bottom - newTop;
                        break;
                    
                    case "BottomLeft":
                        newLeft = Math.Min(_originalRect.Right - 10, _originalRect.Left + deltaX);
                        newWidth = _originalRect.Right - newLeft;
                        newHeight = Math.Max(10, _originalRect.Height + deltaY);
                        break;
                    
                    case "BottomRight":
                        newWidth = Math.Max(10, _originalRect.Width + deltaX);
                        newHeight = Math.Max(10, _originalRect.Height + deltaY);
                        break;
                }
                
                // Constrain to canvas bounds
                if (newLeft < 0) newLeft = 0;
                if (newTop < 0) newTop = 0;
                if (newLeft + newWidth > CropCanvas.ActualWidth) newWidth = CropCanvas.ActualWidth - newLeft;
                if (newTop + newHeight > CropCanvas.ActualHeight) newHeight = CropCanvas.ActualHeight - newTop;
                
                // Update the crop selection
                SetCropSelection(newLeft, newTop, newWidth, newHeight);
            }
        }

        private void ResizeHandle_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                _dragMode = string.Empty;
                (sender as System.Windows.Shapes.Rectangle).ReleaseMouseCapture();
            }
        }
        
        #endregion

        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            // Reset to default selection (50% of image, centered)
            double selWidth = ImageSource.ActualWidth * 0.5;
            double selHeight = ImageSource.ActualHeight * 0.5;
            double left = (ImageSource.ActualWidth - selWidth) / 2;
            double top = (ImageSource.ActualHeight - selHeight) / 2;
            
            SetCropSelection(left, top, selWidth, selHeight);
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void BtnCrop_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;
                
                // Get the crop selection coordinates
                double left = Canvas.GetLeft(CropSelection);
                double top = Canvas.GetTop(CropSelection);
                double width = CropSelection.Width;
                double height = CropSelection.Height;
                
                // Convert to actual image coordinates
                int x = (int)(left * _scaleX);
                int y = (int)(top * _scaleY);
                int w = (int)(width * _scaleX);
                int h = (int)(height * _scaleY);
                
                // Ensure coordinates are valid
                x = Math.Max(0, Math.Min(x, _originalWidth - 1));
                y = Math.Max(0, Math.Min(y, _originalHeight - 1));
                w = Math.Max(1, Math.Min(w, _originalWidth - x));
                h = Math.Max(1, Math.Min(h, _originalHeight - y));
                
                // Create the crop rectangle
                System.Drawing.Rectangle cropRect = new System.Drawing.Rectangle(x, y, w, h);
                
                // Load the original image for cropping
                using (var originalImage = System.Drawing.Image.FromFile(_imagePath))
                {
                    // Create bitmap for the cropped image
                    CroppedImage = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                    
                    // Draw the cropped portion with higher quality
                    using (var g = Graphics.FromImage(CroppedImage))
                    {
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                        g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                        g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                        
                        // Clear background to white if the image doesn't support transparency
                        g.Clear(System.Drawing.Color.White);
                        
                        g.DrawImage(originalImage, new System.Drawing.Rectangle(0, 0, w, h), cropRect, GraphicsUnit.Pixel);
                    }
                    
                    // Create a temporary path to save and reload the image
                    // This ensures that the image data is properly initialized
                    string tempPath = Path.Combine(Path.GetTempPath(), $"temp_crop_{Guid.NewGuid()}.png");
                    
                    try
                    {
                        // Save to temporary file
                        CroppedImage.Save(tempPath, System.Drawing.Imaging.ImageFormat.Png);
                        
                        // Dispose the current cropped image
                        CroppedImage.Dispose();
                        
                        // Reload from file to ensure complete initialization
                        CroppedImage = (Bitmap)System.Drawing.Image.FromFile(tempPath);
                        
                        // Set result and close
                        DialogResult = true;
                        
                        // Clean up temporary file on a background thread
                        Task.Run(() => {
                            try
                            {
                                if (File.Exists(tempPath))
                                    File.Delete(tempPath);
                            }
                            catch { /* Ignore cleanup errors */ }
                        });
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error saving temporary cropped image: {ex.Message}", "Crop Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        if (CroppedImage != null)
                        {
                            CroppedImage.Dispose();
                            CroppedImage = null;
                        }
                        DialogResult = false;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error cropping image: {ex.Message}", "Crop Error", MessageBoxButton.OK, MessageBoxImage.Error);
                DialogResult = false;
            }
            finally
            {
                Mouse.OverrideCursor = null;
                Close();
            }
        }
    }
} 