using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Client.Controllers;

public class HomeController() : Controller
{
    [AllowAnonymous]
    public IActionResult Index() => View();

    public IActionResult Secure() => View();

    public IActionResult Logout() => SignOut(OpenIdConnectDefaults.AuthenticationScheme, CookieAuthenticationDefaults.AuthenticationScheme);

    [AllowAnonymous]
    public IActionResult AccessDenied() => View();

    [AllowAnonymous]
    public IActionResult Error() => View();

    [AllowAnonymous]
    public IActionResult LoggedOut() => View();

}