using api_server.Controllers.Base;
using api_server.Controllers.MessageTemplates.Dto;
using db_model.UserManagement;
using exs.Database.Commons.Interfaces;
using exs.notifications_service.Templates;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace api_server.Controllers.MessageTemplates
{
	/// <summary>
	/// The developers' screen for notification templates: which messages exist and what their templates may use, the versions of
	/// each template, adding a version, choosing the live one, and trying a draft out. For developers only, but access is not
	/// restricted by role yet: any signed-in user can call these endpoints (nobody signed out, 401); the menu only hides the page.
	///
	/// A version is never edited: POST .../versions always adds a row (inactive), POST /versions/{id}/activate moves the live flag,
	/// so earlier wording can always be switched back. Answers are camelCase JSON with every field present (null when empty),
	/// whatever the host's general JSON settings are, because the portal reads them as a fixed contract.
	/// </summary>
	public class MessageTemplatesAdminController : AuthAPIController
	{
		private const string Prefix = "/api/admin/message-templates";

		private static readonly JsonSerializerOptions ResponseJson = new(JsonSerializerDefaults.Web)
		{
			DefaultIgnoreCondition = JsonIgnoreCondition.Never,
		};

		public MessageTemplatesAdminController(TemplateAuthoring authoring, IRepository repository)
		{
			mAuthoring = authoring;
			mRepository = repository;
		}

		/// <summary>Every registered model with the values its template may read and a sample of it.</summary>
		[HttpGet(Prefix + "/models")]
		[ProducesResponseType(typeof(IReadOnlyList<TemplateModelDescription>), StatusCodes.Status200OK)]
		public IActionResult GetModels() => json(mAuthoring.DescribeModels());

		/// <summary>The keys that have rows or a model, with the languages that have rows and which version is live in each.</summary>
		[HttpGet(Prefix)]
		[ProducesResponseType(typeof(IReadOnlyList<TemplateKeySummary>), StatusCodes.Status200OK)]
		public async Task<IActionResult> GetKeys(CancellationToken cancellationToken) =>
			json(await mAuthoring.ListKeysAsync(mRepository, cancellationToken));

		/// <summary>The versions of a key, by language and then newest first; <paramref name="language"/> narrows it to one language.</summary>
		[HttpGet(Prefix + "/{key}/versions")]
		[ProducesResponseType(typeof(IReadOnlyList<MessageTemplateVersionDto>), StatusCodes.Status200OK)]
		public async Task<IActionResult> GetVersions(string key, [FromQuery] string? language, CancellationToken cancellationToken)
		{
			var rows = await mAuthoring.ListVersionsAsync(mRepository, key, language, cancellationToken);
			return json(rows.Select(MessageTemplateVersionDto.From).ToList());
		}

		/// <summary>
		/// Adds the next version of the template (inactive). The draft has to fill in against the model's sample: 404 when no
		/// model has this key, 400 for a language or subject or content that is missing or malformed, 422 when it does not render.
		/// </summary>
		[HttpPost(Prefix + "/{key}/versions")]
		[ProducesResponseType(typeof(MessageTemplateVersionDto), StatusCodes.Status201Created)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
		public async Task<IActionResult> CreateVersion(string key, [FromBody] CreateMessageTemplateVersionRequestDto request, CancellationToken cancellationToken)
		{
			if (!mAuthoring.HasModel(key)) return noModel(key);

			var problem = TemplateAuthoring.CheckDraft(request.Language, request.Subject, request.Text, request.Html);
			if (problem != null) return Problem(title: "Invalid template", detail: problem, statusCode: StatusCodes.Status400BadRequest);

			try
			{
				var row = await mAuthoring.CreateVersionAsync(mRepository, key, request.Language!, request.Subject!, request.Text, request.Html, await currentUserNameAsync(cancellationToken), cancellationToken);
				return json(MessageTemplateVersionDto.From(row), StatusCodes.Status201Created);
			}
			catch (TemplateRenderException ex)
			{
				return cannotBeFilledIn(ex);
			}
		}

		/// <summary>
		/// Makes a version the live one of its key and language (the live one before it stops being). Nothing is checked again.
		/// Activating the live version is not an error.
		/// </summary>
		[HttpPost(Prefix + "/versions/{id:long}/activate")]
		[ProducesResponseType(StatusCodes.Status204NoContent)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
		public async Task<IActionResult> Activate(long id, CancellationToken cancellationToken)
		{
			if (!await mAuthoring.ActivateAsync(mRepository, id, cancellationToken))
			{
				return Problem(title: "Not Found", detail: $"There is no template version with id {id}.", statusCode: StatusCodes.Status404NotFound);
			}

			return NoContent();
		}

		/// <summary>
		/// Fills in a draft (the strings in the request, not the database) with the model's sample, or with <c>model</c> laid over
		/// it, and answers as the recipient would see it: the secure tags removed, their content kept. 404 for an unknown key, 400
		/// for a model that does not fit, 422 when the draft does not render.
		/// </summary>
		[HttpPost(Prefix + "/{key}/preview")]
		[ProducesResponseType(typeof(MessageTemplatePreviewDto), StatusCodes.Status200OK)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
		public async Task<IActionResult> Preview(string key, [FromBody] PreviewMessageTemplateRequestDto request)
		{
			if (!mAuthoring.HasModel(key)) return noModel(key);

			if (request.Model != null && request.Model is not JsonObject)
			{
				return Problem(title: "Invalid model", detail: "The model must be a JSON object.", statusCode: StatusCodes.Status400BadRequest);
			}

			try
			{
				var rendered = await mAuthoring.PreviewAsync(key, request.Language, request.Subject ?? "", request.Text, request.Html, (JsonObject?)request.Model);
				return json(new MessageTemplatePreviewDto { Subject = rendered.Subject, Text = rendered.Text, Html = rendered.Html });
			}
			catch (TemplateRenderException ex)
			{
				return cannotBeFilledIn(ex);
			}
			catch (Exception ex) when (ex is TemplateModelJsonException or TemplateModelException)
			{
				// Names a property path, never a value.
				return Problem(title: "Invalid model", detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
			}
		}

		private static JsonResult json(object value, int statusCode = StatusCodes.Status200OK) => new(value, ResponseJson) { StatusCode = statusCode };

		private ObjectResult noModel(string key) =>
			Problem(title: "Not Found", detail: $"No template model is registered for key '{key}'.", statusCode: StatusCodes.Status404NotFound);

		/// <summary>Names the part and the problem, never a value: the draft is rendered against made-up data.</summary>
		private ObjectResult cannotBeFilledIn(TemplateRenderException ex) =>
			Problem(title: "The template cannot be filled in", detail: $"The {ex.Part.ToLowerInvariant()} part: {ex.Problem}", statusCode: StatusCodes.Status422UnprocessableEntity);

		/// <summary>The login of whoever is calling, for the audit trail of a new version; "user:id" when the account cannot be read.</summary>
		private async Task<string> currentUserNameAsync(CancellationToken cancellationToken)
		{
			var userId = UserId;
			var name = await mRepository.GetQueryable<User>(u => u.Id == userId)
				.AsNoTracking()
				.Select(u => u.UserName)
				.FirstOrDefaultAsync(cancellationToken);
			return string.IsNullOrWhiteSpace(name) ? $"user:{userId}" : name;
		}

		private readonly TemplateAuthoring mAuthoring;
		private readonly IRepository mRepository;
	}
}
