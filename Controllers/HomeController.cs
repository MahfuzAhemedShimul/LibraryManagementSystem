using LibraryManagementSystem.Data;
using LibraryManagementSystem.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace LibraryManagementSystem.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public HomeController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                var currentUser = await _userManager.GetUserAsync(User);
                ViewBag.CurrentUserFullName = currentUser?.FullName;

                if (User.IsInRole("Admin") || User.IsInRole("Librarian"))
                {
                    ViewBag.TotalBooks = await _context.Books.CountAsync();
                    ViewBag.TotalMembers = (await _userManager.GetUsersInRoleAsync("Member")).Count;
                    ViewBag.ActiveBorrowings = await _context.Borrowings.CountAsync(b => b.Status == "Borrowed");
                    ViewBag.OverdueCount = await _context.Borrowings.CountAsync(b => b.Status == "Borrowed" && b.DueDate.Date < DateTime.Today);
                    ViewBag.UnpaidFinesTotal = await _context.Fines.Where(f => !f.IsPaid).SumAsync(f => (decimal?)f.Amount) ?? 0;
                    ViewBag.PendingRequestsCount = await _context.Borrowings.CountAsync(b => b.Status == "Requested");
                }
                else if (User.IsInRole("Staff"))
                {
                    ViewBag.PendingRequestsCount = await _context.Borrowings.CountAsync(b => b.Status == "Requested");
                    ViewBag.UnpaidFinesCount = await _context.Fines.CountAsync(f => !f.IsPaid);
                }
                else if (User.IsInRole("Member"))
                {
                    var memberId = _userManager.GetUserId(User);

                    ViewBag.MyActiveBorrowings = await _context.Borrowings
                        .Include(b => b.Book)
                        .Where(b => b.MemberId == memberId && b.Status == "Borrowed")
                        .ToListAsync();

                    ViewBag.MyPendingRequests = await _context.Borrowings
                        .Include(b => b.Book)
                        .Where(b => b.MemberId == memberId && b.Status == "Requested")
                        .ToListAsync();

                    ViewBag.MyUnpaidFines = await _context.Fines
                        .Include(f => f.Borrowing).ThenInclude(b => b.Book)
                        .Where(f => !f.IsPaid && f.Borrowing.MemberId == memberId)
                        .ToListAsync();

                    var borrowedCategoryIds = await _context.Borrowings
                        .Where(b => b.MemberId == memberId)
                        .Select(b => b.Book!.CategoryId)
                        .Distinct()
                        .ToListAsync();

                    var purchasedCategoryIds = await _context.Purchases
                        .Where(p => p.MemberId == memberId)
                        .Select(p => p.Book!.CategoryId)
                        .Distinct()
                        .ToListAsync();

                    var interestedCategoryIds = borrowedCategoryIds.Union(purchasedCategoryIds).ToList();

                    var alreadyHaveBookIds = (await _context.Borrowings
                        .Where(b => b.MemberId == memberId).Select(b => b.BookId).ToListAsync())
                        .Union(await _context.Purchases
                        .Where(p => p.MemberId == memberId).Select(p => p.BookId).ToListAsync())
                        .ToList();

                    ViewBag.RecommendedBooks = await _context.Books
                        .Include(b => b.Author)
                        .Where(b => interestedCategoryIds.Contains(b.CategoryId) && !alreadyHaveBookIds.Contains(b.BookId))
                        .Take(8)
                        .ToListAsync();
                }
            }

            // Featured Books — chosen manually by Admin/Librarian
            ViewBag.FeaturedBooks = await _context.Books
                .Include(b => b.Author)
                .Where(b => b.IsFeatured)
                .Take(8)
                .ToListAsync();

            // Popular Books — chosen manually by Admin/Librarian
            ViewBag.PopularBooks = await _context.Books
                .Include(b => b.Author)
                .Where(b => b.IsPopular)
                .Take(8)
                .ToListAsync();

            // Public stats (shown to everyone, including Guests)
            ViewBag.PublicTotalBooks = await _context.Books.CountAsync();
            ViewBag.PublicTotalCategories = await _context.Categories.CountAsync();
            ViewBag.PublicTotalAuthors = await _context.Authors.CountAsync();
            ViewBag.PublicTotalMembers = (await _userManager.GetUsersInRoleAsync("Member")).Count;

            return View();
        }

        public IActionResult BecomeAMember()
        {
            return View();
        }

        public IActionResult About()
        {
            return View();
        }

        public IActionResult Contact()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}