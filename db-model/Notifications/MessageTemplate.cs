using System;
using System.Collections.Generic;
using System.Text;

namespace db_model.Notifications
{
	public class MessageTemplate
	{
		public int Id { get; set; }
		public string? Name { get; set; }
		public string? Title { get; set; }
		public string? Body { get; set; }
		public DateTime LatestUpdate { get; set; }
	}
}
