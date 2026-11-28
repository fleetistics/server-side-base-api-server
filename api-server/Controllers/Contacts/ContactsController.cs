using db_model.UserManagement;
using api_server.Controllers.Base;
using api_server.Controllers.Contacts.Dto;
using api_server.Controllers.Contacts.Services;
using Microsoft.AspNetCore.Mvc;

namespace api_server.Controllers.Contacts
{
	/// <summary>
	/// Contacts ▸ Companies and Contacts: the companies and people the caller's company keeps as contacts, plus its own company and colleagues.
	/// Everything belongs to the caller's company, which comes from the access token (403 without one). A contact marked private shows
	/// only to the user it is private to; asking for another user's private contact answers 404.
	///
	/// NOTE: access is not restricted by role yet (same as the rest of the company admin endpoints).
	/// </summary>
	public class ContactsController : AuthAPIController
	{
		public ContactsController(IContactsService contacts, IContactMessagesService messages)
		{
			mContacts = contacts;
			mMessages = messages;
		}

		/// <summary>The lists the forms pick from (types, lead sources, payment terms, account managers).</summary>
		[HttpGet("/api/contacts/lookups")]
		[ProducesResponseType(typeof(ContactLookupsDto), StatusCodes.Status200OK)]
		[ProducesResponseType(StatusCodes.Status403Forbidden)]
		public async Task<IActionResult> GetLookups(CancellationToken cancellationToken)
		{
			if (CompanyId is not int companyId) return Forbid();
			return Ok(await mContacts.GetLookupsAsync(companyId, cancellationToken));
		}

		// ---- companies ----

		/// <summary>The caller's own company and the company contacts they can see.</summary>
		[HttpGet("/api/contacts/companies")]
		[ProducesResponseType(typeof(IReadOnlyList<ContactCompanyRowDto>), StatusCodes.Status200OK)]
		[ProducesResponseType(StatusCodes.Status403Forbidden)]
		public async Task<IActionResult> GetCompanies(CancellationToken cancellationToken)
		{
			if (CompanyId is not int companyId) return Forbid();
			return Ok(await mContacts.ListCompaniesAsync(companyId, UserId, cancellationToken));
		}

		[HttpGet("/api/contacts/companies/{companyId:int}")]
		[ProducesResponseType(typeof(ContactCompanyDto), StatusCodes.Status200OK)]
		[ProducesResponseType(StatusCodes.Status403Forbidden)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
		public async Task<IActionResult> GetCompany(int companyId, CancellationToken cancellationToken)
		{
			if (CompanyId is not int ownerId) return Forbid();
			return Answer(await mContacts.GetCompanyAsync(ownerId, UserId, companyId, cancellationToken));
		}

		/// <summary>Adds a company contact (a company, its info and the link to the caller's company), and optionally its primary contact person.</summary>
		[HttpPost("/api/contacts/companies")]
		[ProducesResponseType(typeof(ContactCompanyDto), StatusCodes.Status201Created)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
		[ProducesResponseType(StatusCodes.Status403Forbidden)]
		public async Task<IActionResult> CreateCompany([FromBody] SaveContactCompanyDto request, CancellationToken cancellationToken)
		{
			if (CompanyId is not int ownerId) return Forbid();
			var result = await mContacts.CreateCompanyAsync(ownerId, UserId, request, cancellationToken);
			return result.Outcome == ContactOutcome.Done ? Created($"/api/contacts/companies/{result.Value!.Id}", result.Value) : Failure(result.Outcome, result.Message);
		}

		/// <summary>Replaces the company's info (send the whole form). The caller's own company can be edited too, except its name and type.</summary>
		[HttpPut("/api/contacts/companies/{companyId:int}")]
		[ProducesResponseType(typeof(ContactCompanyDto), StatusCodes.Status200OK)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
		[ProducesResponseType(StatusCodes.Status403Forbidden)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
		public async Task<IActionResult> UpdateCompany(int companyId, [FromBody] SaveContactCompanyDto request, CancellationToken cancellationToken)
		{
			if (CompanyId is not int ownerId) return Forbid();
			return Answer(await mContacts.UpdateCompanyAsync(ownerId, UserId, companyId, request, cancellationToken));
		}

		// ---- people ----

		/// <summary>The caller's colleagues and the contact people they can see (with their StatusId: 1 Active, 13 Suspended, 14 Archived).</summary>
		[HttpGet("/api/contacts/people")]
		[ProducesResponseType(typeof(IReadOnlyList<ContactPersonRowDto>), StatusCodes.Status200OK)]
		[ProducesResponseType(StatusCodes.Status403Forbidden)]
		public async Task<IActionResult> GetPeople(CancellationToken cancellationToken)
		{
			if (CompanyId is not int companyId) return Forbid();
			return Ok(await mContacts.ListPeopleAsync(companyId, UserId, cancellationToken));
		}

		[HttpGet("/api/contacts/people/{personId:int}")]
		[ProducesResponseType(typeof(ContactPersonDto), StatusCodes.Status200OK)]
		[ProducesResponseType(StatusCodes.Status403Forbidden)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
		public async Task<IActionResult> GetPerson(int personId, CancellationToken cancellationToken)
		{
			if (CompanyId is not int companyId) return Forbid();
			return Answer(await mContacts.GetPersonAsync(companyId, UserId, personId, cancellationToken));
		}

		/// <summary>Adds a contact person (a user of source Contact, its info and the link). With <c>NewCompany</c> the company is made first.</summary>
		[HttpPost("/api/contacts/people")]
		[ProducesResponseType(typeof(ContactPersonDto), StatusCodes.Status201Created)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
		[ProducesResponseType(StatusCodes.Status403Forbidden)]
		public async Task<IActionResult> CreatePerson([FromBody] SaveContactPersonDto request, CancellationToken cancellationToken)
		{
			if (CompanyId is not int companyId) return Forbid();
			var result = await mContacts.CreatePersonAsync(companyId, UserId, request, cancellationToken);
			return result.Outcome == ContactOutcome.Done ? Created($"/api/contacts/people/{result.Value!.Id}", result.Value) : Failure(result.Outcome, result.Message);
		}

		/// <summary>Replaces the person's info (send the whole form). Colleagues are not edited here (404): they are managed in User Management.</summary>
		[HttpPut("/api/contacts/people/{personId:int}")]
		[ProducesResponseType(typeof(ContactPersonDto), StatusCodes.Status200OK)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
		[ProducesResponseType(StatusCodes.Status403Forbidden)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
		public async Task<IActionResult> UpdatePerson(int personId, [FromBody] SaveContactPersonDto request, CancellationToken cancellationToken)
		{
			if (CompanyId is not int companyId) return Forbid();
			return Answer(await mContacts.UpdatePersonAsync(companyId, UserId, personId, request, cancellationToken));
		}

		/// <summary>Changes only the contact type, the active flag and/or whether the person is their company's primary contact (the fields that are sent). Colleagues answer 404.</summary>
		[HttpPatch("/api/contacts/people/{personId:int}")]
		[ProducesResponseType(typeof(ContactPersonDto), StatusCodes.Status200OK)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
		[ProducesResponseType(StatusCodes.Status403Forbidden)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
		public async Task<IActionResult> PatchPerson(int personId, [FromBody] PatchContactPersonDto request, CancellationToken cancellationToken)
		{
			if (CompanyId is not int companyId) return Forbid();
			return Answer(await mContacts.PatchPersonAsync(companyId, UserId, personId, request, cancellationToken));
		}

		/// <summary>Sends the person an email (through the notification system; replies go to the caller) and logs an Email activity for it. Colleagues can be messaged too.</summary>
		[HttpPost("/api/contacts/people/{personId:int}/message")]
		[ProducesResponseType(typeof(ContactMessageResultDto), StatusCodes.Status200OK)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
		[ProducesResponseType(StatusCodes.Status403Forbidden)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
		public async Task<IActionResult> SendMessage(int personId, [FromBody] SendContactMessageDto request, CancellationToken cancellationToken)
		{
			if (CompanyId is not int companyId) return Forbid();
			return Answer(await mMessages.SendAsync(companyId, UserId, personId, request, cancellationToken));
		}

		/// <summary>Suspends the person (reversible with resume). A colleague's membership of the caller's company is suspended, not their account. If they were the company's primary contact, another active contact takes over.</summary>
		[HttpPost("/api/contacts/people/{personId:int}/suspend")]
		[ProducesResponseType(typeof(ContactPersonDto), StatusCodes.Status200OK)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
		[ProducesResponseType(StatusCodes.Status403Forbidden)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
		public async Task<IActionResult> Suspend(int personId, CancellationToken cancellationToken)
		{
			if (CompanyId is not int companyId) return Forbid();
			return Answer(await mContacts.SetStatusAsync(companyId, UserId, personId, UserStatus.Suspended, cancellationToken));
		}

		/// <summary>Makes a suspended person active again. An archived person cannot be resumed (400).</summary>
		[HttpPost("/api/contacts/people/{personId:int}/resume")]
		[ProducesResponseType(typeof(ContactPersonDto), StatusCodes.Status200OK)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
		[ProducesResponseType(StatusCodes.Status403Forbidden)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
		public async Task<IActionResult> Resume(int personId, CancellationToken cancellationToken)
		{
			if (CompanyId is not int companyId) return Forbid();
			return Answer(await mContacts.SetStatusAsync(companyId, UserId, personId, UserStatus.Active, cancellationToken));
		}

		/// <summary>Archives the person. This is final: an archived person cannot be changed or brought back.</summary>
		[HttpPost("/api/contacts/people/{personId:int}/archive")]
		[ProducesResponseType(typeof(ContactPersonDto), StatusCodes.Status200OK)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
		[ProducesResponseType(StatusCodes.Status403Forbidden)]
		[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
		public async Task<IActionResult> Archive(int personId, CancellationToken cancellationToken)
		{
			if (CompanyId is not int companyId) return Forbid();
			return Answer(await mContacts.SetStatusAsync(companyId, UserId, personId, UserStatus.Archived, cancellationToken));
		}

		private IActionResult Answer<T>(ContactResult<T> result) =>
			result.Outcome == ContactOutcome.Done ? Ok(result.Value) : Failure(result.Outcome, result.Message);

		private ObjectResult Failure(ContactOutcome outcome, string? message) => outcome switch
		{
			ContactOutcome.NotFound => Problem(title: "Not Found", detail: message ?? "There is no such contact.", statusCode: StatusCodes.Status404NotFound),
			ContactOutcome.Invalid => Problem(title: "Invalid input", detail: message, statusCode: StatusCodes.Status400BadRequest),
			ContactOutcome.NotAllowed => Problem(title: "Not allowed", detail: message, statusCode: StatusCodes.Status403Forbidden),
			_ => throw new InvalidOperationException("Unhandled outcome"),
		};

		private readonly IContactsService mContacts;
		private readonly IContactMessagesService mMessages;
	}
}
