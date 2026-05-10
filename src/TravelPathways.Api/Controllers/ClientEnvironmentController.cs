using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using TravelPathways.Api.Common;

namespace TravelPathways.Api.Controllers;

/// <summary>Bootstrap-only: lets the SPA learn the API public URL for <c>/uploads</c> without a frontend redeploy.</summary>
[ApiController]
[AllowAnonymous]
[Route("api/public")]
public sealed class ClientEnvironmentController : ControllerBase
{
    [HttpGet("client-environment")]
    public ActionResult<ClientEnvironmentDto> GetClientEnvironment([FromServices] IConfiguration configuration)
    {
        var resolved = PublicApiBaseResolver.Resolve(configuration, HttpContext);
        return Ok(new ClientEnvironmentDto(resolved ?? string.Empty));
    }

    public sealed record ClientEnvironmentDto(string PublicApiBaseUrl);
}
