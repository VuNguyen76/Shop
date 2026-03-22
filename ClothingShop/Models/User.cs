// Models/User.cs
using System.ComponentModel.DataAnnotations;

namespace ClothingShop.Models
{
    public class User
    {
        public int Id { get; set; }

        [Required, MaxLength(100)]
        public string FullName { get; set; } = null!;

        [Required, EmailAddress, MaxLength(100)]
        public string Email { get; set; } = null!;

        [Required, MaxLength(256)]
        public string PasswordHash { get; set; } = null!;

        [Required, Phone, MaxLength(20)]
        public string PhoneNumber { get; set; } = null!;

        [MaxLength(10)]
        public string? Gender { get; set; }

        public DateTime CreatedAt { get; set; }

        public bool IsAdmin { get; set; } = false;
        
        public bool IsActive { get; set; } = true;
    }
}