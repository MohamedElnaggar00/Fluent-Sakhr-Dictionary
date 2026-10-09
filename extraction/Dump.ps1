# Fluent Sakhr Dictionary - meaning dump.
# Drives the 1996 app (running from an ASCII path) through its own UI with
# timeout-guarded SendMessage calls: set the Edit text, click "Meaning", read the
# meaning ListBox items via LB_GETTEXTA (raw Win-1256 bytes) and decode them.
# Output: JSONL, one record per word: {word, match, meanings[]}.
param(
  [string]$WordsFile = "data\words.txt",
  [int]$Start = 0,
  [int]$Count = 20,
  [string]$OutName = "dump.jsonl"
)
$ErrorActionPreference = 'Continue'
$OutDir = "$env:RUNNER_TEMP\dump-results"
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$log = "$OutDir\dump.log"
function Log($m) {
  $line = "{0:HH:mm:ss.fff} {1}" -f (Get-Date), $m
  Add-Content -Path $log -Value $line -Encoding utf8
  Write-Host $line
}

Add-Type -AssemblyName System.Windows.Forms, System.Drawing

$cs = @'
using System;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;
public class WinD {
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] static extern bool EnumChildWindows(IntPtr p, EnumProc cb, IntPtr l);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern int GetDlgCtrlID(IntPtr h);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern bool ShowWindowAsync(IntPtr h, int c);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)]
  public static extern IntPtr SendMessageTimeout(IntPtr h, uint msg, IntPtr w, string l, uint flags, uint timeout, out IntPtr result);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)]
  public static extern IntPtr SendMessageTimeout(IntPtr h, uint msg, IntPtr w, IntPtr l, uint flags, uint timeout, out IntPtr result);
  [DllImport("user32.dll", EntryPoint="SendMessageTimeoutA")]
  public static extern IntPtr SendMessageTimeoutA(IntPtr h, uint msg, IntPtr w, byte[] l, uint flags, uint timeout, out IntPtr result);
  [DllImport("kernel32.dll")] public static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);
  [DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr h);
  [DllImport("kernel32.dll")] public static extern bool ReadProcessMemory(IntPtr proc, IntPtr addr, byte[] buf, IntPtr size, out IntPtr read);
  public static IntPtr OpenProc(int pid) { return OpenProcess(0x0410, false, (uint)pid); }
  public static IntPtr GetItemData(IntPtr lb, int i) {
    IntPtr r;
    SendMessageTimeout(lb, 0x0199, (IntPtr)i, IntPtr.Zero, SMTO_ABORTIFHUNG, 3000, out r); // LB_GETITEMDATA
    return r;
  }
  public static byte[] ReadMem(IntPtr hProc, IntPtr addr, int size) {
    if (hProc == IntPtr.Zero) return null;
    byte[] buf = new byte[size]; IntPtr read;
    if (!ReadProcessMemory(hProc, addr, buf, (IntPtr)size, out read) || read.ToInt64() <= 0) return null;
    if (read.ToInt64() < size) Array.Resize(ref buf, (int)read.ToInt64());
    return buf;
  }

  public const uint SMTO_ABORTIFHUNG = 0x0002;
  public const uint WM_SETTEXT = 0x000C;
  public const uint BM_CLICK = 0x00F5;
  public const uint LB_GETCURSEL = 0x0188;
  public const uint LB_GETTEXT = 0x0189;
  public const uint LB_GETTEXTLEN = 0x018A;
  public const uint LB_GETCOUNT = 0x018B;
  public const uint LB_RESETCONTENT = 0x0184;

  public static string Txt(IntPtr h) { var s = new StringBuilder(512); GetWindowText(h, s, 512); return s.ToString(); }
  public static string Cls(IntPtr h) { var s = new StringBuilder(256); GetClassName(h, s, 256); return s.ToString(); }

  public static List<IntPtr> TopHwnds() {
    var r = new List<IntPtr>();
    EnumWindows((h, l) => { r.Add(h); return true; }, IntPtr.Zero);
    return r;
  }
  public static List<IntPtr> KidHwnds(IntPtr p) {
    var r = new List<IntPtr>();
    EnumChildWindows(p, (h, l) => { r.Add(h); return true; }, IntPtr.Zero);
    return r;
  }
  public static bool Visible(IntPtr h) { return IsWindowVisible(h); }
  public static uint Pid(IntPtr h) { uint pid; GetWindowThreadProcessId(h, out pid); return pid; }

  // returns false on timeout (target hung)
  public static bool Send(IntPtr h, uint msg, IntPtr w, IntPtr l, out IntPtr res) {
    return SendMessageTimeout(h, msg, w, l, SMTO_ABORTIFHUNG, 3000, out res) != IntPtr.Zero;
  }
  public static bool SetText(IntPtr h, string t) {
    IntPtr res;
    return SendMessageTimeout(h, WM_SETTEXT, IntPtr.Zero, t, SMTO_ABORTIFHUNG, 3000, out res) != IntPtr.Zero;
  }
  public static bool Click(IntPtr h) {
    IntPtr res;
    return Send(h, BM_CLICK, IntPtr.Zero, IntPtr.Zero, out res);
  }
  public static int LbCount(IntPtr h, out bool ok) {
    IntPtr res;
    ok = Send(h, LB_GETCOUNT, IntPtr.Zero, IntPtr.Zero, out res);
    return ok ? res.ToInt32() : -1;
  }
  public static int LbCurSel(IntPtr h, out bool ok) {
    IntPtr res;
    ok = Send(h, LB_GETCURSEL, IntPtr.Zero, IntPtr.Zero, out res);
    return ok ? res.ToInt32() : -1;
  }
  // raw Win-1256 bytes of listbox item i; null on timeout/error
  public static byte[] LbTextA(IntPtr h, int i) {
    IntPtr res;
    if (SendMessageTimeout(h, LB_GETTEXTLEN, (IntPtr)i, IntPtr.Zero, SMTO_ABORTIFHUNG, 3000, out res) == IntPtr.Zero) return null;
    int len = res.ToInt32();
    if (len < 0 || len > 16384) return new byte[0];
    var buf = new byte[len + 2];
    if (SendMessageTimeoutA(h, LB_GETTEXT, (IntPtr)i, buf, SMTO_ABORTIFHUNG, 3000, out res) == IntPtr.Zero) return null;
    int got = res.ToInt32();
    if (got < 0) got = 0; if (got > len) got = len;
    var outb = new byte[got];
    Array.Copy(buf, outb, got);
    return outb;
  }
}
'@
Add-Type -TypeDefinition $cs -ReferencedAssemblies System.Windows.Forms
if (-not ([System.Management.Automation.PSTypeName]'WinD').Type) { Write-Host "FATAL: WinD compile failed"; exit 1 }

$cp1256 = [Text.Encoding]::GetEncoding(1256)
function Decode1256([byte[]]$b) { if ($null -eq $b) { return $null }; return $cp1256.GetString($b) }
# meaning records: CP1256 bytes from offset 0, terminated by 0xFE (or NUL)
function Extract-Cp1256([byte[]]$mem) {
  $len = 0
  while ($len -lt $mem.Length -and $mem[$len] -ne 0xFE -and $mem[$len] -ne 0) { $len++ }
  if ($len -eq 0) { return '' }
  return $cp1256.GetString($mem, 0, $len).Trim()
}

# --- unpack + ASCII copy ----------------------------------------------------
$sevenZip = "C:\Program Files\7-Zip\7z.exe"
& $sevenZip x "data\legacy\sakhr.7z" "-oC:\sakhr" -y | Out-File "$OutDir\7z.log"
$srcExe = Get-ChildItem -Path C:\sakhr -Recurse -Filter *.exe |
          Sort-Object Length -Descending | Select-Object -First 1
$ascii = "C:\sakhrapp"
New-Item -ItemType Directory -Force -Path $ascii | Out-Null
$n = 0
Get-ChildItem -Path $srcExe.DirectoryName -File | ForEach-Object {
  $name = $_.Name
  if ($name -match '[^\x00-\x7F]') {
    $ext = [IO.Path]::GetExtension($name)
    if ($name -eq $srcExe.Name) { $name = "sakhr$ext" } else { $n++; $name = "file$n$ext" }
  }
  Copy-Item $_.FullName (Join-Path $ascii $name)
}
$exePath = Join-Path $ascii "sakhr.exe"
Log "ascii app ready: $exePath"

# --- launch helpers ---------------------------------------------------------
function Find-UI {
  $dlg = [IntPtr]::Zero
  foreach ($h in [WinD]::TopHwnds()) {
    if ([WinD]::Cls($h) -eq '#32770' -and [WinD]::Txt($h) -match 'The Dictionary') { $dlg = $h; break }
  }
  if ($dlg -eq [IntPtr]::Zero) { return $null }
  $edit = [IntPtr]::Zero; $btn = [IntPtr]::Zero; $lbWord = [IntPtr]::Zero; $lbMean = [IntPtr]::Zero
  foreach ($k in [WinD]::KidHwnds($dlg)) {
    $id = [WinD]::GetDlgCtrlID($k); $c = [WinD]::Cls($k)
    if ($c -eq 'Edit' -and $id -eq 1001) { $edit = $k }
    elseif ($c -eq 'Button' -and $id -eq 1) { $btn = $k }
    elseif ($c -eq 'ListBox' -and $id -eq 1022) { $lbWord = $k }
    elseif ($c -eq 'ListBox' -and $id -eq 1021) { $lbMean = $k }
  }
  return @{ dlg=$dlg; edit=$edit; btn=$btn; lbWord=$lbWord; lbMean=$lbMean }
}

function Dismiss-Modals($appPid) {
  foreach ($h in [WinD]::TopHwnds()) {
    if ([WinD]::Cls($h) -eq '#32770' -and [WinD]::Visible($h) -and [WinD]::Pid($h) -eq $appPid -and [WinD]::Txt($h) -notmatch 'The Dictionary') {
      Log "dismissing modal: title=[$([WinD]::Txt($h))]"
      foreach ($k in [WinD]::KidHwnds($h)) {
        if ([WinD]::Cls($k) -eq 'Button') { [WinD]::Click($k) | Out-Null; break }
      }
    }
  }
}

$script:proc = $null
function Launch-App {
  if ($script:proc -and -not $script:proc.HasExited) { try { $script:proc.Kill() } catch {} ; Start-Sleep -Seconds 1 }
  $script:proc = Start-Process -FilePath $exePath -WorkingDirectory $ascii -PassThru
  Log "launched pid=$($script:proc.Id)"
  $ui = $null
  for ($i = 0; $i -lt 90; $i++) {
    Start-Sleep -Milliseconds 500
    if ($script:proc.HasExited) { Log "process exited early, code=$($script:proc.ExitCode)"; return $null }
    $ui = Find-UI
    if ($ui -and $ui.edit -ne [IntPtr]::Zero -and $ui.lbMean -ne [IntPtr]::Zero) { return $ui }
    if ($i % 10 -eq 9) {
      $seen = @()
      foreach ($h in [WinD]::TopHwnds()) {
        if ([WinD]::Pid($h) -eq $script:proc.Id) { $seen += ("0x{0:X} [{1}] [{2}]" -f $h.ToInt64(), [WinD]::Cls($h), [WinD]::Txt($h)) }
      }
      Log "wait $i`: app windows so far: $($seen -join ' | ')"
    }
  }
  return $null
}

$script:hProc = [IntPtr]::Zero
function Open-ProcHandle {
  if ($script:hProc -ne [IntPtr]::Zero) { try { [WinD]::CloseHandle($script:hProc) } catch {} }
  $script:hProc = [WinD]::OpenProc($script:proc.Id)
  Log "proc handle: 0x$($script:hProc.ToInt64().ToString('X')) pid=$($script:proc.Id)"
}

# --- main loop ---------------------------------------------------------------
$ui = Launch-App
if (-not $ui -or $ui.edit -eq [IntPtr]::Zero) { Log "FATAL: app UI not found"; exit 1 }
Log "UI ready: dlg=0x$($ui.dlg.ToInt64().ToString('X')) edit=0x$($ui.edit.ToInt64().ToString('X')) btn=0x$($ui.btn.ToInt64().ToString('X')) lbWord=0x$($ui.lbWord.ToInt64().ToString('X')) lbMean=0x$($ui.lbMean.ToInt64().ToString('X'))"

# v6-proven activation sequence: the app only processes lookups once its
# dialog has been shown and brought to the foreground
[WinD]::ShowWindowAsync($ui.dlg, 9) | Out-Null  # SW_RESTORE
[WinD]::ShowWindowAsync($ui.dlg, 5) | Out-Null  # SW_SHOW
[WinD]::SetForegroundWindow($ui.dlg) | Out-Null
Start-Sleep -Seconds 12
Log "dialog shown + foregrounded, engine settle wait done"
Open-ProcHandle

# wait for the dictionary engine: trigger ONE lookup, then wait quietly.
# (spamming SetText/Click every 500ms appears to keep the engine from ever
# finishing its lazy data load)
$ready = $false
for ($attempt = 0; $attempt -lt 4 -and -not $ready; $attempt++) {
  $okS = [WinD]::SetText($ui.edit, 'cat')
  Start-Sleep -Milliseconds 300
  $okC = [WinD]::Click($ui.btn)
  Log "readiness attempt $attempt`: set=$okS click=$okC - waiting quietly"
  for ($i = 0; $i -lt 45; $i++) {
    Start-Sleep -Seconds 1
    $c = [WinD]::LbCount($ui.lbMean, [ref]$okR)
    if ($okR -and $c -gt 0) { $ready = $true; Log "engine ready (cat -> $c meanings)"; break }
    if ($i % 15 -eq 14) { Log "still waiting for engine... (${i}s)" }
  }
}
if (-not $ready) { Log "FATAL: engine never returned meanings for CAT"; exit 1 }

$words = Get-Content $WordsFile -Encoding utf8 | Select-Object -Skip $Start -First $Count
Log "dumping $($words.Count) words from $WordsFile (skip $Start)"

$outPath = Join-Path $OutDir $OutName
$sw = New-Object System.IO.StreamWriter($outPath, $false, (New-Object System.Text.UTF8Encoding($false)))

$done = 0; $failStreak = 0
foreach ($w in $words) {
  $word = $w.Trim()
  if ($word -eq '') { continue }

  $okSet = [WinD]::SetText($ui.edit, $word.ToLower())
  $okClick = $false
  if ($okSet) { $okClick = [WinD]::Click($ui.btn) }

  if (-not ($okSet -and $okClick)) {
    $failStreak++
    Log "send failure on '$word' (streak $failStreak); set=$okSet click=$okClick"
    if ($failStreak -ge 3) {
      Dismiss-Modals $script:proc.Id
      $ui = Launch-App
      if (-not $ui) { Log "FATAL: relaunch failed"; break }
      Open-ProcHandle
      $failStreak = 0
    }
    continue
  }
  $failStreak = 0

  # wait for the meaning list to settle (count stable), max ~1.5s
  $lastCount = -2; $stable = 0
  for ($t = 0; $t -lt 30; $t++) {
    Start-Sleep -Milliseconds 50
    $c = [WinD]::LbCount($ui.lbMean, [ref]$okC)
    if (-not $okC) { break }
    if ($c -eq $lastCount) { $stable++; if ($stable -ge 2) { break } } else { $stable = 0; $lastCount = $c }
  }

  # matched lemma from the word listbox current selection (owner-drawn: item data is a record pointer)
  $match = ''
  $sel = [WinD]::LbCurSel($ui.lbWord, [ref]$okSel)
  if ($okSel -and $sel -ge 0) {
    $wptr = [WinD]::GetItemData($ui.lbWord, $sel)
    if ($wptr.ToInt64() -gt 0) {
      $wmem = [WinD]::ReadMem($script:hProc, $wptr, 128)
      if ($null -ne $wmem) { $match = Extract-Cp1256 $wmem }
    }
  }

  $meanings = @()
  $mc = [WinD]::LbCount($ui.lbMean, [ref]$okMC)
  if ($okMC -and $mc -gt 0) {
    for ($i = 0; $i -lt $mc; $i++) {
      $ptr = [WinD]::GetItemData($ui.lbMean, $i)
      if ($ptr.ToInt64() -le 0) { continue }
      $mem = [WinD]::ReadMem($script:hProc, $ptr, 512)
      if ($null -eq $mem) { $meanings += $null } else { $meanings += (Extract-Cp1256 $mem) }
    }
  }

  $rec = [ordered]@{ word = $word; match = $match; meanings = $meanings }
  $sw.WriteLine(($rec | ConvertTo-Json -Compress))
  $done++
  if ($done % 100 -eq 0) { $sw.Flush(); Log "progress: $done/$($words.Count)" }
}
$sw.Flush(); $sw.Close()
Log "dump done: $done records -> $outPath"

try { if ($script:hProc -ne [IntPtr]::Zero) { [WinD]::CloseHandle($script:hProc) } } catch {}
try { if (-not $script:proc.HasExited) { $script:proc.Kill() } } catch {}
Log "all done"
