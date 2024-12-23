using Telegram.Bot;
using go_around.Models;
using go_around.Services;
using go_around.Interfaces;
using GooglePlaces.Services;
using GooglePlaces.Interfaces;
using GoogleGeocoding.Services;
using GoogleGeocoding.Interfaces;
using System.Globalization;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Localization;
using Microsoft.AspNetCore.Localization;

[assembly: RootNamespace("go_around")]

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
      // Register Bot configuration
      services.Configure<BotConfiguration>(context.Configuration.GetSection("BotConfiguration"));

      // Register named HttpClient to benefits from IHttpClientFactory and consume it with ITelegramBotClient typed client.
      // See https://docs.microsoft.com/en-us/aspnet/core/fundamentals/http-requests?view=aspnetcore-5.0#typed-clients
      // and https://docs.microsoft.com/en-us/dotnet/architecture/microservices/implement-resilient-applications/use-httpclientfactory-to-implement-resilient-http-requests
      services.AddHttpClient("telegram_bot_client").RemoveAllLoggers()
              .AddTypedClient<ITelegramBotClient>((httpClient, sp) =>
              {
                BotConfiguration? botConfiguration = sp.GetService<IOptions<BotConfiguration>>()?.Value;
                ArgumentNullException.ThrowIfNull(botConfiguration);
                TelegramBotClientOptions options = new(botConfiguration.BotToken);
                return new TelegramBotClient(options, httpClient);
              });

      services.AddLocalization(options => options.ResourcesPath = "Resources");

      CultureInfo[] supportedCultures =
      [
          new CultureInfo("en-US"),
          new CultureInfo("uk-UA")
      ];

      services.Configure<RequestLocalizationOptions>(options =>
      {
        options.DefaultRequestCulture = new RequestCulture("uk-UA");
        options.SupportedCultures = supportedCultures;
        options.SupportedUICultures = supportedCultures;
      });

      services.AddScoped<UpdateHandler>();
      services.AddScoped<ReceiverService>();
      services.AddHostedService<PollingService>();

      services.AddTransient<IGooglePlacesService, GooglePlacesService>();
      services.AddTransient<IGoogleGeocodingService, GoogleGeocodingService>();
      services.AddTransient<IMessageLocalizerService, MessageLocalizerService>();

      services.AddTransient<IStoreService, StoreService>();
      services.AddTransient<IHttpCacheService, HttpCacheService>();
      services.AddTransient<ISessionsStoreService, SessionsStoreService>();
      services.AddTransient<IPlacesStoreService, PlacesStoreService>();
      services.AddTransient<IUserSessionService, UserSessionService>();
    })
    .Build();

await host.RunAsync();