using LibraryManagementSystem.Data;
using LibraryManagementSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagementSystem.Controllers
{
    [Authorize]
    public class PurchasesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public PurchasesController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: Purchases/Buy/5 (Member)
        [Authorize(Roles = "Member")]
        public async Task<IActionResult> Buy(int? id)
        {
            if (id == null) return NotFound();

            var memberId = _userManager.GetUserId(User);

            var hasOverdue = await _context.Borrowings.AnyAsync(b =>
                b.MemberId == memberId &&
                b.Status == "Borrowed" &&
                b.DueDate.Date < DateTime.Today);

            if (hasOverdue)
            {
                TempData["Error"] = "You have an overdue book. Please return it before purchasing a book.";
                return RedirectToAction("Details", "Books", new { id });
            }

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

        // POST: Purchases/Buy/5 (Member) — auto-confirms if no unpaid fines/overdue, else goes to Pending
        [HttpPost, ActionName("Buy")]
        [Authorize(Roles = "Member")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BuyConfirmed(int id, string paymentMethod)
        {
            var memberId = _userManager.GetUserId(User);

            var hasOverdue = await _context.Borrowings.AnyAsync(b =>
                b.MemberId == memberId &&
                b.Status == "Borrowed" &&
                b.DueDate.Date < DateTime.Today);

            if (hasOverdue)
            {
                TempData["Error"] = "You have an overdue book. Please return it before purchasing a book.";
                return RedirectToAction("Details", "Books", new { id });
            }

            var book = await _context.Books.FindAsync(id);
            if (book == null) return NotFound();
            if (book.AvailableCopies <= 0)
            {
                TempData["Error"] = "Sorry, this book is currently out of stock.";
                return RedirectToAction("Details", "Books", new { id });
            }

            bool hasUnpaidFines = await _context.Fines
                .AnyAsync(f => !f.IsPaid && f.Borrowing.MemberId == memberId);

            bool autoApprove = !hasUnpaidFines && !hasOverdue;

            var purchase = new Purchase
            {
                BookId = book.BookId,
                MemberId = memberId!,
                PricePaid = book.Price,
                PurchaseDate = DateTime.Today,
                PaymentMethod = string.IsNullOrWhiteSpace(paymentMethod) ? "Cash" : paymentMethod,
                Status = autoApprove ? "Confirmed" : "Pending"
            };

            if (autoApprove)
            {
                book.AvailableCopies -= 1;
                book.TotalCopies -= 1;
            }

            _context.Purchases.Add(purchase);
            await _context.SaveChangesAsync();

            TempData["Success"] = autoApprove
                ? $"Your purchase of \"{book.Title}\" was automatically confirmed!"
                : $"Your purchase request for \"{book.Title}\" has been submitted and is awaiting staff confirmation.";

            return RedirectToAction(nameof(MyPurchases));
        }

        // GET: Purchases/PendingPurchases (Admin/Librarian)
        [Authorize(Roles = "Admin,Librarian")]
        public async Task<IActionResult> PendingPurchases()
        {
            var pending = await _context.Purchases
                .Include(p => p.Book)
                .Include(p => p.Member)
                .Where(p => p.Status == "Pending")
                .OrderBy(p => p.PurchaseDate)
                .ToListAsync();

            return View(pending);
        }

        // POST: Purchases/Approve/5 (Admin/Librarian) — confirms and deducts stock
        [HttpPost]
        [Authorize(Roles = "Admin,Librarian")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id)
        {
            var purchase = await _context.Purchases
                .Include(p => p.Book)
                .FirstOrDefaultAsync(p => p.PurchaseId == id);

            if (purchase == null || purchase.Status != "Pending") return NotFound();

            if (purchase.Book!.AvailableCopies <= 0)
            {
                TempData["Error"] = "No available copies left to confirm this purchase.";
                return RedirectToAction(nameof(PendingPurchases));
            }

            var staffId = _userManager.GetUserId(User);

            purchase.Status = "Confirmed";
            purchase.ApprovedByStaffId = staffId;
            purchase.Book.AvailableCopies -= 1;
            purchase.Book.TotalCopies -= 1;

            await _context.SaveChangesAsync();
            TempData["Success"] = "Purchase confirmed.";
            return RedirectToAction(nameof(PendingPurchases));
        }

        // POST: Purchases/Reject/5 (Admin/Librarian)
        [HttpPost]
        [Authorize(Roles = "Admin,Librarian")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id)
        {
            var purchase = await _context.Purchases.FindAsync(id);
            if (purchase == null || purchase.Status != "Pending") return NotFound();

            purchase.Status = "Rejected";
            await _context.SaveChangesAsync();

            TempData["Success"] = "Purchase rejected.";
            return RedirectToAction(nameof(PendingPurchases));
        }

        // GET: Purchases/Confirmation/5
        public async Task<IActionResult> Confirmation(int id)
        {
            var purchase = await _context.Purchases
                .Include(p => p.Book)
                .FirstOrDefaultAsync(p => p.PurchaseId == id);

            if (purchase == null) return NotFound();

            return View(purchase);
        }

        // GET: Purchases/Receipt/5
        [Authorize(Roles = "Admin,Librarian")]
        public async Task<IActionResult> Receipt(int id)
        {
            var purchase = await _context.Purchases
                .Include(p => p.Book)
                    .ThenInclude(b => b!.Author)
                .Include(p => p.Member)
                .Include(p => p.ApprovedByStaff)
                .FirstOrDefaultAsync(p => p.PurchaseId == id);

            if (purchase == null) return NotFound();

            return View(purchase);
        }

        // GET: Purchases (Admin/Librarian — full history)
        [Authorize(Roles = "Admin,Librarian")]
        public async Task<IActionResult> Index()
        {
            var purchases = await _context.Purchases
                .Include(p => p.Book)
                .Include(p => p.Member)
                .OrderByDescending(p => p.PurchaseDate)
                .ToListAsync();

            return View(purchases);
        }

        // GET: Purchases/MyPurchases (Member)
        [Authorize(Roles = "Member")]
        public async Task<IActionResult> MyPurchases(string status, DateTime? fromDate, DateTime? toDate)
        {
            var memberId = _userManager.GetUserId(User);

            var query = _context.Purchases
                .Include(p => p.Book)
                .Where(p => p.MemberId == memberId);

            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(p => p.Status == status);
            }

            if (fromDate.HasValue)
            {
                query = query.Where(p => p.PurchaseDate.Date >= fromDate.Value.Date);
            }

            if (toDate.HasValue)
            {
                query = query.Where(p => p.PurchaseDate.Date <= toDate.Value.Date);
            }

            var purchases = await query
                .OrderByDescending(p => p.PurchaseDate)
                .ToListAsync();

            ViewBag.Status = status;
            ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
            ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");

            return View(purchases);
        }
    }
}