using api_server.Services;
using api_server.Controllers.Contacts.Dto;
using api_server.Services.Notifications;
using db_model.Companies;
using db_model.Contacts;
using db_model.UserManagement;
using exs.Database.Commons.Interfaces;
using exs.notifications_service.Interfaces;
using exs.notifications_service.Templates;
using Microsoft.EntityFrameworkCore;

namespace api_server.Controllers.Contacts.Services
{
	/// <summary>
	/// Sends a message to a contact (or colleague) by email through the notification system, and logs an Email activity for it.
	/// The caller must be able to see the person. Replies go to the caller's own address.
	/// </summary>
	public interface IContactMessagesService
	{
		Task<ContactResult<ContactMessageResultDto>> SendAsync(int ownerCompanyId, int userId, int personId, SendContactMessageDto request, CancellationToken cancellationToken);
	}

	public sealed class ContactMessagesService : IContactMessagesService
	{
		public ContactMessagesService(IRepository repository, IContactsService contacts, IActivitiesService activities, INotificationSender sender, TimeProvider time, ILogger<ContactMessagesService> logger)
		{
			mRepository = repository;
			mContacts = contacts;
			mActivities = activities;
			mSender = sender;
			mTime = time;
			mLogger = logger;
		}

		public async Task<ContactResult<ContactMessageResultDto>> SendAsync(int ownerCompanyId, int userId, int personId, SendContactMessageDto request, CancellationToken cancellationToken)
		{
			var body = (request.Body ?? "").Trim();
			if (body.Length == 0) return ContactResult<ContactMessageResultDto>.Invalid("Write the message first.");

			// Visibility is the contact rule: another user's private contact is "not found".
			var person = await mContacts.GetPersonAsync(ownerCompanyId, userId, personId, cancellationToken);
			if (person.Outcome != ContactOutcome.Done) return ContactResult<ContactMessageResultDto>.NotFound(person.Message);
			var contact = person.Value!;
			if (contact.StatusId == UserStatus.Archived) return ContactResult<ContactMessageResultDto>.Invalid("An archived contact cannot be messaged.");
			if (string.IsNullOrWhiteSpace(contact.Email)) return ContactResult<ContactMessageResultDto>.Invalid("This contact has no email address.");

			var sender = await mRepository.GetQueryable<User>(u => u.Id == userId).AsNoTracking()
				.Select(u => new { u.DisplayName, u.FirstName, u.LastName, u.UserName, u.Email }).SingleOrDefaultAsync(cancellationToken);
			if (sender == null) return ContactResult<ContactMessageResultDto>.NotFound("There is no such user.");
			var senderName = Pick(sender.DisplayName, UserNameHelper.Compose(sender.FirstName, sender.LastName), sender.UserName);
			var companyName = await mRepository.GetQueryable<Company>(c => c.Id == ownerCompanyId).AsNoTracking().Select(c => c.Name).SingleAsync(cancellationToken);

			var subject = SingleLine(request.Subject);
			if (subject.Length == 0) subject = $"Message from {senderName}";

			var data = new ContactMessageData
			{
				RecipientName = contact.FirstName ?? contact.Name,
				SenderName = senderName,
				SenderCompanyName = companyName,
				Subject = subject,
				Body = body,
			};
			var envelope = new EmailEnvelope
			{
				To = contact.Email!.Trim(),
				ToName = SingleLineOrNull(contact.Name),
				ReplyTo = string.IsNullOrWhiteSpace(sender.Email) ? null : sender.Email.Trim(),
				ReplyToName = string.IsNullOrWhiteSpace(sender.Email) ? null : SingleLineOrNull(senderName),
				Ref = $"contact-message-{personId}-{userId}",
			};

			try
			{
				// Written into the repository, not saved yet: it is saved together with the activity below.
				await mSender.CreateEmailFromTemplateAsync(mRepository, envelope, data, null, cancellationToken);
			}
			catch (TemplateNotFoundException)
			{
				return ContactResult<ContactMessageResultDto>.Invalid("The message template is missing: add 'contact-message' on Portal Admin > Message templates.");
			}
			catch (InvalidOperationException)
			{
				return ContactResult<ContactMessageResultDto>.Invalid("Email sending is not available right now.");
			}
			catch (Exception ex) when (ex is ArgumentException or TemplateRenderException or TemplateModelException)
			{
				// Neither the address nor the text goes to the log.
				mLogger.LogWarning(ex, "A message to contact {PersonId} could not be turned into an email", personId);
				return ContactResult<ContactMessageResultDto>.Invalid("The message could not be turned into an email. Check the address and the text.");
			}

			// The Email activity is made by the activity service (its rules for privacy and colleagues). Saving it also saves the queued email.
			var activity = await mActivities.CreateAsync(ownerCompanyId, userId, new SaveActivityDto
			{
				TypeId = ActivityType.Email,
				Subject = subject,
				Notes = body,
				CompanyId = contact.CompanyId,
				ContactUserId = personId,
				StartAt = mTime.GetUtcNow().UtcDateTime,
				DurationMinutes = 5,
				Log = true,
			}, cancellationToken);
			if (activity.Outcome != ContactOutcome.Done)
			{
				// Nothing was saved: the request's scope ends without the queued email.
				return ContactResult<ContactMessageResultDto>.Invalid(activity.Message ?? "The message could not be recorded.");
			}
			return ContactResult<ContactMessageResultDto>.Ok(new ContactMessageResultDto { ActivityId = activity.Value!.Id });
		}

		private static string SingleLine(string? value) => string.Join(' ', (value ?? "").Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

		private static string? SingleLineOrNull(string? value) { var s = SingleLine(value); return s.Length == 0 ? null : s; }

		private static string Pick(params string?[] values) => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim() ?? "";

		private readonly IRepository mRepository;
		private readonly IContactsService mContacts;
		private readonly IActivitiesService mActivities;
		private readonly INotificationSender mSender;
		private readonly TimeProvider mTime;
		private readonly ILogger<ContactMessagesService> mLogger;
	}
}
