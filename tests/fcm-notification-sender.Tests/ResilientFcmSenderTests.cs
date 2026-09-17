using fcm_notification_sender.Fcm;
using Microsoft.Extensions.Options;
using Shouldly;

namespace fcm_notification_sender.Tests
{
	public class ResilientFcmSenderTests
	{
		[Fact]
		public async Task SendWithRetryAsync_OkOnFirstAttempt_DoesNotRetry()
		{
			var stub = new StubFcmMessageSender(FcmSendResult.Ok("msg-1"));
			var sender = createSender(stub, maxAttempts: 4);

			var (result, attempts) = await sender.SendWithRetryAsync("token", 1, "title", "body", 1, null, "", CancellationToken.None);

			result.Outcome.ShouldBe(FcmSendOutcome.Ok);
			attempts.ShouldBe(1);
			stub.Calls.Count.ShouldBe(1);
		}

		[Fact]
		public async Task SendWithRetryAsync_TransientThenOk_RetriesOnceThenSucceeds()
		{
			var stub = new StubFcmMessageSender(
				FcmSendResult.Failed(FcmSendOutcome.TransientError, "UNAVAILABLE", "temporary"),
				FcmSendResult.Ok("msg-2"));
			var sender = createSender(stub, maxAttempts: 4);

			var (result, attempts) = await sender.SendWithRetryAsync("token", 1, "title", "body", 1, null, "", CancellationToken.None);

			result.Outcome.ShouldBe(FcmSendOutcome.Ok);
			attempts.ShouldBe(2);
			stub.Calls.Count.ShouldBe(2);
		}

		[Fact]
		public async Task SendWithRetryAsync_AlwaysTransient_GivesUpAtMaxAttempts()
		{
			var stub = new StubFcmMessageSender(FcmSendResult.Failed(FcmSendOutcome.TransientError, "UNAVAILABLE", "temporary"));
			var sender = createSender(stub, maxAttempts: 3);

			var (result, attempts) = await sender.SendWithRetryAsync("token", 1, "title", "body", 1, null, "", CancellationToken.None);

			result.Outcome.ShouldBe(FcmSendOutcome.TransientError);
			attempts.ShouldBe(3);
			stub.Calls.Count.ShouldBe(3);
		}

		[Fact]
		public async Task SendWithRetryAsync_PermanentError_NeverRetries()
		{
			var stub = new StubFcmMessageSender(FcmSendResult.Failed(FcmSendOutcome.PermanentError, "UNREGISTERED", "dead token"));
			var sender = createSender(stub, maxAttempts: 5);

			var (result, attempts) = await sender.SendWithRetryAsync("token", 1, "title", "body", 1, null, "", CancellationToken.None);

			result.Outcome.ShouldBe(FcmSendOutcome.PermanentError);
			attempts.ShouldBe(1);
			stub.Calls.Count.ShouldBe(1);
		}

		private static ResilientFcmSender createSender(StubFcmMessageSender stub, int maxAttempts)
		{
			var options = Options.Create(new FcmSenderOptions
			{
				MaxAttemptsPerSession = maxAttempts,
				RetryBaseDelayMilliseconds = 1, // keep the exponential backoff effectively instant in tests
			});
			return new ResilientFcmSender(stub, options);
		}
	}
}
