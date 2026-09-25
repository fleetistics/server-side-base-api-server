

using db_model.Messages;

namespace api_server.Controllers.Messages
{
    public class UserMessageDto
    {
        public UserMessageDto() { }

        public UserMessageDto(UserMessage userMessage)
        {
            Id = userMessage.Id;
            //TeamId = model.TeamId;
            UserId = userMessage.UserId;
            Date = userMessage.Date;
            ToUserId = userMessage.ToUserId;
            Message = userMessage.Message;
        }

        public int Id { get; set; }
        //public int TeamId { get; set; }
        public int UserId { get; set; }
        public DateTime Date { get; set; }
        public int? ToUserId { get; set; }
        public string Message { get; set; } = default!;
        public short? UserReadStatusId { get; set; }
    }
}
