using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TravelPathways.Api.Common;
using TravelPathways.Api.Localization;

namespace TravelPathways.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/localization")]
public sealed class LocalizationController : ControllerBase
{
    [HttpGet("languages")]
    public ActionResult<ApiResponse<IReadOnlyList<LanguageOptionDto>>> GetLanguages()
    {
        var options = PdfLanguageCodes.Supported.Select(code => new LanguageOptionDto
        {
            Code = code,
            Name = LanguageDisplayName(code),
            IsDefault = code == PdfLanguageCodes.English
        }).ToList();

        return Ok(ApiResponse<IReadOnlyList<LanguageOptionDto>>.Ok(options));
    }

    private static string LanguageDisplayName(string code) => code switch
    {
        PdfLanguageCodes.Hindi => "Hindi",
        PdfLanguageCodes.Malayalam => "Malayalam",
        PdfLanguageCodes.Tamil => "Tamil",
        PdfLanguageCodes.Telugu => "Telugu",
        PdfLanguageCodes.Kannada => "Kannada",
        PdfLanguageCodes.Marathi => "Marathi",
        _ => "English"
    };

    public sealed class LanguageOptionDto
    {
        public required string Code { get; init; }
        public required string Name { get; init; }
        public bool IsDefault { get; init; }
    }
}
