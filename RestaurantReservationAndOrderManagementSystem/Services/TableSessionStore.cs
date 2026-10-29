using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;

namespace RestaurantReservationAndOrderManagementSystem.Services
{
    public sealed class TableSessionStore
    {
        private static readonly TimeSpan SessionLifetime = TimeSpan.FromMinutes(59);
        private readonly IMemoryCache _cache;
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _tableLocks = new(StringComparer.Ordinal);

        public TableSessionStore(IMemoryCache cache)
        {
            _cache = cache;
        }

        public SemaphoreSlim GetLock(string tableId) =>
            _tableLocks.GetOrAdd(tableId, static _ => new SemaphoreSlim(1, 1));

        public string? GetOwner(string tableId) =>
            _cache.Get<string>(GetCacheKey(tableId));

        public void Claim(string tableId, string userId)
        {
            _cache.Set(
                GetCacheKey(tableId),
                userId,
                new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = SessionLifetime
                });
        }

        private static string GetCacheKey(string tableId) => $"TableSession_{tableId}";
    }
}
