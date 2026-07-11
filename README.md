# TacomaDiag

TacomaDiag is a Windows 11 WPF prototype for working with an ELM327 or J2534-capable OBD-II adapter on a 2008 Toyota Tacoma Base 2.7L 2TR-FE.

It also includes profiles for a 2016 Jeep Compass FWD four-cylinder, a 2014 Hyundai Elantra 2.0L, and broad generic CAN profiles for common manufacturer families.

## Current prototype features

- USB/Bluetooth serial ELM327 connection through Windows COM ports
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
- Transmission-priority module scan for Jeep/Chrysler TCM candidates using read-only UDS-style requests where supported
- 2014 Hyundai Elantra 2.0L profile with Hyundai/Kia read-only module-scan targets
- Generic manufacturer profiles for Ford/Lincoln, GM, Honda/Acura, Nissan/Infiniti, Toyota/Lexus, Chrysler/Jeep/Dodge/Ram, VW/Audi, BMW/Mini, Mercedes-Benz, Subaru, Mazda, Volvo, Hyundai/Kia, and generic OBD-II CAN vehicles
- Manufacturer-aware module scan catalog with transmission-priority targets where known
- Dark workstation-style interface

## Important limits

Plain serial ELM327 adapters can perform generic OBD-II diagnostics very well, but they are not equivalent to Toyota Techstream with a J2534 interface. If your adapter exposes a J2534 PassThru DLL, TacomaDiag can use that DLL for ISO 15765-4 CAN OBD-II communication while keeping serial ELM327 support for older adapters.

TacomaDiag intentionally does not include ECU programming, immobilizer functions, SRS clearing, ABS bleeding, or unsafe bidirectional controls.

MPPS V16 is handled as an external ECU flasher companion. TacomaDiag can detect common MPPS USB hardware such as `USB\VID_1C43&PID_0500`, report whether Windows has a working driver loaded, scan for `mpps.exe`, launch it, and add MPPS readiness/safety notes to reports. It does not automate MPPS ECU read/write operations. Use TacomaDiag's J2534 mode only if the MPPS package or another adapter installs a real SAE J2534 PassThru DLL.

Windows USB detection, MPPS software installation, and J2534 registration are separate checks. A plugged-in MPPS device with Windows Device Manager Code 28 means the hardware is present but the driver is not installed or failed to install; install the vendor-supplied Windows-compatible driver package before expecting full MPPS functionality.

An experimental WinUSB driver-package scaffold for MPPS-style `USB\VID_1C43&PID_0500` hardware is included in `drivers/mpps-v16-winusb`. It can bind the device to Microsoft's inbox WinUSB driver for user-mode protocol research, but it is not a vendor-equivalent MPPS driver and needs a signed catalog before normal Windows 11 installation.

For the 2016 Jeep Compass, use the ELM/J2534 diagnostic connection for scanning. MPPS V16 is not the preferred tool for module diagnostics; it is treated as a separate ECU/TCU read/write companion. Full Chrysler module functionality depends on adapter quality, module addressing, and Chrysler-enhanced service definitions. The built-in enhanced scan uses read-only requests and does not perform coding, programming, immobilizer work, or bidirectional actuator tests.

For the 2014 Hyundai Elantra 2.0L, select the Hyundai profile and start with CAN 11/500. Transmission and enhanced module scans use read-only UDS-style requests where supported. Full Hyundai/Kia dealer-level coverage still depends on adapter quality, module addressing, and enhanced service definitions.

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
C:\Users\royal\TacomaDiag\installer\output\TacomaDiagSetup.exe
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
4. Open TacomaDiag.
5. For older adapters, choose `Serial ELM327`, select the COM port, and try 38400 baud first, then 9600 or 115200 if needed.
6. For J2534-capable adapters, choose `J2534 PassThru` and select the installed DLL. If it is not listed, use `Browse DLL`.
7. Click Connect.
8. Confirm the raw terminal shows `ATDP` as ISO 15765-4 CAN or J2534 ISO 15765-4 CAN.
9. Click Check Readiness or Full Scan.

## J2534 notes

TacomaDiag discovers installed J2534 drivers from:

- `HKLM\SOFTWARE\PassThruSupport.04.04`
- `HKLM\SOFTWARE\PassThruSupport.05.00`

The selected J2534 DLL must match the app process architecture. If a driver only ships a 32-bit DLL, build/run TacomaDiag as 32-bit or install a 64-bit driver from the adapter vendor.

The current J2534 path targets the Tacoma's expected ISO 15765-4 CAN 11-bit / 500 kbps OBD-II network. It supports the same scan, readiness, live data, VIN, Mode 6, raw command, and report workflows as serial mode.
