using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;
using TrueDocDesktop.App.Services;

namespace TrueDocDesktop.App
{
    public partial class SettingsWindow : Window
    {
        private readonly AppSettings _settings;

        public SettingsWindow(AppSettings settings)
        {
            InitializeComponent();
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));

            // Load available models into combobox
            foreach (var model in DashScopeService.AvailableModels)
            {
                CmbModelName.Items.Add(model);
            }

            // Load existing settings
            TxtApiKey.Password = _settings.DashScopeApiKey;
            CmbModelName.SelectedItem = _settings.ModelName;
            TxtSystemPrompt.Text = _settings.SystemPrompt;
            TxtUserPrompt.Text = _settings.UserPrompt;

            // If model isn't in the list, select the first one
            if (CmbModelName.SelectedItem == null && CmbModelName.Items.Count > 0)
            {
                CmbModelName.SelectedIndex = 0;
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            // Save settings
            _settings.DashScopeApiKey = TxtApiKey.Password;
            _settings.ModelName = CmbModelName.SelectedItem?.ToString() ?? "qwen-vl-max";
            _settings.SystemPrompt = TxtSystemPrompt.Text;
            _settings.UserPrompt = TxtUserPrompt.Text;
            _settings.Save();

            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            // Reset to defaults
            CmbModelName.SelectedItem = "qwen-vl-max";
            TxtSystemPrompt.Text = "You are a helpful assistant that extracts text from images.";
            TxtUserPrompt.Text = "Extract all text content from this image. Just return the extracted text without any additional commentary.";
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            // Open the URL in the default browser
            Process.Start(new ProcessStartInfo
            {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true
            });
            e.Handled = true;
        }
    }
} 