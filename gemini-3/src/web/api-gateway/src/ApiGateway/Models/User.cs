using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace ApiGateway.Models
{
    public class User : IdentityUser
    {
        public string Role { get; set; } = "User";
    }
}