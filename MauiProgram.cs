using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using Syncfusion.Maui.Toolkit.Hosting;
using Camera.MAUI;              
using Plugin.Maui.Audio;

namespace APP
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiCameraView()
                .AddAudio()
                .UseMauiCommunityToolkit()
                .ConfigureSyncfusionToolkit()
                .ConfigureMauiHandlers(handlers =>
                {
#if IOS || MACCATALYST
    				handlers.AddHandler<Microsoft.Maui.Controls.CollectionView, Microsoft.Maui.Controls.Handlers.Items2.CollectionViewHandler2>();
#endif
                })
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                    fonts.AddFont("SegoeUI-Semibold.ttf", "SegoeSemibold");
                    fonts.AddFont("FluentSystemIcons-Regular.ttf", FluentUI.FontFamily);
                });

#if DEBUG
    		builder.Logging.AddDebug();
    		builder.Services.AddLogging(configure => configure.AddDebug());
#endif
            builder.Services.AddSingleton<APP.Services.YoloService>();
            builder.Services.AddSingleton<APP.Services.FuzzyService>();
            builder.Services.AddSingleton<APP.Services.NeuralNetworkService>();
            builder.Services.AddSingleton<APP.Services.GeminiService>();
            builder.Services.AddSingleton<APP.Services.ApiService>();
            builder.Services.AddSingleton<APP.Services.FaceService>();
            builder.Services.AddSingleton(AudioManager.Current);

            builder.Services.AddTransient<APP.Pages.DashboardPage>();
            builder.Services.AddTransient<APP.PageModels.DashboardPageModel>();
            builder.Services.AddTransient<APP.Pages.RegisterPage>();
            builder.Services.AddTransient<APP.PageModels.RegisterPageModel>();
            builder.Services.AddTransient<APP.Pages.LoginPage>();
            builder.Services.AddTransient<APP.PageModels.LoginPageModel>();

            return builder.Build();
        }
    }
}
