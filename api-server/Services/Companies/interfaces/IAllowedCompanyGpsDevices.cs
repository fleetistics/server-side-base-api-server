namespace api_server.Services.Companies
{
    public interface IAllowedCompanyGpsDevices
    {
        Task<List<int>> GetAllowedCompanyGpsDevicesAsync( int companyId, int userId);
    }
}
