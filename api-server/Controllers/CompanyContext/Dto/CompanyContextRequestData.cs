using Microsoft.AspNetCore.Mvc;

namespace api_server.Controllers.CompanyContext.Dto
{
    public class CompanyContextRequestData
    {
         public long LatestUpdateDate { get; set; }
         public List<int>? ToCheckUpdatesUserIds { get; set; }
         public List<int>? RequestedUserIds { get; set; }
    }
}
