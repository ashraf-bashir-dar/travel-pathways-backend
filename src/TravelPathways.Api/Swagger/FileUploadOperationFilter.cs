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
        static bool IsFormFileType(Type? type) =>
            type == typeof(IFormFile) ||
            type == typeof(IFormFile[]) ||
            type == typeof(List<IFormFile>) ||
            (type?.IsGenericType == true &&
             type.GetGenericTypeDefinition() == typeof(List<>) &&
             type.GetGenericArguments()[0] == typeof(IFormFile));

        var hasFormFile = context.ApiDescription.ParameterDescriptions.Any(p => IsFormFileType(p.Type));

        if (!hasFormFile)
            return;

        // Replace request body with a valid multipart/form-data schema for file upload
        var formFileParams = context.ApiDescription.ParameterDescriptions
            .Where(p => IsFormFileType(p.Type))
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
