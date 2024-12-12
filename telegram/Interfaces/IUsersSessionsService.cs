namespace go_around.Services
{
  public interface IUsersSessionsService
  {
    Task SetSessionAttributes(string userId, Dictionary<string, string> fields);
    Task SetSessionAttribute(string userId, string attributeName, string attributeValue);
    Task<Dictionary<string, string>?> GetSessionAttributes(string userId);
    Task<string> GetSessionAttribute(string userId, string attributeName);
  }
}
