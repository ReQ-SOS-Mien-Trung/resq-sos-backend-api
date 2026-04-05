using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace RESQ.Presentation.Extensions;

/// <summary>
/// Makes Swagger render enum properties as string names (e.g. "TransferToDepot")
/// instead of integer values, mirroring the global JsonStringEnumConverter.
/// </summary>
public class EnumSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (!context.Type.IsEnum) return;

        schema.Type = "string";
        schema.Format = null;
        schema.Enum.Clear();

        foreach (var name in Enum.GetNames(context.Type))
            schema.Enum.Add(new OpenApiString(name));
    }
}
