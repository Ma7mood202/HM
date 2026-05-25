using System.Security.Claims;
using HM.AdminPanel.Authorization;
using HM.AdminPanel.Services;
using HM.AdminPanel.ViewModels.Account;
using HM.Domain.Entities;
using HM.Infrastructure.Data;
using HM.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HM.AdminPanel.Controllers;

[AllowAnonymous]
public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser>   _userMgr;
    private readonly SignInManager<ApplicationUser> _signInMgr;
    private readonly ApplicationDbContext           _db;
    private readonly LoginThrottleService           _throttle;

    public AccountController(
        UserManager<ApplicationUser> userMgr,
        SignInManager<ApplicationUser> signInMgr,
        ApplicationDbContext db,
        LoginThrottleService throttle)
    {
        _userMgr   = userMgr;
        _signInMgr = signInMgr;
        _db        = db;
        _throttle  = throttle;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["Title"] = "Sign in";
        return View(new LoginVm { ReturnUrl = returnUrl });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginVm vm)
    {
        ViewData["Title"] = "Sign in";

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "";
        var ua = Request.Headers.UserAgent.ToString();

        if (_throttle.IsBlocked(ip))
        {
            TempData["Error"] = "Too many failed attempts. Try again later.";
            return View(vm);
        }

        if (!ModelState.IsValid) return View(vm);

        async Task RecordAttempt(bool success)
        {
            _db.AdminLoginAttempts.Add(new AdminLoginAttempt
            {
                Id = Guid.NewGuid(),
                Email = vm.Email,
                Success = success,
                IpAddress = ip,
                UserAgent = ua,
                CreatedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
        }

        var appUser = await _userMgr.FindByEmailAsync(vm.Email);
        if (appUser is null)
        {
            _throttle.RecordFailure(ip);
            await RecordAttempt(false);
            TempData["Error"] = "Invalid credentials.";
            return View(vm);
        }

        // Block check on Domain User (shares Guid with ApplicationUser)
        var domain = await _db.Users.FirstOrDefaultAsync(u => u.Id == appUser.Id);
        if (domain is { IsBlocked: true })
        {
            await RecordAttempt(false);
            TempData["Error"] = "Account is blocked.";
            return View(vm);
        }

        var isAdmin = await _userMgr.IsInRoleAsync(appUser, AdminRoles.Admin);
        if (!isAdmin)
        {
            _throttle.RecordFailure(ip);
            await RecordAttempt(false);
            TempData["Error"] = "Account is not an admin.";
            return View(vm);
        }

        var check = await _signInMgr.CheckPasswordSignInAsync(appUser, vm.Password, lockoutOnFailure: false);
        if (!check.Succeeded)
        {
            _throttle.RecordFailure(ip);
            await RecordAttempt(false);
            TempData["Error"] = "Invalid credentials.";
            return View(vm);
        }

        var roles = await _userMgr.GetRolesAsync(appUser);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, appUser.Id.ToString()),
            new(ClaimTypes.Name,           appUser.Email ?? appUser.UserName ?? ""),
            new(ClaimTypes.Email,          appUser.Email ?? "")
        };
        foreach (var r in roles) claims.Add(new Claim(ClaimTypes.Role, r));

        var identity  = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties { IsPersistent = vm.RememberMe });

        _throttle.Reset(ip);
        await RecordAttempt(true);

        if (!string.IsNullOrEmpty(vm.ReturnUrl) && Url.IsLocalUrl(vm.ReturnUrl))
            return Redirect(vm.ReturnUrl);

        return RedirectToAction("Index", "Dashboard");
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public IActionResult AccessDenied() => View("~/Views/Error/AccessDenied.cshtml");
}
