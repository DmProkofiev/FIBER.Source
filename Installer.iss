; ================================================================
; Inno Setup скрипт для FIBER Source (.NET 10, WPF, Windows x64)
; Сохранить как: C:\Users\ПК\Desktop\Pet-Project_FIBER.Source\Installer.iss
; ================================================================

#define MyAppName        "FIBER Source"
#define MyAppVersion     "1.0.0"
#define MyAppPublisher   "Prokofiev Dmitrii"
#define MyAppExeName     "FIBER_Source.exe"
;; Папка с опубликованным приложением (результат dotnet publish -c Release -r win-x64 --self-contained)
#define MyAppSourceDir   "C:\Users\ПК\Desktop\Pet-Project_FIBER.Source\FIBER.Source\bin\Release\net10.0-windows10.0.22621.0\win-x64\publish"

[Setup]
AppId={{373F39EF-8219-4FE3-A927-603142003B94}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppCopyright="Copyright © 2026 Prokofiev Dmitrii"
DefaultDirName={pf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes

; --- Оформление установщика ---
; ВАЖНО: WizardImageFile и WizardSmallImageFile принимают ТОЛЬКО BMP.
; JPG и PNG вызовут ошибку "Bitmap image is not valid".
; Подготовьте файлы в формате BMP и раскомментируйте строки ниже.

; LicenseFile="C:\Users\ПК\Desktop\Pet-Project_FIBER.Source\FIBER.Source\bin\Release\net10.0-windows10.0.22621.0\win-x64\Resources\LICENSE.rtf"
; InfoBeforeFile="C:\Users\ПК\Desktop\Pet-Project_FIBER.Source\FIBER.Source\bin\Release\net10.0-windows10.0.22621.0\win-x64\Resources\readme.txt"

; Большая картинка слева на страницах мастера (примерно 494×312 пикселей, BMP)
WizardImageFile="C:\Users\ПК\Desktop\Pet-Project_FIBER.Source\FIBER.Source\bin\Release\net10.0-windows10.0.22621.0\win-x64\Resources\BackGround.bmp"

; Маленькая картинка в правом верхнем углу (примерно 70×57 пикселей, BMP)
WizardSmallImageFile="C:\Users\ПК\Desktop\Pet-Project_FIBER.Source\FIBER.Source\bin\Release\net10.0-windows10.0.22621.0\win-x64\Resources\FIBERLogo.bmp"

SetupIconFile="C:\Users\ПК\Desktop\Pet-Project_FIBER.Source\FIBER.Source\bin\Release\net10.0-windows10.0.22621.0\win-x64\Resources\FIBERICO.ico"
UninstallIconFile="{app}\{#MyAppExeName}"

ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
ChangesEnvironment=no
ChangesAssociations=no
Compression=lzma
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Копируем ВСЕ файлы опубликованного приложения (self-contained .NET 10)
Source: "{#MyAppSourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Запустить FIBER Source"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Пользовательская база данных (fiber.db) хранится в %LOCALAPPDATA%\FIBER Source\,
; поэтому при деинсталляции НЕ удаляем её — данные пользователя должны остаться.