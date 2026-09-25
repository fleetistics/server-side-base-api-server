using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace api_server.core
{
	/// <summary>
	/// DateTime/DateTime? go over the wire as Unix seconds (see DateTime2UnixSerializer
	/// and DateTimeNullable2UnixSerializer in exs.commons), but System.Text.Json's schema
	/// exporter has no visibility into a converter's actual wire shape and leaves these
	/// properties with an unannotated/unknown schema. This rewrites every DateTime and
	/// DateTime? schema — property or standalone — to the integer/int64 shape that is
	/// actually produced, so DTOs don't need per-property annotations.
	///
	/// Never adds a `null` type even for DateTime? - DefaultIgnoreCondition.WhenWritingNull
	/// (see ConfigureJson.FillSerializerSettings) means a null DateTime? is omitted from the
	/// wire entirely, never written as literal `null` (same rule PlainNullableSchemaTransformer
	/// applies to every other plain-nullable property).
	/// </summary>
	public sealed class UnixDateTimeSchemaTransformer : IOpenApiSchemaTransformer
	{
		public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
		{
			ApplyIfDateTime(schema, context.JsonTypeInfo.Type);

			if (schema.Properties is not null)
			{
				foreach (var jsonProperty in context.JsonTypeInfo.Properties)
				{
					if (schema.Properties.TryGetValue(jsonProperty.Name, out var propertySchema) && propertySchema is OpenApiSchema concretePropertySchema)
					{
						ApplyIfDateTime(concretePropertySchema, jsonProperty.PropertyType);
					}
				}
			}

			return Task.CompletedTask;
		}

		private static void ApplyIfDateTime(OpenApiSchema schema, Type type)
		{
			var underlyingType = Nullable.GetUnderlyingType(type) ?? type;
			if (underlyingType != typeof(DateTime))
			{
				return;
			}

			schema.Type = JsonSchemaType.Integer;
			schema.Format = "int64";
			schema.Description ??= "Unix timestamp (seconds since epoch, UTC)";
		}
	}
}
