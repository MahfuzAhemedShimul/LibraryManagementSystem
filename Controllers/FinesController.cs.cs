using LibraryManagementSystem.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagementSystem.Controllers
{
    [Authorize(Roles = "Admin,Librarian,Staff")]
    public class FinesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public FinesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Fines
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

        // GET: Fines/Pay/5
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
        // GET: Fines/Receipt/5
        public async Task<IActionResult> Receipt(int id)
        {
            var fine = await _context.Fines
                .Include(f => f.Borrowing)
                    .ThenInclude(b => b.Book)
                .Include(f => f.Borrowing)
                    .ThenInclude(b => b.Member)
                .FirstOrDefaultAsync(f => f.FineId == id);

            if (fine == null) return NotFound();

            return View(fine);
        }

        // POST: Fines/Pay/5
        [HttpPost, ActionName("Pay")]
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
    }
}