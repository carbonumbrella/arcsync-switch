; Installer for ArcSyncSwitch (Inno Setup 6). Built by ..\build.cmd.
; Per-user install (no admin required). Checks and downloads .NET 10 Desktop Runtime only if missing.

#define AppName "ArcSyncSwitch"
#define AppExe "ArcSyncSwitch.exe"
#define AppVersion GetVersionNumbersString("..\publish\ArcSyncSwitch.exe")
#define RuntimeUrl "https://aka.ms/dotnet/10.0/windowsdesktop-runtime-win-x64.exe"

[Setup]
AppId={{8F4C2A19-7B3E-4D12-9A5E-3C6F8E2D4B01}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=carbonumbrella
AppComments={cm:AppComments}
DefaultDirName={localappdata}\Programs\ArcSyncSwitch
DisableProgramGroupPage=yes
DisableDirPage=auto
PrivilegesRequired=lowest
OutputDir=..\publish\installer
OutputBaseFilename=ArcSyncSwitch-Setup-{#AppVersion}
SetupIconFile=..\src\app.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
Compression=lzma2/ultra
SolidCompression=yes
WizardStyle=modern
WizardSizePercent=100
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=no

[Languages]
Name: "ru"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "en"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
ru.AppComments=Переключатель профилей Intel Arc Sync (VRR) и флагов LFC для MSI Claw
ru.TaskAutostart=Применять режим VRR и LFC при входе в Windows
ru.TaskGroupExtra=Дополнительно:
ru.TaskDesktop=Ярлык на рабочем столе
ru.RunOpen=Открыть ArcSyncSwitch
ru.DlTitle=Загрузка .NET 10
ru.DlDesc=Для работы нужна среда .NET 10 Desktop Runtime от Microsoft.
ru.DlFail=Не удалось скачать .NET 10:
ru.DlManual=Установите .NET 10 Desktop Runtime вручную с dotnet.microsoft.com.
ru.InstallFail=.NET 10 не установился (код
en.AppComments=Intel Arc Sync (VRR) and LFC profile switcher for MSI Claw
en.TaskAutostart=Apply VRR and LFC profile at Windows logon
en.TaskGroupExtra=Additional options:
en.TaskDesktop=Create desktop shortcut
en.RunOpen=Launch ArcSyncSwitch
en.DlTitle=Downloading .NET 10
en.DlDesc=.NET 10 Desktop Runtime from Microsoft is required.
en.DlFail=Failed to download .NET 10:
en.DlManual=Install .NET 10 Desktop Runtime manually from dotnet.microsoft.com.
en.InstallFail=.NET 10 installation failed (code

[Tasks]
Name: "autostart"; Description: "{cm:TaskAutostart}"; GroupDescription: "{cm:TaskGroupExtra}"
Name: "desktopicon"; Description: "{cm:TaskDesktop}"; GroupDescription: "{cm:TaskGroupExtra}"; Flags: unchecked

[Files]
Source: "..\publish\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Parameters: "--set-autostart on"; Tasks: autostart; Flags: runhidden waituntilterminated
Filename: "{app}\{#AppExe}"; Parameters: "--set-autostart off"; Tasks: not autostart; Flags: runhidden waituntilterminated
Filename: "{app}\{#AppExe}"; Parameters: "--apply"; Tasks: autostart; Flags: runhidden waituntilterminated
Filename: "{app}\{#AppExe}"; Description: "{cm:RunOpen}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{app}\{#AppExe}"; Parameters: "--quit"; RunOnceId: "QuitApp"; Flags: runhidden waituntilterminated
Filename: "{app}\{#AppExe}"; Parameters: "--set-autostart off"; RunOnceId: "RemoveTask"; Flags: runhidden waituntilterminated
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoProfile -Command ""Get-CimInstance Win32_Process | Where-Object {{ $_.ExecutablePath -like '{app}\*' } | ForEach-Object {{ Stop-Process -Id $_.ProcessId -Force }; Start-Sleep -Milliseconds 300"""; RunOnceId: "KillApp"; Flags: runhidden waituntilterminated

[UninstallDelete]
Type: filesandordirs; Name: "{userappdata}\ArcSyncSwitch"
Type: dirifempty; Name: "{app}"

[Code]
var
  DownloadPage: TDownloadWizardPage;

function RuntimeInstalled: Boolean;
var
  R: TFindRec;
begin
  Result := FindFirst(ExpandConstant('{commonpf64}\dotnet\shared\Microsoft.WindowsDesktop.App\10.*'), R);
  if Result then FindClose(R);
end;

procedure InitializeWizard;
begin
  DownloadPage := CreateDownloadPage(CustomMessage('DlTitle'), CustomMessage('DlDesc'), nil);
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var
  Code: Integer;
begin
  Result := True;
  if (CurPageID = wpReady) and not RuntimeInstalled then
  begin
    DownloadPage.Clear;
    DownloadPage.Add('{#RuntimeUrl}', 'windowsdesktop-runtime-win-x64.exe', '');
    DownloadPage.Show;
    try
      try
        DownloadPage.Download;
      except
        SuppressibleMsgBox(CustomMessage('DlFail') + ' ' + AddPeriod(GetExceptionMessage) + #13#10 +
          CustomMessage('DlManual'), mbError, MB_OK, IDOK);
        Result := False;
        Exit;
      end;
    finally
      DownloadPage.Hide;
    end;
    if not ShellExec('', ExpandConstant('{tmp}\windowsdesktop-runtime-win-x64.exe'), '/install /quiet /norestart', '',
                     SW_SHOW, ewWaitUntilTerminated, Code) or not RuntimeInstalled then
    begin
      SuppressibleMsgBox(CustomMessage('InstallFail') + ' ' + IntToStr(Code) + ').', mbError, MB_OK, IDOK);
      Result := False;
    end;
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  Code: Integer;
begin
  Result := '';
  Exec(ExpandConstant('{app}\{#AppExe}'), '--quit', '', SW_HIDE, ewWaitUntilTerminated, Code);
  Sleep(250);
end;
