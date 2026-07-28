using LibraryManagementSystem.Data;
using LibraryManagementSystem.Models;
using LibraryManagementSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagementSystem.Controllers
{
    [Authorize]
    public class BorrowingsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public BorrowingsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: Borrowings  (Staff only — list all active + past borrowings)
        [Authorize(Roles = "Staff")]
        public async Task<IActionResult> Index()
        {
            var borrowings = await _context.Borrowings
                .Include(b => b.Book)
                .Include(b => b.Member)
                .OrderByDescending(b => b.IssueDate)
                .ToListAsync();

            return View(borrowings);
        }

        // GET: Borrowings/Issue (Staff only)
        [Authorize(Roles = "Staff")]
        public async Task<IActionResult> Issue()
        {
            await PopulateDropdowns();
            return View(new IssueBookViewModel());
        }

        // POST: Borrowings/Issue (Staff only)
        [HttpPost]
        [Authorize(Roles = "Staff")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Issue(IssueBookViewModel model)
        {
            var book = await _context.Books.FindAsync(model.BookId);

            if (book == null)
            {
                ModelState.AddModelError("", "Selected book not found.");
            }
            else if (book.AvailableCopies <= 0)
            {
                ModelState.AddModelError("", "No available copies of this book.");
            }

            if (!ModelState.IsValid)
            {
                await PopulateDropdowns();
                return View(model);
            }

            var staffId = _userManager.GetUserId(User);

            var borrowing = new Borrowing
            {
                BookId = model.BookId,
                MemberId = model.MemberId,
                IssuedByStaffId = staffId,
                IssueDate = DateTime.Today,
                DueDate = model.DueDate,
                Status = "Borrowed"
            };

            book.AvailableCopies -= 1;

            _context.Borrowings.Add(borrowing);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // GET: Borrowings/Borrow/5  (Member self-service)
        [Authorize(Roles = "Member")]
        public async Task<IActionResult> Borrow(int? id)
        {
            if (id == null) return NotFound();

            var book = await _context.Books
                .Include(b => b.Author)
                .FirstOrDefaultAsync(b => b.BookId == id);

            if (book == null) return NotFound();

            if (book.AvailableCopies <= 0)
            {
                TempData["Error"] = "Sorry, this book is currently out of stock.";
                return RedirectToAction("Details", "Books", new { id });
            }

            return View(book);
        }

        // POST: Borrowings/Borrow/5  (Member self-service)
        [HttpPost, ActionName("Borrow")]
        [Authorize(Roles = "Member")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BorrowConfirmed(int id)
        {
            var book = await _context.Books.FindAsync(id);
            if (book == null) return NotFound();

            if (book.AvailableCopies <= 0)
            {
                TempData["Error"] = "Sorry, this book is currently out of stock.";
                return RedirectToAction("Details", "Books", new { id });
            }

            var memberId = _userManager.GetUserId(User);

            var borrowing = new Borrowing
            {
                BookId = book.BookId,
                MemberId = memberId!,
                IssuedByStaffId = null, // self-service, no staff involved
                IssueDate = DateTime.Today,
                DueDate = DateTime.Today.AddDays(14),
                Status = "Borrowed"
            };

            book.AvailableCopies -= 1;

            _context.Borrowings.Add(borrowing);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"You've successfully borrowed \"{book.Title}\". Due back on {borrowing.DueDate.ToShortDateString()}.";
            return RedirectToAction("MyBorrowings");
        }

        // GET: Borrowings/MyBorrowings (Member's own borrowing list)
        [Authorize(Roles = "Member")]
        public async Task<IActionResult> MyBorrowings()
        {
            var memberId = _userManager.GetUserId(User);

            var borrowings = await _context.Borrowings
                .Include(b => b.Book)
                .Where(b => b.MemberId == memberId)
                .OrderByDescending(b => b.IssueDate)
                .ToListAsync();

            return View(borrowings);
        }

        // GET: Borrowings/Return/5 (Staff only)
        [Authorize(Roles = "Staff")]
        public async Task<IActionResult> Return(int? id)
        {
            if (id == null) return NotFound();

            var borrowing = await _context.Borrowings
                .Include(b => b.Book)
                .Include(b => b.Member)
                .FirstOrDefaultAsync(b => b.BorrowingId == id);

            if (borrowing == null) return NotFound();
            if (borrowing.Status == "Returned") return RedirectToAction(nameof(Index));

            return View(borrowing);
        }

        // POST: Borrowings/Return/5 (Staff only)
        [HttpPost, ActionName("Return")]
        [Authorize(Roles = "Staff")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReturnConfirmed(int id)
        {
            var borrowing = await _context.Borrowings
                .Include(b => b.Book)
                .FirstOrDefaultAsync(b => b.BorrowingId == id);

            if (borrowing == null) return NotFound();

            borrowing.ReturnDate = DateTime.Today;
            borrowing.Status = "Returned";
            borrowing.Book!.AvailableCopies += 1;

            if (borrowing.ReturnDate > borrowing.DueDate)
            {
                int daysLate = (borrowing.ReturnDate.Value.Date - borrowing.DueDate.Date).Days;
                var fine = new Fine
                {
                    BorrowingId = borrowing.BorrowingId,
                    Amount = daysLate * 10,
                    IsPaid = false
                };
                _context.Fines.Add(fine);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // GET: Borrowings/Renew/5 (Staff only)
        [Authorize(Roles = "Staff")]
        public async Task<IActionResult> Renew(int? id)
        {
            if (id == null) return NotFound();

            var borrowing = await _context.Borrowings
                .Include(b => b.Book)
                .Include(b => b.Member)
                .FirstOrDefaultAsync(b => b.BorrowingId == id);

            if (borrowing == null) return NotFound();

            return View(borrowing);
        }

        // POST: Borrowings/Renew/5 (Staff only)
        [HttpPost, ActionName("Renew")]
        [Authorize(Roles = "Staff")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RenewConfirmed(int id)
        {
            var borrowing = await _context.Borrowings.FindAsync(id);
            if (borrowing == null) return NotFound();

            if (borrowing.Status != "Returned")
            {
                borrowing.DueDate = borrowing.DueDate.AddDays(7);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        private async Task PopulateDropdowns()
        {
            var books = await _context.Books.Where(b => b.AvailableCopies > 0).ToListAsync();
            ViewBag.BookId = new SelectList(books, "BookId", "Title");

            var members = await _userManager.GetUsersInRoleAsync("Member");
            ViewBag.MemberId = new SelectList(members, "Id", "FullName");
        }
    }
}