using Avalonia;
using Avalonia.Controls;

namespace MyOwnDictionary.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        if (!App.IsPhoneLayout)
            return;

        Title = "MyOwnDictionary — Phone";
        Width = 402;
        Height = 874;
        MinWidth = 360;
        MinHeight = 640;
        Classes.Add("phone");
        PageFrame.Margin = new Thickness(14, 12, 14, 8);
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        if (!App.IsPhoneLayout || Screens?.Primary is not { } screen)
            return;

        var area = screen.WorkingArea;
        var width = (int)Bounds.Width;
        var height = (int)Bounds.Height;
        Position = new PixelPoint(
            Math.Max(area.X, area.X + area.Width - width - 16),
            area.Y + Math.Max(0, (area.Height - height) / 2));
    }
}
