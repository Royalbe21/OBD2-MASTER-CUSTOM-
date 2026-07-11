# MPPS V16 WinUSB Driver Package

This folder contains a Windows 11 driver-package scaffold for MPPS-style USB hardware detected as:

```text
USB\VID_1C43&PID_0500
```

It binds the device to Microsoft's built-in `winusb.sys` function driver. This is useful for user-mode protocol research and TacomaDiag detection, but it is not the original MPPS vendor driver and does not provide full ECU flashing functionality by itself.

## Important

- Installing this package may stop the original MPPS application from seeing the adapter if that application expects its own vendor driver.
- Windows 11 requires driver packages to be signed. The INF alone is not enough for a normal secure Windows install.
- For normal MPPS ECU read/write work, prefer the official vendor driver/software package.
- Do not use this package for live vehicle flashing until the USB protocol is understood and tested away from the vehicle.

## Build

Open PowerShell as Administrator:

```powershell
powershell -ExecutionPolicy Bypass -File .\Build-MppsV16WinUsbPackage.ps1
```

The build script looks for WDK tools:

- `inf2cat.exe`
- `signtool.exe`

If they are available, it creates `MppsV16WinUsb.cat`. If you pass `-CreateTestCertificate`, it also creates a local test certificate and signs the catalog for test-machine use.

## Install

Open PowerShell as Administrator:

```powershell
powershell -ExecutionPolicy Bypass -File .\Install-MppsV16WinUsbPackage.ps1
```

Then unplug and replug the MPPS V16 adapter.

## Test

```powershell
powershell -ExecutionPolicy Bypass -File .\Test-MppsV16Device.ps1
```

A successful WinUSB bind should show the device with `Service : WinUSB` and problem code `0`.
