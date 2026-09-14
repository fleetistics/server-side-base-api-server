using db_model.Team;

namespace api_server.Controllers.Teams.Dto
{
	public class TeamMediaDto
	{
		public TeamMediaDto() { }

		public TeamMediaDto(TeamUploadedMedia model)
		{
			UploadedMediaId = model.UploadedMediaId;
			GroupKey = model.GroupKey;

		}

		public int UploadedMediaId { get; set; }
		public short? GroupKey { get; set; }

	}
}
