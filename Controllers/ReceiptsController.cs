using LibraryManagementSystem.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagementSystem.Controllers
{
    [Authorize(Roles = "Admin,Librarian,Staff")]
    public class ReceiptsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ReceiptsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Receipts  (central hub — pick Borrow / Purchase / Fine)
        public async Task<IActionResult> Index()
        {
            var borrowings = await _context.Borrowings
                .Include(b => b.Book)
                .Include(b => b.Member)
                .Where(b => b.Status == "Returned" || b.Status == "Borrowed")
                .OrderByDescending(b => b.BorrowingId)
                .ToListAsync();

            var purchases = await _context.Purchases
                .Include(p => p.Book)
                .Include(p => p.Member)
                .Where(p => p.Status == "Confirmed")
                .OrderByDescending(p => p.PurchaseId)
                .ToListAsync();

            var fines = await _context.Fines
                .Include(f => f.Borrowing)
                    .ThenInclude(b => b.Book)
                .Include(f => f.Borrowing)
                    .ThenInclude(b => b.Member)
                .Where(f => f.IsPaid)
                .OrderByDescending(f => f.FineId)
                .ToListAsync();

            ViewBag.Borrowings = borrowings;
            ViewBag.Purchases = purchases;
            ViewBag.Fines = fines;

            return View();
        }
    }
}