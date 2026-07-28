using LibraryManagementSystem.Data;
using LibraryManagementSystem.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;

namespace LibraryManagementSystem.Controllers
{
    public class ChatController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        public ChatController(ApplicationDbContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Ask([FromBody] ChatMessageInput input)
        {
            if (string.IsNullOrWhiteSpace(input.Message))
            {
                return Json(new { reply = "Please type a question." });
            }

            // Pull a small snapshot of book data to give the AI real context
            var books = await _context.Books
                .Include(b => b.Author)
                .Include(b => b.Category)
                .Select(b => new { b.Title, Author = b.Author.Name, Category = b.Category.Name, b.AvailableCopies })
                .Take(50)
                .ToListAsync();

            var bookListText = string.Join("\n", books.Select(b =>
                $"- \"{b.Title}\" by {b.Author} ({b.Category}) - {b.AvailableCopies} copies available"));

            var systemPrompt =
                "You are a helpful library assistant for a university library system. " +
                "Only answer questions about the library, books, borrowing, fines, and how to use this system. " +
                "If asked something unrelated, politely say you can only help with library-related questions. " +
                "Here is the current book catalog you can reference:\n" + bookListText;

            var apiKey = _configuration["Groq:ApiKey"];
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

            var requestBody = new
            {
                model = "llama-3.1-8b-instant",
                messages = new object[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = input.Message }
                }
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                var response = await client.PostAsync("https://api.groq.com/openai/v1/chat/completions", content);
                var responseText = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return Json(new { reply = "Sorry, the AI assistant is unavailable right now." });
                }

                using var doc = JsonDocument.Parse(responseText);
                var reply = doc.RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString();

                return Json(new { reply });
            }
            catch (Exception)
            {
                return Json(new { reply = "Sorry, something went wrong contacting the AI assistant." });
            }
        }
    }
}