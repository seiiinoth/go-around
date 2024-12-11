namespace go_around.Services
{
  public interface IHttpCacheService
  {
    Task<HttpResponseMessage> SendAsync(HttpRequestMessage request);
  }
}
