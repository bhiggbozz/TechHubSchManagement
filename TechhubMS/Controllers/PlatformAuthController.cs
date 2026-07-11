using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TechHub.Core;
using TechHub.Core.Model;
using TechHub.Core.ViewModel.Platform;
using TechHub.Service.Interface;

namespace TechhubMS.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PlatformAuthController : ControllerBase
{
    private readonly IPlatformAuthService _platformAuthService;

    public PlatformAuthController(IPlatformAuthService platformAuthService)
    {
        _platformAuthService = platformAuthService;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginPlatformViewModel model)
    {
        var result = await _platformAuthService.LoginAsync(model);
        return Ok(result);
    }
}
