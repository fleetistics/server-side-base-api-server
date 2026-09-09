using api_server.Controllers.Base;
using api_server.Controllers.MapData.Dto;
using api_server.Controllers.MapData.Services;
using db_model.UserManagement;
using exs.Database.Commons.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Nodes;

namespace api_server.Controllers.MapData
{
	public class MobileGpsController : AuthAPIController
	{
		public MobileGpsController(IRepository repository, MobileGpsDeviceMapTrackService mapTrackService)
		{
			mRepository = repository;
			mMapTrackService = mapTrackService;
		}

		[HttpPost("/api/mobile-gps/location-report")]
		[ProducesResponseType(StatusCodes.Status200OK)]
		[ProducesResponseType(StatusCodes.Status400BadRequest)]
		public async Task<IActionResult> LocationReport([FromBody] JsonArray requestData, CancellationToken cancellationToken)
		{
			if (MobileGpsDeviceId is null)
			{
				return BadRequest(new { message = "MobileGpsDeviceId claim is missing in the access token." });
			}
			await mMapTrackService.ParseAndUpdateDeviceLocationsAsync(UserId, MobileGpsDeviceId.Value, requestData, cancellationToken);
			return Ok();
		}

		private readonly IRepository mRepository;
		private readonly MobileGpsDeviceMapTrackService mMapTrackService;
	}
}
