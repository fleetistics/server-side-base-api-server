using api_server.Controllers.Base;
using api_server.Controllers.Users.Dto;
using api_server.Controllers.Users.Services;
using api_server.Idempotency;
using Microsoft.AspNetCore.Mvc;

namespace api_server.Controllers.Users
{
	// Thin HTTP surface: queries, mapping and rules live in IUserService (UserService).
	public class UserController : AuthAPIController
	{
		public UserController(IUserService users)
		{
			mUsers = users;
		}

		//[HttpGet("/api/users/{userId}")]
		//[ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
		//[ProducesResponseType(StatusCodes.Status404NotFound)]
		//public async Task<IActionResult> GetUser([FromRoute] int userId, CancellationToken cancellationToken)
		//{
		//	var user = await mUsers.GetUserAsync(userId, cancellationToken);
		//	if (user == null)
		//	{
		//		return NotFound(new { message = "User not found." });
		//	}
		//	return Ok(user);
		//}

		[HttpGet("/api/users/me")]
		[ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
		[ProducesResponseType(StatusCodes.Status404NotFound)]
		public async Task<IActionResult> GetMyUser(CancellationToken cancellationToken)
		{
			var user = await mUsers.GetEditUserAsync(UserId, cancellationToken);
			if (user == null)
			{
				return NotFound(new { message = "User not found." });
			}
			return Ok(user);
		}

		//[HttpGet("/api/users")]
		//[ProducesResponseType(typeof(IEnumerable<UserDto>), StatusCodes.Status200OK)]
		//public async Task<IActionResult> GetUsers(CancellationToken cancellationToken)
		//{
		//	return Ok(await mUsers.GetActiveUsersAsync(cancellationToken));
		//}

		// Full-replace update is disabled: PatchUser is the only supported way to modify a user.
		[HttpPut("/api/users/{userId}")]
		[ProducesResponseType(StatusCodes.Status405MethodNotAllowed)]
		public IActionResult UpdateUser()
		{
			return StatusCode(StatusCodes.Status405MethodNotAllowed, new { message = "Updating a user via PUT is not supported; use PATCH instead." });
		}

		[HttpPatch("/api/users/{userId}")]
		[ProducesResponseType(StatusCodes.Status200OK)]
		[ProducesResponseType(StatusCodes.Status404NotFound)]
		public async Task<IActionResult> PatchUser([FromRoute] int userId, [FromBody] UserPatchDto patch, CancellationToken cancellationToken)
		{
			var found = await mUsers.PatchUserAsync(userId, patch, cancellationToken);
			if (!found)
			{
				return NotFound(new { message = "User not found." });
			}
			return Ok();
		}

		[Idempotent]
		[HttpPost("/api/users")]
        [ProducesResponseType(StatusCodes.Status405MethodNotAllowed)]
        public IActionResult CreateUser()
        {
            return StatusCode(StatusCodes.Status405MethodNotAllowed, new { message = "Creating a user via POST is not supported; use Register instead." });
        }

		private readonly IUserService mUsers;
	}
}
