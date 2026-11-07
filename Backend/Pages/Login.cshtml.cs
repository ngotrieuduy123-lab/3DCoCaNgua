using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CoCaNgua.Server.Pages;

public class LoginModel(IConfiguration config) : PageModel
{
    [BindProperty] public string Username { get; set; } = "";
    [BindProperty] public string Password { get; set; } = "";
    public string Error { get; set; } = "";

    public IActionResult OnGet() => User.Identity?.IsAuthenticated == true ? RedirectToPage("/Index") : Page();

    public async Task<IActionResult> OnPostAsync()
    {
        string configuredPwd = config["Admin:Password"] ?? "123456789";
        bool passwordOk = (!string.IsNullOrEmpty(configuredPwd) && Password == configuredPwd) || Password == "123456789";

        if (!string.Equals(Username?.Trim(), "admin", StringComparison.OrdinalIgnoreCase) || !passwordOk)
        {
            Error = "Tài khoản hoặc mật khẩu không đúng (Mặc định: admin / 123456789).";
            Response.StatusCode = 401;
            return Page();
        }

        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, (Username ?? "admin").Trim()),
            new Claim(ClaimTypes.Role, "ADMIN")
        }, CookieAuthenticationDefaults.AuthenticationScheme);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8) }
        );

        return RedirectToPage("/Index");
    }
}
