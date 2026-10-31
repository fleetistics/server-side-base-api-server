

namespace api_server.Services.Companies
{
    public class AllowedCompanyGpsDevicesProvider : IAllowedCompanyGpsDevices
    {
        public Task<List<int>> GetAllowedCompanyGpsDevicesAsync(int companyId, int userId)
        {
            throw new NotImplementedException();
        }
    }
}
