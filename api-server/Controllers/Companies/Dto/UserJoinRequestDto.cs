using db_model.Companies;

namespace api_server.Companies
{
	public class UserJoinRequestDto
	{
		public UserJoinRequestDto() { }

		public UserJoinRequestDto(UserJoinRequest model)
		{
			Id = model.Id;
			CompanyId = model.CompanyId;
			UserJoinInvitationId = model.UserJoinInvitationId;
			UserId = model.UserId;
			StatusId = model.StatusId;
			CreationDate = model.CreationDate;
			HandledByCompanyUserId = model.HandledByCompanyUserId;
		}

		public int Id { get; set; }
		public int CompanyId { get; set; }
		public int UserJoinInvitationId { get; set; }
		public int UserId { get; set; }
		public short StatusId { get; set; }
		public DateTime CreationDate { get; set; }
		public int HandledByCompanyUserId { get; set; }
	}
}
