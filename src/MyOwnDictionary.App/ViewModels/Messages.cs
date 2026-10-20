using MyOwnDictionary.Core;

namespace MyOwnDictionary.App.ViewModels;

public sealed record ReTranslateMessage(string Text, Language SourceLanguage);

public sealed record PreferencesChanged(AppSettings Settings);
