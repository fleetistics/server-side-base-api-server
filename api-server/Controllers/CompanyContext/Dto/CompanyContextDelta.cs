using api_server.BusinessLogic.Dto;
using api_server.Companies;
using api_server.Controllers.MapData.Dto;
using api_server.Controllers.Messages;

using api_server.Fleet;
using api_server.Medias;
using api_server.Missions;
using api_server.Teams;
using api_server.Users;

namespace api_server.CompanyContext
{
    public class CompanyContextDelta
    {
        public long LastUpdate { get; set; }
 
        //public bool? DoReloadAll { get; set; }
        public MissionDto? Mission { get; set; }
        public MissionDetailsDto? MissionDetails { get; set; }
        public List<UploadedMediaDto>? MissionUploadedMedias { get; set; }
        public List<int>? RemoveMissionMediaIds { get; set; }

        public List<User2CompanyDto>? Members { get; set; }
        public List<int>? RemoveMemberUserIds { get; set; }

        public MissionPrimaryTargetUserDto? MissionPrimaryTargetUser { get; set; }
        public bool? DoRemoveMissionPrimaryTargetUser { get; set; }


        public List<UserDto>? Users { get; set; }
        public List<int>? RemoveUserIds { get; set; }

        public List<UserLocationPrivacyDto>? UserLocationPrivacies { get; set; }
        public List<int>? RemoveUserLocationPrivacyIds { get; set; }

        public List<GpsDeviceDto>? GpsDevices { get; set; }
        public List<int>? RemoveGpsDeviceIds { get; set; }

        public List<ActiveUserEmergencyAlertDto>? UserEmergencyAlerts { get; set; }
        public List<int>? RemoveUserEmergencyAlertIds { get; set; }

        public List<UserMessageDto>? UserMessages { get; set; }
        public List<int>? RemoveUserMessageIds { get; set; }


        public List<GpsDeviceMapStateDto>? GpsDeviceMapStates { get; set; }
        public List<int>? RemoveGpsDeviceMapStateIds { get; set; }

    }
}
