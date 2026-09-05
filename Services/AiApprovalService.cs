using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace LibraryManagementSystem.Services
{
    public class AiApprovalService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AiApprovalService> _logger;

        public AiApprovalService(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<AiApprovalService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
        }

        public class ApprovalContext
        {
            public int ActiveBorrowingsCount { get; set; }
            public bool HasUnpaidFines { get; set; }
            public bool HasOverdueBook { get; set; }
            public string BookTitle { get; set; } = "";
        }

        public class ApprovalDecision
        {
            public bool Approve { get; set; }
            public string Reason { get; set; } = "";
        }

        public async Task<ApprovalDecision> DecideAsync(ApprovalContext context)
        {
            try
            {
                var apiKey = _configuration["Groq:ApiKey"];

                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    _logger.LogWarning("Groq API key is missing.");
                    return new ApprovalDecision
                    {
                        Approve = false,
                        Reason = "AI service is not configured — sent for manual review."
                    };
                }

                var client = _httpClientFactory.CreateClient("Groq");

                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", apiKey);

                var prompt = $@"You are a library borrowing approval assistant.
Decide whether to auto-approve this book borrow request based on library policy.

Policy: Approve ONLY if the member has fewer than 5 active borrowings,
has no unpaid fines, and has no overdue books.

Member data:
- Active borrowings: {context.ActiveBorrowingsCount}
- Has unpaid fines: {context.HasUnpaidFines}
- Has an overdue book: {context.HasOverdueBook}
- Requested book: {context.BookTitle}

Respond with ONLY raw JSON, no markdown, no extra text, in this exact shape:
{{""approve"": true or false, ""reason"": ""short one-sentence reason""}}";

                var requestBody = new
                {
                    model = "openai/gpt-oss-20b",
                    messages = new[]
                    {
                        new
                        {
                            role = "user",
                            content = prompt
                        }
                    },
                    temperature = 0.1
                };

                var response = await client.PostAsJsonAsync(
                    "chat/completions",
                    requestBody);

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync();

                    _logger.LogWarning(
                        "Groq API returned {StatusCode}: {Body}",
                        response.StatusCode,
                        errorBody);
                }

                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadFromJsonAsync<JsonElement>();

                var text = json
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString() ?? "";

                text = text
                    .Replace("```json", "")
                    .Replace("```", "")
                    .Trim();

                var parsed = JsonSerializer.Deserialize<ApprovalDecision>(
                    text,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                return parsed ?? new ApprovalDecision
                {
                    Approve = false,
                    Reason = "Could not parse AI response — sent for manual review."
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "AI approval call failed — falling back to manual review.");

                return new ApprovalDecision
                {
                    Approve = false,
                    Reason = "AI service unavailable — sent for manual review."
                };
            }
        }
    }
}

