using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Reflection;

namespace FluentSakhrDictionary;

public partial class AboutPage : Page
{
    static string CurrentVersion => Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.1.0";

    public AboutPage()
    {
        InitializeComponent();
        string v = CurrentVersion;
        VersionText.Text = Loc.T("Version ", "الإصدار ") + v;
        HeaderText.Text = Loc.T("About", "حول");
        DescText.Text = Loc.T(
            "A modern rebirth of the classic 1996 Sakhr English-Arabic dictionary: 59,427 lemmas, 125,896 Arabic meanings with full diacritics.",
            "إحياء عصري لقاموس صخر الإنجليزي-العربي الصادر عام 1996: 59,427 مدخلة و125,896 معنى عربيًا بالتشكيل الكامل.");
        OfflineText.Text = Loc.T(
            "Works fully offline - both databases are bundled inside the app, no internet needed.",
            "يعمل دون اتصال بالإنترنت إطلاقًا - كلتا قاعدتي البيانات مضمّنتان داخل التطبيق.");
        DevText.Text = Loc.T("Developer: Mohamed Elnaggar", "المطور: Mohamed Elnaggar");
        ContribText.Text = Loc.T("Contributors: app.instinct.com", "المساهمون: app.instinct.com");
        LicenseText.Text = Loc.T("License: MIT", "الترخيص: MIT");
        DataEnText.Text = Loc.T("English-Arabic data: the original 1996 Sakhr dictionary", "بيانات إنجليزي-عربي: قاموس صخر الأصلي لعام 1996");
        DataArText.Text = Loc.T("Arabic-English data: English Wiktionary via kaikki.org (wiktextract), CC BY-SA 4.0", "بيانات عربي-إنجليزي: قاموس Wiktionary الإنجليزي عبر kaikki.org (wiktextract)، رخصة CC BY-SA 4.0");
        CreditLink.Content = Loc.T("brought to you by Instinct", "مقدَّم لكم من Instinct");
        UpdatesHeader.Text = Loc.T("Updates", "التحديثات");
        UpdatesNote.Text = Loc.T(
            "The app never checks for updates on its own. Press the button to check once, right now.",
            "لا يتحقق التطبيق من التحديثات من تلقاء نفسه. اضغط الزر للتحقق مرة واحدة الآن.");
        CheckButton.Content = Loc.T("Check for updates", "التحقق من التحديثات");
        UpdateLink.Content = Loc.T("Open download page", "فتح صفحة التنزيل");
    }

    /// <summary>Manual update check: fires only from this button, never in the background.</summary>
    async void CheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        CheckButton.IsEnabled = false;
        UpdateLink.Visibility = Visibility.Collapsed;
        UpdateResult.Text = Loc.T("Checking...", "جارٍ التحقق...");
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("SakhrDictionaryRevive/" + CurrentVersion);
            http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            // releases?per_page=1 rather than /latest: our releases are marked pre-release, which /latest skips.
            using var response = await http.GetAsync("https://api.github.com/repos/MohamedElnaggar00/Sakhr-Dictionary-Revive/releases?per_page=1");
            response.EnsureSuccessStatusCode();
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            string tag = "";
            foreach (var release in json.RootElement.EnumerateArray())
            {
                tag = (release.GetProperty("tag_name").GetString() ?? "").TrimStart('v', 'V');
                break;
            }
            if (Version.TryParse(tag, out var latest) && Version.TryParse(CurrentVersion, out var current) && latest > current)
            {
                UpdateResult.Text = Loc.T("Version " + tag + " is available.", "الإصدار " + tag + " متاح.");
                UpdateLink.NavigateUri = new Uri("https://github.com/MohamedElnaggar00/Sakhr-Dictionary-Revive/releases/tag/v" + tag);
                UpdateLink.Visibility = Visibility.Visible;
            }
            else
            {
                UpdateResult.Text = tag.Length > 0
                    ? Loc.T("You're up to date (v" + CurrentVersion + ").", "أنت على أحدث إصدار (v" + CurrentVersion + ").")
                    : Loc.T("No releases found yet.", "لا توجد إصدارات بعد.");
            }
        }
        catch
        {
            UpdateResult.Text = Loc.T("Couldn't check for updates. Check your internet connection and try again.", "تعذر التحقق من التحديثات. تحقق من اتصالك بالإنترنت وحاول مرة أخرى.");
        }
        finally
        {
            CheckButton.IsEnabled = true;
        }
    }
}
