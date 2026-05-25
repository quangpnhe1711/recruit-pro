using Microsoft.AspNetCore.Mvc;

namespace RecruitPro.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HomeController : ControllerBase
    {
        [HttpGet]
        public IActionResult Index()
        {
            return Ok("Hello RecruitPro");
        }
    }
}