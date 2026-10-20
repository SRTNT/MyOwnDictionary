using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using MyOwnDictionary.App.ViewModels;
using MyOwnDictionary.Core;

namespace MyOwnDictionary.App.Services;

public static class AppHost
{
    public static IServiceProvider Build()
    {
        var settings = new SettingsStore(AppPaths.Settings);
        var customFolder = !AppPaths.IsDefaultFolder(settings.Current.DataFolder);
        var services = new ServiceCollection();
        services.AddSingleton(new AppDatabase(AppPaths.DatabaseFile(settings.Current), customFolder));
        services.AddSingleton(settings);
        services.AddSingleton<DictionaryRepository>();
        services.AddSingleton<HistoryRepository>();
        services.AddSingleton<QuizRepository>();
        services.AddSingleton<IMessenger>(WeakReferenceMessenger.Default);
        services.AddSingleton<ClipboardService>();
        services.AddSingleton<ITranslationProvider>(sp =>
            new GoogleFreeTranslationProvider(new HttpClient(), sp.GetRequiredService<SettingsStore>()));
        services.AddSingleton<TranslationService>();
        services.AddSingleton<TranslateViewModel>();
        services.AddSingleton<DictionaryViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddKeyedSingleton<QuizViewModel>("en", (sp, _) => new QuizViewModel(Language.En, sp.GetRequiredService<QuizRepository>()));
        services.AddKeyedSingleton<QuizViewModel>("fa", (sp, _) => new QuizViewModel(Language.Fa, sp.GetRequiredService<QuizRepository>()));
        services.AddKeyedSingleton<HistoryViewModel>("en", (sp, _) => CreateHistory(
            sp,
            Language.En,
            "English history",
            "English → Persian",
            "en"));
        services.AddKeyedSingleton<HistoryViewModel>("fa", (sp, _) => CreateHistory(
            sp,
            Language.Fa,
            "Persian history",
            "Persian → English",
            "fa"));
        services.AddSingleton(sp => new MainViewModel(
            sp.GetRequiredService<TranslateViewModel>(),
            sp.GetRequiredService<DictionaryViewModel>(),
            sp.GetRequiredKeyedService<HistoryViewModel>("en"),
            sp.GetRequiredKeyedService<HistoryViewModel>("fa"),
            sp.GetRequiredService<SettingsViewModel>(),
            sp.GetRequiredService<SettingsStore>(),
            sp.GetRequiredService<IMessenger>()));
        return services.BuildServiceProvider();
    }

    private static HistoryViewModel CreateHistory(IServiceProvider services, Language language, string title, string subtitle, string quizKey)
    {
        return new HistoryViewModel(
            language,
            title,
            subtitle,
            services.GetRequiredService<HistoryRepository>(),
            services.GetRequiredService<ClipboardService>(),
            services.GetRequiredService<IMessenger>(),
            services.GetRequiredKeyedService<QuizViewModel>(quizKey));
    }
}
