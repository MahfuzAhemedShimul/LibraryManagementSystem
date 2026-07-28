namespace LibraryManagementSystem.Models
{
    public class Borrowing
    {
        public int BorrowingId { get; set; }
        public int BookId { get; set; }
        public Book? Book { get; set; }
        public string MemberId { get; set; } = string.Empty;
        public ApplicationUser? Member { get; set; }
        public string? IssuedByStaffId { get; set; }
        public ApplicationUser? IssuedByStaff { get; set; }
        public DateTime IssueDate { get; set; }
        public DateTime DueDate { get; set; }
        public DateTime? ReturnDate { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}