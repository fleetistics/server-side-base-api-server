using api_server.Controllers.Media.Dto;
using db_model.UserManagement;

namespace api_server.Controllers.Users.Dto
{
	public class UserDto : MediaAttachableDto
	{
		public UserDto() { }

		public UserDto(User user)
		{
			Id = user.Id;
			DisplayName = user.DisplayName;
			UserName = user.UserName;
			//StatusId = user.StatusId;
			FullName = user.FullName;
			Phone = user.Phone;
			Email = user.Email;
			if (user.AvatarImage != null)
			{
				Medias = new List<UploadedMediaDto> { new UploadedMediaDto(user.AvatarImage) };
			}
		}

		public int Id { get; set; }
		public string DisplayName { get; set; } = default!;
		public string UserName { get; set; } = default!;
		//public short StatusId { get; set; }
		public string? FullName { get; set; } = default!;
		public string? Phone { get; set; }
		public string? Email { get; set; }
		
	}
}
