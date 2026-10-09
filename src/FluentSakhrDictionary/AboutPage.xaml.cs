using Microsoft.UI.Xaml.Controls;
using System.Reflection;

namespace FluentSakhrDictionary;

public partial class AboutPage : Page
{
    public AboutPage()
    {
        InitializeComponent();
        string v = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.1.0";
        VersionText.Text = Loc.T("Version ", "الإصدار ") + v;
        HeaderText.Text = Loc.T("About", "حول");
        DescText.Text = Loc.T(
            "A modern rebirth of the classic 1996 Sakhr English-Arabic dictionary: 59,427 lemmas, 125,896 Arabic meanings with full diacritics.",
            "إحياء عصري لقاموس صخر الإنجليزي-العربي الصادر عام 1996: 59,427 مدخلة و125,896 معنى عربيًا بالتشكيل الكامل.");
        OfflineText.Text = Loc.T(
            "Works fully offline - both databases are bundled inside the app, no internet needed.",
            "يعمل دون اتصال بالإنترنت إطلاقًا - كلتا قاعدتي البيانات مضمّنتان داخل التطبيق.");
        DevText.Text = Loc.T("Developer: Mohamed Elnaggar", "المطور: Mohamed Elnaggar");
        ContribText.Text = Loc.T("Contributors: app.instinct", "المساهمون: app.instinct");
        LicenseText.Text = Loc.T("License: MIT", "الترخيص: MIT");
        DataEnText.Text = Loc.T("English-Arabic data: the original 1996 Sakhr dictionary", "بيانات إنجليزي-عربي: قاموس صخر الأصلي لعام 1996");
        DataArText.Text = Loc.T("Arabic-English data: English Wiktionary via kaikki.org (wiktextract), CC BY-SA 4.0", "بيانات عربي-إنجليزي: قاموس Wiktionary الإنجليزي عبر kaikki.org (wiktextract)، رخصة CC BY-SA 4.0");
    }
}
