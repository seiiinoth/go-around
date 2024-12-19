namespace go_around.Interfaces
{
  public interface IHttpCacheService
  {
    Task<HttpResponseMessage> SendAsync(HttpRequestMessage request);
  }
}
