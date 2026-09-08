using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MVCCoreApp.Services;

public class SwaggerFileUploadFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var hasFormFile = context.MethodInfo.GetParameters().Any(p => p.ParameterType == typeof(IFormFile) || p.ParameterType == typeof(IFormFileCollection));
        if (!hasFormFile) return;

        // keep non-file query params (like folder)
        if (operation.Parameters != null)
        {
            foreach (var r in operation.Parameters.Where(p => p.Name == "file").ToList())
                operation.Parameters.Remove(r);
        }

        operation.RequestBody = new OpenApiRequestBody
        {
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["multipart/form-data"] = new OpenApiMediaType
                {
                    Schema = new OpenApiSchema
                    {
                        Type = JsonSchemaType.Object,
                        Properties = new Dictionary<string, IOpenApiSchema>
                        {
                            ["file"] = new OpenApiSchema { Type = JsonSchemaType.String, Format = "binary" },
                            ["name"] = new OpenApiSchema { Type = JsonSchemaType.String },
                            ["folder"] = new OpenApiSchema { Type = JsonSchemaType.String }
                        },
                        Required = new HashSet<string> { "file" }
                    }
                }
            }
        };
    }
}
