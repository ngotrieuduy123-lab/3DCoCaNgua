using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CoCaNgua.Server.Pages;

[Authorize]
public class IndexModel : PageModel
{
    public void OnGet() { }
    public async Task<IActionResult> OnPostLogoutAsync() { await HttpContext.SignOutAsync(); return RedirectToPage("/Login"); }
}
