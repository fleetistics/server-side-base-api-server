using db_model.Team;

namespace api_server.Controllers.Teams.Dto
{
    public class TeamActivityDto
    {
        public TeamActivityDto() { }

        public TeamActivityDto(TeamActivity model)
        {
            Id = model.Id;
            //TeamId = model.TeamId;
            Date = model.Date;
            UserId = model.UserId;
            ActivityTypeId = model.ActivityTypeId;
            Latitude = model.Location?.Y;
            Longitude = model.Location?.X;
            Message = model.Message;
        }

        public int Id { get; set; }
        //public int TeamId { get; set; }
        public DateTime Date { get; set; }
        public int? UserId { get; set; }
        public short ActivityTypeId { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string? Message { get; set; }
        public short? UserReadStatusId { get; set; }
    }
}
