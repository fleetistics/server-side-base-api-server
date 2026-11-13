namespace db_model.Auth
{
	/// <summary>
	/// The security settings (<see cref="SecuritySettings"/>) stored as a JSON document in <see cref="Value"/>.
	/// <see cref="CompanyId"/> null is the system-wide default; a row with a CompanyId overrides it for that company.
	/// There is at most one row per company and exactly one system row.
	/// </summary>
	public class SettingsEntry
	{
		public int Id { get; set; }
		public int? CompanyId { get; set; }

		/// <summary>Serialized <see cref="SecuritySettings"/>; jsonb column. Use <see cref="SecuritySettings.ToJson"/> / <see cref="SecuritySettings.FromJson"/>.</summary>
		public string Value { get; set; } = "{}";

		public DateTime LatestUpdate { get; set; }
	}
}
