using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LibraryManagementSystem.Models
{
    public class OnlineReadingAccess
    {
        [Key]
        public int OnlineReadingAccessId { get; set; }

        [Required]
        public int BookId { get; set; }

        [ForeignKey("BookId")]
        public virtual Book? Book { get; set; }

        [Required]
        public string MemberId { get; set; } = string.Empty;

        [ForeignKey("MemberId")]
        public virtual ApplicationUser? Member { get; set; }

        public DateTime PurchaseDate { get; set; } = DateTime.Today;

        public DateTime ExpiryDate { get; set; }

        public bool IsPaid { get; set; }
    }
}