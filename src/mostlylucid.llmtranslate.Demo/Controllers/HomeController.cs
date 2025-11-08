using Microsoft.AspNetCore.Mvc;

namespace mostlylucid.llmtranslate.Demo.Controllers;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
