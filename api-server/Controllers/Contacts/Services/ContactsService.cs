using System.Net.Mail;
using api_server.Controllers.Contacts.Dto;
using db_model.Companies;
using db_model.Contacts;
using db_model.UserManagement;
using exs.Database.Commons.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace api_server.Controllers.Contacts.Services
{
	public enum ContactOutcome
	{
		Done,
		NotFound,
		/// <summary>The input is not acceptable; <see cref="ContactResult{T}.Message"/> says why.</summary>
		Invalid,
		/// <summary>A rule says no (for example only the owner user can share a private contact).</summary>
		NotAllowed,
	}

	public sealed record ContactResult<T>(ContactOutcome Outcome, T? Value = default, string? Message = null)
	{
		public static ContactResult<T> Ok(T value) => new(ContactOutcome.Done, value);
		public static ContactResult<T> NotFound(string? message = null) => new(ContactOutcome.NotFound, default, message);
		public static ContactResult<T> Invalid(string message) => new(ContactOutcome.Invalid, default, message);
		public static ContactResult<T> NotAllowed(string message) => new(ContactOutcome.NotAllowed, default, message);
	}

	/// <summary>
	/// The CRM side of companies and people for the caller's company (the owner). What the owner sees:
	/// its own company, the companies it added as contacts and the people it added, except the ones another user marked private
	/// (OwnerUserId set to someone else). Company and person rows are never read by id alone: every read starts from these links.
	/// A contact person is a user row (source Contact, status Inactive: nothing treats them as an account); its email and direct phone are the user's own.
	/// </summary>
	public interface IContactsService
	{
		Task<ContactLookupsDto> GetLookupsAsync(int ownerCompanyId, CancellationToken cancellationToken);

		Task<IReadOnlyList<ContactCompanyRowDto>> ListCompaniesAsync(int ownerCompanyId, int userId, CancellationToken cancellationToken);
		Task<ContactResult<ContactCompanyDto>> GetCompanyAsync(int ownerCompanyId, int userId, int companyId, CancellationToken cancellationToken);
		Task<ContactResult<ContactCompanyDto>> CreateCompanyAsync(int ownerCompanyId, int userId, SaveContactCompanyDto request, CancellationToken cancellationToken);
		Task<ContactResult<ContactCompanyDto>> UpdateCompanyAsync(int ownerCompanyId, int userId, int companyId, SaveContactCompanyDto request, CancellationToken cancellationToken);

		Task<IReadOnlyList<ContactPersonRowDto>> ListPeopleAsync(int ownerCompanyId, int userId, CancellationToken cancellationToken);
		Task<ContactResult<ContactPersonDto>> GetPersonAsync(int ownerCompanyId, int userId, int personId, CancellationToken cancellationToken);
		Task<ContactResult<ContactPersonDto>> CreatePersonAsync(int ownerCompanyId, int userId, SaveContactPersonDto request, CancellationToken cancellationToken);
		Task<ContactResult<ContactPersonDto>> UpdatePersonAsync(int ownerCompanyId, int userId, int personId, SaveContactPersonDto request, CancellationToken cancellationToken);
		Task<ContactResult<ContactPersonDto>> SetArchivedAsync(int ownerCompanyId, int userId, int personId, bool archived, CancellationToken cancellationToken);
	}

	public sealed class ContactsService : IContactsService
	{
		private const int MaxLocations = 20;

		public ContactsService(IRepository repository, TimeProvider time)
		{
			mRepository = repository;
			mTime = time;
		}

		// ---- lookups ----

		public async Task<ContactLookupsDto> GetLookupsAsync(int ownerCompanyId, CancellationToken cancellationToken)
		{
			var result = new ContactLookupsDto
			{
				CompanyTypes = (await mRepository.GetQueryable<CompanyType>(t => t.Id != CompanyType.Internal).AsNoTracking().OrderBy(t => t.Id).ToListAsync(cancellationToken)).Select(t => new LookupItemDto { Id = t.Id, Name = t.Name }).ToList(),
				ContactTypes = (await mRepository.GetQueryable<ContactType>().AsNoTracking().OrderBy(t => t.Id).ToListAsync(cancellationToken)).Select(t => new LookupItemDto { Id = t.Id, Name = t.Name }).ToList(),
				LeadSources = (await mRepository.GetQueryable<LeadSource>().AsNoTracking().OrderBy(t => t.Id).ToListAsync(cancellationToken)).Select(t => new LookupItemDto { Id = t.Id, Name = t.Name }).ToList(),
				PaymentTerms = (await mRepository.GetQueryable<PaymentTerm>().AsNoTracking().OrderBy(t => t.Id).ToListAsync(cancellationToken)).Select(t => new LookupItemDto { Id = t.Id, Name = t.Name }).ToList(),
			};
			var memberIds = await MemberIdsAsync(ownerCompanyId, cancellationToken);
			result.AccountManagers = (await mRepository.GetQueryable<User>(u => memberIds.Contains(u.Id)).AsNoTracking().Select(u => new { u.Id, u.DisplayName, u.FullName, u.UserName }).ToListAsync(cancellationToken))
				.Select(u => new UserLookupDto { Id = u.Id, Name = Pick(u.DisplayName, u.FullName, u.UserName) }).OrderBy(u => u.Name).ToList();
			return result;
		}

		// ---- companies ----

		public async Task<IReadOnlyList<ContactCompanyRowDto>> ListCompaniesAsync(int ownerCompanyId, int userId, CancellationToken cancellationToken)
		{
			var links = await VisibleCompanyLinksAsync(ownerCompanyId, userId, cancellationToken);
			var ids = links.Select(l => l.ContactCompanyId).Append(ownerCompanyId).ToList();

			var companies = await mRepository.GetQueryable<Company>(c => ids.Contains(c.Id)).AsNoTracking().ToListAsync(cancellationToken);
			var infos = (await mRepository.GetQueryable<CompanyInfo>(i => ids.Contains(i.CompanyId)).AsNoTracking().ToListAsync(cancellationToken)).ToDictionary(i => i.CompanyId);
			var locations = (await mRepository.GetQueryable<CompanyLocation>(l => ids.Contains(l.CompanyId) && l.IsJobSite).AsNoTracking().OrderBy(l => l.Id).ToListAsync(cancellationToken))
				.GroupBy(l => l.CompanyId).ToDictionary(g => g.Key, g => g.First());

			var primaryIds = infos.Values.Where(i => i.PrimaryContactUserId != null).Select(i => i.PrimaryContactUserId!.Value).ToList();
			var primaries = await PeopleNamesAsync(primaryIds, cancellationToken);
			var privateIds = links.Where(l => l.OwnerUserId != null).Select(l => l.ContactCompanyId).ToHashSet();

			return companies.Select(c =>
			{
				infos.TryGetValue(c.Id, out var info);
				locations.TryGetValue(c.Id, out var place);
				var primary = info?.PrimaryContactUserId is int pid && primaries.TryGetValue(pid, out var p) ? p : default;
				return new ContactCompanyRowDto
				{
					Id = c.Id,
					Name = c.Name,
					TypeId = c.Id == ownerCompanyId ? CompanyType.Internal : info?.TypeId ?? CompanyType.Customer,
					Industry = info?.Industry,
					AccountNumber = info?.AccountNumber,
					StatusId = c.StatusId,
					City = place?.City,
					State = place?.State,
					FleetVehicles = info?.FleetVehicles,
					AnnualRevenue = info?.AnnualRevenue,
					PrimaryContactUserId = info?.PrimaryContactUserId,
					PrimaryContactName = primary.Name,
					PrimaryContactTitle = primary.Title,
					IsOwnCompany = c.Id == ownerCompanyId,
					IsPrivate = privateIds.Contains(c.Id),
				};
			}).OrderBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
		}

		public async Task<ContactResult<ContactCompanyDto>> GetCompanyAsync(int ownerCompanyId, int userId, int companyId, CancellationToken cancellationToken)
		{
			if (!await CanSeeCompanyAsync(ownerCompanyId, userId, companyId, cancellationToken)) return ContactResult<ContactCompanyDto>.NotFound();
			return ContactResult<ContactCompanyDto>.Ok(await CompanyDtoAsync(ownerCompanyId, userId, companyId, cancellationToken));
		}

		public async Task<ContactResult<ContactCompanyDto>> CreateCompanyAsync(int ownerCompanyId, int userId, SaveContactCompanyDto request, CancellationToken cancellationToken)
		{
			var error = await ValidateCompanyAsync(ownerCompanyId, request, isOwn: false, cancellationToken);
			if (error != null) return ContactResult<ContactCompanyDto>.Invalid(error);
			if (request.PrimaryContact != null)
			{
				var personError = await ValidatePersonAsync(request.PrimaryContact, cancellationToken);
				if (personError != null) return ContactResult<ContactCompanyDto>.Invalid(personError);
			}

			var now = Now();
			var company = new Company { Name = request.Name.Trim(), StatusId = request.StatusId ?? CompanyStatus.Active, LatestUpdate = now };
			mRepository.Create(company);
			await mRepository.SaveAsync(cancellationToken);

			try
			{
				var info = new CompanyInfo { CompanyId = company.Id, AccountNumber = AccountNumberOf(company.Id), LatestUpdate = now };
				ApplyInfo(info, request, isOwn: false);
				mRepository.Create(info);
				mRepository.Create(new CompanyContact { OwnerCompanyId = ownerCompanyId, ContactCompanyId = company.Id, OwnerUserId = request.IsPrivate == true ? userId : null, CreationDate = now, LatestUpdate = now });
				ReplaceLocations(company.Id, new List<CompanyLocation>(), request.Locations, now);
				await mRepository.SaveAsync(cancellationToken);

				if (request.PrimaryContact != null)
				{
					var person = request.PrimaryContact;
					person.CompanyId = company.Id;
					person.IsPrimary = true;
					person.IsPrivate = request.IsPrivate;
					var created = await CreatePersonCoreAsync(ownerCompanyId, userId, person, cancellationToken);
					if (created.Outcome != ContactOutcome.Done) throw new InvalidOperationException(created.Message ?? "The primary contact could not be added.");
				}
			}
			catch
			{
				// Nothing else holds the new company yet, and the foreign keys cascade, so removing it removes what was made for it.
				await TryRemoveCompanyAsync(company.Id);
				throw;
			}

			return ContactResult<ContactCompanyDto>.Ok(await CompanyDtoAsync(ownerCompanyId, userId, company.Id, cancellationToken));
		}

		public async Task<ContactResult<ContactCompanyDto>> UpdateCompanyAsync(int ownerCompanyId, int userId, int companyId, SaveContactCompanyDto request, CancellationToken cancellationToken)
		{
			var isOwn = companyId == ownerCompanyId;
			var link = isOwn ? null : await FindVisibleCompanyLinkAsync(ownerCompanyId, userId, companyId, cancellationToken);
			if (!isOwn && link == null) return ContactResult<ContactCompanyDto>.NotFound();

			var error = await ValidateCompanyAsync(ownerCompanyId, request, isOwn, cancellationToken);
			if (error != null) return ContactResult<ContactCompanyDto>.Invalid(error);

			if (link != null && request.IsPrivate == false && link.OwnerUserId != null)
			{
				if (link.OwnerUserId != userId) return ContactResult<ContactCompanyDto>.NotAllowed("Only the user it is private to can share this company.");
				link.OwnerUserId = null;
			}
			else if (link != null && request.IsPrivate == true && link.OwnerUserId == null)
			{
				return ContactResult<ContactCompanyDto>.NotAllowed("A company can be made private only when it is added.");
			}

			var now = Now();
			var company = await mRepository.GetQueryable<Company>(c => c.Id == companyId).SingleAsync(cancellationToken);
			if (!isOwn)
			{
				company.Name = request.Name.Trim();
				if (request.StatusId != null) company.StatusId = request.StatusId.Value;
				company.LatestUpdate = now;
			}

			var info = await mRepository.GetQueryable<CompanyInfo>(i => i.CompanyId == companyId).SingleOrDefaultAsync(cancellationToken);
			if (info == null)
			{
				info = new CompanyInfo { CompanyId = companyId, AccountNumber = AccountNumberOf(companyId), LatestUpdate = now };
				mRepository.Create(info);
			}
			ApplyInfo(info, request, isOwn);
			info.LatestUpdate = now;

			if (request.Locations != null)
			{
				var existing = await mRepository.GetQueryable<CompanyLocation>(l => l.CompanyId == companyId).ToListAsync(cancellationToken);
				ReplaceLocations(companyId, existing, request.Locations, now);
			}
			if (link != null) link.LatestUpdate = now;

			await mRepository.SaveAsync(cancellationToken);
			return ContactResult<ContactCompanyDto>.Ok(await CompanyDtoAsync(ownerCompanyId, userId, companyId, cancellationToken));
		}

		// ---- people ----

		public async Task<IReadOnlyList<ContactPersonRowDto>> ListPeopleAsync(int ownerCompanyId, int userId, CancellationToken cancellationToken)
		{
			var links = await VisiblePersonLinksAsync(ownerCompanyId, userId, cancellationToken);
			var memberIds = await MemberIdsAsync(ownerCompanyId, cancellationToken);
			var ids = links.Select(l => l.ContactUserId).Concat(memberIds).Distinct().ToList();

			var users = await mRepository.GetQueryable<User>(u => ids.Contains(u.Id)).AsNoTracking().ToListAsync(cancellationToken);
			var infos = (await mRepository.GetQueryable<UserInfo>(i => ids.Contains(i.UserId)).AsNoTracking().ToListAsync(cancellationToken)).ToDictionary(i => i.UserId);
			var companyIds = users.Where(u => u.CompanyId != null).Select(u => u.CompanyId!.Value).Distinct().ToList();
			var companyNames = (await mRepository.GetQueryable<Company>(c => companyIds.Contains(c.Id)).AsNoTracking().Select(c => new { c.Id, c.Name }).ToListAsync(cancellationToken)).ToDictionary(c => c.Id, c => c.Name);
			var primaryIds = (await mRepository.GetQueryable<CompanyInfo>(i => i.PrimaryContactUserId != null && ids.Contains(i.PrimaryContactUserId!.Value)).AsNoTracking().Select(i => i.PrimaryContactUserId!.Value).ToListAsync(cancellationToken)).ToHashSet();
			var linkByUser = links.ToDictionary(l => l.ContactUserId);
			var ownName = (await mRepository.GetQueryable<Company>(c => c.Id == ownerCompanyId).AsNoTracking().Select(c => c.Name).SingleOrDefaultAsync(cancellationToken)) ?? "";

			var rows = new List<ContactPersonRowDto>();
			foreach (var u in users)
			{
				infos.TryGetValue(u.Id, out var info);
				if (linkByUser.TryGetValue(u.Id, out var link))
				{
					rows.Add(new ContactPersonRowDto
					{
						Id = u.Id,
						Name = PersonName(u, info),
						IsEmployee = false,
						ContactTypeId = info?.ContactTypeId ?? ContactType.Contact,
						CompanyId = u.CompanyId,
						CompanyName = u.CompanyId is int cid && companyNames.TryGetValue(cid, out var n) ? n : null,
						Title = info?.Title,
						Email = u.Email,
						Phone = u.Phone,
						IsPrimary = primaryIds.Contains(u.Id),
						IsActive = info?.IsActive ?? true,
						IsArchived = link.IsArchived,
						IsPrivate = link.OwnerUserId != null,
					});
				}
				else if (memberIds.Contains(u.Id))
				{
					rows.Add(new ContactPersonRowDto
					{
						Id = u.Id,
						Name = PersonName(u, info),
						IsEmployee = true,
						CompanyId = ownerCompanyId,
						CompanyName = ownName,
						Title = info?.Title,
						Email = u.Email,
						Phone = u.Phone,
						IsPrimary = primaryIds.Contains(u.Id),
						IsActive = true,
					});
				}
			}
			return rows.OrderBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
		}

		public async Task<ContactResult<ContactPersonDto>> GetPersonAsync(int ownerCompanyId, int userId, int personId, CancellationToken cancellationToken)
		{
			var dto = await PersonDtoAsync(ownerCompanyId, userId, personId, cancellationToken);
			return dto == null ? ContactResult<ContactPersonDto>.NotFound() : ContactResult<ContactPersonDto>.Ok(dto);
		}

		public async Task<ContactResult<ContactPersonDto>> CreatePersonAsync(int ownerCompanyId, int userId, SaveContactPersonDto request, CancellationToken cancellationToken)
		{
			var error = await ValidatePersonAsync(request, cancellationToken);
			if (error != null) return ContactResult<ContactPersonDto>.Invalid(error);

			if (request.NewCompany != null)
			{
				// The person is added as the primary contact of the company that is made for them.
				request.NewCompany.PrimaryContact = null;
				var made = await CreateCompanyAsync(ownerCompanyId, userId, request.NewCompany, cancellationToken);
				if (made.Outcome != ContactOutcome.Done) return ContactResult<ContactPersonDto>.Invalid(made.Message ?? "The company could not be added.");
				request.CompanyId = made.Value!.Id;
				request.IsPrimary ??= true;
				request.IsPrivate ??= request.NewCompany.IsPrivate;
			}
			else if (request.CompanyId is not int cid || !await CanSeeCompanyAsync(ownerCompanyId, userId, cid, cancellationToken))
			{
				return ContactResult<ContactPersonDto>.Invalid("Pick the company the person works for.");
			}

			var created = await CreatePersonCoreAsync(ownerCompanyId, userId, request, cancellationToken);
			if (created.Outcome != ContactOutcome.Done) return ContactResult<ContactPersonDto>.Invalid(created.Message ?? "The person could not be added.");
			return ContactResult<ContactPersonDto>.Ok((await PersonDtoAsync(ownerCompanyId, userId, created.Value, cancellationToken))!);
		}

		public async Task<ContactResult<ContactPersonDto>> UpdatePersonAsync(int ownerCompanyId, int userId, int personId, SaveContactPersonDto request, CancellationToken cancellationToken)
		{
			var link = await FindVisiblePersonLinkAsync(ownerCompanyId, userId, personId, cancellationToken);
			// Colleagues are managed in User Management, so only contacts can be edited here.
			if (link == null) return ContactResult<ContactPersonDto>.NotFound();

			var error = await ValidatePersonAsync(request, cancellationToken);
			if (error != null) return ContactResult<ContactPersonDto>.Invalid(error);
			if (request.CompanyId is not int newCompanyId || !await CanSeeCompanyAsync(ownerCompanyId, userId, newCompanyId, cancellationToken))
				return ContactResult<ContactPersonDto>.Invalid("Pick the company the person works for.");

			if (request.IsPrivate == false && link.OwnerUserId != null)
			{
				if (link.OwnerUserId != userId) return ContactResult<ContactPersonDto>.NotAllowed("Only the user it is private to can share this person.");
				link.OwnerUserId = null;
			}
			else if (request.IsPrivate == true && link.OwnerUserId == null)
			{
				return ContactResult<ContactPersonDto>.NotAllowed("A person can be made private only when they are added.");
			}

			var now = Now();
			var user = await mRepository.GetQueryable<User>(u => u.Id == personId).SingleAsync(cancellationToken);
			var oldCompanyId = user.CompanyId;
			ApplyPerson(user, request);
			user.CompanyId = newCompanyId;
			user.LatestUpdate = now;

			var info = await mRepository.GetQueryable<UserInfo>(i => i.UserId == personId).SingleOrDefaultAsync(cancellationToken);
			if (info == null)
			{
				info = new UserInfo { UserId = personId };
				mRepository.Create(info);
			}
			ApplyInfo(info, request);
			info.LatestUpdate = now;
			link.LatestUpdate = now;

			// A person who moves to another company stops being the primary contact of the old one.
			if (oldCompanyId is int oldId && oldId != newCompanyId)
			{
				var oldInfo = await mRepository.GetQueryable<CompanyInfo>(i => i.CompanyId == oldId).SingleOrDefaultAsync(cancellationToken);
				if (oldInfo?.PrimaryContactUserId == personId) { oldInfo.PrimaryContactUserId = null; oldInfo.LatestUpdate = now; }
			}
			await ApplyPrimaryAsync(newCompanyId, personId, request.IsPrimary, now, cancellationToken);

			await mRepository.SaveAsync(cancellationToken);
			return ContactResult<ContactPersonDto>.Ok((await PersonDtoAsync(ownerCompanyId, userId, personId, cancellationToken))!);
		}

		public async Task<ContactResult<ContactPersonDto>> SetArchivedAsync(int ownerCompanyId, int userId, int personId, bool archived, CancellationToken cancellationToken)
		{
			var link = await FindVisiblePersonLinkAsync(ownerCompanyId, userId, personId, cancellationToken);
			if (link == null) return ContactResult<ContactPersonDto>.NotFound();
			if (link.IsArchived != archived)
			{
				link.IsArchived = archived;
				link.LatestUpdate = Now();
				await mRepository.SaveAsync(cancellationToken);
			}
			return ContactResult<ContactPersonDto>.Ok((await PersonDtoAsync(ownerCompanyId, userId, personId, cancellationToken))!);
		}

		// ---- people: shared parts ----

		/// <summary>Makes the user, user_info and the link (checks the company is visible are the caller's). Returns the new user id.</summary>
		private async Task<ContactResult<int>> CreatePersonCoreAsync(int ownerCompanyId, int userId, SaveContactPersonDto request, CancellationToken cancellationToken)
		{
			if (request.CompanyId is not int companyId || !await CanSeeCompanyAsync(ownerCompanyId, userId, companyId, cancellationToken))
				return ContactResult<int>.Invalid("Pick the company the person works for.");

			var now = Now();
			var user = new User
			{
				// The name is not a login: the account is Inactive and has no password, so a random one only has to be unique.
				UserName = Guid.NewGuid().ToString("N"),
				CompanyId = companyId,
				StatusId = UserStatus.Inactive,
				UserSourceId = UserSource.Contact,
				LatestUpdate = now,
			};
			ApplyPerson(user, request);
			mRepository.Create(user);
			await mRepository.SaveAsync(cancellationToken);

			try
			{
				var info = new UserInfo { UserId = user.Id, LatestUpdate = now };
				ApplyInfo(info, request);
				mRepository.Create(info);
				mRepository.Create(new CompanyUserContact { OwnerCompanyId = ownerCompanyId, ContactUserId = user.Id, OwnerUserId = request.IsPrivate == true ? userId : null, CreationDate = now, LatestUpdate = now });
				await ApplyPrimaryAsync(companyId, user.Id, request.IsPrimary, now, cancellationToken);
				await mRepository.SaveAsync(cancellationToken);
			}
			catch
			{
				await TryRemoveUserAsync(user.Id);
				throw;
			}
			return ContactResult<int>.Ok(user.Id);
		}

		/// <summary>True: the person becomes the company's primary contact (its info row is made when there is none); false: they stop being it. Null: unchanged.</summary>
		private async Task ApplyPrimaryAsync(int companyId, int personId, bool? isPrimary, DateTime now, CancellationToken cancellationToken)
		{
			if (isPrimary == null) return;
			var info = await mRepository.GetQueryable<CompanyInfo>(i => i.CompanyId == companyId).SingleOrDefaultAsync(cancellationToken);
			if (info == null)
			{
				if (isPrimary != true) return;
				info = new CompanyInfo { CompanyId = companyId, AccountNumber = AccountNumberOf(companyId), LatestUpdate = now };
				mRepository.Create(info);
			}
			if (isPrimary == true) info.PrimaryContactUserId = personId;
			else if (info.PrimaryContactUserId == personId) info.PrimaryContactUserId = null;
			info.LatestUpdate = now;
		}

		private static void ApplyPerson(User user, SaveContactPersonDto request)
		{
			var first = request.FirstName.Trim();
			var last = Blank(request.LastName);
			var full = last == null ? first : $"{first} {last}";
			user.DisplayName = full;
			user.FullName = full;
			user.Email = Blank(request.Email);
			user.Phone = Blank(request.Phone);
		}

		private static void ApplyInfo(UserInfo info, SaveContactPersonDto request)
		{
			info.FirstName = request.FirstName.Trim();
			info.LastName = Blank(request.LastName);
			info.Title = Blank(request.Title);
			info.Mobile = Blank(request.Mobile);
			info.ContactTypeId = request.ContactTypeId ?? ContactType.Contact;
			info.LeadSourceId = request.LeadSourceId;
			info.IsActive = request.IsActive;
		}

		private async Task<string?> ValidatePersonAsync(SaveContactPersonDto request, CancellationToken cancellationToken)
		{
			if (string.IsNullOrWhiteSpace(request.FirstName)) return "Enter the first name.";
			var email = Blank(request.Email);
			if (email != null && !(MailAddress.TryCreate(email, out var parsed) && parsed.Address == email && email.Contains('@'))) return "Enter a valid email address.";
			if (request.ContactTypeId is short ct && !await mRepository.GetQueryable<ContactType>(t => t.Id == ct).AnyAsync(cancellationToken)) return "Unknown contact type.";
			if (request.LeadSourceId is short ls && !await mRepository.GetQueryable<LeadSource>(t => t.Id == ls).AnyAsync(cancellationToken)) return "Unknown lead source.";
			return null;
		}

		/// <summary>The person with this id if the caller may see them: a contact they can see, or a colleague.</summary>
		private async Task<ContactPersonDto?> PersonDtoAsync(int ownerCompanyId, int userId, int personId, CancellationToken cancellationToken)
		{
			var link = await FindVisiblePersonLinkAsync(ownerCompanyId, userId, personId, cancellationToken, track: false);
			var isEmployee = false;
			if (link == null)
			{
				isEmployee = (await MemberIdsAsync(ownerCompanyId, cancellationToken)).Contains(personId);
				if (!isEmployee) return null;
			}

			var user = await mRepository.GetQueryable<User>(u => u.Id == personId).AsNoTracking().SingleOrDefaultAsync(cancellationToken);
			if (user == null) return null;
			var info = await mRepository.GetQueryable<UserInfo>(i => i.UserId == personId).AsNoTracking().SingleOrDefaultAsync(cancellationToken);
			var companyId = isEmployee ? ownerCompanyId : user.CompanyId;
			string? companyName = null;
			var primary = false;
			if (companyId is int cid)
			{
				companyName = await mRepository.GetQueryable<Company>(c => c.Id == cid).AsNoTracking().Select(c => c.Name).SingleOrDefaultAsync(cancellationToken);
				primary = await mRepository.GetQueryable<CompanyInfo>(i => i.CompanyId == cid && i.PrimaryContactUserId == personId).AnyAsync(cancellationToken);
			}

			return new ContactPersonDto
			{
				Id = user.Id,
				Name = PersonName(user, info),
				IsEmployee = isEmployee,
				FirstName = info?.FirstName,
				LastName = info?.LastName,
				Title = info?.Title,
				Email = user.Email,
				Phone = user.Phone,
				Mobile = info?.Mobile,
				ContactTypeId = isEmployee ? null : info?.ContactTypeId ?? ContactType.Contact,
				LeadSourceId = info?.LeadSourceId,
				IsActive = isEmployee || (info?.IsActive ?? true),
				IsArchived = link?.IsArchived ?? false,
				IsPrivate = link?.OwnerUserId != null,
				IsPrimary = primary,
				CompanyId = companyId,
				CompanyName = companyName,
			};
		}

		// ---- companies: shared parts ----

		private async Task<ContactCompanyDto> CompanyDtoAsync(int ownerCompanyId, int userId, int companyId, CancellationToken cancellationToken)
		{
			var isOwn = companyId == ownerCompanyId;
			var company = await mRepository.GetQueryable<Company>(c => c.Id == companyId).AsNoTracking().SingleAsync(cancellationToken);
			var info = await mRepository.GetQueryable<CompanyInfo>(i => i.CompanyId == companyId).AsNoTracking().SingleOrDefaultAsync(cancellationToken);
			var locations = await mRepository.GetQueryable<CompanyLocation>(l => l.CompanyId == companyId).AsNoTracking().OrderBy(l => l.Id).ToListAsync(cancellationToken);
			var link = isOwn ? null : await FindVisibleCompanyLinkAsync(ownerCompanyId, userId, companyId, cancellationToken, track: false);

			string? managerName = null;
			if (info?.AccountManagerUserId is int mid)
			{
				var m = await mRepository.GetQueryable<User>(u => u.Id == mid).AsNoTracking().Select(u => new { u.DisplayName, u.FullName, u.UserName }).SingleOrDefaultAsync(cancellationToken);
				managerName = m == null ? null : Pick(m.DisplayName, m.FullName, m.UserName);
			}

			var visibleIds = (await VisiblePersonLinksAsync(ownerCompanyId, userId, cancellationToken)).Select(l => l.ContactUserId).ToList();
			var contactCount = await mRepository.GetQueryable<User>(u => visibleIds.Contains(u.Id) && u.CompanyId == companyId).CountAsync(cancellationToken);

			return new ContactCompanyDto
			{
				Id = company.Id,
				Name = company.Name,
				StatusId = company.StatusId,
				TypeId = isOwn ? CompanyType.Internal : info?.TypeId ?? CompanyType.Customer,
				IsOwnCompany = isOwn,
				IsPrivate = link?.OwnerUserId != null,
				LegalName = info?.LegalName,
				Industry = info?.Industry,
				Website = info?.Website,
				MainPhone = info?.MainPhone,
				AccountNumber = info?.AccountNumber ?? AccountNumberOf(company.Id),
				AccountManagerUserId = info?.AccountManagerUserId,
				AccountManagerName = managerName,
				PaymentTermId = info?.PaymentTermId,
				TaxId = info?.TaxId,
				Employees = info?.Employees,
				FleetVehicles = info?.FleetVehicles,
				CustomerSince = info?.CustomerSince,
				AnnualRevenue = info?.AnnualRevenue,
				Territory = info?.Territory,
				LeadSourceId = info?.LeadSourceId,
				PrimaryContactUserId = info?.PrimaryContactUserId,
				ContactCount = contactCount,
				Locations = locations.Select(l => new ContactLocationDto { Id = l.Id, Name = l.Name, Street = l.Street, City = l.City, State = l.State, Zip = l.Zip, IsJobSite = l.IsJobSite, IsBilling = l.IsBilling }).ToList(),
			};
		}

		private async Task<string?> ValidateCompanyAsync(int ownerCompanyId, SaveContactCompanyDto request, bool isOwn, CancellationToken cancellationToken)
		{
			if (!isOwn && string.IsNullOrWhiteSpace(request.Name)) return "Enter the company name.";
			if (!isOwn && request.TypeId is short t && (t == CompanyType.Internal || !await mRepository.GetQueryable<CompanyType>(x => x.Id == t).AnyAsync(cancellationToken))) return "Unknown company type.";
			if (!isOwn && request.StatusId is short s && s != CompanyStatus.Active && s != CompanyStatus.Inactive) return "Unknown company status.";
			if (request.PaymentTermId is short p && !await mRepository.GetQueryable<PaymentTerm>(x => x.Id == p).AnyAsync(cancellationToken)) return "Unknown payment terms.";
			if (request.LeadSourceId is short l && !await mRepository.GetQueryable<LeadSource>(x => x.Id == l).AnyAsync(cancellationToken)) return "Unknown lead source.";
			if (request.AccountManagerUserId is int m && !(await MemberIdsAsync(ownerCompanyId, cancellationToken)).Contains(m)) return "The account manager must be a member of your company.";
			if (request.Employees is < 0 || request.FleetVehicles is < 0) return "Counts cannot be negative.";
			if (request.AnnualRevenue is < 0) return "Annual revenue cannot be negative.";
			if (request.Locations != null)
			{
				if (request.Locations.Count > MaxLocations) return $"A company can have at most {MaxLocations} addresses.";
				if (request.Locations.Count(x => x.IsBilling) > 1) return "Only one address can be the billing address.";
				if (request.Locations.Any(x => !x.IsJobSite && !x.IsBilling)) return "An address must be a job site, the billing address, or both.";
			}
			return null;
		}

		private static void ApplyInfo(CompanyInfo info, SaveContactCompanyDto request, bool isOwn)
		{
			if (!isOwn) info.TypeId = request.TypeId ?? CompanyType.Customer;
			info.LegalName = Blank(request.LegalName);
			info.Industry = Blank(request.Industry);
			info.Website = Blank(request.Website);
			info.MainPhone = Blank(request.MainPhone);
			info.AccountManagerUserId = request.AccountManagerUserId;
			info.PaymentTermId = request.PaymentTermId;
			info.TaxId = Blank(request.TaxId);
			info.Employees = request.Employees;
			info.FleetVehicles = request.FleetVehicles;
			info.CustomerSince = request.CustomerSince;
			info.AnnualRevenue = request.AnnualRevenue;
			info.Territory = Blank(request.Territory);
			info.LeadSourceId = request.LeadSourceId;
		}

		/// <summary>Makes the stored addresses equal to the sent ones: matched by id they are updated, new ones are added, the rest are removed.</summary>
		private void ReplaceLocations(int companyId, List<CompanyLocation> existing, List<ContactLocationDto>? sent, DateTime now)
		{
			if (sent == null) return;
			var keep = new HashSet<int>();
			foreach (var s in sent)
			{
				var row = s.Id is int id ? existing.FirstOrDefault(e => e.Id == id) : null;
				if (row == null)
				{
					row = new CompanyLocation { CompanyId = companyId };
					mRepository.Create(row);
				}
				else keep.Add(row.Id);
				row.Name = Blank(s.Name);
				row.Street = Blank(s.Street);
				row.City = Blank(s.City);
				row.State = Blank(s.State);
				row.Zip = Blank(s.Zip);
				row.IsJobSite = s.IsJobSite;
				row.IsBilling = s.IsBilling;
				row.LatestUpdate = now;
			}
			foreach (var gone in existing.Where(e => !keep.Contains(e.Id))) mRepository.Delete(gone);
		}

		// ---- visibility ----

		private async Task<List<CompanyContact>> VisibleCompanyLinksAsync(int ownerCompanyId, int userId, CancellationToken cancellationToken) =>
			await mRepository.GetQueryable<CompanyContact>(c => c.OwnerCompanyId == ownerCompanyId && (c.OwnerUserId == null || c.OwnerUserId == userId)).AsNoTracking().ToListAsync(cancellationToken);

		private async Task<List<CompanyUserContact>> VisiblePersonLinksAsync(int ownerCompanyId, int userId, CancellationToken cancellationToken) =>
			await mRepository.GetQueryable<CompanyUserContact>(c => c.OwnerCompanyId == ownerCompanyId && (c.OwnerUserId == null || c.OwnerUserId == userId)).AsNoTracking().ToListAsync(cancellationToken);

		private async Task<CompanyContact?> FindVisibleCompanyLinkAsync(int ownerCompanyId, int userId, int companyId, CancellationToken cancellationToken, bool track = true)
		{
			var query = mRepository.GetQueryable<CompanyContact>(c => c.OwnerCompanyId == ownerCompanyId && c.ContactCompanyId == companyId && (c.OwnerUserId == null || c.OwnerUserId == userId));
			return await (track ? query : query.AsNoTracking()).SingleOrDefaultAsync(cancellationToken);
		}

		private async Task<CompanyUserContact?> FindVisiblePersonLinkAsync(int ownerCompanyId, int userId, int personId, CancellationToken cancellationToken, bool track = true)
		{
			var query = mRepository.GetQueryable<CompanyUserContact>(c => c.OwnerCompanyId == ownerCompanyId && c.ContactUserId == personId && (c.OwnerUserId == null || c.OwnerUserId == userId));
			return await (track ? query : query.AsNoTracking()).SingleOrDefaultAsync(cancellationToken);
		}

		/// <summary>The caller's own company, or one of the companies the caller can see as contacts.</summary>
		private async Task<bool> CanSeeCompanyAsync(int ownerCompanyId, int userId, int companyId, CancellationToken cancellationToken) =>
			companyId == ownerCompanyId || await FindVisibleCompanyLinkAsync(ownerCompanyId, userId, companyId, cancellationToken, track: false) != null;

		/// <summary>The users who are active members of the company (its colleagues).</summary>
		private async Task<HashSet<int>> MemberIdsAsync(int companyId, CancellationToken cancellationToken) =>
			(await mRepository.GetQueryable<User2Company>(m => m.CompanyId == companyId && (m.StatusId == User2CompanyStatus.Active || m.StatusId == User2CompanyStatus.WithOtherCompany))
				.AsNoTracking().Select(m => m.UserId).ToListAsync(cancellationToken)).ToHashSet();

		// ---- small helpers ----

		private async Task<Dictionary<int, (string Name, string? Title)>> PeopleNamesAsync(List<int> userIds, CancellationToken cancellationToken)
		{
			if (userIds.Count == 0) return new();
			var users = await mRepository.GetQueryable<User>(u => userIds.Contains(u.Id)).AsNoTracking().ToListAsync(cancellationToken);
			var infos = (await mRepository.GetQueryable<UserInfo>(i => userIds.Contains(i.UserId)).AsNoTracking().ToListAsync(cancellationToken)).ToDictionary(i => i.UserId);
			return users.ToDictionary(u => u.Id, u => (PersonName(u, infos.GetValueOrDefault(u.Id)), infos.GetValueOrDefault(u.Id)?.Title));
		}

		private async Task TryRemoveCompanyAsync(int companyId)
		{
			try
			{
				mRepository.DeleteAll<Company>(c => c.Id == companyId);
				await mRepository.SaveAsync();
			}
			catch
			{
				// The original failure is the one worth reporting.
			}
		}

		private async Task TryRemoveUserAsync(int userId)
		{
			try
			{
				mRepository.DeleteAll<User>(u => u.Id == userId);
				await mRepository.SaveAsync();
			}
			catch
			{
				// The original failure is the one worth reporting.
			}
		}

		private DateTime Now() => mTime.GetUtcNow().UtcDateTime;

		private static string AccountNumberOf(int companyId) => $"ORG-{companyId:D5}";

		private static string PersonName(User user, UserInfo? info)
		{
			var fromInfo = $"{info?.FirstName} {info?.LastName}".Trim();
			return fromInfo.Length > 0 ? fromInfo : Pick(user.DisplayName, user.FullName, user.UserName);
		}

		private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

		private static string Pick(params string?[] names) => names.FirstOrDefault(n => !string.IsNullOrWhiteSpace(n)) ?? "";

		private readonly IRepository mRepository;
		private readonly TimeProvider mTime;
	}
}
