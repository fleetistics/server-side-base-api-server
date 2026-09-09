using Microsoft.Extensions.Caching.Memory;
using System.Collections.Concurrent;

namespace api_server.Idempotency
{
	public class IdempotencyStore
	{
		private static readonly TimeSpan Ttl = TimeSpan.FromHours(24);

		private readonly IMemoryCache mCache;
		private readonly ConcurrentDictionary<string, SemaphoreSlim> mLocks = new();

		public IdempotencyStore(IMemoryCache cache)
		{
			mCache = cache;
		}

		public bool TryGetResponse(string key, out CachedResponse? response)
		{
			return mCache.TryGetValue(key, out response);
		}

		public void StoreResponse(string key, CachedResponse response)
		{
			mCache.Set(key, response, Ttl);
		}

		public SemaphoreSlim GetLock(string key)
		{
			return mLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
		}

		public void ReleaseLock(string key)
		{
			if (mLocks.TryGetValue(key, out var semaphore))
			{
				semaphore.Release();
				mLocks.TryRemove(key, out _);
			}
		}
	}
}
