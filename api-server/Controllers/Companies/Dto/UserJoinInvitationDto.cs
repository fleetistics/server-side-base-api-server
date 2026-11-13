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
			JoinCode = model.JoinCode;
			JoinQrCodeImageName = model.JoinQrCodeImageName;
			PermissionRoleId = model.PermissionRoleId;
			GroupId = model.GroupId;
			IsPersonal = model.IsPersonal;
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
		public string JoinCode { get; set; } = default!;
		public string JoinQrCodeImageName { get; set; } = default!;

		public int? PermissionRoleId { get; set; }
		public int? GroupId { get; set; }

		public bool? IsPersonal { get; set; }
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
