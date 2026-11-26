using api_server.Medias;
using db_model.Missions;

namespace api_server.Missions
{
	public class MissionMediaDto
	{
		public MissionMediaDto() { }

		public MissionMediaDto(MissionUploadedMedia model)
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
