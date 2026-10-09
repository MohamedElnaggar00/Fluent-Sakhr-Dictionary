# Fluent Sakhr Dictionary - probe v7: WHY does the meaning listbox read empty?
# Hypothesis: the lookup WORKS (v6 screenshot showed 7 meanings for cat) but the
# meaning ListBox is LBS_NODATA/owner-drawn, so LB_GETCOUNT/LB_GETTEXT return nothing.
# This probe: click 'cat', then (1) screenshot for visual truth, (2) LB counts/texts,
# (3) UIA/MSAA tree dump of the dialog, (4) scan app memory for known Win-1256 bytes.
$ErrorActionPreference = 'Continue'
$OutDir = "$env:RUNNER_TEMP\probe-results"
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$log = "$OutDir\probe.log"
function Log($m) {
  $line = "{0:HH:mm:ss.fff} {1}" -f (Get-Date), $m
  Add-Content -Path $log -Value $line -Encoding utf8
  Write-Host $line
}
Add-Type -AssemblyName System.Windows.Forms, System.Drawing, UIAutomationClient, UIAutomationTypes

$cs = @'
using System;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;
public class WinQ {
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] static extern bool EnumChildWindows(IntPtr p, EnumProc cb, IntPtr l);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern int GetDlgCtrlID(IntPtr h);
  [DllImport("user32.dll")] public static extern bool ShowWindowAsync(IntPtr h, int c);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)]
  public static extern IntPtr SendMessageTimeout(IntPtr h, uint msg, IntPtr w, string l, uint flags, uint timeout, out IntPtr result);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)]
  public static extern IntPtr SendMessageTimeout(IntPtr h, uint msg, IntPtr w, IntPtr l, uint flags, uint timeout, out IntPtr result);
  [DllImport("user32.dll", EntryPoint="SendMessageTimeoutA")]
  public static extern IntPtr SendMessageTimeoutA(IntPtr h, uint msg, IntPtr w, byte[] l, uint flags, uint timeout, out IntPtr result);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)]
  public static extern IntPtr SendMessageTimeout(IntPtr h, uint msg, IntPtr w, StringBuilder l, uint flags, uint timeout, out IntPtr result);
  [DllImport("kernel32.dll")] public static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);
  [DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr h);
  [DllImport("kernel32.dll")] public static extern int VirtualQueryEx(IntPtr proc, IntPtr addr, out MEMORY_BASIC_INFORMATION mbi, uint len);
  [DllImport("kernel32.dll")] public static extern bool ReadProcessMemory(IntPtr proc, IntPtr addr, byte[] buf, IntPtr size, out IntPtr read);
  public static IntPtr GetItemData(IntPtr lb, int i) {
    IntPtr r;
    SendMessageTimeout(lb, 0x0199, (IntPtr)i, IntPtr.Zero, 2, 2000, out r); // LB_GETITEMDATA
    return r;
  }
  public static byte[] ReadMem(int pid, IntPtr addr, int size) {
    IntPtr proc = OpenProcess(0x0410, false, pid);
    if (proc == IntPtr.Zero) return null;
    try {
      byte[] buf = new byte[size]; IntPtr read;
      if (!ReadProcessMemory(proc, addr, buf, (IntPtr)size, out read) || read.ToInt64() <= 0) return null;
      if (read.ToInt64() < size) Array.Resize(ref buf, (int)read.ToInt64());
      return buf;
    } finally { CloseHandle(proc); }
  }

  [StructLayout(LayoutKind.Sequential)]
  public struct MEMORY_BASIC_INFORMATION {
    public IntPtr BaseAddress; public IntPtr AllocationBase; public uint AllocationProtect;
    public IntPtr RegionSize; public uint State; public uint Protect; public uint Type;
  }

  public const uint SMTO_ABORTIFHUNG = 0x0002;
  public const uint WM_SETTEXT = 0x000C;
  public const uint BM_CLICK = 0x00F5;
  public const uint LB_GETCOUNT = 0x018B;
  public const uint LB_GETTEXT = 0x0189;
  public const uint LB_GETTEXTLEN = 0x018A;
  public const uint MEM_COMMIT = 0x1000;
  public const uint PAGE_NOACCESS = 0x01;
  public const uint PAGE_GUARD = 0x100;

  public static string Txt(IntPtr h) { var s = new StringBuilder(512); GetWindowText(h, s, 512); return s.ToString(); }
  public static string Cls(IntPtr h) { var s = new StringBuilder(256); GetClassName(h, s, 256); return s.ToString(); }
  public static List<IntPtr> TopHwnds() { var r = new List<IntPtr>(); EnumWindows((h,l)=>{r.Add(h);return true;}, IntPtr.Zero); return r; }
  public static List<IntPtr> KidHwnds(IntPtr p) { var r = new List<IntPtr>(); EnumChildWindows(p, (h,l)=>{r.Add(h);return true;}, IntPtr.Zero); return r; }
  [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  public static uint Pid(IntPtr h) { uint pid; GetWindowThreadProcessId(h, out pid); return pid; }
  public static bool SetText(IntPtr h, string t) { IntPtr res; return SendMessageTimeout(h, WM_SETTEXT, IntPtr.Zero, t, SMTO_ABORTIFHUNG, 3000, out res) != IntPtr.Zero; }
  public static bool Click(IntPtr h) { IntPtr res; return SendMessageTimeout(h, BM_CLICK, IntPtr.Zero, IntPtr.Zero, SMTO_ABORTIFHUNG, 3000, out res) != IntPtr.Zero; }
  public static int LbCount(IntPtr h) { IntPtr res; if (SendMessageTimeout(h, LB_GETCOUNT, IntPtr.Zero, IntPtr.Zero, SMTO_ABORTIFHUNG, 3000, out res) == IntPtr.Zero) return -2; return res.ToInt32(); }
  public static int LbTextW(IntPtr h, int i, StringBuilder sb) { IntPtr res; if (SendMessageTimeout(h, LB_GETTEXT, (IntPtr)i, sb, SMTO_ABORTIFHUNG, 3000, out res) == IntPtr.Zero) return -2; return res.ToInt32(); }
  public static byte[] LbTextA(IntPtr h, int i) {
    IntPtr res;
    if (SendMessageTimeout(h, LB_GETTEXTLEN, (IntPtr)i, IntPtr.Zero, SMTO_ABORTIFHUNG, 3000, out res) == IntPtr.Zero) return null;
    int len = res.ToInt32(); if (len < 0 || len > 16384) return new byte[0];
    var buf = new byte[len + 2];
    if (SendMessageTimeoutA(h, LB_GETTEXT, (IntPtr)i, buf, SMTO_ABORTIFHUNG, 3000, out res) == IntPtr.Zero) return null;
    int got = res.ToInt32(); if (got < 0) got = 0; if (got > len) got = len;
    var ob = new byte[got]; Array.Copy(buf, ob, got); return ob;
  }

  public static List<long> FindBytes(uint pid, byte[] needle) {
    var hits = new List<long>();
    IntPtr proc = OpenProcess(0x0410, false, pid); // QUERY_INFORMATION|VM_READ
    if (proc == IntPtr.Zero) return hits;
    long addr = 0;
    while (addr < 0x7FFFFFFF) {
      var mbi = new MEMORY_BASIC_INFORMATION();
      int q = VirtualQueryEx(proc, (IntPtr)addr, out mbi, (uint)Marshal.SizeOf(typeof(MEMORY_BASIC_INFORMATION)));
      if (q == 0) break;
      if (mbi.State == MEM_COMMIT && (mbi.Protect & (PAGE_NOACCESS | PAGE_GUARD)) == 0 && mbi.RegionSize.ToInt64() < 64*1024*1024) {
        var buf = new byte[mbi.RegionSize.ToInt64()];
        IntPtr read;
        if (ReadProcessMemory(proc, mbi.BaseAddress, buf, (IntPtr)buf.Length, out read) && read.ToInt64() > 0) {
          int n = (int)read.ToInt64();
          for (int i = 0; i + needle.Length <= n; i++) {
            bool m = true;
            for (int j = 0; j < needle.Length; j++) if (buf[i+j] != needle[j]) { m = false; break; }
            if (m) { hits.Add(mbi.BaseAddress.ToInt64() + i); i += needle.Length - 1; if (hits.Count > 100) break; }
          }
        }
      }
      addr = mbi.BaseAddress.ToInt64() + mbi.RegionSize.ToInt64();
      if (hits.Count > 100) break;
    }
    CloseHandle(proc);
    return hits;
  }
}
'@
Add-Type -TypeDefinition $cs -ReferencedAssemblies System.Windows.Forms
if (-not ([System.Management.Automation.PSTypeName]'WinQ').Type) { Log "FATAL: WinQ compile failed"; exit 1 }

function Shot($name) {
  try {
    $vs = [System.Windows.Forms.SystemInformation]::VirtualScreen
    $bmp = New-Object System.Drawing.Bitmap $vs.Width, $vs.Height
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($vs.Left, $vs.Top, 0, 0, $bmp.Size)
    $bmp.Save("$OutDir\$name", [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose()
    Log "screenshot: $name"
  } catch { Log "screenshot failed: $_" }
}

& "C:\Program Files\7-Zip\7z.exe" x "data\legacy\sakhr.7z" "-oC:\sakhr" -y | Out-File "$OutDir\7z.log"
$srcExe = Get-ChildItem -Path C:\sakhr -Recurse -Filter *.exe | Sort-Object Length -Descending | Select-Object -First 1
$ascii = "C:\sakhrapp"; New-Item -ItemType Directory -Force -Path $ascii | Out-Null
$n = 0
Get-ChildItem -Path $srcExe.DirectoryName -File | ForEach-Object {
  $name = $_.Name
  if ($name -match '[^\x00-\x7F]') { $ext = [IO.Path]::GetExtension($name); if ($name -eq $srcExe.Name) { $name = "sakhr$ext" } else { $n++; $name = "file$n$ext" } }
  Copy-Item $_.FullName (Join-Path $ascii $name)
}
$proc = Start-Process -FilePath (Join-Path $ascii "sakhr.exe") -WorkingDirectory $ascii -PassThru
Log "launched pid=$($proc.Id)"
$dlg = [IntPtr]::Zero
for ($i = 0; $i -lt 90; $i++) {
  Start-Sleep -Milliseconds 500
  if ($proc.HasExited) { Log "process exited, code=$($proc.ExitCode)"; break }
  foreach ($h in [WinQ]::TopHwnds()) { if ([WinQ]::Cls($h) -eq '#32770' -and [WinQ]::Txt($h) -match 'The Dictionary') { $dlg = $h; break } }
  if ($dlg -ne [IntPtr]::Zero) { break }
}
if ($dlg -eq [IntPtr]::Zero) {
  Log "FATAL: no dialog after 45s; top windows:"
  foreach ($h in [WinQ]::TopHwnds()) { Log "  0x$($h.ToInt64().ToString('X')) pid=$([WinQ]::Pid($h)) cls=[$([WinQ]::Cls($h))] txt=[$([WinQ]::Txt($h))]" }
  Shot "probe-no-dialog.png"
  exit 1
}
Log "dialog found after ~$([math]::Round(($i+1)*0.5,1))s"
$edit=[IntPtr]::Zero; $btn=[IntPtr]::Zero; $lbWord=[IntPtr]::Zero; $lbMean=[IntPtr]::Zero
foreach ($k in [WinQ]::KidHwnds($dlg)) {
  $id = [WinQ]::GetDlgCtrlID($k); $c = [WinQ]::Cls($k)
  if ($c -eq 'Edit' -and $id -eq 1001) { $edit = $k }
  elseif ($c -eq 'Button' -and $id -eq 1) { $btn = $k }
  elseif ($c -eq 'ListBox' -and $id -eq 1022) { $lbWord = $k }
  elseif ($c -eq 'ListBox' -and $id -eq 1021) { $lbMean = $k }
}
Log "dlg=0x$($dlg.ToInt64().ToString('X')) edit=0x$($edit.ToInt64().ToString('X')) btn=0x$($btn.ToInt64().ToString('X')) lbWord=0x$($lbWord.ToInt64().ToString('X')) lbMean=0x$($lbMean.ToInt64().ToString('X'))"

[WinQ]::ShowWindowAsync($dlg, 9) | Out-Null
[WinQ]::ShowWindowAsync($dlg, 5) | Out-Null
[WinQ]::SetForegroundWindow($dlg) | Out-Null
Start-Sleep -Seconds 2

$okS = [WinQ]::SetText($edit, 'cat')
Start-Sleep -Milliseconds 500
$okC = [WinQ]::Click($btn)
Log "lookup: set=$okS click=$okC"
Start-Sleep -Seconds 3
Shot "probe-cat-lookup.png"

Log "lbWord count: $([WinQ]::LbCount($lbWord))  lbMean count: $([WinQ]::LbCount($lbMean))"
$cp1256 = [Text.Encoding]::GetEncoding(1256)
for ($i = 0; $i -lt 10; $i++) {
  $idata = [WinQ]::GetItemData($lbMean, $i)
  if ($idata -eq [IntPtr]::Zero) { Log "lbMean[$i]: itemdata=0"; continue }
  $mem = [WinQ]::ReadMem($proc.Id, $idata, 512)
  if ($null -eq $mem) { Log "lbMean[$i]: itemdata=0x$($idata.ToInt64().ToString('X')) READ FAILED"; continue }
  $hex = ($mem[0..47] | ForEach-Object { $_.ToString('X2') }) -join ' '
  # decode cp1256 string starting at offset 8 until NUL
  $bytes = New-Object System.Collections.Generic.List[byte]
  for ($o = 8; $o -lt $mem.Length; $o++) { if ($mem[$o] -eq 0) { break }; $bytes.Add($mem[$o]) }
  $txt = $cp1256.GetString($bytes.ToArray())
  Log "lbMean[$i]: itemdata=0x$($idata.ToInt64().ToString('X')) hex=[$hex] decoded=[$txt]"
}
$sb2 = New-Object System.Text.StringBuilder 4096
$w2 = [WinQ]::LbTextW($lbWord, 0, $sb2)
Log "lbWord[0]: gettextW=$w2 textW=[$sb2]"

try {
  $root = [System.Windows.Automation.AutomationElement]::FromHandle($dlg)
  $walker = [System.Windows.Automation.TreeWalker]::RawViewWalker
  function DumpUIA($el, $depth) {
    if ($depth -gt 6 -or $null -eq $el) { return }
    $ct = $el.Current.ControlType.ProgrammaticName
    $nm = $el.Current.Name
    Log ("  " * $depth + "UIA: [$ct] name=[$nm] class=[$($el.Current.ClassName)]")
    $child = $walker.GetFirstChild($el)
    while ($null -ne $child) {
      DumpUIA $child ($depth + 1)
      $child = $walker.GetNextSibling($child)
    }
  }
  DumpUIA $root 0
} catch { Log "UIA failed: $_" }

# memory scan for known Win-1256 byte sequences of cat's meanings
$needles = @{
  'raf3'     = [byte[]](0xD1,0xDD,0xDA,0x20,0xC7,0xE1,0xE3,0xD3,0xC7,0xC9)
  'khabitha' = [byte[]](0xCE,0xC8,0xED,0xCB,0xC9)
}
foreach ($k in $needles.Keys) {
  $hits = [WinQ]::FindBytes($proc.Id, $needles[$k])
  Log "memory scan '$k': $($hits.Count) hits$(if ($hits.Count -gt 0) { ' at ' + (($hits | Select-Object -First 8 | ForEach-Object { '0x' + $_.ToString('X') }) -join ', ') })"
}

try { if (-not $proc.HasExited) { $proc.Kill() } } catch {}
Log "probe done"
