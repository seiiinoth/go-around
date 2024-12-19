using go_around.Interfaces;
using StackExchange.Redis;

namespace go_around.Services
{
  public class StoreService : IStoreService
  {
    private readonly IDatabase _db;

    public StoreService(IConfiguration configuration)
    {
      var _redis = ConnectionMultiplexer.Connect(configuration.GetConnectionString("RedisStore") ?? throw new InvalidOperationException("Redis store connection string is not configured"));
      _db = _redis.GetDatabase();
    }

    public async Task<bool> HashExistsAsync(RedisKey key, RedisValue hashField, CommandFlags flags = CommandFlags.None)
    {
      return await _db.HashExistsAsync(key, hashField, flags);
    }

    public async Task<HashEntry[]> HashGetAllAsync(RedisKey key, CommandFlags flags = CommandFlags.None)
    {
      return await _db.HashGetAllAsync(key, flags);
    }

    public async Task<RedisValue> HashGetAsync(RedisKey key, RedisValue hashField, CommandFlags flags = CommandFlags.None)
    {
      return await _db.HashGetAsync(key, hashField, flags);
    }

    public async Task HashSetAsync(RedisKey key, HashEntry[] hashFields, CommandFlags flags = CommandFlags.None)
    {
      await _db.HashSetAsync(key, hashFields, flags);
    }

    public async Task HashDeleteAsync(RedisKey key, RedisValue hashField, CommandFlags flags = CommandFlags.None)
    {
      await _db.HashDeleteAsync(key, hashField, flags);
    }

    public async Task HashDeleteAsync(RedisKey key, RedisValue[] hashFields, CommandFlags flags = CommandFlags.None)
    {
      await _db.HashDeleteAsync(key, hashFields, flags);
    }

    public async Task<bool> KeyExpireAsync(RedisKey key, TimeSpan? expiry, ExpireWhen when = ExpireWhen.Always, CommandFlags flags = CommandFlags.None)
    {
      return await _db.KeyExpireAsync(key, expiry, when, flags);
    }

    public async Task<RedisValue> StringGetAsync(RedisKey key, CommandFlags flags = CommandFlags.None)
    {
      return await _db.StringGetAsync(key, flags);
    }

    public async Task<bool> StringSetAsync(RedisKey key, RedisValue value, TimeSpan? expiry = null, bool keepTtl = false, When when = When.Always, CommandFlags flags = CommandFlags.None)
    {
      return await _db.StringSetAsync(key, value, expiry, keepTtl, when, flags);
    }
  }
}