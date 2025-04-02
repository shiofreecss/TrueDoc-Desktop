# Native Libraries Directory

This directory contains native libraries required by the TrueDocDesktop application.

## Required Libraries

* **pdfium.dll** - Native library for PDF rendering and manipulation 

## Installation

These libraries will be automatically installed when you select the "Install PDF Dependencies" option from the application menu.

Alternatively, you can manually download the appropriate libraries from:
https://github.com/bblanchon/pdfium-binaries/releases

Choose the appropriate architecture (x64 or x86) based on your system.

## Troubleshooting

If you're experiencing issues with PDF functionality, ensure that the native libraries are properly installed in this directory.

The application will automatically check for these dependencies on startup and will prompt you to install them if they are missing. 