using System.Text.Json.Nodes;
using exs.notifications_model.Templates;

namespace api_server.Controllers.MessageTemplates.Dto
{
	/// <summary>Body of POST /api/admin/message-templates/{key}/versions.</summary>
	public class CreateMessageTemplateVersionRequestDto
	{
		/// <summary>Letters, optionally with dashes ("en", "pt-br"), at most 16. Stored lower case.</summary>
		public string? Language { get; set; }

		public string? Subject { get; set; }

		public string? Text { get; set; }

		public string? Html { get; set; }
	}

	/// <summary>Body of POST /api/admin/message-templates/{key}/preview. The strings are a draft; nothing is read from or written to the database.</summary>
	public class PreviewMessageTemplateRequestDto
	{
		/// <summary>Whose culture formats dates and numbers; null for the default language.</summary>
		public string? Language { get; set; }

		public string? Subject { get; set; }

		public string? Text { get; set; }

		public string? Html { get; set; }

		/// <summary>A JSON object (snake_case keys) laid over the model's sample; null for the sample as it is.</summary>
		public JsonNode? Model { get; set; }
	}

	/// <summary>One stored version of a template. Immutable once written: only <see cref="IsActive"/> ever changes.</summary>
	public class MessageTemplateVersionDto
	{
		public long Id { get; set; }

		public string Language { get; set; } = "";

		public int Version { get; set; }

		public bool IsActive { get; set; }

		public string Subject { get; set; } = "";

		public string? Text { get; set; }

		public string? Html { get; set; }

		public DateTime CreatedAt { get; set; }

		public string? CreatedBy { get; set; }

		public static MessageTemplateVersionDto From(NotificationTemplate row) => new()
		{
			Id = row.Id,
			Language = row.Language,
			Version = row.Version,
			IsActive = row.IsActive,
			Subject = row.Subject,
			Text = row.Text,
			Html = row.Html,
			// Stored as UTC; saying so makes the JSON end in Z.
			CreatedAt = DateTime.SpecifyKind(row.CreatedAt, DateTimeKind.Utc),
			CreatedBy = row.CreatedBy,
		};
	}

	/// <summary>The three parts of a preview, as the recipient would see them (the secure tags are gone, their content is kept).</summary>
	public class MessageTemplatePreviewDto
	{
		public string Subject { get; set; } = "";

		public string? Text { get; set; }

		public string? Html { get; set; }
	}
}
