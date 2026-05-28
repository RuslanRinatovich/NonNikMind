using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace MindKeeper.Services
{
    public class GeminiService : IAiService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private string _activeModelName;

        public GeminiService(string apiKey)
        {
            _apiKey = apiKey;
            _httpClient = new HttpClient();
        }


        private async Task<string> SendPromptAsync(string prompt)
        {
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={_apiKey}";

            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = prompt }
                        }
                    }
                }
            };

            var json = Newtonsoft.Json.JsonConvert.SerializeObject(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PostAsync(url, content);
                var responseString = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Gemini API error: {response.StatusCode} - {responseString}");
                }

                var jsonResponse = JObject.Parse(responseString);
                var result = jsonResponse["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.ToString();

                return result ?? string.Empty;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Ошибка при обращении к Gemini: {ex.Message}", "Ошибка");
                return string.Empty;
            }
        }

        public async Task<string> GenerateTagsAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            var prompt = $"Извлеки из следующего текста ключевые слова (теги). Ответь только списком слов, разделённых запятыми. Не добавляй нумерацию, пояснения, только слова.\n\nТекст: {text}";
            var result = await SendPromptAsync(prompt);
            return result;
        }

        public async Task<string> GenerateSummaryAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            var prompt = $"Сделай краткий конспект (2-3 предложения) следующего текста:\n\n{text}";
            var result = await SendPromptAsync(prompt);
            return result;
        }
    }
}