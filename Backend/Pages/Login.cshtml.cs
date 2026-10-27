using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace CoCaNgua.Server.Pages;

[EnableRateLimiting("auth")]
public class LoginModel(IConfiguration config) : PageModel
{
    [BindProperty] public string Username { get; set; } = "";
    [BindProperty] public string Password { get; set; } = "";
    public string Error { get; set; } = "";
    public IActionResult OnGet() => User.Identity?.IsAuthenticated == true ? RedirectToPage("/Index") : Page();
    public async Task<IActionResult> OnPostAsync()
    {
        string expected = config["Admin:Password"] ?? "";
        bool passwordOk = CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(Password ?? "")), SHA256.HashData(Encoding.UTF8.GetBytes(expected)));
        if (string.IsNullOrWhiteSpace(expected) || Username != config["Admin:Username"] || !passwordOk)
        {
            Error = "Tài khoản hoặc mật khẩu không đúng.";
            Response.StatusCode = 401;
            return Page();
        }
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, Username), new Claim(ClaimTypes.Role, "Admin")], CookieAuthenticationDefaults.AuthenticationScheme)));
        return RedirectToPage("/Index");
    }
}
