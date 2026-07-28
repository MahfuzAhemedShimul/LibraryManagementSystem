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
    [Authorize(Roles = "Staff")]
    public class BorrowingsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public BorrowingsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: Borrowings  (list all active + past borrowings)
        public async Task<IActionResult> Index()
        {
            var borrowings = await _context.Borrowings
                .Include(b => b.Book)
                .Include(b => b.Member)
                .OrderByDescending(b => b.IssueDate)
                .ToListAsync();

            return View(borrowings);
        }

        // GET: Borrowings/Issue
        public async Task<IActionResult> Issue()
        {
            await PopulateDropdowns();
            return View(new IssueBookViewModel());
        }

        // POST: Borrowings/Issue
        [HttpPost]
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

        // GET: Borrowings/Return/5
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

        // POST: Borrowings/Return/5
        [HttpPost, ActionName("Return")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReturnConfirmed(int id)
        {
            var borrowing = await _context.Borrowings
                .Include(b => b.Book)
                .FirstOrDefaultAsync(b => b.BorrowingId == id);

            if (borrowing == null) return NotFound();

            borrowing.ReturnDate = DateTime.Today;
            borrowing.Status = "Returned";
            borrowing.Book.AvailableCopies += 1;

            // Flat-rate fine: 10 currency units per day overdue
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

        // GET: Borrowings/Renew/5
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

        // POST: Borrowings/Renew/5
        [HttpPost, ActionName("Renew")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RenewConfirmed(int id)
        {
            var borrowing = await _context.Borrowings.FindAsync(id);
            if (borrowing == null) return NotFound();

            if (borrowing.Status != "Returned")
            {
                borrowing.DueDate = borrowing.DueDate.AddDays(7); // extend by 7 days
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