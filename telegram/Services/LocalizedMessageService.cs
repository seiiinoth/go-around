using Microsoft.Extensions.Localization;
using System.Diagnostics.CodeAnalysis;
using go_around.Models;
using go_around.Interfaces;

namespace go_around.Services
{
  public class MessageLocalizerService(IStringLocalizerFactory factory) : IMessageLocalizerService
  {
    private readonly IStringLocalizer _localizer =
        factory.Create(typeof(MessageLocalizerService));

    [return: NotNullIfNotNull(nameof(_localizer))]
    public string GetMessage(string message, Language locale)
    {
      var culture = locale switch
      {
        Language.ENGLISH => "en-US",
        Language.UKRAINIAN => "uk-UA",
        _ => "uk-UA"
      };
      var originalCulture = System.Globalization.CultureInfo.CurrentCulture;

      System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo(culture);
      System.Globalization.CultureInfo.CurrentUICulture = new System.Globalization.CultureInfo(culture);

      var localizedMessage = _localizer[message];

      System.Globalization.CultureInfo.CurrentCulture = originalCulture;
      System.Globalization.CultureInfo.CurrentUICulture = originalCulture;

      return localizedMessage;
    }
  }
}

