using System.ComponentModel.DataAnnotations;

namespace ApiGateway.Models
{
    public class LoginView
    {
        [Required]
        public string Username { get; set; }
        [Required]
        public string Password { get; set; }
        public string ReturnUrl { get; set; }
    }
}