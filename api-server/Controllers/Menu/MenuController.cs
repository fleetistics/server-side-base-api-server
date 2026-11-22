using api_server.Controllers.Base;
using api_server.Controllers.Menu.Dto;
using api_server.Controllers.Menu.Services;
using Microsoft.AspNetCore.Mvc;

namespace api_server.Controllers.Menu
{
	/// <summary>The portal's menu, as the signed-in user's role may see it. Read-only.</summary>
	public class MenuController : AuthAPIController
	{
		public MenuController(IMenuService menu)
		{
			mMenu = menu;
		}

		/// <summary>
		/// The tree of menu items the caller's role may view in their current company (CompanyId claim), in display order.
		/// An empty list, not an error, when there is no current company or the role may view nothing.
		/// </summary>
		[HttpGet("/api/menu")]
		[ProducesResponseType(typeof(IReadOnlyList<MenuItemDto>), StatusCodes.Status200OK)]
		public async Task<IActionResult> GetMyMenu(CancellationToken cancellationToken) =>
			Ok(await mMenu.GetMyMenuAsync(UserId, CompanyId, cancellationToken));

		private readonly IMenuService mMenu;
	}
}
