using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HM.AdminPanel.Controllers;

[AllowAnonymous]
[Route("Error")]
public class ErrorController : Controller
{
    [HttpGet("{code:int}")]
    public IActionResult Status(int code)
    {
        return code switch
        {
            404 => View("NotFound"),
            403 => View("AccessDenied"),
            _   => View("GenericError")
        };
    }
}
