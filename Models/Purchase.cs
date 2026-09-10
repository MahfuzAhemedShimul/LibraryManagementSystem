using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LibraryManagementSystem.Models
{
    public class Purchase
    {
        [Key]
        public int PurchaseId { get; set; }

        [Required]
        public int BookId { get; set; }

        [ForeignKey("BookId")]
        public virtual Book? Book { get; set; }

        [Required]
        public string MemberId { get; set; } = string.Empty;

        [ForeignKey("MemberId")]
        public virtual ApplicationUser? Member { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal PricePaid { get; set; }

        public DateTime PurchaseDate { get; set; } = DateTime.Today;

        // "Pending", "Confirmed", "Rejected"
        public string Status { get; set; } = "Pending";

        public string PaymentMethod { get; set; } = "Cash";

        public string? ApprovedByStaffId { get; set; }

        [ForeignKey("ApprovedByStaffId")]
        public virtual ApplicationUser? ApprovedByStaff { get; set; }
    }
}