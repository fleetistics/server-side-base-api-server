using System.Text.Json.Serialization;

namespace api_server.Controllers.Test.Dto
{
	/// <summary>Answer of POST /api/test/email: the key to poll GET /api/test/email/{reference} with.</summary>
	public class EmailTestAcceptedDto
	{
		[JsonPropertyName("reference")]
		public string Reference { get; set; } = "";
	}

	/// <summary>
	/// What this host knows about one test email, from its outbox row. Never holds the recipient address or the text
	/// (the row's Envelope is not read). Fields are always present, null when not known yet, so a poller sees them all.
	/// The property names are written out because this host's JSON setup uses PascalCase and drops nulls.
	/// </summary>
	public class EmailTestStatusDto
	{
		[JsonPropertyName("reference"), JsonIgnore(Condition = JsonIgnoreCondition.Never)]
		public string Reference { get; set; } = "";

		[JsonPropertyName("messageId"), JsonIgnore(Condition = JsonIgnoreCondition.Never)]
		public Guid MessageId { get; set; }

		/// <summary>pending, submitted, completed, rejected or expired. Completed is final, and resultStatus then says sent or failed.</summary>
		[JsonPropertyName("state"), JsonIgnore(Condition = JsonIgnoreCondition.Never)]
		public string State { get; set; } = "";

		/// <summary>sent, failed or expired; null until the email sender has reported. "sent" means its SMTP server took the mail.</summary>
		[JsonPropertyName("resultStatus"), JsonIgnore(Condition = JsonIgnoreCondition.Never)]
		public string? ResultStatus { get; set; }

		[JsonPropertyName("errorKind"), JsonIgnore(Condition = JsonIgnoreCondition.Never)]
		public string? ErrorKind { get; set; }

		[JsonPropertyName("errorCode"), JsonIgnore(Condition = JsonIgnoreCondition.Never)]
		public string? ErrorCode { get; set; }

		[JsonPropertyName("errorMessage"), JsonIgnore(Condition = JsonIgnoreCondition.Never)]
		public string? ErrorMessage { get; set; }

		[JsonPropertyName("providerMessageId"), JsonIgnore(Condition = JsonIgnoreCondition.Never)]
		public string? ProviderMessageId { get; set; }

		[JsonPropertyName("destinationInvalid"), JsonIgnore(Condition = JsonIgnoreCondition.Never)]
		public bool DestinationInvalid { get; set; }

		[JsonPropertyName("attempts"), JsonIgnore(Condition = JsonIgnoreCondition.Never)]
		public int? Attempts { get; set; }

		/// <summary>Why the last submission to the email sender failed, or why it refused the message.</summary>
		[JsonPropertyName("submitError"), JsonIgnore(Condition = JsonIgnoreCondition.Never)]
		public string? SubmitError { get; set; }

		[JsonPropertyName("account"), JsonIgnore(Condition = JsonIgnoreCondition.Never)]
		public string? Account { get; set; }

		/// <summary>ISO 8601, UTC.</summary>
		[JsonPropertyName("createdAt"), JsonIgnore(Condition = JsonIgnoreCondition.Never)]
		public DateTimeOffset CreatedAt { get; set; }

		[JsonPropertyName("completedAt"), JsonIgnore(Condition = JsonIgnoreCondition.Never)]
		public DateTimeOffset? CompletedAt { get; set; }
	}
}
