namespace api_server.Services
{
	/// <summary>The one place that turns a user's FirstName and LastName into the full name shown in the UI and in emails.</summary>
	public static class UserNameHelper
	{
		/// <summary>"First Last", "First" when there is no last name, or "" when there is neither.</summary>
		public static string Compose(string? firstName, string? lastName) =>
			string.Join(' ', new[] { firstName?.Trim(), lastName?.Trim() }.Where(s => !string.IsNullOrEmpty(s)));

		/// <summary>Splits a typed full name at the first space ("Mary Ann Smith" gives "Mary" and "Ann Smith"); a single word is only a first name.</summary>
		public static (string FirstName, string? LastName) Split(string? fullName)
		{
			var s = (fullName ?? "").Trim();
			var i = s.IndexOf(' ');
			return i < 0 ? (s, null) : (s[..i], s[(i + 1)..].Trim() is { Length: > 0 } rest ? rest : null);
		}
	}
}
