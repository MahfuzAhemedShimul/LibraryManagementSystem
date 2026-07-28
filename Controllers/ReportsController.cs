using LibraryManagementSystem.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagementSystem.Controllers
{
    [Authorize(Roles = "Staff")]
    public class ReportsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ReportsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Reports
        public IActionResult Index()
        {
            return View();
        }

        // GET: Reports/Borrowed
        public async Task<IActionResult> Borrowed()
        {
            var borrowed = await _context.Borrowings
                .Include(b => b.Book)
                .Include(b => b.Member)
                .Where(b => b.Status == "Borrowed")
                .OrderBy(b => b.DueDate)
                .ToListAsync();

            return View(borrowed);
        }

        // GET: Reports/Overdue
        public async Task<IActionResult> Overdue()
        {
            var overdue = await _context.Borrowings
                .Include(b => b.Book)
                .Include(b => b.Member)
                .Where(b => b.Status == "Borrowed" && b.DueDate.Date < DateTime.Today)
                .OrderBy(b => b.DueDate)
                .ToListAsync();

            return View(overdue);
        }
    }
}