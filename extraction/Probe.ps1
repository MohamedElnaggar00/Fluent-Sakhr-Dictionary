# Fluent Sakhr - extraction probe v4.
# v3 findings: the app's own FatalAppExit says "Error in DLL'S" (dialog is hosted by
# csrss.exe). The DLLs import only KERNEL32/USER32 - no missing runtime deps. Likely
# cause: the exe finds its own dir via ANSI GetModuleFileNameA; under the runner's
# en-US codepage the Arabic folder name mangles to '?' so LoadLibrary of its own
# DLLs fails. v4: run a copy from an all-ASCII path (C:\sakhrapp\sakhr.exe).
$ErrorActionPreference = 'Continue'
$OutDir = "$env:RUNNER_TEMP\probe-results"
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$log = "$OutDir\probe.log"
function Log($m) { "{0:HH:mm:ss.fff} {1}" -f (Get-Date), $m | Tee-Object -FilePath $log -Append }

Add-Type -AssemblyName System.Windows.Forms, System.Drawing

# --- C# interop ------------------------------------------------------------
$cs = @'
using System;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;
public class WinP {
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] static extern bool EnumChildWindows(IntPtr p, EnumProc cb, IntPtr l);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int c);
  [DllImport("user32.dll")] public static extern bool ShowWindowAsync(IntPtr h, int c);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)]
  public static extern IntPtr SendMessageTimeout(IntPtr h, uint msg, IntPtr w, string l, uint flags, uint timeout, out IntPtr result);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)]
  public static extern IntPtr SendMessageTimeout(IntPtr h, uint msg, IntPtr w, IntPtr l, uint flags, uint timeout, out IntPtr result);
  [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);

  public const uint SMTO_ABORTIFHUNG = 0x0002;
  public const uint WM_SETTEXT = 0x000C;
  public const uint WM_GETTEXT = 0x000D;
  public const uint BM_CLICK = 0x00F5;
  public const uint WM_KEYDOWN = 0x0100;
  public const uint WM_KEYUP = 0x0101;
  public const uint WM_CHAR = 0x0102;
  public const uint VK_RETURN = 0x0D;

  public static string Txt(IntPtr h) { var s = new StringBuilder(512); GetWindowText(h, s, 512); return s.ToString(); }
  public static string Cls(IntPtr h) { var s = new StringBuilder(256); GetClassName(h, s, 256); return s.ToString(); }

  public static List<string> Top() {
    var r = new List<string>();
    EnumWindows((h, l) => {
      uint pid; GetWindowThreadProcessId(h, out pid);
      r.Add((IsWindowVisible(h) ? "vis " : "hid ") + string.Format("0x{0:X} pid={1} class=[{2}] title=[{3}]", h.ToInt64(), pid, Cls(h), Txt(h)));
      return true;
    }, IntPtr.Zero);
    return r;
  }
  public static List<IntPtr> TopHwnds() {
    var r = new List<IntPtr>();
    EnumWindows((h, l) => { r.Add(h); return true; }, IntPtr.Zero);
    return r;
  }
  public static List<string> Kids(IntPtr p) {
    var r = new List<string>();
    EnumChildWindows(p, (h, l) => {
      r.Add(string.Format("  0x{0:X} class=[{1}] text=[{2}]", h.ToInt64(), Cls(h), Txt(h)));
      return true;
    }, IntPtr.Zero);
    return r;
  }
  public static List<IntPtr> KidHwnds(IntPtr p) {
    var r = new List<IntPtr>();
    EnumChildWindows(p, (h, l) => { r.Add(h); return true; }, IntPtr.Zero);
    return r;
  }
  // returns true if the SendMessageTimeout call completed (did not time out)
  public static bool Click(IntPtr h) {
    IntPtr res;
    return SendMessageTimeout(h, BM_CLICK, IntPtr.Zero, IntPtr.Zero, SMTO_ABORTIFHUNG, 3000, out res) != IntPtr.Zero;
  }
  public static bool SetText(IntPtr h, string t) {
    IntPtr res;
    return SendMessageTimeout(h, WM_SETTEXT, IntPtr.Zero, t, SMTO_ABORTIFHUNG, 3000, out res) != IntPtr.Zero;
  }
  public static bool Key(IntPtr h, uint vk) {
    bool ok = PostMessage(h, WM_KEYDOWN, (IntPtr)vk, IntPtr.Zero);
    PostMessage(h, WM_KEYUP, (IntPtr)vk, IntPtr.Zero);
    return ok;
  }
  public static bool CharMsg(IntPtr h, char c) {
    return PostMessage(h, WM_CHAR, (IntPtr)c, IntPtr.Zero);
  }
}
'@
Add-Type -TypeDefinition $cs -ReferencedAssemblies System.Windows.Forms

function Shot($name) {
  try {
    $vs = [System.Windows.Forms.SystemInformation]::VirtualScreen
    $bmp = New-Object System.Drawing.Bitmap $vs.Width, $vs.Height
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($vs.Left, $vs.Top, 0, 0, $bmp.Size)
    $bmp.Save("$OutDir\$name", [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose()
    Log "screenshot saved: $name"
  } catch { Log "screenshot $name failed: $_" }
}

# --- 1. unpack -------------------------------------------------------------
$sevenZip = "C:\Program Files\7-Zip\7z.exe"
$dest = "C:\sakhr"
& $sevenZip x "data\legacy\sakhr.7z" "-o$dest" -y | Out-File "$OutDir\7z.log"
$exe = Get-ChildItem -Path $dest -Recurse -Filter *.exe |
       Sort-Object Length -Descending | Select-Object -First 1
Log "main exe candidate: $($exe.FullName) ($($exe.Length) bytes)"
Get-ChildItem -Path $dest -Recurse | Select-Object FullName, Length |
  Out-File "$OutDir\files.txt"
Get-WinSystemLocale | Out-File "$OutDir\system-locale.txt"
Log "system locale: $((Get-WinSystemLocale).Name)"

# all-ASCII copy: old ANSI code path mangles the Arabic folder name under en-US ACP
$ascii = "C:\sakhrapp"
New-Item -ItemType Directory -Force -Path $ascii | Out-Null
$n = 0
Get-ChildItem -Path $exe.DirectoryName -File | ForEach-Object {
  $name = $_.Name
  if ($name -match '[^\x00-\x7F]') {
    $ext = [IO.Path]::GetExtension($name)
    if ($name -eq $exe.Name) { $name = "sakhr$ext" } else { $n++; $name = "file$n$ext" }
  }
  Copy-Item $_.FullName (Join-Path $ascii $name)
}
$exe = Get-Item (Join-Path $ascii "sakhr.exe")
Log "ascii copy launched from: $($exe.FullName)"

# --- 2. launch -------------------------------------------------------------
$proc = Start-Process -FilePath $exe.FullName -WorkingDirectory $exe.DirectoryName -PassThru
Log "launched pid=$($proc.Id)"
Start-Sleep -Seconds 8
Log "hasExited=$($proc.HasExited)"

# process tree + modules
Get-CimInstance Win32_Process |
  Select-Object ProcessId, ParentProcessId, Name, CommandLine |
  Format-List | Out-File "$OutDir\processes.txt"
Log "process tree dumped"
try {
  (Get-Process -Id $proc.Id).Modules | Select-Object ModuleName, FileName |
    Format-Table -AutoSize | Out-File "$OutDir\modules.txt" -Width 200
  Log "modules dumped: $((Get-Process -Id $proc.Id).Modules.Count) modules"
} catch { Log "module dump failed: $_" }

Shot "probe-01-launched.png"

# --- 3. window inventory ---------------------------------------------------
$tops = [WinP]::Top()
$tops | Out-File "$OutDir\windows.txt"
Log "top-level windows: $($tops.Count)"

# --- 4. dismiss Fatal Application Exit dialogs -----------------------------
foreach ($h in [WinP]::TopHwnds()) {
  $t = [WinP]::Txt($h); $c = [WinP]::Cls($h)
  if ($c -eq '#32770' -and $t -match 'Fatal Application Exit') {
    Log "fatal dialog found: 0x$($h.ToInt64().ToString('X')) title=[$t]"
    foreach ($k in [WinP]::KidHwnds($h)) {
      if ([WinP]::Cls($k) -eq 'Button' -and [WinP]::Txt($k) -match 'OK') {
        $ok = [WinP]::Click($k)
        Log "clicked OK on fatal dialog: sent=$ok"
      }
    }
  }
}
Start-Sleep -Seconds 2
Shot "probe-02-after-dialog-dismiss.png"

# --- 5. locate the app window by class -------------------------------------
$appHwnd = [IntPtr]::Zero
foreach ($h in [WinP]::TopHwnds()) {
  if ([WinP]::Cls($h) -eq 'DictWClass1') { $appHwnd = $h; break }
}
if ($appHwnd -eq [IntPtr]::Zero) {
  Log "FATAL: no DictWClass1 window found"
} else {
  Log "app window: 0x$($appHwnd.ToInt64().ToString('X')) title=[$([WinP]::Txt($appHwnd))]"
  [WinP]::Kids($appHwnd) | Out-File "$OutDir\children.txt"
  Log "children dumped: $([WinP]::KidHwnds($appHwnd).Count) child windows"

  [WinP]::ShowWindowAsync($appHwnd, 9) | Out-Null  # SW_RESTORE, non-blocking
  [WinP]::ShowWindowAsync($appHwnd, 5) | Out-Null  # SW_SHOW
  [WinP]::SetForegroundWindow($appHwnd) | Out-Null
  Start-Sleep -Seconds 2
  Shot "probe-03-window-shown.png"

  # find edit control + likely lookup button among children
  $editHwnd = [IntPtr]::Zero
  $btnHwnd = [IntPtr]::Zero
  foreach ($k in [WinP]::KidHwnds($appHwnd)) {
    $kc = [WinP]::Cls($k)
    if ($editHwnd -eq [IntPtr]::Zero -and $kc -match 'edit') { $editHwnd = $k }
    if ($btnHwnd -eq [IntPtr]::Zero -and $kc -match 'button') { $btnHwnd = $k }
  }
  Log "edit child: 0x$($editHwnd.ToInt64().ToString('X'))  button child: 0x$($btnHwnd.ToInt64().ToString('X'))"

  # --- 6. drive lookup "cat" ---------------------------------------------
  if ($editHwnd -ne [IntPtr]::Zero) {
    $ok = [WinP]::SetText($editHwnd, 'cat')
    Log "WM_SETTEXT cat on edit: sent=$ok"
  } else {
    foreach ($ch in 'c','a','t') { [WinP]::CharMsg($appHwnd, $ch) | Out-Null }
    Log "posted WM_CHAR cat to main window (no edit child found)"
  }
  Start-Sleep -Milliseconds 500
  if ($btnHwnd -ne [IntPtr]::Zero) {
    $ok = [WinP]::Click($btnHwnd)
    Log "BM_CLICK on first button: sent=$ok"
  } else {
    [WinP]::Key($appHwnd, [WinP]::VK_RETURN) | Out-Null
    Log "posted Enter to main window"
  }
  Start-Sleep -Seconds 3
  Shot "probe-04-after-cat.png"

  # --- 7. clipboard probe (STA-guarded) ------------------------------------
  try {
    $clipText = $null
    $th = [System.Threading.Thread]::new({ $script:clipText = [System.Windows.Forms.Clipboard]::GetText() })
    $th.SetApartmentState([System.Threading.ApartmentState]::STA)
    $th.Start()
    if ($th.Join(5000)) {
      Log "clipboard length: $($script:clipText.Length)"
      $script:clipText | Out-File "$OutDir\clipboard.txt" -Encoding utf8
    } else { Log "clipboard read timed out (5s)" }
  } catch { Log "clipboard read failed: $_" }
}

# --- 8. OCR availability (no install this time) ----------------------------
try {
  Add-Type -AssemblyName System.Runtime.WindowsRuntime
  $null = [Windows.Media.Ocr.OcrEngine, Windows.Foundation, ContentType=WindowsRuntime]
  $langs = [Windows.Media.Ocr.OcrEngine]::AvailableRecognizerLanguages
  $langs | ForEach-Object { Log "OCR language: $($_.LanguageTag)" }
  $langs | Out-File "$OutDir\ocr-langs.txt"
} catch { Log "OCR API probe failed: $_" }

# --- 9. cleanup ------------------------------------------------------------
try { if (-not $proc.HasExited) { $proc.Kill() } } catch {}
Log "probe done"
