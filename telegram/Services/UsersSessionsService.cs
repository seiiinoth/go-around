using StackExchange.Redis;

namespace go_around.Services
{
  public class UsersSessionsService : IUsersSessionsService
  {
    private readonly IDatabase _db;
    private readonly ILogger<HttpCacheService> _logger;

    public UsersSessionsService(IConfiguration configuration, ILogger<HttpCacheService> logger)
    {
      var _redis = ConnectionMultiplexer.Connect(configuration.GetConnectionString("RedisStore") ?? throw new InvalidOperationException("Redis store connection string is not configured"));
      _db = _redis.GetDatabase();

      _logger = logger;
    }

    public async Task SetSessionAttributes(string userId, Dictionary<string, string> attributes)
    {
      var hashFields = attributes.Select(kv => new HashEntry(kv.Key, kv.Value)).ToArray();
      await _db.HashSetAsync(userId, hashFields);
      await _db.KeyExpireAsync(userId, TimeSpan.FromDays(30));
    }

    public async Task SetSessionAttribute(string userId, string attributeName, string attributeValue)
    {
      var hashFields = new HashEntry[] { new(attributeName, attributeValue) };
      await _db.HashSetAsync(userId, hashFields);
      await _db.KeyExpireAsync(userId, TimeSpan.FromDays(30));
    }

    public async Task<Dictionary<string, string>?> GetSessionAttributes(string userId)
    {
      var hashFields = await _db.HashGetAllAsync(userId);
      return hashFields.ToDictionary(x => x.Name.ToString(), x => x.Value.ToString());
    }

    public async Task<string> GetSessionAttribute(string userId, string attributeName)
    {
      var value = await _db.HashGetAsync(userId, attributeName);

      if (value.IsNull)
      {
        _logger.LogWarning("Session attribute {attributeName} not found for user {userId}", attributeName, userId);
        return "";
      }

      return value.ToString();
    }
  }
}