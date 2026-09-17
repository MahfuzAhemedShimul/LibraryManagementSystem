using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using LibraryManagementSystem.Models;
using LibraryManagementSystem.Data;
using LibraryManagementSystem.Services;

namespace LibraryManagementSystem.Controllers
{
    public class BooksController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly BookSuggestionService _bookSuggestionService;

        public BooksController(
            ApplicationDbContext context,
            IWebHostEnvironment webHostEnvironment,
            UserManager<ApplicationUser> userManager,
            BookSuggestionService bookSuggestionService)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
            _userManager = userManager;
            _bookSuggestionService = bookSuggestionService;
        }

        // GET: Books
        // Public browsing allowed
        [AllowAnonymous]
        public async Task<IActionResult> Index(string searchString)
        {
            var books = from b in _context.Books
                        .Include(b => b.Author)
                        .Include(b => b.Category)
                        select b;

            if (!string.IsNullOrEmpty(searchString))
            {
                books = books.Where(b => b.Title.Contains(searchString)
                                       || b.ISBN.Contains(searchString));
            }

            return View(await books.ToListAsync());
        }

        // GET: Books/Details/5
        [AllowAnonymous]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var book = await _context.Books
                .Include(b => b.Author)
                .Include(b => b.Category)
                .FirstOrDefaultAsync(m => m.BookId == id);

            if (book == null)
                return NotFound();

            var comments = await _context.BookComments
                .Include(c => c.Member)
                .Where(c => c.BookId == id)
                .OrderByDescending(c => c.PostedDate)
                .ToListAsync();

            ViewBag.Comments = comments;

            return View(book);
        }

        // POST: Books/PostComment
        [HttpPost]
        [Authorize(Roles = "Member")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PostComment(int bookId, string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                TempData["Error"] = "Comment cannot be empty.";
                return RedirectToAction(nameof(Details), new { id = bookId });
            }

            var book = await _context.Books.FindAsync(bookId);

            if (book == null)
                return NotFound();

            var memberId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var comment = new BookComment
            {
                BookId = bookId,
                MemberId = memberId!,
                Text = text.Trim(),
                PostedDate = DateTime.Now
            };

            _context.BookComments.Add(comment);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Comment posted.";

            return RedirectToAction(nameof(Details), new { id = bookId });
        }

        // GET: Books/Suggestions
        // Member only — AI-powered, based on their history
        [Authorize(Roles = "Member")]
        public async Task<IActionResult> Suggestions()
        {
            var memberId = _userManager.GetUserId(User);

            var borrowedBooks = await _context.Borrowings
                .Include(b => b.Book)
                .ThenInclude(bk => bk!.Category)
                .Where(b => b.MemberId == memberId)
                .Select(b => b.Book!)
                .ToListAsync();

            var purchasedBooks = await _context.Purchases
                .Include(p => p.Book)
                .ThenInclude(bk => bk!.Category)
                .Where(p => p.MemberId == memberId)
                .Select(p => p.Book!)
                .ToListAsync();

            var allOwnedBooks = borrowedBooks
                .Concat(purchasedBooks)
                .ToList();

            var hasHistory = allOwnedBooks.Any();

            ViewBag.HasHistory = hasHistory;

            if (!hasHistory)
            {
                return View(new List<Book>());
            }

            var alreadyHaveBookIds = allOwnedBooks
                .Select(b => b.BookId)
                .Distinct()
                .ToList();

            var borrowCounts = borrowedBooks
                .GroupBy(b => new
                {
                    b.Title,
                    Category = b.Category?.Name ?? "Uncategorized"
                })
                .Select(g =>
                    $"Borrowed \"{g.Key.Title}\" ({g.Key.Category}) x{g.Count()}");

            var purchaseCounts = purchasedBooks
                .GroupBy(b => new
                {
                    b.Title,
                    Category = b.Category?.Name ?? "Uncategorized"
                })
                .Select(g =>
                    $"Purchased \"{g.Key.Title}\" ({g.Key.Category}) x{g.Count()}");

            var historyText = string.Join(
                "\n",
                borrowCounts.Concat(purchaseCounts)
            );

            var availableBooks = await _context.Books
                .Include(b => b.Author)
                .Include(b => b.Category)
                .Where(b => !alreadyHaveBookIds.Contains(b.BookId))
                .ToListAsync();

            List<Book> suggestions = new();

            bool usedAi = false;
            bool isRevisit = false;

            if (availableBooks.Any())
            {
                var catalogText = string.Join(
                    "\n",
                    availableBooks.Select(b =>
                        $"{b.BookId}: {b.Title} ({b.Category?.Name ?? "Uncategorized"})")
                );

                var suggestedIds =
                    await _bookSuggestionService
                        .GetSuggestedBookIdsAsync(
                            historyText,
                            catalogText,
                            8
                        );

                if (suggestedIds.Any())
                {
                    suggestions = suggestedIds
                        .Select(id =>
                            availableBooks.FirstOrDefault(
                                b => b.BookId == id
                            ))
                        .Where(b => b != null)
                        .Select(b => b!)
                        .ToList();

                    usedAi = suggestions.Any();
                }

                // Fallback tier 1: category matching
                if (!suggestions.Any())
                {
                    var interestedCategoryIds = allOwnedBooks
                        .Select(b => b.CategoryId)
                        .Distinct()
                        .ToList();

                    suggestions = availableBooks
                        .Where(b =>
                            interestedCategoryIds.Contains(b.CategoryId))
                        .ToList();
                }

                // Fallback tier 2: any unowned book at all
                if (!suggestions.Any())
                {
                    suggestions = availableBooks
                        .Take(8)
                        .ToList();
                }
            }

            // Fallback tier 3:
            // Member already owns everything in the catalog
            if (!suggestions.Any())
            {
                isRevisit = true;

                suggestions = allOwnedBooks
                    .GroupBy(b => b.BookId)
                    .OrderByDescending(g => g.Count())
                    .Select(g => g.First())
                    .Take(8)
                    .ToList();
            }

            ViewBag.UsedAi = usedAi;
            ViewBag.IsRevisit = isRevisit;

            return View(suggestions);
        }

        // GET: Books/Create
        [Authorize(Roles = "Admin,Librarian,Staff")]
        public IActionResult Create()
        {
            ViewBag.AuthorId =
                new SelectList(
                    _context.Authors,
                    "AuthorId",
                    "Name"
                );

            ViewBag.CategoryId =
                new SelectList(
                    _context.Categories,
                    "CategoryId",
                    "Name"
                );

            return View();
        }

        // POST: Books/Create
        [HttpPost]
        [Authorize(Roles = "Admin,Librarian,Staff")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [Bind("Title,AuthorId,CategoryId,ISBN,TotalCopies,Price,IsFeatured,IsPopular,OnlineReadingPrice,PdfContent")]
            Book book,
            IFormFile? CoverImage)
        {
            if (ModelState.IsValid)
            {
                book.AvailableCopies = book.TotalCopies;

                if (CoverImage != null && CoverImage.Length > 0)
                {
                    book.ImageUrl =
                        await SaveCoverImage(CoverImage);
                }

                _context.Add(book);

                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }

            ViewBag.AuthorId =
                new SelectList(
                    _context.Authors,
                    "AuthorId",
                    "Name",
                    book.AuthorId
                );

            ViewBag.CategoryId =
                new SelectList(
                    _context.Categories,
                    "CategoryId",
                    "Name",
                    book.CategoryId
                );

            return View(book);
        }

        // GET: Books/Edit/5
        [Authorize(Roles = "Admin,Librarian,Staff")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var book = await _context.Books.FindAsync(id);

            if (book == null)
                return NotFound();

            ViewBag.AuthorId =
                new SelectList(
                    _context.Authors,
                    "AuthorId",
                    "Name",
                    book.AuthorId
                );

            ViewBag.CategoryId =
                new SelectList(
                    _context.Categories,
                    "CategoryId",
                    "Name",
                    book.CategoryId
                );

            return View(book);
        }

        // POST: Books/Edit/5
        [HttpPost]
        [Authorize(Roles = "Admin,Librarian,Staff")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            [Bind("BookId,Title,AuthorId,CategoryId,ISBN,TotalCopies,AvailableCopies,Price,IsFeatured,IsPopular,ImageUrl,OnlineReadingPrice,PdfContent")]
            Book book,
            IFormFile? CoverImage)
        {
            if (id != book.BookId)
                return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    if (CoverImage != null && CoverImage.Length > 0)
                    {
                        book.ImageUrl =
                            await SaveCoverImage(CoverImage);
                    }

                    _context.Update(book);

                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!BookExists(book.BookId))
                        return NotFound();

                    else
                        throw;
                }

                return RedirectToAction(nameof(Index));
            }

            ViewBag.AuthorId =
                new SelectList(
                    _context.Authors,
                    "AuthorId",
                    "Name",
                    book.AuthorId
                );

            ViewBag.CategoryId =
                new SelectList(
                    _context.Categories,
                    "CategoryId",
                    "Name",
                    book.CategoryId
                );

            return View(book);
        }

        // GET: Books/Delete/5
        [Authorize(Roles = "Admin,Librarian,Staff")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return NotFound();

            var book = await _context.Books
                .Include(b => b.Author)
                .Include(b => b.Category)
                .FirstOrDefaultAsync(m => m.BookId == id);

            if (book == null)
                return NotFound();

            return View(book);
        }

        // POST: Books/Delete/5
        [HttpPost, ActionName("Delete")]
        [Authorize(Roles = "Admin,Librarian,Staff")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var book = await _context.Books.FindAsync(id);

            if (book != null)
            {
                _context.Books.Remove(book);
            }

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // GET: Books/ManageFeatured
        // Admin/Librarian only
        [Authorize(Roles = "Admin,Librarian")]
        public async Task<IActionResult> ManageFeatured()
        {
            var books = await _context.Books
                .Include(b => b.Author)
                .OrderBy(b => b.Title)
                .ToListAsync();

            return View(books);
        }

        // POST: Books/ManageFeatured
        [HttpPost]
        [Authorize(Roles = "Admin,Librarian")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ManageFeatured(
            List<int> featuredIds,
            List<int> popularIds)
        {
            var books = await _context.Books.ToListAsync();

            foreach (var book in books)
            {
                book.IsFeatured =
                    featuredIds != null &&
                    featuredIds.Contains(book.BookId);

                book.IsPopular =
                    popularIds != null &&
                    popularIds.Contains(book.BookId);
            }

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Featured and Popular books updated.";

            return RedirectToAction(nameof(ManageFeatured));
        }

        private async Task<string> SaveCoverImage(
            IFormFile coverImage)
        {
            var uploadsFolder =
                Path.Combine(
                    _webHostEnvironment.WebRootPath,
                    "images",
                    "book-covers"
                );

            Directory.CreateDirectory(uploadsFolder);

            var uniqueFileName =
                Guid.NewGuid().ToString() +
                Path.GetExtension(coverImage.FileName);

            var filePath =
                Path.Combine(
                    uploadsFolder,
                    uniqueFileName
                );

            using (var fileStream =
                   new FileStream(
                       filePath,
                       FileMode.Create))
            {
                await coverImage.CopyToAsync(fileStream);
            }

            return "/images/book-covers/" +
                   uniqueFileName;
        }

        private bool BookExists(int id)
        {
            return _context.Books
                .Any(e => e.BookId == id);
        }
    }
}