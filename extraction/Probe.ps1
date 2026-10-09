# Fluent Sakhr - extraction probe.
# Answers, on a GitHub Windows runner:
#   1. Does the 1996 32-bit app launch and show its window?
#   2. What are its window/child-control classes (automation surface)?
#   3. Can we drive a lookup ("cat") with keystrokes? What does it show?
#   4. Does the results pane put text on the clipboard (Ctrl+A/Ctrl+C)?
#   5. Is Windows OCR available, and can the Arabic OCR pack be installed?
# Everything is best-effort; findings go to $OutDir and the workflow artifact.
$ErrorActionPreference = 'Continue'
$OutDir = "$env:RUNNER_TEMP\probe-results"
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$log = "$OutDir\probe.log"
function Log($m) { $m | Tee-Object -FilePath $log -Append }

Add-Type -AssemblyName System.Windows.Forms, System.Drawing

# --- 1. unpack -------------------------------------------------------------
$sevenZip = "C:\Program Files\7-Zip\7z.exe"
$dest = "C:\sakhr"
& $sevenZip x "data\legacy\sakhr.7z" "-o$dest" -y | Out-File "$OutDir\7z.log"
$exe = Get-ChildItem -Path $dest -Recurse -Filter *.exe |
       Sort-Object Length -Descending | Select-Object -First 1
Log "main exe candidate: $($exe.FullName) ($($exe.Length) bytes)"
Get-ChildItem -Path $dest -Recurse | Select-Object FullName, Length |
  Out-File "$OutDir\files.txt"

# --- 2. launch -------------------------------------------------------------
$proc = Start-Process -FilePath $exe.FullName -WorkingDirectory $exe.DirectoryName -PassThru
Start-Sleep -Seconds 6
Log "process started: id=$($proc.Id) hasExited=$($proc.HasExited)"
Get-Process | Where-Object { $_.Id -eq $proc.Id } |
  Select-Object Id, ProcessName, MainWindowTitle, MainWindowHandle, Responding |
  Format-List | Out-File "$OutDir\process.txt"

function Shot($name) {
  $vs = [System.Windows.Forms.SystemInformation]::VirtualScreen
  $bmp = New-Object System.Drawing.Bitmap $vs.Width, $vs.Height
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.CopyFromScreen($vs.Left, $vs.Top, 0, 0, $bmp.Size)
  $bmp.Save("$OutDir\$name", [System.Drawing.Imaging.ImageFormat]::Png)
  $g.Dispose(); $bmp.Dispose()
  Log "screenshot saved: $name"
}
Shot "probe-01-launched.png"

# --- 3. window inventory ---------------------------------------------------
$cs = @'
using System;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;
public class WinProbe {
  [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] static extern bool EnumChildWindows(IntPtr p, EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] static extern int GetClassName(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int c);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  public static List<string> Top() {
    var r = new List<string>();
    EnumWindows((h, l) => {
      var t = new StringBuilder(256); GetWindowText(h, t, 256);
      var c = new StringBuilder(256); GetClassName(h, c, 256);
      uint pid; GetWindowThreadProcessId(h, out pid);
      if (IsWindowVisible(h))
        r.Add(string.Format("0x{0:X} pid={1} class=[{2}] title=[{3}]", h.ToInt64(), pid, c, t));
      return true;
    }, IntPtr.Zero);
    return r;
  }
  public static List<string> Kids(IntPtr p) {
    var r = new List<string>();
    EnumChildWindows(p, (h, l) => {
      var t = new StringBuilder(256); GetWindowText(h, t, 256);
      var c = new StringBuilder(256); GetClassName(h, c, 256);
      r.Add(string.Format("  0x{0:X} class=[{1}] text=[{2}]", h.ToInt64(), c, t));
      return true;
    }, IntPtr.Zero);
    return r;
  }
}
'@
Add-Type -TypeDefinition $cs -ReferencedAssemblies System.Windows.Forms
$tops = [WinProbe]::Top()
$tops | Out-File "$OutDir\windows.txt"
Log "visible top-level windows: $($tops.Count)"
$appHwnd = $null
foreach ($line in $tops) {
  if ($line -match 'DictWClass|32770' -and $line -match "pid=$($proc.Id)") { $appHwnd = $line }
  if ($line -match "pid=$($proc.Id)") { $appHwnd = $line }
}
Log "app window line: $appHwnd"
if ($appHwnd -match '0x([0-9A-F]+)') {
  $hwnd = [IntPtr]([Convert]::ToInt64($Matches[1], 16))
  [WinProbe]::Kids($hwnd) | Out-File "$OutDir\children.txt"
  Log "child windows dumped"
  [WinProbe]::ShowWindow($hwnd, 9) | Out-Null   # SW_RESTORE
  [WinProbe]::SetForegroundWindow($hwnd) | Out-Null
  Start-Sleep -Milliseconds 800

  # --- 4. drive a lookup -------------------------------------------------
  [System.Windows.Forms.SendKeys]::SendWait("cat")
  Start-Sleep -Milliseconds 500
  [System.Windows.Forms.SendKeys]::SendWait("{ENTER}")
  Start-Sleep -Seconds 2
  Shot "probe-02-after-cat.png"

  # --- 5. clipboard probe ------------------------------------------------
  [System.Windows.Forms.SendKeys]::SendWait("^a")
  Start-Sleep -Milliseconds 300
  [System.Windows.Forms.SendKeys]::SendWait("^c")
  Start-Sleep -Milliseconds 800
  try {
    $clip = [System.Windows.Forms.Clipboard]::GetText()
    Log "clipboard length: $($clip.Length)"
    $clip | Out-File "$OutDir\clipboard.txt" -Encoding utf8
  } catch { Log "clipboard read failed: $_" }
}

# --- 6. Windows OCR --------------------------------------------------------
try {
  Add-Type -AssemblyName System.Runtime.WindowsRuntime
  $null = [Windows.Media.Ocr.OcrEngine, Windows.Foundation, ContentType=WindowsRuntime]
  $langs = [Windows.Media.Ocr.OcrEngine]::AvailableRecognizerLanguages
  $langs | ForEach-Object { Log "OCR language: $($_.LanguageTag) ($($_.DisplayName))" }
  $langs | Out-File "$OutDir\ocr-langs.txt"
  Log "OCR MaxImageDimension: $([Windows.Media.Ocr.OcrEngine]::MaxImageDimension)"
} catch { Log "OCR API probe failed: $_" }

try {
  $cap = Add-WindowsCapability -Online -Name "Language.OCR~~~ar-SA~0.0.1.0" -ErrorAction Stop
  Log "Arabic OCR install: $($cap.State)"
} catch { Log "Arabic OCR install failed: $_" }
try {
  $langs2 = [Windows.Media.Ocr.OcrEngine]::AvailableRecognizerLanguages
  $langs2 | ForEach-Object { Log "post-install OCR language: $($_.LanguageTag)" }
  $langs2 | Out-File "$OutDir\ocr-langs-after.txt"
} catch { Log "post-install OCR probe failed: $_" }

# --- 7. cleanup ------------------------------------------------------------
try { $proc.CloseMainWindow() | Out-Null; Start-Sleep -Seconds 2 } catch {}
try { if (-not $proc.HasExited) { $proc.Kill() } } catch {}
Log "probe done"
