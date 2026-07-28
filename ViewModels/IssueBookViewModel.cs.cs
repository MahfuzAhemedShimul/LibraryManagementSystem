using System.ComponentModel.DataAnnotations;

namespace LibraryManagementSystem.ViewModels
{
    public class IssueBookViewModel
    {
        [Required]
        public int BookId { get; set; }

        [Required]
        public string MemberId { get; set; }

        [Required]
        [DataType(DataType.Date)]
        public DateTime DueDate { get; set; } = DateTime.Today.AddDays(14);
    }
}