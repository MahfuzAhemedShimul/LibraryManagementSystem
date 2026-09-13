using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LibraryManagementSystem.Models
{
    public class BookComment
    {
        [Key]
        public int BookCommentId { get; set; }

        [Required]
        public int BookId { get; set; }

        [ForeignKey("BookId")]
        public virtual Book? Book { get; set; }

        [Required]
        public string MemberId { get; set; } = string.Empty;

        [ForeignKey("MemberId")]
        public virtual ApplicationUser? Member { get; set; }

        [Required]
        [StringLength(500)]
        public string Text { get; set; } = string.Empty;

        public DateTime PostedDate { get; set; } = DateTime.Now;
    }
}