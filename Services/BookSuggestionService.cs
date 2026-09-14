using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace LibraryManagementSystem.Services
{
    public class BookSuggestionService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<BookSuggestionService> _logger;

        public BookSuggestionService(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<BookSuggestionService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        /// Asks the AI to pick the best-fit book IDs for this member based on their
        /// borrow/purchase history and the available (not-yet-owned) catalog.
        /// Returns an empty list if the AI call fails or returns nothing usable —
        /// caller should fall back to category-matching in that case.
        /// </summary>
        public async Task<List<int>> GetSuggestedBookIdsAsync(string historyText, string catalogText, int maxResults)
        {
            try
            {
                var apiKey = _configuration["Groq:ApiKey"];
                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    _logger.LogWarning("Groq API key is missing — skipping AI suggestions.");
                    return new List<int>();
                }

                var client = _httpClientFactory.CreateClient("Groq");
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

                var prompt = $@"You are a library book recommendation assistant.

A member has this borrowing/purchase history:
{historyText}

Here is the catalog of books they do NOT currently own, each shown as BookId: Title (Category):
{catalogText}

Pick up to {maxResults} BookIds from the catalog above that best match this member's reading interests, based on the categories and titles they've engaged with. Only choose BookIds that appear in the catalog list.

Respond with ONLY raw JSON, no markdown, no extra text, in this exact shape:
{{""bookIds"": [1, 2, 3]}}";

                var requestBody = new
                {
                    model = "openai/gpt-oss-20b",
                    messages = new[]
                    {
                        new { role = "user", content = prompt }
                    },
                    temperature = 0.3
                };

                var response = await client.PostAsJsonAsync("chat/completions", requestBody);

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("Groq API returned {StatusCode}: {Body}", response.StatusCode, errorBody);
                    return new List<int>();
                }

                var json = await response.Content.ReadFromJsonAsync<JsonElement>();

                var text = json
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString() ?? "";

                text = text.Replace("```json", "").Replace("```", "").Trim();

                using var doc = JsonDocument.Parse(text);
                var ids = doc.RootElement
                    .GetProperty("bookIds")
                    .EnumerateArray()
                    .Select(x => x.GetInt32())
                    .ToList();

                return ids;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AI suggestion call failed — falling back to category matching.");
                return new List<int>();
            }
        }
    }
}