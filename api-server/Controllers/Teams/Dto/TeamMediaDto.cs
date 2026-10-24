using api_server.Medias;
using db_model.Team;
using db_model.UserManagement;

namespace api_server.Teams
{
	public class TeamMediaDto
	{
		public TeamMediaDto() { }

		public TeamMediaDto(TeamUploadedMedia model)
		{
			UploadedMediaId = model.UploadedMediaId;
			GroupKey = model.GroupKey;
            Media = model.Media != null ? new UploadedMediaDto(model.Media) : null;

        }

		public int UploadedMediaId { get; set; }
		public short? GroupKey { get; set; }

		public UploadedMediaDto? Media { get; set; } = default!;

    }
}
