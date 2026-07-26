using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;


namespace Agent.Serialization;

public class FormFileSchemaTransformer : IOpenApiSchemaTransformer
{
  public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
  {
    // Intercept if the underlying type is IFormFile
    if (context.JsonTypeInfo.Type == typeof(IFormFile))
    {
      // Fixed the typo from JsonScehmaType to JsonSchemaType
      schema.Type = JsonSchemaType.String;
      schema.Format = "binary";
    }
    return Task.CompletedTask;
  }
}
