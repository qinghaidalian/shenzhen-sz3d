using System.Windows;

namespace Sz3D.Presentation.WPF.Services;

public enum AppTheme { Light, Dark }

public sealed class ThemeManager
{
    private const string LightUri = "/Sz3D.Presentation.WPF;component/Themes/Light.xaml";
    private const string DarkUri  = "/Sz3D.Presentation.WPF;component/Themes/Dark.xaml";

    public AppTheme CurrentTheme { get; private set; } = AppTheme.Dark;

    public void ApplyTheme(AppTheme theme)
    {
        var uri = theme == AppTheme.Dark ? DarkUri : LightUri;
        var dict = new ResourceDictionary { Source = new Uri(uri, UriKind.Relative) };
        Application.Current.Resources.MergedDictionaries[0] = dict;
        CurrentTheme = theme;
    }
}
