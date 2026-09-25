using System;

namespace api_server.core
{
	/// <summary>
	/// Marks a plain-nullable (non-Optional&lt;T&gt;) DTO property whose explicit JSON <c>null</c> is a
	/// real, distinct value - not just C# nullability noise. Everywhere else, DefaultIgnoreCondition
	/// .WhenWritingNull (see ConfigureJson.FillSerializerSettings) means a plain-nullable property's
	/// key is omitted entirely when its value is null, so it can never actually be observed as
	/// literal <c>null</c> on the wire; PlainNullableSchemaTransformer drops the spurious <c>null</c>
	/// type from the generated OpenAPI schema (and hence the generated TypeScript client type) for
	/// those. This opts a specific property back into keeping <c>null</c> as a documented, sendable
	/// value - e.g. a PUT/PATCH field where the client is expected to send an explicit null to mean
	/// "clear this" or "reset to the broader tier", as opposed to omitting the field.
	/// </summary>
	[AttributeUsage(AttributeTargets.Property)]
	public sealed class MeaningfulNullAttribute : Attribute
	{
	}
}
