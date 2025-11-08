using Microsoft.AspNetCore.Mvc;

namespace mostlylucid.activetranslatetag.Demo.Controllers;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
