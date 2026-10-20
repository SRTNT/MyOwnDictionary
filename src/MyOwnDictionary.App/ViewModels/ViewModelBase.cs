using CommunityToolkit.Mvvm.ComponentModel;

namespace MyOwnDictionary.App.ViewModels;

public abstract class ViewModelBase : ObservableObject
{
    public virtual string Title => string.Empty;
    public virtual string Subtitle => string.Empty;
    public virtual Task OnShownAsync() => Task.CompletedTask;
}
