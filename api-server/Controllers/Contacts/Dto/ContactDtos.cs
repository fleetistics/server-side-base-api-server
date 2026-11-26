using System.ComponentModel.DataAnnotations;

namespace api_server.Controllers.Contacts.Dto
{
	public class LookupItemDto
	{
		public short Id { get; set; }
		public string Name { get; set; } = default!;
	}

	public class UserLookupDto
	{
		public int Id { get; set; }
		public string Name { get; set; } = default!;
	}

	/// <summary>The lists the contact forms pick from.</summary>
	public class ContactLookupsDto
	{
		/// <summary>1 Customer, 2 Vendor, 3 Partner, 4 Affiliate (5 Internal is the caller's own company and cannot be chosen).</summary>
		public List<LookupItemDto> CompanyTypes { get; set; } = new();
		/// <summary>1 Contact, 2 Vendor, 3 Partner.</summary>
		public List<LookupItemDto> ContactTypes { get; set; } = new();
		public List<LookupItemDto> LeadSources { get; set; } = new();
		public List<LookupItemDto> PaymentTerms { get; set; } = new();
		/// <summary>The statuses a contact company can have: 1 Active, 13 Inactive.</summary>
		public List<LookupItemDto> CompanyStatuses { get; set; } = new();
		/// <summary>The people who can be a company's account manager: the members of the caller's company.</summary>
		public List<UserLookupDto> AccountManagers { get; set; } = new();
	}

	/// <summary>One address of a company: a job site, the billing address, or one place that is both.</summary>
	public class ContactLocationDto
	{
		/// <summary>Null for a new one.</summary>
		public int? Id { get; set; }
		[MaxLength(200)] public string? Name { get; set; }
		[MaxLength(300)] public string? Street { get; set; }
		[MaxLength(120)] public string? City { get; set; }
		[MaxLength(60)] public string? State { get; set; }
		[MaxLength(20)] public string? Zip { get; set; }
		public bool IsJobSite { get; set; } = true;
		public bool IsBilling { get; set; }
	}

	/// <summary>One line of the companies list.</summary>
	public class ContactCompanyRowDto
	{
		public int Id { get; set; }
		public string Name { get; set; } = default!;
		/// <summary>A CompanyType id; 5 (Internal) for the caller's own company.</summary>
		public short TypeId { get; set; }
		public string? Industry { get; set; }
		public string? AccountNumber { get; set; }
		/// <summary>A CompanyStatus id: 1 Active, 13 Inactive.</summary>
		public short StatusId { get; set; }
		/// <summary>City and state of the first job site.</summary>
		public string? City { get; set; }
		public string? State { get; set; }
		public int? FleetVehicles { get; set; }
		public decimal? AnnualRevenue { get; set; }
		public int? PrimaryContactUserId { get; set; }
		public string? PrimaryContactName { get; set; }
		public string? PrimaryContactTitle { get; set; }
		public bool IsOwnCompany { get; set; }
		/// <summary>Visible to its owner user only.</summary>
		public bool IsPrivate { get; set; }
	}

	/// <summary>A company with its CRM facts and addresses.</summary>
	public class ContactCompanyDto
	{
		public int Id { get; set; }
		public string Name { get; set; } = default!;
		public short StatusId { get; set; }
		public short TypeId { get; set; }
		public bool IsOwnCompany { get; set; }
		public bool IsPrivate { get; set; }

		public string? LegalName { get; set; }
		public string? Industry { get; set; }
		public string? Website { get; set; }
		public string? MainPhone { get; set; }
		public string? AccountNumber { get; set; }
		public int? AccountManagerUserId { get; set; }
		public string? AccountManagerName { get; set; }
		public short? PaymentTermId { get; set; }
		public string? TaxId { get; set; }
		public int? Employees { get; set; }
		public int? FleetVehicles { get; set; }
		/// <summary>yyyy-MM-dd.</summary>
		public DateOnly? CustomerSince { get; set; }
		public decimal? AnnualRevenue { get; set; }
		public string? Territory { get; set; }
		public short? LeadSourceId { get; set; }
		public int? PrimaryContactUserId { get; set; }

		/// <summary>How many contact people the caller can see in this company.</summary>
		public int ContactCount { get; set; }
		public List<ContactLocationDto> Locations { get; set; } = new();
	}

	/// <summary>Creates a company contact, or replaces the fields of one. A field left out on update is cleared, so send the whole form.</summary>
	public class SaveContactCompanyDto
	{
		[Required, MaxLength(200)] public string Name { get; set; } = default!;
		/// <summary>1 Customer, 2 Vendor, 3 Partner, 4 Affiliate. Ignored for the caller's own company.</summary>
		public short? TypeId { get; set; }
		/// <summary>1 Active, 13 Inactive. Ignored for the caller's own company and left as it is when not sent.</summary>
		public short? StatusId { get; set; }

		[MaxLength(200)] public string? LegalName { get; set; }
		[MaxLength(200)] public string? Industry { get; set; }
		[MaxLength(300)] public string? Website { get; set; }
		[MaxLength(60)] public string? MainPhone { get; set; }
		public int? AccountManagerUserId { get; set; }
		public short? PaymentTermId { get; set; }
		[MaxLength(60)] public string? TaxId { get; set; }
		public int? Employees { get; set; }
		public int? FleetVehicles { get; set; }
		public DateOnly? CustomerSince { get; set; }
		public decimal? AnnualRevenue { get; set; }
		[MaxLength(200)] public string? Territory { get; set; }
		public short? LeadSourceId { get; set; }

		/// <summary>Replaces the company's addresses when sent (an empty list removes them all); left as they are when null.</summary>
		public List<ContactLocationDto>? Locations { get; set; }

		/// <summary>On create: true makes the company visible to the caller only. On update: false shares a private one (only its owner user can).</summary>
		public bool? IsPrivate { get; set; }

		/// <summary>Create only: a first person to add as the company's primary contact.</summary>
		public SaveContactPersonDto? PrimaryContact { get; set; }
	}

	/// <summary>One line of the contacts list: a colleague (member of the caller's company) or a contact person.</summary>
	public class ContactPersonRowDto
	{
		public int Id { get; set; }
		public string Name { get; set; } = default!;
		/// <summary>True for a member of the caller's own company (managed in User Management, not edited here).</summary>
		public bool IsEmployee { get; set; }
		/// <summary>A ContactType id: 1 Contact, 2 Vendor, 3 Partner. Null for an employee.</summary>
		public short? ContactTypeId { get; set; }
		public int? CompanyId { get; set; }
		public string? CompanyName { get; set; }
		public string? Title { get; set; }
		public string? Email { get; set; }
		public string? Phone { get; set; }
		public bool IsPrimary { get; set; }
		public bool IsActive { get; set; }
		public bool IsArchived { get; set; }
		public bool IsPrivate { get; set; }
	}

	public class ContactPersonDto
	{
		public int Id { get; set; }
		public string Name { get; set; } = default!;
		public bool IsEmployee { get; set; }
		public string? FirstName { get; set; }
		public string? LastName { get; set; }
		public string? Title { get; set; }
		public string? Email { get; set; }
		/// <summary>The direct phone.</summary>
		public string? Phone { get; set; }
		public string? Mobile { get; set; }
		public short? ContactTypeId { get; set; }
		public short? LeadSourceId { get; set; }
		public bool IsActive { get; set; }
		public bool IsArchived { get; set; }
		public bool IsPrivate { get; set; }
		public bool IsPrimary { get; set; }
		public int? CompanyId { get; set; }
		public string? CompanyName { get; set; }
	}

	/// <summary>Creates a contact person, or replaces the fields of one (send the whole form).</summary>
	public class SaveContactPersonDto
	{
		[Required, MaxLength(100)] public string FirstName { get; set; } = default!;
		[MaxLength(100)] public string? LastName { get; set; }
		[MaxLength(200)] public string? Title { get; set; }
		[MaxLength(320)] public string? Email { get; set; }
		[MaxLength(60)] public string? Phone { get; set; }
		[MaxLength(60)] public string? Mobile { get; set; }
		public short? ContactTypeId { get; set; }
		public short? LeadSourceId { get; set; }
		public bool IsActive { get; set; } = true;

		/// <summary>The company the person works for: the caller's own company or one of their company contacts. Required, except inside <see cref="SaveContactCompanyDto.PrimaryContact"/>.</summary>
		public int? CompanyId { get; set; }

		/// <summary>On create: true makes the person visible to the caller only. On update: false shares a private one (only its owner user can).</summary>
		public bool? IsPrivate { get; set; }

		/// <summary>True makes the person the primary contact of their company; false removes that if they were; left out keeps it as it is.</summary>
		public bool? IsPrimary { get; set; }

		/// <summary>Create only: makes this company first (as for a new company contact) and adds the person to it; <see cref="CompanyId"/> is then ignored.</summary>
		public SaveContactCompanyDto? NewCompany { get; set; }
	}
}
