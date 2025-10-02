using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Threading.Tasks;
using System.Collections.Generic;
using ApiGateway.Models;
using OpenIddict.Validation.AspNetCore;
using Microsoft.AspNetCore.Identity;

namespace ApiGateway.Controllers
{
    [Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme, Roles = "Admin")]
    [ApiController]
    [Route("api/[controller]")]
    public class UserController(UserManager<User> userManager) : ControllerBase
    {
        private readonly UserManager<User> _userManager = userManager;

        // GET: api/user/id
        [HttpGet("{id}")]
        public async Task<IActionResult> GetUserById(int id)
        {
            // query db for user 
            await Task.CompletedTask;
            return Ok($"found user {id}");
        }

        // POST: api/user
        [HttpPost]
        public async Task<IActionResult> CreateUser([FromBody] GeminiUserModel model)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var user = new User
            {
                UserName = model.UserName,
                Email = model.Email,
                Role = model.Role
            };
            var result = await _userManager.CreateAsync(user, model.Password);
            if (!result.Succeeded)
            {
                foreach (var e in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, e.Description);  
                }
                return BadRequest(ModelState);
            }
            await Task.CompletedTask;
            return CreatedAtAction(nameof(GetUserById), new {id = user.Id}, user);
        }

        // PUT: api/user/id
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateValveConfig(int id, [FromBody] GeminiUserModel updatedUser)
        {
            // db query to fetch valve
            // logic to check for id match, check for nulls, and update user in memory
            // db query to update user configs
            // get more info on how user config should look like
            await Task.CompletedTask;
            return NoContent();
        }

        // DELETE: api/user/id
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            // DB Query to delete user with {id}
            await Task.CompletedTask;
            return NoContent();
        }
   }
}