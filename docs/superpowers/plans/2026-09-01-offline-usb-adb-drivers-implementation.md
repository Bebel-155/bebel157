# Offline USB/ADB Drivers Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a fully offline USB/ADB driver subsystem to Bebel 155 v5.2.0 that detects Windows USB/ADB state, recommends the correct vendor package, validates redistribution/hash/signature, and installs or repairs only after explicit user action.

**Architecture:** Keep USB discovery, state resolution, package validation, installation, and WinForms presentation in separate C# units. Driver binaries live under `Drivers\` and are included by the Setup only when the manifest marks redistribution as allowed and release gates confirm SHA-256 plus Authenticode publisher.

**Tech Stack:** C#/.NET Framework 4.x, WinForms, ADB, `pnputil`, WMI, PowerShell `Get-AuthenticodeSignature`, Python 3 standard library, Inno Setup 6, GitHub Actions Windows runner.

**Spec:** `Bebel-155_offline-usb-adb-drivers-design.md`

## Global Constraints

- Target application remains `5.2.0` while the release/tag does not exist.
- Never bundle a package with `RedistributionStatus=Unknown` or `NotAllowed`.
- Never install unsigned/modified drivers.
- Never disable signature enforcement, Secure Boot, or enable Test Signing.
- `ADB_UNAUTHORIZED` is an authorization state, not a missing-driver state.
- `ADB_READY` must not trigger reinstall recommendations.
- Every installation/reparation starts only from a user button.
- Every bundled package requires official source, redistribution evidence, license/source note, SHA-256, and expected Authenticode publisher.
- Driver files are installed under `{app}\Drivers`, not embedded into EXE resources.
- Logs exclude IMEI, serial, UDID, tokens, and unrelated personal data.

---

## File Structure

**Create**
- `Drivers/DriverModels.cs`
- `Drivers/UsbDriverDiscovery.cs`
- `Drivers/DriverResolver.cs`
- `Drivers/DriverPackageManager.cs`
- `Drivers/DriverInstaller.cs`
- `Drivers/DriverService.cs`
- `Drivers/drivers-manifest.json`
- `scripts/validate_driver_packages.py`
- `scripts/validate_driver_signatures.ps1`
- `tests/DriverTests.cs`
- `tests/run-driver-tests.ps1`
- `tests/test_driver_manifest_validation.py`
- `tests/driver-fixtures/*`

**Modify**
- `Bebel155_v5_2_0.cs`
- `Bebel155_v5_2_0.iss`
- `CRIAR_EXE_V5_2_0.bat`
- `CRIAR_SETUP_V5_2_0.bat`
- `COMPILAR_COM_DOTNET_MODERNO.ps1`
- `.github/workflows/release.yml`
- `tests/run-all-tests.ps1`

### Task 1: Define driver domain contracts

**Files:**
- Create: `Drivers/DriverModels.cs`
- Create: `tests/DriverTests.cs`

**Interfaces:**
- Consumes: none.
- Produces: `UsbAdbState`, `DriverConfidence`, `DriverPackageType`, `DriverRedistributionStatus`, `UsbDeviceSnapshot`, `InstalledDriverInfo`, `DriverPackage`, `DriverRecommendation`, `DriverInstallResult`.

- [ ] **Step 1: Write RED state-name test**

```csharp
TestAssert.Equal("DRIVER_MISSING", UsbAdbState.DriverMissing.ToWireName(), "missing");
TestAssert.Equal("ADB_UNAUTHORIZED", UsbAdbState.AdbUnauthorized.ToWireName(), "unauthorized");
TestAssert.Equal("ADB_READY", UsbAdbState.AdbReady.ToWireName(), "ready");
```

- [ ] **Step 2: Run test and confirm compile failure because `UsbAdbState` is absent.**

- [ ] **Step 3: Implement enums/models**

Required state enum:

```csharp
public enum UsbAdbState
{
    DriverMissing,
    DriverIncorrect,
    AdbUnauthorized,
    AdbOffline,
    AdbReady,
    UsbOnly,
    Unknown
}
```

`UsbDeviceSnapshot` must carry InstanceId, HardwareIds, CompatibleIds, VendorId, ProductId, Manufacturer, FriendlyName, nullable ProblemCode, InstalledDriverInfo, AdbSerial, and raw ADB state.

`DriverPackage` must carry Id, Manufacturer, DisplayName, Version, PackageType, RelativePath, install args, architecture/Windows lists, VID/HardwareId patterns, SHA-256, SignaturePublisher, SourceUrl, RedistributionStatus, LicenseFile, Priority, and FallbackPackageId.

- [ ] **Step 4: Run GREEN and confirm state contract PASS.**

- [ ] **Step 5: Commit**

```bash
git add Drivers/DriverModels.cs tests/DriverTests.cs
git commit -m "feat: add USB driver domain models"
```

### Task 2: Parse connected USB devices and installed drivers

**Files:**
- Create: `Drivers/UsbDriverDiscovery.cs`
- Create: `tests/driver-fixtures/pnputil-samsung.txt`
- Create: `tests/driver-fixtures/pnputil-winusb.txt`
- Create: `tests/run-driver-tests.ps1`
- Modify: `tests/DriverTests.cs`

**Interfaces:**
- Consumes: `ICommandRunner`.
- Produces: `List<UsbDeviceSnapshot> DiscoverConnectedDevices()` and `InstalledDriverInfo FindInstalledDriver(...)`.

- [ ] **Step 1: Write RED fixtures**

Samsung fixture includes:

```text
Instance ID: USB\VID_04E8&PID_6860\R58...
Device Description: SAMSUNG Mobile USB Composite Device
Manufacturer Name: SAMSUNG Electronics Co., Ltd.
Status: Started
```

Assert VID=`04E8`, PID=`6860`, manufacturer contains `SAMSUNG`.

- [ ] **Step 2: Run RED; expect `UsbDriverDiscovery` missing.**

- [ ] **Step 3: Implement discovery**

Execute:

```text
pnputil /enum-devices /connected
pnputil /enum-drivers
```

Regex:
```text
VID_([0-9A-Fa-f]{4})
PID_([0-9A-Fa-f]{4})
```

Normalize uppercase. Preserve technical IDs internally; never infer commercial phone model from VID/PID.

- [ ] **Step 4: Create test runner compiling existing core runner plus `Drivers/*.cs` and `DriverTests.cs`.**

- [ ] **Step 5: Run GREEN; parser tests PASS.**

- [ ] **Step 6: Commit**

```bash
git add Drivers/UsbDriverDiscovery.cs tests
git commit -m "feat: discover Windows USB driver evidence"
```

### Task 3: Resolve driver/ADB state

**Files:**
- Create: `Drivers/DriverResolver.cs`
- Modify: `tests/DriverTests.cs`

**Interfaces:**
- Consumes: `UsbDeviceSnapshot` plus candidate packages.
- Produces: `DriverRecommendation Resolve(...)`.

- [ ] **Step 1: Write RED cases**

Expected mapping:

```text
ADB device        -> ADB_READY
ADB unauthorized  -> ADB_UNAUTHORIZED
ADB offline       -> ADB_OFFLINE
Windows USB problem + no ADB -> DRIVER_MISSING or DRIVER_INCORRECT
USB present + no ADB/problem -> USB_ONLY
otherwise         -> UNKNOWN
```

- [ ] **Step 2: Verify RED.**

- [ ] **Step 3: Implement exact rule order**

`ADB_READY` and `ADB_UNAUTHORIZED` take precedence over vendor-driver recommendations.

Recommendation priority:
1. exact Hardware ID;
2. matching VID;
3. matching manufacturer;
4. allowed Generic fallback;
5. lowest numeric Priority wins ties.

- [ ] **Step 4: Run GREEN.**

- [ ] **Step 5: Commit**

```bash
git add Drivers/DriverResolver.cs tests/DriverTests.cs
git commit -m "feat: resolve USB and ADB driver state"
```

### Task 4: Load manifest and validate package metadata/hash

**Files:**
- Create: `Drivers/DriverPackageManager.cs`
- Create: `Drivers/drivers-manifest.json`
- Modify: `tests/DriverTests.cs`

**Interfaces:**
- Consumes: manifest and local `Drivers` tree.
- Produces: `LoadManifest`, `GetPackage`, `GetInstallablePackages`, `VerifyHash`.

- [ ] **Step 1: Write RED validation tests**

Reject:
- 63/65-char SHA;
- missing SourceUrl;
- missing LicenseFile;
- `Unknown`/`NotAllowed` from installable list;
- path traversal like `..\..\evil.exe`.

- [ ] **Step 2: Verify RED.**

- [ ] **Step 3: Implement with `JavaScriptSerializer`**

`RelativePath` must resolve under Drivers root. SHA must be exactly 64 hex chars. Installable packages must be `Allowed`.

- [ ] **Step 4: Stream file through `SHA256.Create()` and compare lowercase hash.**

- [ ] **Step 5: Run GREEN.**

- [ ] **Step 6: Commit**

```bash
git add Drivers/DriverPackageManager.cs Drivers/drivers-manifest.json tests
git commit -m "feat: validate offline driver package manifest"
```

### Task 5: Add repository package validator

**Files:**
- Create: `scripts/validate_driver_packages.py`
- Create: `tests/test_driver_manifest_validation.py`

**Interfaces:**
- Consumes: manifest + driver files.
- Produces: exit 0 only when repository packaging is valid.

- [ ] **Step 1: Write RED Python tests for wrong SHA, duplicate ID, missing license, path traversal, undeclared binary, Unknown/NotAllowed binary.**

- [ ] **Step 2: Verify RED because validator does not exist.**

- [ ] **Step 3: Implement using only `json`, `hashlib`, and `pathlib`.**

Every `.exe`, `.msi`, `.inf`, `.cat`, `.sys` must belong to a declared `Allowed` package. Metadata files may be `drivers-manifest.json`, `LICENSE.txt`, `SOURCE.txt`, `README.txt`.

- [ ] **Step 4: Run GREEN**

```text
python tests/test_driver_manifest_validation.py
python scripts/validate_driver_packages.py --root Drivers
```

- [ ] **Step 5: Commit**

```bash
git add scripts/validate_driver_packages.py tests/test_driver_manifest_validation.py
git commit -m "ci: validate bundled driver packages"
```

### Task 6: Add Authenticode release gate

**Files:**
- Create: `scripts/validate_driver_signatures.ps1`
- Modify: `.github/workflows/release.yml`

**Interfaces:**
- Consumes: allowed manifest entries.
- Produces: nonzero exit for unsigned or wrong-publisher package.

- [ ] **Step 1: Prove RED with a test-only unsigned binary; `Get-AuthenticodeSignature` must not return `Valid`.**

- [ ] **Step 2: Implement validator**

For each allowed package:
```powershell
$sig = Get-AuthenticodeSignature $path
if ($sig.Status -ne "Valid") { throw "Assinatura invalida: $path" }
if ($sig.SignerCertificate.Subject -notlike "*$expectedPublisher*") {
  throw "Publisher inesperado: $path"
}
```

- [ ] **Step 3: Add workflow step before compilation**

```yaml
- name: Validar drivers offline
  shell: pwsh
  run: |
    python scripts/validate_driver_packages.py --root Drivers
    .\scripts\validate_driver_signatures.ps1
```

- [ ] **Step 4: Run GREEN only with approved signed packages.**

- [ ] **Step 5: Commit**

```bash
git add scripts/validate_driver_signatures.ps1 .github/workflows/release.yml
git commit -m "ci: verify offline driver signatures"
```

### Task 7: Implement safe elevated installation

**Files:**
- Create: `Drivers/DriverInstaller.cs`
- Modify: `tests/DriverTests.cs`

**Interfaces:**
- Consumes: already validated `DriverPackage`.
- Produces: `DriverInstallResult Install(DriverPackage package, bool repair)`.

- [ ] **Step 1: Write RED command-construction tests**

INF:
```text
pnputil /add-driver "<file.inf>" /install
```

MSI:
```text
msiexec.exe /i "<file.msi>" <manifest args>
```

EXE uses exact manifest arguments; never guesses `/S` or other switches.

- [ ] **Step 2: Verify RED.**

- [ ] **Step 3: Implement preflight**

Require Allowed, file exists, SHA valid, signature valid. Elevate using `ProcessStartInfo.Verb="runas"` only when required.

Treat Win32 error 1223 as user-cancelled UAC, not generic failure.

- [ ] **Step 4: Run process and capture exit code/reboot-required result.**

- [ ] **Step 5: Run GREEN with fake runner abstraction.**

- [ ] **Step 6: Commit**

```bash
git add Drivers/DriverInstaller.cs tests/DriverTests.cs
git commit -m "feat: install validated offline drivers"
```

### Task 8: Add repair/full-package service orchestration

**Files:**
- Create: `Drivers/DriverService.cs`
- Modify: `tests/DriverTests.cs`

**Interfaces:**
- Consumes: discovery, resolver, package manager, installer.
- Produces: `Diagnose`, `InstallRecommended`, `RepairRecommended`, `GetSelectableOfflinePackages`.

- [ ] **Step 1: Write RED service tests**

`ADB_READY` must refuse install. `ADB_UNAUTHORIZED` must advise device authorization. Exact Samsung missing-driver case selects Samsung package. Full-package list excludes disallowed entries.

- [ ] **Step 2: Verify RED.**

- [ ] **Step 3: Implement post-install sequence**

```text
re-enumerate USB
adb kill-server
adb start-server
adb devices -l
re-diagnose
```

- [ ] **Step 4: Run GREEN.**

- [ ] **Step 5: Commit**

```bash
git add Drivers/DriverService.cs tests/DriverTests.cs
git commit -m "feat: orchestrate driver diagnosis and repair"
```

### Task 9: Add Drivers USB / ADB WinForms page

**Files:**
- Modify: `Bebel155_v5_2_0.cs`

**Interfaces:**
- Consumes: `DriverService`.
- Produces: driver page and explicit action buttons.

- [ ] **Step 1: Add RED source check proving navigation lacks `Drivers USB / ADB`.**

- [ ] **Step 2: Add page showing**

```text
Dispositivo
Fabricante
VID/PID
Driver atual
ADB
Driver recomendado
Versão
Assinatura
Status offline
```

- [ ] **Step 3: Add buttons**

```text
Instalar driver recomendado
Reparar driver
Instalar pacote completo
Reexaminar dispositivo
Ver todos os drivers
```

- [ ] **Step 4: Enforce no auto-install on startup/discovery.**

- [ ] **Step 5: Show exact messages**

Unauthorized:
```text
Driver funcionando. Autorize a depuração USB na tela do aparelho.
```

Offline:
```text
ADB detectado como offline. Reinicie ADB e verifique cabo/porta.
```

- [ ] **Step 6: Add all-driver table with Manufacturer, Package, Version, Architecture, Signed, Installed, Action.**

- [ ] **Step 7: Run driver tests + source checks GREEN.**

- [ ] **Step 8: Commit**

```bash
git add Bebel155_v5_2_0.cs tests
git commit -m "feat: add offline driver management page"
```

### Task 10: Package the offline driver tree in Inno Setup

**Files:**
- Modify: `Bebel155_v5_2_0.iss`
- Modify: `CRIAR_SETUP_V5_2_0.bat`

**Interfaces:**
- Consumes: validated `Drivers\` tree.
- Produces: Setup containing only approved offline packages.

- [ ] **Step 1: Make Setup preflight fail when package validator/signature validator fails.**

- [ ] **Step 2: Add Inno entry**

```ini
Source: "Drivers\*"; DestDir: "{app}\Drivers"; Flags: ignoreversion recursesubdirs createallsubdirs
```

No install-time execution of every vendor driver.

- [ ] **Step 3: Run local validators before ISCC.**

- [ ] **Step 4: Build on Windows and inspect Setup contents.**

- [ ] **Step 5: Commit**

```bash
git add Bebel155_v5_2_0.iss CRIAR_SETUP_V5_2_0.bat
git commit -m "build: package offline USB drivers"
```

### Task 11: Add driver suite to application/release builds

**Files:**
- Modify: `CRIAR_EXE_V5_2_0.bat`
- Modify: `COMPILAR_COM_DOTNET_MODERNO.ps1`
- Modify: `tests/run-all-tests.ps1`
- Modify: `.github/workflows/release.yml`

**Interfaces:**
- Consumes: `Drivers/*.cs` and driver tests.
- Produces: build/release gate.

- [ ] **Step 1: Include `Drivers\*.cs` in production source discovery; exclude tests/JSON/binaries.**

- [ ] **Step 2: Add**

```powershell
& "$PSScriptRoot\run-driver-tests.ps1"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

python "$PSScriptRoot\test_driver_manifest_validation.py"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
```

to `run-all-tests.ps1`.

- [ ] **Step 3: Run all tests GREEN on Windows.**

- [ ] **Step 4: Commit**

```bash
git add CRIAR_EXE_V5_2_0.bat COMPILAR_COM_DOTNET_MODERNO.ps1 tests/run-all-tests.ps1 .github/workflows/release.yml
git commit -m "ci: gate release on driver subsystem tests"
```

### Task 12: Acquire and approve actual vendor packages

**Files:**
- Populate: `Drivers/<Manufacturer>/`
- Modify: `Drivers/drivers-manifest.json`

**Interfaces:**
- Consumes: official vendor packages/licenses.
- Produces: only approved, signed, redistributable binaries.

- [ ] **Step 1: Process vendors in this order**

```text
Samsung
Motorola/Lenovo
Xiaomi/Redmi/POCO
OnePlus
OPPO
Realme
Vivo
Huawei/Honor
Sony
ASUS
Nokia/HMD
Nothing
ZTE
TCL/Alcatel
LG legacy
Google/Generic
```

- [ ] **Step 2: For each candidate record official URL, exact version/file, redistribution evidence, SHA-256, publisher, architecture/Windows, VID/HardwareId patterns, install method.**

- [ ] **Step 3: If redistribution evidence is missing, keep status Unknown/NotAllowed and do not place binary in Setup tree.**

- [ ] **Step 4: Run package + signature validators immediately after each accepted vendor.**

- [ ] **Step 5: Commit each vendor/small batch separately.**

Example:
```bash
git add Drivers/Samsung Drivers/drivers-manifest.json
git commit -m "data: add validated Samsung USB driver package"
```

### Task 13: Execute Windows acceptance matrix

**Files:**
- Modify: `LEIA-ME_V5_2_0.txt` only after actual tests.

**Interfaces:**
- Consumes: built application/Setup and physical devices.
- Produces: documented real acceptance evidence.

- [ ] **Step 1: Samsung missing-driver test: detect, recommend, install, re-enumerate, confirm resulting ADB state.**

- [ ] **Step 2: Correct driver + unauthorized prompt: confirm `ADB_UNAUTHORIZED` and no reinstall-first recommendation.**

- [ ] **Step 3: ADB offline: confirm restart/cable guidance.**

- [ ] **Step 4: Unknown compatible Android: confirm Generic confidence instead of invented vendor package.**

- [ ] **Step 5: Two connected devices: confirm page/action follows selected device.**

- [ ] **Step 6: Full-package selection: mix success/failure/UAC cancellation and verify per-package report.**

- [ ] **Step 7: Record actual Windows/device versions and test date.**

### Task 14: Final release evidence gate

**Files:**
- All application, drivers, tests, setup, workflow files.

**Interfaces:**
- Consumes: outputs from Tasks 1–13.
- Produces: evidence permitting v5.2.0 release.

- [ ] **Step 1:** `tests\run-all-tests.ps1` exits 0.
- [ ] **Step 2:** `validate_driver_packages.py` exits 0.
- [ ] **Step 3:** Authenticode validator exits 0 for every bundled package.
- [ ] **Step 4:** `CRIAR_EXE_V5_2_0.bat` exits 0.
- [ ] **Step 5:** `CRIAR_SETUP_V5_2_0.bat` exits 0.
- [ ] **Step 6:** GitHub `release.yml` is green.
- [ ] **Step 7:** Inspect Setup and confirm only approved driver packages are included.
- [ ] **Step 8:** Verify existing v5.1.9 panel discovers/downloads/applies live v5.2.0.
- [ ] **Step 9:** Only then declare the offline-driver subsystem released.
