using System.ComponentModel.DataAnnotations.Schema;

namespace LibraryManagementSystem.Models
{
    public class Book
    {
        public int BookId { get; set; }
        public string Title { get; set; } = string.Empty;

        public int AuthorId { get; set; }
        [ForeignKey("AuthorId")]
        public Author? Author { get; set; }

        public int CategoryId { get; set; }
        [ForeignKey("CategoryId")]
        public Category? Category { get; set; }

        public string ISBN { get; set; } = string.Empty;
        public int TotalCopies { get; set; }
        public int AvailableCopies { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal Price { get; set; }

        public bool IsFeatured { get; set; }
        public bool IsPopular { get; set; }

        public string? ImageUrl { get; set; }
    }
}