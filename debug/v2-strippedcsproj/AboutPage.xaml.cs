using Microsoft.UI.Xaml.Controls;
using System.Reflection;

namespace FluentSakhrDictionary;

public partial class AboutPage : Page
{
    public AboutPage()
    {
        InitializeComponent();
        string v = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.1.0";
        VersionText.Text = "Version " + v;
    }
}
