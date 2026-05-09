namespace TravelPathways.Api.Common;

/// <summary>Thrown when tenant PDF settings or library template HTML are missing / invalid.</summary>
public sealed class PdfTemplateConfigurationException : Exception
{
    public PdfTemplateConfigurationException(string message) : base(message)
    {
    }
}
