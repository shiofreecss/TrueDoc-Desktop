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

## Requirements

- Windows OS
- .NET 8.0 or higher
- Microsoft Edge WebView2 Runtime (for PDF viewing)
- DashScope API key (for Qwen-VL functionality)

## Getting Started

1. Clone this repository
2. Open the solution in Visual Studio
3. Build and run the application
4. Configure your DashScope API key in the Settings menu

## How to Use

1. Click on "Upload PDF" or "Upload Image" to load a document
2. View the document in the preview panel on the left
3. Use the tools on the right panel to perform actions on the document
4. For images, click "OCR with Qwen-VL" to extract text content

## AI Text Extraction Functionality

The text extraction feature uses DashScope's Qwen-VL models to extract text from images. To use this feature:

1. Open the Settings menu and enter your DashScope API key
2. Configure your preferred model and prompts (see below)
3. Load an image document (JPG, PNG, etc.)
4. Click the "OCR with Qwen-VL" button
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

## Development

This application is built with WPF (Windows Presentation Foundation) using C# in the .NET 8 framework.

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Contact

For more information about TrueDoc, visit [truedoc.xyz](https://truedoc.xyz). 