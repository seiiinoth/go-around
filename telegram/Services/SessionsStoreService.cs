using go_around.Interfaces;
using StackExchange.Redis;

namespace go_around.Services
{
  public class SessionsStoreService(IStoreService storeService) : ISessionsStoreService
  {
    private readonly IStoreService _storeService = storeService;
    private readonly TimeSpan _defaultDataTTL = TimeSpan.FromDays(30);

    private static string GetSessionKey(string userId)
    {
      return $"session:{userId}";
    }

    public async Task SetSessionAttributes(string userId, Dictionary<string, string> attributes)
    {
      var hashFields = attributes.Select(kv => new HashEntry(kv.Key, kv.Value)).ToArray();
      await _storeService.HashSetAsync(GetSessionKey(userId), hashFields);
      await _storeService.KeyExpireAsync(GetSessionKey(userId), _defaultDataTTL);
    }

    public async Task SetSessionAttribute(string userId, string attributeName, string attributeValue = "")
    {
      var hashFields = new HashEntry[] { new(attributeName, attributeValue) };
      await _storeService.HashSetAsync(GetSessionKey(userId), hashFields);
      await _storeService.KeyExpireAsync(GetSessionKey(userId), _defaultDataTTL);
    }

    public async Task<bool> SessionAttributeExists(string userId, string attributeName)
    {
      return await _storeService.HashExistsAsync(GetSessionKey(userId), attributeName);
    }

    public async Task<Dictionary<string, string>?> GetSessionAttributes(string userId)
    {
      var hashFields = await _storeService.HashGetAllAsync(GetSessionKey(userId));
      return hashFields.ToDictionary(x => x.Name.ToString(), x => x.Value.ToString());
    }

    public async Task<string> GetSessionAttribute(string userId, string attributeName)
    {
      var value = await _storeService.HashGetAsync(GetSessionKey(userId), attributeName);

      if (value.IsNull)
        return "";

      return value.ToString();
    }

    public async Task DeleteSessionAttributes(string userId, List<string> attributes)
    {
      var hashFields = attributes.Select(attr => new RedisValue(attr)).ToArray();
      await _storeService.HashDeleteAsync(GetSessionKey(userId), hashFields);
      await _storeService.KeyExpireAsync(GetSessionKey(userId), _defaultDataTTL);
    }

    public async Task DeleteSessionAttribute(string userId, string attribute)
    {
      if (!await SessionAttributeExists(GetSessionKey(userId), attribute))
      {
        return;
      }
      await _storeService.HashDeleteAsync(GetSessionKey(userId), attribute);
      await _storeService.KeyExpireAsync(GetSessionKey(userId), _defaultDataTTL);
    }
  }
}