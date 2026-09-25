using System.Reflection;
using exs.commons.Json;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace api_server.core
{
	/// <summary>
	/// DefaultIgnoreCondition = WhenWritingNull (see ConfigureJson.FillSerializerSettings) means
	/// a plain-nullable property - value type or reference type, anywhere in the API - has its
	/// key omitted entirely from the JSON when its value is null; System.Text.Json never writes
	/// an explicit `null` for it. The built-in OpenApi schema generator doesn't account for that
	/// and adds a `null` schema type to any C#-nullable property regardless, which
	/// openapi-typescript then turns into a spurious `T | null` on the generated client. This
	/// strips that `null` type back off, so the schema matches what can actually appear on the
	/// wire: the field is present with a real value, or (already reflected elsewhere as
	/// optional/not-Required) absent.
	///
	/// Two kinds of properties are left untouched:
	/// - Optional&lt;T&gt; properties: a non-nullable struct at the CLR level, so
	///   DefaultIgnoreCondition never applies to them - an explicit wire `null` there is a real,
	///   intentional value distinct from omission (see OptionalPropertySchemaTransformer /
	///   OptionalJsonConverterFactory), and OptionalPropertySchemaTransformer has already added
	///   `null` deliberately where warranted.
	/// - Properties marked [MeaningfulNull]: a handful of plain-nullable request fields (e.g.
	///   UserSettingsPutDto.Value, UpdateTranslationDto.TranslatedText) use explicit null as a
	///   real sentinel ("reset this tier", "clear this translation") rather than incidental C#
	///   nullability, so the schema needs to keep documenting `null` as sendable there.
	///
	/// Two different mechanisms produce the `null` this strips, matching two different property
	/// shapes (confirmed by decompiling Microsoft.AspNetCore.OpenApi 10.0.11's
	/// OpenApiSchemaService - it isn't documented as public API, so this is coupled to that
	/// version's behavior):
	/// - Scalar/array properties (int?, List&lt;T&gt;?, ...) get `null` folded directly into the
	///   property's own Type flags, at the point this transformer runs - stripping the flag here
	///   is enough.
	/// - Componentized ($ref) properties (a nested DTO like TeamHeaderDto?) can't carry `type`
	///   alongside `$ref` yet at this point - the schema still just carries an
	///   "x-is-nullable-property" metadata marker, and a *later* pass (after every schema
	///   transformer has already run) reads that marker to decide whether to wrap the resolved
	///   $ref in `oneOf: [null, $ref]`. Type-flag stripping never reaches these at all; clearing
	///   the marker here, before that later pass runs, is what actually suppresses the wrap.
	/// </summary>
	public sealed class PlainNullableSchemaTransformer : IOpenApiSchemaTransformer
	{
		private const string NullablePropertyMetadataKey = "x-is-nullable-property";

		public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
		{
			if (schema.Properties is null)
			{
				return Task.CompletedTask;
			}

			foreach (var jsonProperty in context.JsonTypeInfo.Properties)
			{
				var propertyType = jsonProperty.PropertyType;
				if (propertyType.IsGenericType && propertyType.GetGenericTypeDefinition() == typeof(Optional<>))
				{
					continue;
				}

				if (jsonProperty.AttributeProvider is PropertyInfo propertyInfo &&
					propertyInfo.GetCustomAttribute<MeaningfulNullAttribute>() is not null)
				{
					continue;
				}

				if (!schema.Properties.TryGetValue(jsonProperty.Name, out var propertySchema) ||
					propertySchema is not OpenApiSchema concreteSchema)
				{
					continue;
				}

				if (concreteSchema.Type is { } type && (type & JsonSchemaType.Null) != 0)
				{
					concreteSchema.Type = type & ~JsonSchemaType.Null;
				}

				concreteSchema.Metadata?.Remove(NullablePropertyMetadataKey);
			}

			return Task.CompletedTask;
		}
	}
}
