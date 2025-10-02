using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using ApiGateway.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace ApiGateway.Controllers
{
    public class AccountController(SignInManager<User> signInManager, UserManager<User> userManager) : Controller
    {
        private readonly SignInManager<User> _signInManager = signInManager;
        private readonly UserManager<User> _userManager = userManager;

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login(string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginView model)
        {
            ViewData["ReturnUrl"] = model.ReturnUrl;

            if (ModelState.IsValid) 
            {
                var user= await _signInManager.UserManager.FindByNameAsync(model.Username);
                var isValid = await _signInManager.UserManager.CheckPasswordAsync(user, model.Password);
                if (isValid)
                {
                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.Name, model.Username),
                        new Claim(ClaimTypes.Role, "User"),
                    };

                    if (user.Role == "Admin")
                    {
                        claims.Add(new Claim(ClaimTypes.Role, "Admin"));
                    }

                    var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

                    await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity));
                    await _signInManager.SignInAsync(user, isPersistent: true);

                    if (Url.IsLocalUrl(model.ReturnUrl))
                    {
                        return Redirect(model.ReturnUrl);
                    }
                    return RedirectToAction(nameof(HomeController.Index), "Home");
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                    return View(model);
                }
            }
            return View(model);
        }

        [HttpGet]
        public IActionResult Logout()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LogoutConfirmed()
        {
            await _signInManager.SignOutAsync();
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction(nameof(HomeController.Index), "Home");
        }
    }
}