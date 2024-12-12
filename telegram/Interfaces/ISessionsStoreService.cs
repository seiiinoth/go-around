namespace go_around.Interfaces
{
  public interface ISessionsStoreService
  {
    Task SetSessionAttributes(string userId, Dictionary<string, string> fields);
    Task SetSessionAttribute(string userId, string attributeName, string attributeValue);
    Task<Dictionary<string, string>?> GetSessionAttributes(string userId);
    Task<string> GetSessionAttribute(string userId, string attributeName);
    Task DeleteSessionAttributes(string userId, List<string> attributes);
    Task DeleteSessionAttribute(string userId, string attribute);
  }
}
