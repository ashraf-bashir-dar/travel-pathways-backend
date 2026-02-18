using Microsoft.AspNetCore.Http;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace TravelPathways.Api.Swagger;

/// <summary>
/// Fixes Swagger document generation for actions that have IFormFile parameters (avoids 500 on swagger.json).
/// </summary>
public sealed class FileUploadOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var hasFormFile = context.ApiDescription.ParameterDescriptions
            .Any(p => p.Type == typeof(IFormFile) || p.Type == typeof(IFormFile[]));

        if (!hasFormFile)
            return;

        // Replace request body with a valid multipart/form-data schema for file upload
        var formFileParams = context.ApiDescription.ParameterDescriptions
            .Where(p => p.Type == typeof(IFormFile) || p.Type == typeof(IFormFile[]))
            .ToList();

        var properties = new Dictionary<string, OpenApiSchema>();
        foreach (var p in formFileParams)
        {
            var name = p.Name ?? "file";
            properties[name] = new OpenApiSchema
            {
                Type = "string",
                Format = "binary",
                Description = "File to upload"
            };
        }

        operation.RequestBody = new OpenApiRequestBody
        {
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["multipart/form-data"] = new OpenApiMediaType
                {
                    Schema = new OpenApiSchema
                    {
                        Type = "object",
                        Properties = properties
                    }
                }
            }
        };

        // Remove any parameters that Swashbuckle might have added for IFormFile (they belong in request body)
        var toRemove = operation.Parameters
            .Where(pp => formFileParams.Any(fp => fp.Name == pp.Name))
            .ToList();
        foreach (var r in toRemove)
            operation.Parameters.Remove(r);
    }
}
