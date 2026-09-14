using db_model.Team;

namespace api_server.Controllers.Teams.Dto
{
	public class TeamHeaderDto
	{
		public TeamHeaderDto() { }

		public TeamHeaderDto(Team model)
		{
			Id = model.Id;
			CreatorUserId = model.CreatorUserId;
			TeamName = model.TeamName;
			StatusId = model.StatusId;
			CreatedDate = model.CreatedDate;
			ClosedDate = model.ClosedDate;
			JoinKey = model.JoinKey;
			JoinQrCode = model.JoinQrCode;
		}

		public int? Id { get; set; }
		public int CreatorUserId { get; set; }
		public string TeamName { get; set; } = default!;
		public short? StatusId { get; set; }
		public DateTime CreatedDate { get; set; }
		public DateTime? ClosedDate { get; set; }
		public string? JoinKey { get; set; }
		public string? JoinQrCode { get; set; }
	}
}
