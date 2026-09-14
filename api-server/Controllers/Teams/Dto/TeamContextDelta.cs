using api_server.BusinessLogic.Dto;
using api_server.Controllers.MapData.Dto;
using api_server.Controllers.Media.Dto;
using api_server.Controllers.Users.Dto;

namespace api_server.Controllers.Teams.Dto
{
    public class TeamContextDelta
    {
        public DateTime LastUpdate { get; set; }
        public TeamHeaderDto? TeamHeader { get; set; }
        public TeamDetailsDto? TeamDetails { get; set; }
        public List<UploadedMediaDto>? TeamUploadedMedias { get; set; }

        public List<TeamMemberDto>? Members { get; set; }
        
        public List<UserDto>? Users { get; set; }
        public List<MobileGpsDeviceDto>? MobileGpsDevices { get; set; }

        public List<MobileGpsDeviceMapStateDto>? GpsDeviceMapStates { get; set; }
        public List<ActiveUserEmergencyAlertDto>? UserAlerts { get; set; }
        
        
        public List<int>? RemoveUserIds { get; set; }
        public List<int>? RemoveUserAlertIds { get; set; }
        public List<int>? RemoveTeamMediaIds { get; set; }
    }
}
