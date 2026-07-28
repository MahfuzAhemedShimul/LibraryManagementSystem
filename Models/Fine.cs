using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LibraryManagementSystem.Models
{
    public class Fine
    {
        [Key]
        public int FineId { get; set; }

        [Required]
        public int BorrowingId { get; set; }

        [ForeignKey("BorrowingId")]
        public virtual Borrowing? Borrowing { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal Amount { get; set; }

        public bool IsPaid { get; set; } = false;

        public DateTime? PaidDate { get; set; }
    }
}