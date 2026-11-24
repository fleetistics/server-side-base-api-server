using db_model.Companies;

namespace api_server.Companies
{
	public class UserJoinInvitationDto
	{
		public UserJoinInvitationDto() { }

		public UserJoinInvitationDto(UserJoinInvitation model)
		{
			Id = model.Id;
			CompanyId = model.CompanyId;
			CreatedByUserId = model.CreatedByUserId;
			// The context list does not carry codes; the Invites & Join tab gets the company code from its own endpoint.
			JoinCode = null;
			JoinQrCodeImageName = model.JoinQrCodeImageName;
			PermissionRoleId = model.PermissionRoleId;
			GroupId = model.GroupId;
			TypeId = model.TypeId;
			IsAutoAppRegistrationAllowed = model.IsAutoAppRegistrationAllowed;
			DoesNotRequireAdminApproval = model.DoesNotRequireAdminApproval;
			UserId = model.UserId;
			Email = model.Email;
			Name = model.Name;
			Message = model.Message;
			StatusId = model.StatusId;
			CreationDate = model.CreationDate;
			ExpirationDate = model.ExpirationDate;
		}

		public int Id { get; set; }
		public int CompanyId { get; set; }
		public int CreatedByUserId { get; set; }
		public string? JoinCode { get; set; }
		public string JoinQrCodeImageName { get; set; } = default!;

		public int? PermissionRoleId { get; set; }
		public int? GroupId { get; set; }

		public short TypeId { get; set; }
		public bool? IsAutoAppRegistrationAllowed { get; set; }
		public bool? DoesNotRequireAdminApproval { get; set; }

		public int? UserId { get; set; }
		public string? Email { get; set; }
		public string? Name { get; set; }
		public string? Message { get; set; }

		public short StatusId { get; set; }
		public DateTime CreationDate { get; set; }
		public DateTime ExpirationDate { get; set; }
	}
}
