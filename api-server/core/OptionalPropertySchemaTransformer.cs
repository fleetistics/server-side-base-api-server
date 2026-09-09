using System.Reflection;
using exs.commons.Json;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace api_server.core
{
	/// <summary>
	/// Optional&lt;T&gt; has no reflectable shape worth describing in the generated
	/// OpenAPI document (it's opaque behind OptionalJsonConverterFactory) — left alone,
	/// the generator would emit the struct's own {IsSet, Value} members. This replaces
	/// each Optional&lt;T&gt; property's schema with T's own schema instead, and removes it
	/// from Required (an Optional&lt;T&gt; field is by definition never required), so a
	/// PATCH DTO like UserPatchDto shows up in apiSchema.d.ts as plain optional/nullable
	/// fields — exactly what a client author expects.
	/// </summary>
	public sealed class OptionalPropertySchemaTransformer : IOpenApiSchemaTransformer
	{
		public async Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
		{
			if (schema.Properties is null)
			{
				return;
			}

			foreach (var jsonProperty in context.JsonTypeInfo.Properties)
			{
				var propertyType = jsonProperty.PropertyType;
				if (!propertyType.IsGenericType || propertyType.GetGenericTypeDefinition() != typeof(Optional<>))
				{
					continue;
				}

				var wireName = jsonProperty.Name;
				if (!schema.Properties.ContainsKey(wireName))
				{
					continue;
				}

				var innerType = propertyType.GetGenericArguments()[0];
				var innerSchema = await context.GetOrCreateSchemaAsync(innerType, null, cancellationToken);

				// Optional<string> and Optional<string?> are the same runtime closed generic
				// type — NRT annotations on a generic argument are compile-time-only, so
				// innerType alone can never reveal whether T was declared nullable (this is
				// a non-issue for value types: Optional<int?> and Optional<int> really are
				// different closed types, T=Nullable<Int32> vs T=Int32, so GetOrCreateSchemaAsync
				// already gets this right for AvatarImageId-shaped fields on its own).
				// Recovered here from the *property's* own compile-time nullability metadata.
				if (jsonProperty.AttributeProvider is PropertyInfo propertyInfo)
				{
					var nullability = new NullabilityInfoContext().Create(propertyInfo);
					if (nullability.GenericTypeArguments is [{ ReadState: NullabilityState.Nullable }, ..])
					{
						innerSchema.Type |= JsonSchemaType.Null;
					}
				}

				schema.Properties[wireName] = innerSchema;
				schema.Required?.Remove(wireName);
			}
		}
	}
}
