using LibraryManagementSystem.Data;
using LibraryManagementSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagementSystem.Controllers
{
    [Authorize(Roles = "Member")]
    public class PurchasesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public PurchasesController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

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

        [HttpPost, ActionName("Buy")]
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
                PurchaseDate = DateTime.Today
            };

            book.AvailableCopies -= 1;
            book.TotalCopies -= 1;

            _context.Purchases.Add(purchase);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Confirmation), new { id = purchase.PurchaseId });
        }

        public async Task<IActionResult> Confirmation(int id)
        {
            var purchase = await _context.Purchases
                .Include(p => p.Book)
                .FirstOrDefaultAsync(p => p.PurchaseId == id);

            if (purchase == null) return NotFound();

            return View(purchase);
        }

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