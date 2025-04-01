using System;
using System.IO;
using Newtonsoft.Json;
using System.Security.Cryptography;
using System.Text;

namespace TrueDocDesktop.App
{
    public class AppSettings
    {
        private static readonly string SettingsFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "TrueDocDesktop",
            "settings.json");

        private static readonly byte[] EntropyBytes = Encoding.UTF8.GetBytes("TrueDocDesktopSettings");

        public string DashScopeApiKey { get; set; }
        public string ModelName { get; set; }
        public string SystemPrompt { get; set; }
        public string UserPrompt { get; set; }

        private AppSettings()
        {
            DashScopeApiKey = string.Empty;
            ModelName = "qwen-vl-max";
            SystemPrompt = "You are a helpful assistant that extracts text from images.";
            UserPrompt = "Extract all text content from this image. Just return the extracted text without any additional commentary.";
        }

        public static AppSettings Load()
        {
            try
            {
                if (!File.Exists(SettingsFilePath))
                {
                    return new AppSettings();
                }

                string encryptedJson = File.ReadAllText(SettingsFilePath);
                
                // Decrypt the settings if they exist
                if (!string.IsNullOrEmpty(encryptedJson))
                {
                    byte[] encryptedBytes = Convert.FromBase64String(encryptedJson);
                    byte[] decryptedBytes = ProtectedData.Unprotect(encryptedBytes, EntropyBytes, DataProtectionScope.CurrentUser);
                    string decryptedJson = Encoding.UTF8.GetString(decryptedBytes);
                    
                    return JsonConvert.DeserializeObject<AppSettings>(decryptedJson) ?? new AppSettings();
                }
            }
            catch (Exception)
            {
                // If there's an error, return default settings
            }

            return new AppSettings();
        }

        public void Save()
        {
            try
            {
                string? directory = Path.GetDirectoryName(SettingsFilePath);
                if (directory != null && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string json = JsonConvert.SerializeObject(this);
                
                // Encrypt the settings before saving
                byte[] plainBytes = Encoding.UTF8.GetBytes(json);
                byte[] encryptedBytes = ProtectedData.Protect(plainBytes, EntropyBytes, DataProtectionScope.CurrentUser);
                string encryptedJson = Convert.ToBase64String(encryptedBytes);
                
                File.WriteAllText(SettingsFilePath, encryptedJson);
            }
            catch (Exception)
            {
                // Log or handle the error
            }
        }
    }
} 