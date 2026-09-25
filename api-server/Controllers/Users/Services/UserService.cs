using api_server.Controllers.Media;
using api_server.Controllers.Media.Dto;
using api_server.Controllers.Users.Dto;
using db_model.UserManagement;
using exs.Database.Commons.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace api_server.Controllers.Users.Services
{
	public interface IUserService
	{
		/// <summary>Null when the user does not exist.</summary>
		Task<UserDto?> GetEditUserAsync(int userId, CancellationToken cancellationToken);

		//Task<IReadOnlyList<UserDto>> GetActiveUsersAsync(CancellationToken cancellationToken);

		/// <summary>Null when the user does not exist.</summary>
		//Task<UserDto?> UpdateUserAsync(int userId, UserDto data, CancellationToken cancellationToken);

		/// <summary>Applies only the fields present in <paramref name="patch"/>. Returns false when the user does not exist.</summary>
		Task<bool> PatchUserAsync(int userId, UserPatchDto patch, CancellationToken cancellationToken);

		//Task<UserDto> CreateUserAsync(UserDto data, CancellationToken cancellationToken);
	}

	/// <summary>
	/// User CRUD behind an interface: queries, field mapping and DTO assembly live
	/// here; UserController only translates HTTP. Avatar URLs are made absolute by
	/// the shared MediaUrlResolver so every endpoint returns the same URL shape.
	/// </summary>
	public sealed class UserService : IUserService
	{
		private readonly IRepository mRepository;
		private readonly MediaUrlResolver mMediaUrls;
        private readonly IMediaInboundProcessor mMediaInboundProcessor;

        public UserService(IRepository repository, MediaUrlResolver mediaUrls, IMediaInboundProcessor mediaInboundProcessor)
		{
			mRepository = repository;
			mMediaUrls = mediaUrls;
			mMediaInboundProcessor = mediaInboundProcessor;
		}

		public async Task<UserDto?> GetEditUserAsync(int userId, CancellationToken cancellationToken)
		{
			var user = await mRepository.GetQueryable<User>(u => u.Id == userId)
				.Include(u => u.AvatarImage)
				.Include(u => u.GovIDImage)
				.AsNoTracking()
				.FirstOrDefaultAsync(cancellationToken);
			if (user == null) return null;
			else
			{
                var dto = new UserDto(user);
                //mMediaUrls.ApplyAbsoluteUrls(dto.Medias);
                mMediaUrls.ApplyAbsoluteUrl(dto.AvatarImage);
                mMediaUrls.ApplyAbsoluteUrl(dto.GovIDImage);
				return dto;
            }
		}

		//public async Task<IReadOnlyList<UserDto>> GetActiveUsersAsync(CancellationToken cancellationToken)
		//{
		//	var users = await mRepository.GetQueryable<User>(u => u.StatusId == UserStatus.Active)
		//		.Include(u => u.AvatarImage)
		//		.AsNoTracking()
		//		.ToListAsync(cancellationToken);
		//	return users.Select(ToDto).ToList();
		//}

		//public async Task<UserDto?> UpdateUserAsync(int userId, UserDto data, CancellationToken cancellationToken)
		//{
		//	var user = await mRepository.GetQueryable<User>(u => u.Id == userId)
		//		.Include(u => u.AvatarImage)
		//		.FirstOrDefaultAsync(cancellationToken);
		//	if (user == null)
		//	{
		//		return null;
		//	}

		//	user.DisplayName = data.DisplayName;
		//	user.FullName = data.FullName;
		//	user.Phone = data.Phone;
		//	user.Email = data.Email;
		//	user.AvatarImageId = data.AvatarImageId;

		//	await mRepository.SaveAsync(cancellationToken);
		//	return ToDto(user);
		//}

		public async Task<bool> PatchUserAsync(int userId, UserPatchDto patch, CancellationToken cancellationToken)
		{
			var user = await mRepository.GetQueryable<User>(u => u.Id == userId).FirstOrDefaultAsync(cancellationToken);
			if (user == null)
			{
                return false;
            }

            // Only the user's own current avatar may be removed by this call — RemoveMediaIds
            // is client-supplied, so anything else in it is silently ignored by ProcessAsync
            // rather than deleted.
            var allowedRemoveIds = new List<int>();
            if (patch.RemoveMediaIds is { Count: > 0 })
			{
				
				if (user.AvatarImageId.HasValue) allowedRemoveIds.Add(user.AvatarImageId.Value);
				if (user.GovIDImageId.HasValue) allowedRemoveIds.Add(user.GovIDImageId.Value);
			}
            var newMedias = await mMediaInboundProcessor.ProcessAsync(patch, allowedRemoveIds, cancellationToken);
			if (patch.RemoveMediaIds is { Count: > 0 } && allowedRemoveIds.Count > 0) 
			{
                if (user.AvatarImageId.HasValue && patch.RemoveMediaIds.Contains(user.AvatarImageId.Value))
                {
                    user.AvatarImageId = null;
                }
				if (user.GovIDImageId.HasValue && patch.RemoveMediaIds.Contains(user.GovIDImageId.Value))
                {
                    user.GovIDImageId = null;
                }
            }
            if (patch.InsertMedias != null && newMedias.Count > 0)
            {
				var newMediaIdx = patch.InsertMedias.FindIndex(m => m.GroupKey == (short)EditUserGroupKey.Avatar);
				if (newMediaIdx >= 0) user.AvatarImageId = newMedias[newMediaIdx].Id;
				else
				{
                    newMediaIdx = patch.InsertMedias.FindIndex(m => m.GroupKey == null);
                    if (newMediaIdx >= 0) user.AvatarImageId = newMedias[newMediaIdx].Id;
                }
                newMediaIdx = patch.InsertMedias.FindIndex(m => m.GroupKey == (short)EditUserGroupKey.GovID);
                if (newMediaIdx >= 0) user.GovIDImageId = newMedias[newMediaIdx].Id;
            }

            // Unset fields are left untouched — that's the entire point of Optional<T>.
            // DisplayName can never end up null here: UserPatchDto's validation already
            // rejects an explicitly-empty DisplayName, and omission simply skips the line.
            if (patch.DisplayName.IsSet) user.DisplayName = patch.DisplayName.Value!;
			if (patch.FullName.IsSet) user.FullName = patch.FullName.Value!;
			if (patch.Phone.IsSet) user.Phone = patch.Phone.Value;
			if (patch.Email.IsSet) user.Email = patch.Email.Value;

			await mRepository.SaveAsync(cancellationToken);
			return true;
		}

		//public async Task<UserDto> CreateUserAsync(UserDto data, CancellationToken cancellationToken)
		//{
		//	var user = new User
		//	{
		//		DisplayName = data.DisplayName,
		//		UserName = data.UserName,
		//		// TODO: replace together with AuthService.VerifyPassword when hashing lands.
		//		Password = "some password",
		//		FullName = data.FullName,
		//		Phone = data.Phone,
		//		Email = data.Email,
		//		StatusId = UserStatus.Active,
		//		AvatarImageId = data.AvatarImageId,
		//	};
		//	mRepository.Create(user);
		//	await mRepository.SaveAsync(cancellationToken);

		//	// Reload with the avatar included so the response DTO is complete.
		//	var created = await mRepository.GetQueryable<User>(u => u.Id == user.Id)
		//		.Include(u => u.AvatarImage)
		//		.AsNoTracking()
		//		.FirstAsync(cancellationToken);
		//	return ToDto(created);
		//}
	}
}
