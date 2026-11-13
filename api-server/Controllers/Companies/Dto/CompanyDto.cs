using db_model.Companies;

namespace api_server.Companies
{
	public class CompanyDto
	{
		public CompanyDto() { }

		public CompanyDto(Company model)
		{
			Id = model.Id;
			Name = model.Name;
			StatusId = model.StatusId;
		}

		public int Id { get; set; }
		public string Name { get; set; } = default!;
		public short StatusId { get; set; }
	}
}
