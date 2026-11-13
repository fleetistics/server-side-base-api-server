using api_server.Auth.Dto;
using db_model.Auth;
using exs.Database.Commons.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace api_server.Auth
{
	public interface ISecuritySettingsService
	{
		/// <summary>
		/// The company's own security settings, or the system-wide ones (CompanyId null) when the company has none.
		/// With no row at all, the defaults of <see cref="SecuritySettings"/> are returned.
		/// </summary>
		Task<SecuritySettingsDto> GetSecuritySettingsAsync(int? companyId, CancellationToken cancellationToken);
	}

	public sealed class SecuritySettingsService : ISecuritySettingsService
	{
		public SecuritySettingsService(IRepository repository)
		{
			mRepository = repository;
		}

		public async Task<SecuritySettingsDto> GetSecuritySettingsAsync(int? companyId, CancellationToken cancellationToken)
		{
			var entries = await mRepository.GetQueryable<SettingsEntry>(e => e.CompanyId == null || e.CompanyId == companyId)
				.AsNoTracking()
				.ToListAsync(cancellationToken);

			var entry = entries.FirstOrDefault(e => e.CompanyId != null && e.CompanyId == companyId)
				?? entries.FirstOrDefault(e => e.CompanyId == null);

			if (entry == null) return new SecuritySettingsDto(null, new SecuritySettings());

			return new SecuritySettingsDto(entry.CompanyId, SecuritySettings.FromJson(entry.Value));
		}

		private readonly IRepository mRepository;
	}
}
