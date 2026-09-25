using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace api_server.core
{
	/// <summary>
	/// The built-in OpenApi schema exporter documents every float/double property as
	/// `number | ("NaN" | "Infinity" | "-Infinity")` - it treats NaN/Infinity as states the
	/// CLR type can hold, without consulting JsonSerializerOptions.NumberHandling. But
	/// NumberHandling is pinned to Strict everywhere (see ConfigureJson.FillSerializerSettings),
	/// so System.Text.Json throws rather than emit those literals - they can never actually
	/// appear on the wire. This collapses the anyOf back down to a plain number schema, so
	/// generated clients (e.g. openapi-typescript) see `Latitude?: number` instead of the
	/// spurious string-literal union.
	/// </summary>
	public sealed class FloatingPointSchemaTransformer : IOpenApiSchemaTransformer
	{
		public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
		{
			StripIfFloatingPoint(schema, context.JsonTypeInfo.Type);

			if (schema.Properties is not null)
			{
				foreach (var jsonProperty in context.JsonTypeInfo.Properties)
				{
					if (schema.Properties.TryGetValue(jsonProperty.Name, out var propertySchema) && propertySchema is OpenApiSchema concretePropertySchema)
					{
						StripIfFloatingPoint(concretePropertySchema, jsonProperty.PropertyType);
					}
				}
			}

			return Task.CompletedTask;
		}

		private static void StripIfFloatingPoint(OpenApiSchema schema, Type type)
		{
			var underlyingType = Nullable.GetUnderlyingType(type) ?? type;
			if (underlyingType != typeof(float) && underlyingType != typeof(double))
			{
				return;
			}

			if (schema.AnyOf is null && schema.OneOf is null)
			{
				return;
			}

			schema.AnyOf = null;
			schema.OneOf = null;
			schema.Type = JsonSchemaType.Number;
			schema.Format = underlyingType == typeof(float) ? "float" : "double";
		}
	}
}
