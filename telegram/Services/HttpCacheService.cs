using StackExchange.Redis;

namespace go_around.Services
{
  public class HttpCacheService : IHttpCacheService
  {
    private readonly HttpClient _httpClient;
    private readonly IDatabase _db;
    private readonly ILogger<HttpCacheService> _logger;

    public HttpCacheService(HttpClient httpClient, IConfiguration configuration, ILogger<HttpCacheService> logger)
    {
      _httpClient = httpClient;

      var _redis = ConnectionMultiplexer.Connect(configuration.GetConnectionString("RedisCache") ?? throw new InvalidOperationException("Redis cache connection string is not configured"));
      _db = _redis.GetDatabase();

      _logger = logger;
    }

    private string GetRequestKey(HttpRequestMessage request)
    {
      var keyString = $"{request.Method}{request.RequestUri}{request.Headers}{request.Content?.ReadAsStringAsync().Result}";

      _logger.LogInformation("Using request key string: {keyString}", keyString);

      return keyString;
    }

    private async Task<HttpResponseMessage?> GetFromCacheAsync(HttpRequestMessage requestMessage)
    {
      var requestKey = GetRequestKey(requestMessage);

      if (!await _db.KeyExistsAsync(requestKey))
      {
        return null;
      }

      var hashFields = await _db.HashGetAllAsync(requestKey);

      var hashFieldsDict = hashFields.ToDictionary(x => x.Name.ToString(), x => x.Value.ToString());

      var responseMessage = new HttpResponseMessage
      {
        StatusCode = (System.Net.HttpStatusCode)Enum.Parse(typeof(System.Net.HttpStatusCode), hashFieldsDict["StatusCode"]),
        Content = new StringContent(hashFieldsDict["Content"])
      };

      var headersList = hashFieldsDict["Headers"].Split('\n');
      foreach (var header in headersList)
      {
        if (!string.IsNullOrEmpty(header))
        {
          var parts = header.Split(':');
          if (parts.Length == 2)
          {
            responseMessage.Headers.Add(parts[0].Trim(), parts[1].Trim());
          }
        }
      }
      return responseMessage;
    }

    private async Task SetCacheAsync(HttpRequestMessage requestMessage, HttpResponseMessage responseMessage)
    {
      var requestKey = GetRequestKey(requestMessage);

      var hashFields = new HashEntry[]
      {
        new("StatusCode", responseMessage.StatusCode.ToString()),
        new("Headers", responseMessage.Headers.ToString()),
        new("Content", responseMessage.Content.ReadAsStringAsync().Result),
      };

      await _db.HashSetAsync(requestKey, hashFields);
    }

    public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request)
    {
      var cachedResponse = await GetFromCacheAsync(request);

      if (cachedResponse != null)
      {
        return cachedResponse;
      }

      var response = await _httpClient.SendAsync(request);

      if (response.IsSuccessStatusCode)
      {
        await SetCacheAsync(request, response);
      }

      return response;
    }
  }
}

