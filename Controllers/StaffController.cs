using LibraryManagementSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace LibraryManagementSystem.Controllers
{
    [Authorize(Roles = "Admin")]
    public class StaffController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public StaffController(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        // GET: Staff  (Admin views all Librarian and Staff accounts)
        public async Task<IActionResult> Index()
        {
            var librarians = await _userManager.GetUsersInRoleAsync("Librarian");
            var staff = await _userManager.GetUsersInRoleAsync("Staff");
            var admins = await _userManager.GetUsersInRoleAsync("Admin");

            var result = new List<(ApplicationUser User, string Role)>();
            result.AddRange(admins.Select(u => (u, "Admin")));
            result.AddRange(librarians.Select(u => (u, "Librarian")));
            result.AddRange(staff.Select(u => (u, "Staff")));

            return View(result.OrderBy(x => x.Role).ThenBy(x => x.User.FullName).ToList());
        }
    }
}