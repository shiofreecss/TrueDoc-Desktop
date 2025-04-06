using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.IO;
using System.Diagnostics;
using System.Windows;
using System.Reflection;
using System.Collections.Generic;

namespace TrueDocDesktop.App.Services
{
    public class DashScopeService
    {
        private readonly string _apiKey;
        private static readonly HttpClient _httpClient = new HttpClient();
        private const string ApiBaseUrl = "https://dashscope.aliyuncs.com/compatible-mode/v1/chat/completions";
        private const int TimeoutSeconds = 120; // 120-second timeout
        private bool _debug = true; // Enable debug mode
        private readonly string _appDirectory;
        private readonly string _logsDirectory;

        // Default values for prompts and model
        public string ModelName { get; set; } = "qwen-vl-max";
        public string SystemContent { get; set; } = "You are a helpful assistant that extracts text from images.";
        public string UserContent { get; set; } = "Extract all text content from this image. Just return the extracted text without any additional commentary.";

        // Available model options for UI
        public static readonly string[] AvailableModels = new string[]
        {
            "qwen-vl-max",
            "qwen-vl-plus",
            "qwen-vl-chat"
        };

        public DashScopeService(string apiKey)
        {
            _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
            // Set default timeout for all requests
            _httpClient.Timeout = TimeSpan.FromSeconds(TimeoutSeconds);
            
            // Get application directory
            _appDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            _logsDirectory = Path.Combine(_appDirectory, "logs");
            
            // Ensure logs directory exists
            try 
            {
                if (!Directory.Exists(_logsDirectory))
                {
                    Directory.CreateDirectory(_logsDirectory);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to create logs directory: {ex.Message}");
            }
            
            LogDebug($"DashScopeService initialized with API key: {MaskApiKey(apiKey)}");
            LogDebug($"Logs directory: {_logsDirectory}");
        }

        private string MaskApiKey(string apiKey)
        {
            if (string.IsNullOrEmpty(apiKey)) return "null";
            if (apiKey.Length <= 8) return "****";
            return apiKey.Substring(0, 4) + "..." + apiKey.Substring(apiKey.Length - 4);
        }

        private void LogDebug(string message)
        {
            try
            {
                // Ensure the logs directory exists
                if (!Directory.Exists(_logsDirectory))
                {
                    Directory.CreateDirectory(_logsDirectory);
                }
                
                // Build the log file path
                string logFileName = $"dashscope_log_{DateTime.Now:yyyyMMdd}.txt";
                string logPath = Path.Combine(_logsDirectory, logFileName);
                
                // Append to the log file
                using (StreamWriter writer = File.AppendText(logPath))
                {
                    writer.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}");
                }
            }
            catch
            {
                // Silently fail if logging fails
            }
        }

        public async Task<string> PerformOcrAsync(string imagePath)
        {
            return await PerformOcrAsync(imagePath, ModelName, SystemContent, UserContent);
        }

        public async Task<string> PerformOcrAsync(string imagePath, string customPrompt)
        {
            return await PerformOcrAsync(imagePath, ModelName, SystemContent, customPrompt);
        }

        public async Task<string> PerformOcrAsync(string imagePath, string modelName, string systemContent, string userContent)
        {
            try
            {
                LogDebug($"Starting OCR for image: {imagePath}");
                LogDebug($"Using model: {modelName}");
                LogDebug($"System content: {systemContent}");
                LogDebug($"User content: {userContent}");
                
                if (!File.Exists(imagePath))
                {
                    LogDebug("Image file not found");
                    return "Error: Image file not found.";
                }

                byte[] imageBytes = File.ReadAllBytes(imagePath);
                LogDebug($"Image size: {imageBytes.Length / 1024} KB");
                
                if (imageBytes.Length > 10 * 1024 * 1024) // 10MB limit
                {
                    LogDebug("Image too large (>10MB)");
                    return "Error: Image file is too large. Please use an image smaller than 10MB.";
                }
                
                string base64Image = Convert.ToBase64String(imageBytes);
                LogDebug($"Base64 image length: {base64Image.Length} chars");
                string imageUrl = $"data:image/jpeg;base64,{base64Image}";

                // Write the exact request to debug log
                string jsonContent = $@"{{
                    ""model"": ""{modelName}"",
                    ""messages"": [
                        {{
                            ""role"": ""system"",
                            ""content"": ""{EscapeJsonString(systemContent)}""
                        }},
                        {{
                            ""role"": ""user"",
                            ""content"": [
                                {{
                                    ""type"": ""image_url"",
                                    ""image_url"": {{
                                        ""url"": ""{imageUrl}""
                                    }}
                                }},
                                {{
                                    ""type"": ""text"",
                                    ""text"": ""{EscapeJsonString(userContent)}""
                                }}
                            ]
                        }}
                    ]
                }}";

                // Log a truncated version of the request for debugging
                LogDebug($"API URL: {ApiBaseUrl}");
                LogDebug($"Request content (truncated): {jsonContent.Substring(0, Math.Min(500, jsonContent.Length))}...");

                // Save the full request to a file for comparison with Postman
                try 
                {
                    string requestFileName = $"last_request_{DateTime.Now:yyyyMMdd_HHmmss}.json";
                    string requestPath = Path.Combine(_logsDirectory, requestFileName);
                    
                    File.WriteAllText(requestPath, jsonContent);
                    LogDebug($"Full request saved to: {requestPath}");
                }
                catch (Exception ex)
                {
                    LogDebug($"Failed to save request: {ex.Message}");
                }

                LogDebug("Sending request to DashScope API...");
                string result = await SendPostRequestAsync(ApiBaseUrl, jsonContent, _apiKey);
                
                // Save the response to a file for inspection
                try
                {
                    string responseFileName = $"last_response_{DateTime.Now:yyyyMMdd_HHmmss}.json";
                    string responsePath = Path.Combine(_logsDirectory, responseFileName);
                    
                    File.WriteAllText(responsePath, result);
                    LogDebug($"Full response saved to: {responsePath}");
                }
                catch (Exception ex)
                {
                    LogDebug($"Failed to save response: {ex.Message}");
                }
                
                LogDebug($"Response received (truncated): {result.Substring(0, Math.Min(500, result.Length))}...");
                
                try 
                {
                    var compatibleResponse = JsonConvert.DeserializeObject<CompatibleApiResponse>(result);
                    if (compatibleResponse?.Choices != null && compatibleResponse.Choices.Length > 0)
                    {
                        LogDebug("Successfully parsed response");
                        return compatibleResponse.Choices[0]?.Message?.Content ?? "No text detected.";
                    }
                    else
                    {
                        LogDebug("Response parsing successful but no content found");
                        return "No text detected.";
                    }
                }
                catch (Exception ex)
                {
                    LogDebug($"Failed to parse response: {ex.Message}");
                    
                    if (result.Contains("error"))
                    {
                        try
                        {
                            var errorResponse = JsonConvert.DeserializeObject<ErrorResponse>(result);
                            LogDebug($"Error response parsed: {errorResponse?.Error?.Message}");
                            return $"Error from DashScope API: {errorResponse?.Error?.Message}";
                        }
                        catch (Exception parseEx)
                        {
                            LogDebug($"Failed to parse error response: {parseEx.Message}");
                            return $"Error from DashScope API: {result}";
                        }
                    }
                    return $"Error parsing API response: {ex.Message}";
                }
            }
            catch (TaskCanceledException)
            {
                LogDebug("Request timed out after 120 seconds");
                return "Error: Request timed out after 120 seconds. Please try with a smaller image or try again later.";
            }
            catch (Exception ex)
            {
                LogDebug($"General error: {ex.GetType().Name}: {ex.Message}");
                return $"Error performing OCR: {ex.Message}";
            }
        }

        // Helper method to escape special characters in JSON strings
        private string EscapeJsonString(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return string.Empty;
            }

            s = s.Replace("\\", "\\\\");
            s = s.Replace("\"", "\\\"");
            s = s.Replace("\n", "\\n");
            s = s.Replace("\r", "\\r");
            s = s.Replace("\t", "\\t");
            s = s.Replace("\b", "\\b");
            s = s.Replace("\f", "\\f");
            
            return s;
        }

        // Method to translate text to different languages
        public async Task<string> TranslateTextAsync(string textToTranslate, string targetLanguage)
        {
            try
            {
                LogDebug($"Starting translation to {targetLanguage}");
                
                if (string.IsNullOrEmpty(textToTranslate))
                {
                    LogDebug("No text to translate");
                    return "Error: No text to translate.";
                }
                
                // Use qwen-chat as the default model for translation (text-only model)
                string modelName = "qwen-max";
                
                // Create system prompt for translation
                string systemContent = $"You are a helpful translation assistant. Translate the provided text to {targetLanguage}.";
                
                // Create user prompt
                string userContent = $"Translate the following text to {targetLanguage}. Maintain the original formatting and structure.\n\n{textToTranslate}";
                
                // Create the request JSON
                string jsonContent = $@"{{
                    ""model"": ""{modelName}"",
                    ""messages"": [
                        {{
                            ""role"": ""system"",
                            ""content"": ""{EscapeJsonString(systemContent)}""
                        }},
                        {{
                            ""role"": ""user"",
                            ""content"": ""{EscapeJsonString(userContent)}""
                        }}
                    ]
                }}";
                
                // Log a truncated version of the request for debugging
                LogDebug($"API URL: {ApiBaseUrl}");
                LogDebug($"Translation request content (truncated): {jsonContent.Substring(0, Math.Min(500, jsonContent.Length))}...");
                
                // Save the full request to a file for comparison with Postman
                try 
                {
                    string requestFileName = $"translation_request_{DateTime.Now:yyyyMMdd_HHmmss}.json";
                    string requestPath = Path.Combine(_logsDirectory, requestFileName);
                    
                    File.WriteAllText(requestPath, jsonContent);
                    LogDebug($"Full translation request saved to: {requestPath}");
                }
                catch (Exception ex)
                {
                    LogDebug($"Failed to save translation request: {ex.Message}");
                }
                
                LogDebug("Sending translation request to DashScope API...");
                string result = await SendPostRequestAsync(ApiBaseUrl, jsonContent, _apiKey);
                
                // Save the response to a file for inspection
                try
                {
                    string responseFileName = $"translation_response_{DateTime.Now:yyyyMMdd_HHmmss}.json";
                    string responsePath = Path.Combine(_logsDirectory, responseFileName);
                    
                    File.WriteAllText(responsePath, result);
                    LogDebug($"Full translation response saved to: {responsePath}");
                }
                catch (Exception ex)
                {
                    LogDebug($"Failed to save translation response: {ex.Message}");
                }
                
                LogDebug($"Translation response received (truncated): {result.Substring(0, Math.Min(500, result.Length))}...");
                
                try 
                {
                    var compatibleResponse = JsonConvert.DeserializeObject<CompatibleApiResponse>(result);
                    if (compatibleResponse?.Choices != null && compatibleResponse.Choices.Length > 0)
                    {
                        LogDebug("Successfully parsed translation response");
                        return compatibleResponse.Choices[0]?.Message?.Content ?? "Translation failed.";
                    }
                    else
                    {
                        LogDebug("Response parsing successful but no content found");
                        return "Translation failed: No content returned.";
                    }
                }
                catch (Exception ex)
                {
                    LogDebug($"Failed to parse translation response: {ex.Message}");
                    
                    if (result.Contains("error"))
                    {
                        try
                        {
                            var errorResponse = JsonConvert.DeserializeObject<ErrorResponse>(result);
                            LogDebug($"Error response parsed: {errorResponse?.Error?.Message}");
                            return $"Error from DashScope API: {errorResponse?.Error?.Message}";
                        }
                        catch (Exception parseEx)
                        {
                            LogDebug($"Failed to parse error response: {parseEx.Message}");
                            return $"Error from DashScope API: {result}";
                        }
                    }
                    return $"Error parsing API response: {ex.Message}";
                }
            }
            catch (TaskCanceledException)
            {
                LogDebug("Translation request timed out after 120 seconds");
                return "Error: Translation request timed out after 120 seconds. Please try with shorter text or try again later.";
            }
            catch (Exception ex)
            {
                LogDebug($"General translation error: {ex.GetType().Name}: {ex.Message}");
                return $"Error performing translation: {ex.Message}";
            }
        }

        // Method to translate text to multiple languages at once
        public async Task<Dictionary<string, string>> TranslateTextToMultipleLanguagesAsync(string textToTranslate, List<string> targetLanguages)
        {
            var results = new Dictionary<string, string>();
            
            LogDebug($"Starting translation to multiple languages: {string.Join(", ", targetLanguages)}");
            
            // Process each language sequentially to avoid API rate limiting
            foreach (var language in targetLanguages)
            {
                LogDebug($"Translating to {language}...");
                string translatedText = await TranslateTextAsync(textToTranslate, language);
                results.Add(language, translatedText);
                
                // Small delay between requests to avoid DashScope API rate limits
                await Task.Delay(500);
            }
            
            return results;
        }

        private async Task<string> SendPostRequestAsync(string url, string jsonContent, string apiKey)
        {
            try
            {
                using (var content = new StringContent(jsonContent, Encoding.UTF8, "application/json"))
                {
                    // Clear and set headers for each request to avoid conflicts
                    _httpClient.DefaultRequestHeaders.Clear();
                    _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                    _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    // Log all request headers for debugging
                    LogDebug("Request headers:");
                    foreach (var header in _httpClient.DefaultRequestHeaders)
                    {
                        LogDebug($"  {header.Key}: {string.Join(", ", header.Value)}");
                    }

                    // Send request and get response
                    LogDebug("Sending HTTP request...");
                    HttpResponseMessage response = await _httpClient.PostAsync(url, content);
                    LogDebug($"Response status code: {(int)response.StatusCode} ({response.StatusCode})");

                    // Log response headers for debugging
                    LogDebug("Response headers:");
                    foreach (var header in response.Headers)
                    {
                        LogDebug($"  {header.Key}: {string.Join(", ", header.Value)}");
                    }

                    // Read response content
                    string responseContent = await response.Content.ReadAsStringAsync();
                    LogDebug($"Response length: {responseContent.Length} chars");

                    // Handle status code
                    if (response.IsSuccessStatusCode)
                    {
                        LogDebug("Request successful");
                        return responseContent;
                    }
                    else
                    {
                        LogDebug($"Request failed: {response.StatusCode} - {responseContent}");
                        return $"{{\"error\": {{\"message\": \"Request failed: {response.StatusCode} - {responseContent}\"}}, \"status_code\": \"{(int)response.StatusCode}\"}}";
                    }
                }
            }
            catch (Exception ex)
            {
                LogDebug($"Exception in SendPostRequestAsync: {ex.GetType().Name}: {ex.Message}");
                throw; // Re-throw to be handled by caller
            }
        }

        private class CompatibleApiResponse
        {
            [JsonProperty("choices")]
            public ChoiceData[]? Choices { get; set; }

            [JsonProperty("id")]
            public string? Id { get; set; }

            [JsonProperty("model")]
            public string? Model { get; set; }

            public class ChoiceData
            {
                [JsonProperty("message")]
                public MessageData? Message { get; set; }

                [JsonProperty("finish_reason")]
                public string? FinishReason { get; set; }

                [JsonProperty("index")]
                public int Index { get; set; }

                public class MessageData
                {
                    [JsonProperty("content")]
                    public string? Content { get; set; }

                    [JsonProperty("role")]
                    public string? Role { get; set; }
                }
            }
        }

        private class ErrorResponse
        {
            [JsonProperty("error")]
            public ErrorData? Error { get; set; }

            [JsonProperty("status_code")]
            public string? StatusCode { get; set; }

            public class ErrorData
            {
                [JsonProperty("message")]
                public string? Message { get; set; }
            }
        }
    }
} 