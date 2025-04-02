# TrueDoc Desktop

A C# WPF application for truedoc.xyz that allows users to view, manage, and work with PDF and image files.

## Features

- Upload and view PDF documents and images
- Side-by-side layout with document preview on the left and tools on the right
- Text extraction using Qwen-VL models (Max, Plus, Chat)
- Customizable AI prompts for optimized text extraction
- Comprehensive debug logging system for troubleshooting
- Save documents to your local system
- Print documents directly from the application
- Automatic creation of processing folders to organize output files
- PDF operations: Convert to images, password protection, and digital signatures
- Image operations: Convert to PDF, increase DPI, and crop
- Drag-and-drop support for easy file loading

## Requirements

- Windows OS
- .NET 8.0 or higher
- Microsoft Edge WebView2 Runtime (for PDF viewing)
- DashScope API key (for Qwen-VL functionality)
- PdfiumViewer native dependencies (for PDF to image conversion)

## Getting Started

1. Clone this repository
2. Open the solution in Visual Studio
3. Build and run the application
4. Configure your DashScope API key in the Settings menu
5. Install PdfiumViewer native libraries (automatically installed via the "Install PDF Dependencies" option)

## Installing PdfiumViewer Dependencies

For PDF to image conversion to work properly, you need to install the native Pdfium libraries:

### Automatic Installation
1. Start the application
2. Click on the "Install PDF Dependencies" option in the top-right corner of the application
3. The application will automatically download and install the appropriate dependencies

### Manual Installation
1. Download the latest version of the Pdfium binary distribution from:
   - [Pdfium Binary Downloads](https://github.com/bblanchon/pdfium-binaries/releases)
   - Choose the appropriate package for your system (e.g., `pdfium-windows-x64.zip` for 64-bit Windows)

2. Extract the downloaded ZIP file

3. Copy the following DLL files to your application's bin directory:
   - `pdfium.dll` and all related DLLs in the package
   - Also copy them to the `Libraries` subfolder in the application directory

4. Alternatively, you can install PdfiumViewer via NuGet with native dependencies:
   ```
   Install-Package PdfiumViewer.Native.x86.v8-xfa
   ```
   or
   ```
   Install-Package PdfiumViewer.Native.x86_64.v8-xfa
   ```

## How to Use

1. Upload documents:
   - Click on "Upload PDF" or "Upload Image" to load a document
   - Or drag and drop files directly onto the application window

2. View the document in the preview panel on the left

3. Use the tools on the right panel to perform actions on the document

4. All processed files are automatically saved to a `<filename>_processing` folder in the same directory as the original file

### PDF Operations

- **Convert to Image**: Convert PDF pages to image files (JPEG or PNG) with selectable DPI
- **Set Password**: Add password protection to a PDF document
- **Remove Password**: Remove password protection from a PDF document
- **Sign Document**: Add a digital signature to a PDF document

### Image Operations

- **Convert to PDF**: Convert an image to a PDF document
- **Increase DPI**: Enhance image resolution by increasing its DPI
- **Crop Image**: Select and crop a portion of an image

### Common Actions

- **Save As**: Save a copy of the document
- **Print**: Print the document using the system's default print handler
- **Extract Data with AI**: Use Qwen-VL to extract text from images

## AI Text Extraction Functionality

The text extraction feature uses DashScope's Qwen-VL models to extract text from images. To use this feature:

1. Open the Settings menu and enter your DashScope API key
2. Configure your preferred model and prompts (see below)
3. Load an image document (JPG, PNG, etc.)
4. Click the "Extract Data with AI" button
5. View the extracted text in the AI Text Extraction Results section
6. Use the "Copy Text" button to copy text to clipboard

## Model and Prompt Configuration

You can customize the AI settings to optimize text extraction for different types of documents:

### Available Models

- **qwen-vl-max**: The most powerful model with highest accuracy (recommended for complex documents)
- **qwen-vl-plus**: Mid-tier model with good balance of performance and speed
- **qwen-vl-chat**: Lighter model optimized for real-time applications

### System Prompt

The system prompt sets the context for the AI. You can customize this to make the model focus on specific types of content:

- Default: "You are a helpful assistant that extracts text from images."
- For legal documents: "You are a legal document specialist that extracts text from legal documents with high accuracy."
- For tables: "You are an expert at extracting tabular data from images and preserving the table structure."

### User Prompt

The user prompt provides specific instructions for each image:

- Default: "Extract all text content from this image. Just return the extracted text without any additional commentary."
- For formatting: "Extract all text from this image, preserving the original formatting, paragraphs, and bullet points."
- For selective extraction: "Extract only the phone numbers and email addresses from this image."

To get a DashScope API key, visit [DashScope Platform](https://dashscope.aliyun.com/).

## Processing Folders

All processed files are automatically saved to a dedicated processing folder:

- The folder is named `<filename>_processing` and is created in the same directory as the original file
- This organization keeps your original files separate from processed versions
- Each operation (convert, crop, sign, etc.) saves its output to this folder with a descriptive prefix:
  - protected_filename.pdf
  - cropped_filename.jpg
  - signed_filename.pdf
  - etc.

## Debug and Logging

The application includes a comprehensive logging system that saves:

- API requests and responses
- Image processing details
- Error messages and troubleshooting information

Logs are stored in the "logs" folder in the application directory and are organized by date.

## Libraries Used

- Microsoft.Web.WebView2 - For PDF viewing
- System.Drawing.Common - For image handling
- WindowsAPICodePack-Shell - For enhanced file dialogs
- Newtonsoft.Json - For JSON serialization/deserialization
- System.Security.Cryptography.ProtectedData - For secure API key storage
- iTextSharp - For PDF manipulation
- PdfiumViewer - For PDF to image conversion

## Development

This application is built with WPF (Windows Presentation Foundation) using C# in the .NET 8 framework.

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Contact

For more information about TrueDoc, visit [truedoc.xyz](https://truedoc.xyz). 