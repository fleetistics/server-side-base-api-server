using api_server.Companies;
using api_server.Users;

namespace api_server.Controllers.Companies.Dto
{
    public class CompanyUserManagementContextDto
    {
        public long LatestUpdate { get; set; }
        public List<UserDto>? Users { get; set; }
        public List<PermissionRoleDto>? PermissionRoles { get; set; }
        public List<UserGroupDto>? UserGroups { get; set; }

        public List<UserJoinRequestDto>? UserJoinRequests { get; set; }
        public List<UserJoinInvitationDto>? UserJoinInvitations { get; set; }

        public List<User2GroupDto>? User2Groups { get; set; }
        public List<User2CompanyDto>? User2Companies { get; set; }
        public List<LatestUserActivityDto>? LatestUserActivities { get; set; }
    }
}
