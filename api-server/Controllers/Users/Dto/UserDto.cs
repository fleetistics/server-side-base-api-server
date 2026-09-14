using api_server.Controllers.Media.Dto;
using db_model.UserManagement;

namespace api_server.Controllers.Users.Dto
{
	public class UserDto 
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
                AvatarImage = new UploadedMediaDto(user.AvatarImage);
			}
			if (user.GovIDImage != null)
			{
                GovIDImage = new UploadedMediaDto(user.GovIDImage);
			}
		}

		public int Id { get; set; }
		public string DisplayName { get; set; } = default!;
		public string UserName { get; set; } = default!;
		//public short StatusId { get; set; }
		public string? FullName { get; set; } = default!;
		public string? Phone { get; set; }
		public string? Email { get; set; }
        public UploadedMediaDto? AvatarImage { get; set; }
        public UploadedMediaDto? GovIDImage { get; set; }
    }
}
