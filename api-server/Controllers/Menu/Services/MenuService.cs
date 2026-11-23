using api_server.Controllers.Menu.Dto;
using api_server.Controllers.Roles.Services;
using db_model.AppStructure;
using db_model.Companies;
using exs.Database.Commons.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace api_server.Controllers.Menu.Services
{
	public interface IMenuService
	{
		/// <summary>
		/// The menu the user sees in the company they are working in: what their role there may view, as a tree. Empty when the
		/// user has no current company, no role in it, or the role may view nothing.
		/// </summary>
		Task<IReadOnlyList<MenuItemDto>> GetMyMenuAsync(int userId, int? companyId, CancellationToken cancellationToken);
	}

	public sealed class MenuService : IMenuService
	{
		/// <summary>The "Main" row of application_menu (loaded by main-database/custom-sql/insert-menu-items.sql).</summary>
		public const int MainMenuId = 1;

		public MenuService(IRepository repository)
		{
			mRepository = repository;
		}

		public async Task<IReadOnlyList<MenuItemDto>> GetMyMenuAsync(int userId, int? companyId, CancellationToken cancellationToken)
		{
			if (companyId is not int company) return [];

			// The role is read here, not taken from the token, so a changed role applies without a new login.
			var roleId = await mRepository.GetQueryable<User2Company>(m => m.CompanyId == company && m.UserId == userId)
				.AsNoTracking()
				.Select(m => (int?)m.PermissionRoleId)
				.FirstOrDefaultAsync(cancellationToken);
			if (roleId is not int role) return [];

			// The company's own rows for the role when it has any (its own role, or its override of a system role), otherwise the defaults.
			var rows = await mRepository.GetQueryable<Role2MenuItem>(g => g.PermissionRoleId == role && (g.CompanyId == null || g.CompanyId == company))
				.AsNoTracking()
				.ToListAsync(cancellationToken);
			var grants = RoleMenuPermissionRules.Effective(rows, company).Where(g => g.CanView == true).ToList();
			if (grants.Count == 0) return [];

			var items = await mRepository.GetQueryable<ApplicationMenuItem>(i => i.MenuId == MainMenuId)
				.AsNoTracking()
				.ToListAsync(cancellationToken);

			return MenuTreeBuilder.Build(items, grants);
		}

		private readonly IRepository mRepository;
	}
}
