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

        // POST: Purchases/Buy/5 (Member) — creates a pending purchase, doesn't deduct stock yet
        [HttpPost, ActionName("Buy")]
        [Authorize(Roles = "Member")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BuyConfirmed(int id)
        {
            var book = await _context.Books.FindAsync(id);
            if (book == null) return NotFound();
            if (book.AvailableCopies <= 0)
            {
                TempData["Error"] = "Sorry, this book is currently out of stock.";
                return RedirectToAction("Details", "Books", new { id });
            }

            var memberId = _userManager.GetUserId(User);

            var purchase = new Purchase
            {
                BookId = book.BookId,
                MemberId = memberId!,
                PricePaid = book.Price,
                PurchaseDate = DateTime.Today,
                Status = "Pending"
            };

            _context.Purchases.Add(purchase);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Your purchase request for \"{book.Title}\" has been submitted and is awaiting staff confirmation.";
            return RedirectToAction(nameof(MyPurchases));
        }

        // GET: Purchases/PendingPurchases (Admin/Librarian/Staff)
        [Authorize(Roles = "Admin,Librarian,Staff")]
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

        // POST: Purchases/Approve/5 (Admin/Librarian/Staff) — confirms and deducts stock
        [HttpPost]
        [Authorize(Roles = "Admin,Librarian,Staff")]
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

        // POST: Purchases/Reject/5 (Admin/Librarian/Staff)
        [HttpPost]
        [Authorize(Roles = "Admin,Librarian,Staff")]
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

        // GET: Purchases/MyPurchases (Member)
        [Authorize(Roles = "Member")]
        public async Task<IActionResult> MyPurchases()
        {
            var memberId = _userManager.GetUserId(User);

            var purchases = await _context.Purchases
                .Include(p => p.Book)
                .Where(p => p.MemberId == memberId)
                .OrderByDescending(p => p.PurchaseDate)
                .ToListAsync();

            return View(purchases);
        }
    }
}