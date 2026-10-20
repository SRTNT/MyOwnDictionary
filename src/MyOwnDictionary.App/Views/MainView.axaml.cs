using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Platform;

namespace MyOwnDictionary.App.Views;

public partial class MainView : UserControl
{
    private IInsetsManager? _insets;

    public MainView()
    {
        InitializeComponent();
        if (!App.IsPhoneLayout)
            return;

        Classes.Add("phone");
        PageFrame.Margin = new Thickness(14, 12, 14, 8);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (!OperatingSystem.IsAndroid() || TopLevel.GetTopLevel(this)?.InsetsManager is not { } insets)
            return;

        _insets = insets;
        insets.DisplayEdgeToEdgePreference = true;
        insets.SafeAreaChanged += OnSafeAreaChanged;
        ApplySafeArea(insets.SafeAreaPadding);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_insets is not null)
        {
            _insets.SafeAreaChanged -= OnSafeAreaChanged;
            _insets = null;
        }

        base.OnDetachedFromVisualTree(e);
    }

    private void OnSafeAreaChanged(object? sender, SafeAreaChangedArgs e) => ApplySafeArea(e.SafeAreaPadding);

    private void ApplySafeArea(Thickness safeArea)
    {
        TopBarContent.Margin = new Thickness(0, safeArea.Top, 0, 0);
        TabBarContent.Margin = new Thickness(0, 0, 0, safeArea.Bottom);
        ShellGrid.Margin = new Thickness(safeArea.Left, 0, safeArea.Right, 0);
    }
}
