using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using LibraryManagementSystem.Models;
using LibraryManagementSystem.Data;

namespace LibraryManagementSystem.Controllers
{
    [Authorize(Roles = "Admin,Librarian")]
    public class BooksController : Controller
    {
        private readonly ApplicationDbContext _context;

        public BooksController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Books  (public browsing allowed too, so override the class-level restriction here)
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
            if (id == null) return NotFound();

            var book = await _context.Books
                .Include(b => b.Author)
                .Include(b => b.Category)
                .FirstOrDefaultAsync(m => m.BookId == id);

            if (book == null) return NotFound();

            return View(book);
        }

        // GET: Books/Create
        public IActionResult Create()
        {
            ViewBag.AuthorId = new SelectList(_context.Authors, "AuthorId", "Name");
            ViewBag.CategoryId = new SelectList(_context.Categories, "CategoryId", "Name");
            return View();
        }

        // POST: Books/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Title,AuthorId,CategoryId,ISBN,TotalCopies,Price,IsFeatured,IsPopular,ImageUrl")] Book book)
        {
            if (ModelState.IsValid)
            {
                book.AvailableCopies = book.TotalCopies; // new books start fully available
                _context.Add(book);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            ViewBag.AuthorId = new SelectList(_context.Authors, "AuthorId", "Name", book.AuthorId);
            ViewBag.CategoryId = new SelectList(_context.Categories, "CategoryId", "Name", book.CategoryId);
            return View(book);
        }

        // GET: Books/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var book = await _context.Books.FindAsync(id);
            if (book == null) return NotFound();

            ViewBag.AuthorId = new SelectList(_context.Authors, "AuthorId", "Name", book.AuthorId);
            ViewBag.CategoryId = new SelectList(_context.Categories, "CategoryId", "Name", book.CategoryId);
            return View(book);
        }

        // POST: Books/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("BookId,Title,AuthorId,CategoryId,ISBN,TotalCopies,AvailableCopies,Price,IsFeatured,IsPopular,ImageUrl")] Book book)
        {
            if (id != book.BookId) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(book);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!BookExists(book.BookId)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }

            ViewBag.AuthorId = new SelectList(_context.Authors, "AuthorId", "Name", book.AuthorId);
            ViewBag.CategoryId = new SelectList(_context.Categories, "CategoryId", "Name", book.CategoryId);
            return View(book);
        }

        // GET: Books/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var book = await _context.Books
                .Include(b => b.Author)
                .Include(b => b.Category)
                .FirstOrDefaultAsync(m => m.BookId == id);

            if (book == null) return NotFound();

            return View(book);
        }

        // POST: Books/Delete/5
        [HttpPost, ActionName("Delete")]
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
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ManageFeatured(List<int> featuredIds, List<int> popularIds)
        {
            var books = await _context.Books.ToListAsync();

            foreach (var book in books)
            {
                book.IsFeatured = featuredIds != null && featuredIds.Contains(book.BookId);
                book.IsPopular = popularIds != null && popularIds.Contains(book.BookId);
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "Featured and Popular books updated.";
            return RedirectToAction(nameof(ManageFeatured));
        }

        private bool BookExists(int id)
        {
            return _context.Books.Any(e => e.BookId == id);
        }
    }
}