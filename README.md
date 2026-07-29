# OBD2 Master, Custom

OBD2 Master, Custom is a Windows 11 WPF prototype for working with ELM327 serial/Bluetooth adapters and J2534-capable OBD-II adapters.

It includes profiles for a 2008 Toyota Tacoma Base 2.7L, a 2016 Jeep Compass FWD four-cylinder, a 2014 Hyundai Elantra 2.0L, Dodge/Freightliner Sprinter diesel vans, Ram ProMaster EcoDiesel vans, Chevrolet Express/GMC Savana vans, and broad generic CAN profiles for common manufacturer families.

## Current prototype features

- USB/Bluetooth serial ELM327 connection through Windows COM ports
- Dedicated USB ELM327 HS/MS-CAN CH340 adapter profile with driver detection and switch-position guidance
- Adapter Wizard tab for COM/J2534 open checks, ELM identity tests, protocol confirmation, vehicle ECU response validation, VIN attempts, HS/MS switch checks, and saved hardware reports
- Vehicle Discovery tab for safe baseline creation: VIN, protocol, supported PIDs, codes, readiness, adapter capability, and read-only module map labels
- Manufacturer-specific read-only scan buttons for Toyota enhanced data, Chrysler transmission, Hyundai/Kia modules, Ford/Mazda HS-CAN modules, and Ford/Mazda MS-CAN modules
- J2534 PassThru connection through installed Windows J2534 DLLs
- Manual J2534 DLL selection when the driver is not registered
- Demo mode for UI testing without the vehicle connected
- ELM initialization for Toyota CAN OBD-II
- Stored, pending, and permanent emissions DTC reads
- Confirmed emissions DTC clear command with a warning
- Readiness monitor decoding from Mode 01 PID 01
- VIN read from Mode 09 PID 02
- Supported PID discovery
- Live data read and polling
- Raw Mode 6 reads
- Read-only CAN OBD module probe using OBD service requests
- Plain text diagnostic report export
- Adapter self-test for serial, Bluetooth serial, demo, and J2534 workflows
- Selectable ELM protocol profiles for broader OBD-II adapter compatibility
- Freeze-frame reader for key Mode 02 data points
- Advisor tab with health findings, next steps, and readiness guidance
- Session history saved under local app data
- Guided diagnostic workflows for check-engine triage, readiness, fuel trim, misfire, and adapter audits
- Generic Mode 06 monitor-test decoding with raw logs for manufacturer-specific rows
- Live-data recording with CSV export for before/after repair comparisons
- HTML report export with advisor findings and recent live-recording frames
- MPPS V16 companion-tool tab for detecting plugged-in MPPS-style USB hardware, reporting Windows driver state, finding/selecting/launching an installed MPPS executable, and documenting readiness notes
- 2016 Jeep Compass FWD four-cylinder profile with Chrysler/FCA enhanced module-scan targets
- Transmission-priority module scan for Jeep/Chrysler TCM candidates using read-only UDS-style DTC, ECU identity, and calibration/part identifier requests where supported
- 2014 Hyundai Elantra 2.0L profile with Hyundai/Kia read-only module-scan targets
- Toyota/Tacoma enhanced read-only candidates for ECM, ECT/TCM, ABS/VSC, SRS, body ECU, combination meter, and A/C amplifier identity/data checks
- Dodge/Freightliner Sprinter diesel van profile with Mercedes/Sprinter-derived read-only module-scan targets
- Ram ProMaster 3.0L EcoDiesel van profile with FCA/Ram diesel van read-only module-scan targets
- Chevrolet Express / GMC Savana van profile with GM van read-only module-scan targets
- Generic manufacturer profiles for Ford/Lincoln, GM, Honda/Acura, Nissan/Infiniti, Toyota/Lexus, Chrysler/Jeep/Dodge/Ram, VW/Audi, BMW/Mini, Mercedes-Benz, Subaru, Mazda, Volvo, Hyundai/Kia, and generic OBD-II CAN vehicles
- Manufacturer-aware module scan catalog with transmission-priority targets where known, including Ford/Mazda HS-CAN and MS-CAN switch notes
- Bus-aware module scan results showing CAN, HS-CAN, or MS-CAN in the module grid and reports
- Safe module map labels such as Confirmed, Possible, No response, Requires MS-CAN, and Requires enhanced adapter
- Dark workstation-style interface

## Important limits

Plain serial ELM327 adapters can perform generic OBD-II diagnostics very well, but they are not equivalent to Toyota Techstream with a J2534 interface. If your adapter exposes a J2534 PassThru DLL, OBD2 Master, Custom can use that DLL for ISO 15765-4 CAN OBD-II communication while keeping serial ELM327 support for older adapters.

OBD2 Master, Custom intentionally does not include ECU programming, immobilizer functions, SRS clearing, ABS bleeding, or unsafe bidirectional controls.

Manufacturer-specific buttons are read-only discovery tools. They request DTC records, ECU identity, and common UDS-style data identifiers such as VIN, part number, calibration, or strategy data where a module supports them. They do not perform relearns, resets, coding, active tests, immobilizer functions, SRS/ABS service routines, or programming.

MPPS V16 is handled as an external ECU flasher companion. OBD2 Master, Custom can detect common MPPS USB hardware such as `USB\VID_1C43&PID_0500`, report whether Windows has a working driver loaded, scan for `mpps.exe`, launch it, and add MPPS readiness/safety notes to reports. It does not automate MPPS ECU read/write operations. Use OBD2 Master, Custom's J2534 mode only if the MPPS package or another adapter installs a real SAE J2534 PassThru DLL.

Windows USB detection, MPPS software installation, and J2534 registration are separate checks. A plugged-in MPPS device with Windows Device Manager Code 28 means the hardware is present but the driver is not installed or failed to install; install the vendor-supplied Windows-compatible driver package before expecting full MPPS functionality.

An experimental WinUSB driver-package scaffold for MPPS-style `USB\VID_1C43&PID_0500` hardware is included in `drivers/mpps-v16-winusb`. It can bind the device to Microsoft's inbox WinUSB driver for user-mode protocol research, but it is not a vendor-equivalent MPPS driver and needs a signed catalog before normal Windows 11 installation.

For the 2016 Jeep Compass, use the ELM/J2534 diagnostic connection for scanning. MPPS V16 is not the preferred tool for module diagnostics; it is treated as a separate ECU/TCU read/write companion. Full Chrysler module functionality depends on adapter quality, module addressing, and Chrysler-enhanced service definitions. The built-in enhanced scan uses read-only requests and does not perform coding, programming, immobilizer work, or bidirectional actuator tests.

For the 2014 Hyundai Elantra 2.0L, select the Hyundai profile and start with CAN 11/500. Transmission and enhanced module scans use read-only UDS-style requests where supported. Full Hyundai/Kia dealer-level coverage still depends on adapter quality, module addressing, and enhanced service definitions.

For Toyota/Tacoma enhanced checks, start with CAN 11/500 and use `Toyota Data` after the baseline. It can document likely ECM, transmission, ABS/VSC, SRS, body, cluster, and HVAC candidates but is not a Techstream replacement for active tests, utility resets, customizations, or immobilizer work.

For Dodge/Freightliner Sprinter diesel vans, select the Sprinter diesel profile instead of the generic Chrysler profile. Sprinter diagnostics are Mercedes-derived, and early vans may need K-line/ISO support rather than CAN. Full ABS/SRS/transmission/body coverage normally requires Sprinter/Mercedes-capable tooling or J2534 with enhanced definitions.

For Ram ProMaster 3.0L EcoDiesel vans, select the ProMaster EcoDiesel profile and start with CAN 11/500. The built-in scan is read-only and can document diesel powertrain and module candidates, but it does not perform DPF regeneration, DEF/SCR resets, injector coding, adaptations, or relearns.

For Chevrolet Express and GMC Savana vans, select the Express/Savana profile and use auto-detect first. Newer vans commonly use CAN, while older vans may use GM VPW/Class 2. Diesel and body/chassis modules may require GM-enhanced definitions beyond generic OBD-II.

For the USB ELM327 HS/MS-CAN adapter with a CH340T/CH341 USB serial chip, install the CH340 driver before connecting to the vehicle. OBD2 Master, Custom detects common CH340/CH341 devices, auto-selects their COM port where possible, and includes Ford/Mazda HS-CAN/MS-CAN switch guidance in the raw terminal and reports. The Adapter Wizard also looks for common USB serial and Bluetooth serial adapter families such as FTDI, CP210x, Prolific, WCH CH9102, and Bluetooth COM devices. Set the physical switch to HS-CAN for normal OBD-II and most powertrain scans; set it to MS-CAN only when the workflow or module notes ask for body/cluster/HVAC/comfort modules.

Readiness cannot be forced ready by software. Clearing DTCs resets readiness monitors. The truck must run the required monitor checks during normal or drive-cycle operation.

## Build

```powershell
cd C:\Users\royal\TacomaDiag
dotnet build .\TacomaDiag.slnx
```

## Build installer

```powershell
cd C:\Users\royal\TacomaDiag
.\build-installer.ps1
```

The installer is created at:

```text
C:\Users\royal\TacomaDiag\installer\output\OBD2MasterCustomSetup.exe
```

## Run

```powershell
cd C:\Users\royal\TacomaDiag
dotnet run --project .\src\TacomaDiag\TacomaDiag.csproj
```

## First real-vehicle test

1. Plug the ELM327 into the Tacoma DLC3/OBD-II port.
2. Turn the key to ON. Start the engine for live data.
3. Pair Bluetooth serial adapters in Windows first if needed.
4. Open OBD2 Master, Custom.
5. For older adapters, choose `Serial ELM327`, select the COM port, and try 38400 baud first, then 9600 or 115200 if needed.
6. For J2534-capable adapters, choose `J2534 PassThru` and select the installed DLL. If it is not listed, use `Browse DLL`.
7. Open the `Adapter Wizard` tab and click `Refresh Hardware`.
8. Check `Vehicle connected / ignition ON`, then click `Run Adapter Wizard`.
9. Confirm the wizard shows adapter identity, protocol, and a vehicle ECU response.
10. Open `Vehicle Discovery` and click `Discover Vehicle` to create the baseline.
11. For Ford/Mazda HS/MS-CAN adapters, click `HS/MS Switch Check` before body, cluster, HVAC, or comfort-module scans.
12. Click `Save Baseline`, then continue with Check Readiness or Full Scan as needed.

## J2534 notes

OBD2 Master, Custom discovers installed J2534 drivers from:

- `HKLM\SOFTWARE\PassThruSupport.04.04`
- `HKLM\SOFTWARE\PassThruSupport.05.00`

The selected J2534 DLL must match the app process architecture. If a driver only ships a 32-bit DLL, build/run OBD2 Master, Custom as 32-bit or install a 64-bit driver from the adapter vendor.

The current J2534 path targets the Tacoma's expected ISO 15765-4 CAN 11-bit / 500 kbps OBD-II network. It supports the same scan, readiness, live data, VIN, Mode 6, raw command, and report workflows as serial mode.
