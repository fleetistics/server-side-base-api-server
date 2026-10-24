using System.Text.RegularExpressions;
using api_server.Controllers.Base;
using api_server.Controllers.Test.Dto;
using exs.Database.Commons.Interfaces;
using exs.notifications_contracts;
using exs.notifications_model.Outbox;
using exs.notifications_service.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace api_server.Controllers.Test
{
	/// <summary>
	/// Tries the real email sender from a dev machine: POST sends one mail through INotificationSender.CreateEmail,
	/// GET shows what the host knows about it. Opt-in: both endpoints are 404 unless <c>TestEmail:Enabled</c> is true
	/// (see <see cref="TestEmailEnabledAttribute"/>), because any logged-in user could otherwise send mail from the
	/// company's SMTP account. The recipient address is never put in a response or a log line.
	/// </summary>
	[TestEmailEnabled]
	public partial class EmailTestController : AuthAPIController
	{
		public EmailTestController(INotificationSender sender, IRepository repository, ILogger<EmailTestController> logger)
		{
			mSender = sender;
			mRepository = repository;
			mLogger = logger;
		}

		[AllowAnonymous]
		[HttpPost("/api/test/email")]
		[ProducesResponseType(typeof(EmailTestAcceptedDto), StatusCodes.Status202Accepted)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
		public async Task<IActionResult> Send([FromBody] EmailTestRequestDto request, CancellationToken cancellationToken)
		{
			var reference = Guid.NewGuid().ToString("N");
			var hasContent = !string.IsNullOrWhiteSpace(request.Text) || !string.IsNullOrWhiteSpace(request.Html);

			var message = new EmailMessage
			{
				To = request.To,
				ToName = blankToNull(request.ToName),
				Subject = blankToNull(request.Subject) ?? "Test email " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + " UTC",
				Text = hasContent ? blankToNull(request.Text) : "This is a test email sent from base-api-server through the email sender.",
				Html = blankToNull(request.Html),
				ReplyTo = blankToNull(request.ReplyTo),
				Priority = blankToNull(request.Priority),
				Account = blankToNull(request.Account),
				Ref = reference,
			};

			try
			{
				mSender.CreateEmail(mRepository, message);
			}
			catch (ArgumentException ex)
			{
				// The library never puts an address or text in this message.
				return Problem(title: "Invalid email message", detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
			}
			catch (InvalidOperationException)
			{
				return Problem(title: "Email transport is off",
					detail: "NotificationService:EmailTransport must be 'Sender' (with NotificationService:EmailSender configured) to send email.",
					statusCode: StatusCodes.Status409Conflict);
			}

			await mRepository.SaveAsync(cancellationToken);

			//mLogger.LogInformation("Test email {Reference} queued by user {UserId}", reference, UserId);
			return Accepted(new EmailTestAcceptedDto { Reference = reference });
		}

		/// <summary>Only reads, so it can be polled until state is completed (or rejected / expired).</summary>
		[HttpGet("/api/test/email/{reference}")]
		[ProducesResponseType(typeof(EmailTestStatusDto), StatusCodes.Status200OK)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
		public async Task<IActionResult> GetStatus(string reference, CancellationToken cancellationToken)
		{
			// Projected column by column: the Envelope (address and text) is never loaded.
			var row = await mRepository.GetQueryable<OutboxMessage>(o => o.Ref == reference && o.Channel == Channels.Email)
				.AsNoTracking()
				.OrderByDescending(o => o.Id)
				.Select(o => new
				{
					o.MessageId, o.State, o.ResultStatus, o.ErrorKind, o.ErrorCode, o.ErrorMessage, o.ProviderMessageId,
					o.DestinationInvalid, o.Attempts, o.SubmitError, o.Account, o.CreatedAt, o.CompletedAt,
				})
				.FirstOrDefaultAsync(cancellationToken);

			if (row == null) return Problem(title: "Not Found", statusCode: StatusCodes.Status404NotFound);

			return Ok(new EmailTestStatusDto
			{
				Reference = reference,
				MessageId = row.MessageId,
				State = stateName(row.State),
				ResultStatus = row.ResultStatus,
				ErrorKind = row.ErrorKind,
				// What a mail server says about a bounce may quote the address; this host's answer must not.
				ErrorCode = withoutAddresses(row.ErrorCode),
				ErrorMessage = withoutAddresses(row.ErrorMessage),
				ProviderMessageId = row.ProviderMessageId,
				DestinationInvalid = row.DestinationInvalid,
				Attempts = row.Attempts,
				SubmitError = withoutAddresses(row.SubmitError),
				Account = row.Account,
				CreatedAt = asUtc(row.CreatedAt),
				CompletedAt = row.CompletedAt == null ? null : asUtc(row.CompletedAt.Value),
			});
		}

		private static string stateName(byte state) => state switch
		{
			OutboxState.Pending => "pending",
			OutboxState.Submitted => "submitted",
			OutboxState.Completed => "completed",
			OutboxState.Rejected => "rejected",
			OutboxState.Expired => "expired",
			_ => "unknown",
		};

		private static string? blankToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

		private static string? withoutAddresses(string? value) => value == null ? null : AddressPattern().Replace(value, "[address]");

		private static DateTimeOffset asUtc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

		[GeneratedRegex(@"[^\s<>""',;:()]+@[^\s<>""',;()]+")]
		private static partial Regex AddressPattern();

		private readonly INotificationSender mSender;
		private readonly IRepository mRepository;
		private readonly ILogger<EmailTestController> mLogger;
	}
}
