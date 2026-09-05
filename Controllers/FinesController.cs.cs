using LibraryManagementSystem.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace LibraryManagementSystem.Controllers
{
    [Authorize]
    public class FinesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public FinesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Fines (Admin/Librarian/Staff — full list)
        [Authorize(Roles = "Admin,Librarian,Staff")]
        public async Task<IActionResult> Index()
        {
            var fines = await _context.Fines
                .Include(f => f.Borrowing)
                    .ThenInclude(b => b.Book)
                .Include(f => f.Borrowing)
                    .ThenInclude(b => b.Member)
                .OrderByDescending(f => f.FineId)
                .ToListAsync();

            return View(fines);
        }

        // GET: Fines/Pay/5 (Admin/Librarian/Staff)
        [Authorize(Roles = "Admin,Librarian,Staff")]
        public async Task<IActionResult> Pay(int? id)
        {
            if (id == null) return NotFound();

            var fine = await _context.Fines
                .Include(f => f.Borrowing)
                    .ThenInclude(b => b.Book)
                .Include(f => f.Borrowing)
                    .ThenInclude(b => b.Member)
                .FirstOrDefaultAsync(f => f.FineId == id);

            if (fine == null) return NotFound();
            if (fine.IsPaid) return RedirectToAction(nameof(Index));

            return View(fine);
        }

        // POST: Fines/Pay/5 (Admin/Librarian/Staff)
        [HttpPost, ActionName("Pay")]
        [Authorize(Roles = "Admin,Librarian,Staff")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PayConfirmed(int id)
        {
            var fine = await _context.Fines.FindAsync(id);
            if (fine == null) return NotFound();

            fine.IsPaid = true;
            fine.PaidDate = DateTime.Today;

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // GET: Fines/MyFines (Member) — their own fine history (paid + unpaid)
        [Authorize(Roles = "Member")]
        public async Task<IActionResult> MyFines()
        {
            var memberId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var fines = await _context.Fines
                .Include(f => f.Borrowing)
                    .ThenInclude(b => b.Book)
                .Where(f => f.Borrowing.MemberId == memberId)
                .OrderByDescending(f => f.FineId)
                .ToListAsync();

            return View(fines);
        }

        // GET: Fines/Receipt/5
        [Authorize(Roles = "Admin,Librarian,Staff,Member")]
        public async Task<IActionResult> Receipt(int id)
        {
            var fine = await _context.Fines
                .Include(f => f.Borrowing)
                    .ThenInclude(b => b.Book)
                .Include(f => f.Borrowing)
                    .ThenInclude(b => b.Member)
                .FirstOrDefaultAsync(f => f.FineId == id);

            if (fine == null) return NotFound();

            // Members can only view their own paid fine receipt
            if (User.IsInRole("Member"))
            {
                var memberId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (fine.Borrowing?.MemberId != memberId) return Forbid();
                if (!fine.IsPaid) return Forbid();
            }

            return View(fine);
        }
    }
}