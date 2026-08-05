using LibraryManagementSystem.Data;
using LibraryManagementSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LibraryManagementSystem.Controllers
{
    [Authorize(Roles = "Admin,Librarian")]
    public class MembersController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public MembersController(UserManager<ApplicationUser> userManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        // GET: Members
        public async Task<IActionResult> Index(string searchString)
        {
            var membersInRole = await _userManager.GetUsersInRoleAsync("Member");

            var members = membersInRole.AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                members = members.Where(m =>
                    m.FullName.Contains(searchString) ||
                    m.Email.Contains(searchString));
            }

            return View(members.OrderBy(m => m.FullName).ToList());
        }

        // GET: Members/Details/{id}
        public async Task<IActionResult> Details(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var member = await _userManager.FindByIdAsync(id);
            if (member == null) return NotFound();

            var isMember = await _userManager.IsInRoleAsync(member, "Member");
            if (!isMember) return NotFound();

            // Borrowing history for this member
            var borrowings = await _context.Borrowings
                .Include(b => b.Book)
                .Where(b => b.MemberId == id)
                .OrderByDescending(b => b.IssueDate)
                .ToListAsync();

            ViewBag.Borrowings = borrowings;

            return View(member);
        }
    }
}