using System.Reflection;
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
        var asm = typeof(ClientEnvironmentController).Assembly;
        var info =
            asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? asm.GetName().Version?.ToString()
            ?? "unknown";
        var dllUtc = GetAssemblyFileWriteTimeUtcIso(asm);
        return Ok(new ClientEnvironmentDto(resolved ?? string.Empty, info, dllUtc));
    }

    /// <summary>Verify deploys: <c>publicApiBaseUrl</c> + version / DLL timestamp of the running API.</summary>
    public sealed record ClientEnvironmentDto(
        string PublicApiBaseUrl,
        string ApiAssemblyVersion,
        string ApiDllLastWriteUtc);

    private static string GetAssemblyFileWriteTimeUtcIso(Assembly asm)
    {
        try
        {
            var path = asm.Location;
            if (!string.IsNullOrEmpty(path) && System.IO.File.Exists(path))
                return System.IO.File.GetLastWriteTimeUtc(path).ToString("o");
        }
        catch
        {
            /* single-file publish has no Location, etc. */
        }

        return "";
    }
}
