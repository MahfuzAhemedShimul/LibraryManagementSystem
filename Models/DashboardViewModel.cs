namespace LibraryManagementSystem.Models
{
    public class DashboardViewModel
    {
        public int TotalBooks { get; set; }
        public int ActiveBorrowers { get; set; }
        public int OverdueFinesCount { get; set; }
        public decimal TotalRevenueCollected { get; set; }

        // Data for Category Pie Chart
        public List<string> CategoryLabels { get; set; } = new();
        public List<int> CategoryCounts { get; set; } = new();

        // Data for Monthly Borrowings Line Graph
        public List<string> MonthLabels { get; set; } = new();
        public List<int> MonthlyBorrowingCounts { get; set; } = new();
    }
}