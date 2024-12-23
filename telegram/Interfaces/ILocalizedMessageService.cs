using go_around.Models;

namespace go_around.Interfaces
{
  public interface IMessageLocalizerService
  {
    string GetMessage(string message, Language locale);
  }
}
