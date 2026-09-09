using api_server.Controllers.Base;
using api_server.Controllers.Log.Dto;
using api_server.core;
using db_model.Log;
using exs.Database.Commons.Interfaces;
using Microsoft.AspNetCore.Mvc;
using System.IO.Compression;

namespace api_server.Controllers.Log
{
	public class ClientLogController : AuthAPIController
	{
		public ClientLogController(IRepository repository)
		{
			mRepository = repository;
		}

		[HttpPost("/api/client-log")]
		// The SPA flight recorder sends application/gzip (gzipped JSON) or application/json;
		// the raw body is read from the stream, so no [Consumes] restriction here.
		[ProducesResponseType(StatusCodes.Status200OK)]
		public async Task<IActionResult> Upload(CancellationToken cancellationToken)
		{
			var body = Request.ContentType?.Contains("gzip") == true
					? new GZipStream(Request.Body, CompressionMode.Decompress)
					: Request.Body;

			using var reader = new StreamReader(body);
			var payload = await reader.ReadToEndAsync(cancellationToken);
			if (payload is null) return BadRequest();

			// Requests to this endpoint are excluded from tracing (see Program.cs), so a
			// span here would surface as orphaned roots; a size metric keeps aggregate
			// visibility over intake volume instead.
			Telemetry.ClientLogPayloadBytes.Record(payload.Length);

			mRepository.Create(new ClientLogRecord			
			{
				//Date = DateTime.UtcNow,
				UserId = UserId,
				Log = payload
			});

			await mRepository.SaveAsync(cancellationToken);

			return Ok();
		}

		[HttpPost("/api/client-log-pack")]
		// Mirrors the media-upload pattern (MediaController.Upload): [FromForm] binds the
		// text fields plus the optional file part from the multipart body built by
		// logUploadService.ts's submitIssueReport.
		[Consumes("multipart/form-data")]
		[RequestSizeLimit(32 * 1024 * 1024)]
		[ProducesResponseType(StatusCodes.Status200OK)]
		public async Task<IActionResult> UploadAppIssue([FromForm] ClientLogPackRequest request, CancellationToken cancellationToken)
		{
			var log = string.Empty;
			if (request.File is { Length: > 0 } file)
			{
				await using var fileStream = file.OpenReadStream();
				await using var gzip = new GZipStream(fileStream, CompressionMode.Decompress);
				using var reader = new StreamReader(gzip);
				log = await reader.ReadToEndAsync(cancellationToken);

				Telemetry.ClientLogPayloadBytes.Record(log.Length);
			}

			mRepository.Create(new ClientLogRecord
			{
				//Date = DateTime.UtcNow,
				UserId = UserId,
				Log = log,
				UserDescription = request.UserDescription,
				IssueContext = request.IssueContext,
				ClientSideInfo = request.ClientSideInfo,
				ClientSideDate = request.ClientSideDate,
			});

			await mRepository.SaveAsync(cancellationToken);

			return Ok();
		}

		private readonly IRepository mRepository;
	}
}
