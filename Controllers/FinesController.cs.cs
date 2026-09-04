using LibraryManagementSystem.Data;
using LibraryManagementSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagementSystem.Controllers
{
    [Authorize]
    public class FinesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public FinesController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: Fines (Admin/Librarian/Staff — all fines)
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

        // GET: Fines/MyFines (Member — their own fines, current + history)
        [Authorize(Roles = "Member")]
        public async Task<IActionResult> MyFines()
        {
            var memberId = _userManager.GetUserId(User);

            var fines = await _context.Fines
                .Include(f => f.Borrowing)
                    .ThenInclude(b => b.Book)
                .Where(f => f.Borrowing.MemberId == memberId)
                .OrderByDescending(f => f.FineId)
                .ToListAsync();

            return View(fines);
        }

        // GET: Fines/Receipt/5 (Admin/Librarian/Staff, or the Member who owns this fine)
        [Authorize]
        public async Task<IActionResult> Receipt(int id)
        {
            var fine = await _context.Fines
                .Include(f => f.Borrowing)
                    .ThenInclude(b => b.Book)
                .Include(f => f.Borrowing)
                    .ThenInclude(b => b.Member)
                .FirstOrDefaultAsync(f => f.FineId == id);

            if (fine == null) return NotFound();

            // If the current user is a Member, only let them view their own receipt
            if (User.IsInRole("Member"))
            {
                var memberId = _userManager.GetUserId(User);
                if (fine.Borrowing?.MemberId != memberId) return Forbid();
            }

            return View(fine);
        }
    }
}