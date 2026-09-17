using LibraryManagementSystem.Data;
using LibraryManagementSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagementSystem.Controllers
{
    [Authorize]
    public class OnlineReadingController : Controller
    {
        private const int AccessDays = 14;

        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public OnlineReadingController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: OnlineReading/Read/5 (Member) — entry point from Book Details / Index.
        // Redirects to Pay if the Member hasn't paid, or if a prior access window expired.
        [Authorize(Roles = "Member")]
        public async Task<IActionResult> Read(int id)
        {
            var memberId = _userManager.GetUserId(User);

            var book = await _context.Books.FindAsync(id);
            if (book == null || string.IsNullOrEmpty(book.PdfContent)) return NotFound();

            var access = await _context.OnlineReadingAccesses
                .FirstOrDefaultAsync(a => a.BookId == id && a.MemberId == memberId && a.IsPaid);

            if (access == null)
            {
                return RedirectToAction(nameof(Pay), new { id });
            }

            if (access.ExpiryDate.Date < DateTime.Today)
            {
                TempData["Error"] = "Your 14-day online reading access for this book has expired. Please pay again to renew it.";
                return RedirectToAction(nameof(Pay), new { id });
            }

            ViewBag.ExpiryDate = access.ExpiryDate;
            return View(book);
        }

        // GET: OnlineReading/Pay/5 (Member)
        [Authorize(Roles = "Member")]
        public async Task<IActionResult> Pay(int id)
        {
            var memberId = _userManager.GetUserId(User);

            var activeAccess = await _context.OnlineReadingAccesses
                .AnyAsync(a => a.BookId == id && a.MemberId == memberId && a.IsPaid && a.ExpiryDate.Date >= DateTime.Today);

            if (activeAccess) return RedirectToAction(nameof(Read), new { id });

            var book = await _context.Books.FindAsync(id);
            if (book == null || string.IsNullOrEmpty(book.PdfContent)) return NotFound();

            return View(book);
        }

        // POST: OnlineReading/Pay/5 (Member) — grants a fresh 14-day access window.
        // Same simulated-payment pattern as Purchases/Buy (no real gateway yet).
        [HttpPost, ActionName("Pay")]
        [Authorize(Roles = "Member")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PayConfirmed(int id, string paymentMethod)
        {
            var memberId = _userManager.GetUserId(User);

            var book = await _context.Books.FindAsync(id);
            if (book == null || string.IsNullOrEmpty(book.PdfContent)) return NotFound();

            var access = await _context.OnlineReadingAccesses
                .FirstOrDefaultAsync(a => a.BookId == id && a.MemberId == memberId);

            var today = DateTime.Today;

            if (access == null)
            {
                access = new OnlineReadingAccess
                {
                    BookId = id,
                    MemberId = memberId!,
                    PurchaseDate = today,
                    ExpiryDate = today.AddDays(AccessDays),
                    IsPaid = true
                };
                _context.OnlineReadingAccesses.Add(access);
            }
            else
            {
                access.IsPaid = true;
                access.PurchaseDate = today;
                access.ExpiryDate = today.AddDays(AccessDays);
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = $"Payment confirmed — you have online reading access to \"{book.Title}\" for {AccessDays} days (until {access.ExpiryDate:MMM d, yyyy}).";
            return RedirectToAction(nameof(Read), new { id });
        }

        // GET: OnlineReading/MyOnlineBooks (Member)
        [Authorize(Roles = "Member")]
        public async Task<IActionResult> MyOnlineBooks()
        {
            var memberId = _userManager.GetUserId(User);

            var accesses = await _context.OnlineReadingAccesses
                .Include(a => a.Book)
                .Where(a => a.MemberId == memberId && a.IsPaid)
                .OrderByDescending(a => a.PurchaseDate)
                .ToListAsync();

            return View(accesses);
        }
    }
}