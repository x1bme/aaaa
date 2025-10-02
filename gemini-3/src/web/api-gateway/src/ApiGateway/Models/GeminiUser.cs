using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace ApiGateway.Models
{
    public class GeminiUserModel
    {
        [Required]
        public string UserName { get; set; }
        [Required]
        public string Password { get; set; }
        public string Email { get; set; }
        public string Role { get; set; } = "User";
    }
}