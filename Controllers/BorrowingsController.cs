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

        // GET: Borrowings (Admin/Librarian only — full history)
        [Authorize(Roles = "Admin,Librarian")]
        public async Task<IActionResult> Index()
        {
            var borrowings = await _context.Borrowings
                .Include(b => b.Book)
                .Include(b => b.Member)
                .OrderByDescending(b => b.IssueDate)
                .ToListAsync();

            return View(borrowings);
        }

        // GET: Borrowings/PendingRequests (Admin/Librarian/Staff)
        [Authorize(Roles = "Admin,Librarian,Staff")]
        public async Task<IActionResult> PendingRequests()
        {
            var requests = await _context.Borrowings
                .Include(b => b.Book)
                .Include(b => b.Member)
                .Where(b => b.Status == "Requested")
                .OrderBy(b => b.IssueDate)
                .ToListAsync();

            return View(requests);
        }

        // GET: Borrowings/Approve/5 (Admin/Librarian/Staff) — shows a form to pick the due date
        [Authorize(Roles = "Admin,Librarian,Staff")]
        public async Task<IActionResult> Approve(int? id)
        {
            if (id == null) return NotFound();

            var borrowing = await _context.Borrowings
                .Include(b => b.Book)
                .Include(b => b.Member)
                .FirstOrDefaultAsync(b => b.BorrowingId == id);

            if (borrowing == null || borrowing.Status != "Requested") return NotFound();

            if (borrowing.Book!.AvailableCopies <= 0)
            {
                TempData["Error"] = "No available copies left to approve this request.";
                return RedirectToAction(nameof(PendingRequests));
            }

            return View(borrowing);
        }

        // POST: Borrowings/Approve/5 (Admin/Librarian/Staff)
        [HttpPost, ActionName("Approve")]
        [Authorize(Roles = "Admin,Librarian,Staff")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveConfirmed(int id, DateTime dueDate)
        {
            var borrowing = await _context.Borrowings
                .Include(b => b.Book)
                .FirstOrDefaultAsync(b => b.BorrowingId == id);

            if (borrowing == null || borrowing.Status != "Requested") return NotFound();

            if (borrowing.Book!.AvailableCopies <= 0)
            {
                TempData["Error"] = "No available copies left to approve this request.";
                return RedirectToAction(nameof(PendingRequests));
            }

            if (dueDate.Date <= DateTime.Today)
            {
                TempData["Error"] = "Due date must be in the future.";
                return RedirectToAction(nameof(Approve), new { id });
            }

            var staffId = _userManager.GetUserId(User);

            borrowing.Status = "Borrowed";
            borrowing.IssueDate = DateTime.Today;
            borrowing.DueDate = dueDate;
            borrowing.IssuedByStaffId = staffId;
            borrowing.Book.AvailableCopies -= 1;

            await _context.SaveChangesAsync();
            TempData["Success"] = "Request approved and book issued.";
            return RedirectToAction(nameof(PendingRequests));
        }

        // POST: Borrowings/Reject/5 (Admin/Librarian/Staff)
        [HttpPost]
        [Authorize(Roles = "Admin,Librarian,Staff")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id)
        {
            var borrowing = await _context.Borrowings.FindAsync(id);
            if (borrowing == null || borrowing.Status != "Requested") return NotFound();

            borrowing.Status = "Rejected";
            await _context.SaveChangesAsync();

            TempData["Success"] = "Request rejected.";
            return RedirectToAction(nameof(PendingRequests));
        }

        // GET: Borrowings/Issue (Admin/Librarian/Staff — direct counter issue, no request needed)
        [Authorize(Roles = "Admin,Librarian,Staff")]
        public async Task<IActionResult> Issue()
        {
            await PopulateDropdowns();
            return View(new IssueBookViewModel());
        }

        // POST: Borrowings/Issue
        [HttpPost]
        [Authorize(Roles = "Admin,Librarian,Staff")]
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

        // GET: Borrowings/Request/5  (Member requests a book — needs approval)
        [Authorize(Roles = "Member")]
        public async Task<IActionResult> Request(int? id)
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

        // POST: Borrowings/Request/5
        [HttpPost, ActionName("Request")]
        [Authorize(Roles = "Member")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestConfirmed(int id)
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
                IssuedByStaffId = null,
                IssueDate = DateTime.Today, // placeholder until approved
                DueDate = DateTime.Today,   // placeholder until approved
                Status = "Requested"
            };

            _context.Borrowings.Add(borrowing);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Your request to borrow \"{book.Title}\" has been submitted and is awaiting staff approval.";
            return RedirectToAction("MyBorrowings");
        }

        // GET: Borrowings/MyBorrowings (Member)
        [Authorize(Roles = "Member")]
        public async Task<IActionResult> MyBorrowings()
        {
            var memberId = _userManager.GetUserId(User);

            var borrowings = await _context.Borrowings
                .Include(b => b.Book)
                .Where(b => b.MemberId == memberId)
                .OrderByDescending(b => b.BorrowingId)
                .ToListAsync();

            return View(borrowings);
        }

        // GET: Borrowings/Return/5 (Admin/Librarian/Staff)
        [Authorize(Roles = "Admin,Librarian,Staff")]
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
        [Authorize(Roles = "Admin,Librarian,Staff")]
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

        // GET: Borrowings/Renew/5 (Admin/Librarian only)
        [Authorize(Roles = "Admin,Librarian")]
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

        // POST: Borrowings/Renew/5 (Admin/Librarian only)
        [HttpPost, ActionName("Renew")]
        [Authorize(Roles = "Admin,Librarian")]
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
        // GET: Borrowings/Receipt/5
        // Admin/Librarian only
        [Authorize(Roles = "Admin,Librarian")]
        public async Task<IActionResult> Receipt(int id)
        {
            var borrowing = await _context.Borrowings
                .Include(b => b.Book)
                    .ThenInclude(b => b!.Author)
                .Include(b => b.Member)
                .Include(b => b.IssuedByStaff)
                .FirstOrDefaultAsync(b => b.BorrowingId == id);

            if (borrowing == null)
                return NotFound();

            var fine = await _context.Fines
                .FirstOrDefaultAsync(f => f.BorrowingId == id);

            ViewBag.Fine = fine;

            return View(borrowing);
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