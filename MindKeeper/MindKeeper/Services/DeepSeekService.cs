using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace MindKeeper.Services
{
    public class DeepSeekService : IAiService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private const string ApiUrl = "https://api.deepseek.com/v1/chat/completions";

        public DeepSeekService(string apiKey)
        {
            _apiKey = apiKey;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
        }

        public async Task<string> GenerateTagsAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            var prompt = "Извлеки ключевые слова из следующего текста. Ответь только списком слов, разделённых запятыми. Не добавляй нумерацию, пояснения, только слова.\n\nТекст: " + text;

            return await SendChatRequestAsync(prompt);
        }

        public async Task<string> GenerateSummaryAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            var prompt = $"Сделай краткий конспект (2-3 предложения) следующего текста:\n\n{text}";

            return await SendChatRequestAsync(prompt);
        }

        private async Task<string> SendChatRequestAsync(string prompt)
        {
            try
            {
                var requestBody = new
                {
                    model = "deepseek-chat",
                    messages = new[]
                    {
                        new { role = "user", content = prompt }
                    },
                    max_tokens = 500,
                    temperature = 0.7
                };

                var json = Newtonsoft.Json.JsonConvert.SerializeObject(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(ApiUrl, content);
                var responseString = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"DeepSeek API error: {response.StatusCode} - {responseString}");
                }

                var jsonResponse = JObject.Parse(responseString);
                var result = jsonResponse["choices"]?[0]?["message"]?["content"]?.ToString();

                return result?.Trim() ?? string.Empty;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Ошибка при обращении к DeepSeek: {ex.Message}", "Ошибка");
                return string.Empty;
            }
        }
    }
}