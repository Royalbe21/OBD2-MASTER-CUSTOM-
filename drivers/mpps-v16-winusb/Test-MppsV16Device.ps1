[CmdletBinding()]
param()

$hardwareId = 'USB\VID_1C43&PID_0500'

$devices = Get-PnpDevice -PresentOnly |
    Where-Object { $_.InstanceId -like "$hardwareId*" -or $_.FriendlyName -like '*Amt Flash*' -or $_.FriendlyName -like '*MPPS*' }

if (-not $devices) {
    Write-Warning 'No MPPS V16 USB device was detected. Plug in the adapter and retry.'
    exit 1
}

foreach ($device in $devices) {
    $device | Select-Object FriendlyName,Status,Problem,Class,Service,InstanceId | Format-List

    Get-PnpDeviceProperty -InstanceId $device.InstanceId `
        -KeyName 'DEVPKEY_Device_DriverInfPath',
                 'DEVPKEY_Device_DriverProvider',
                 'DEVPKEY_Device_DriverVersion',
                 'DEVPKEY_Device_Service',
                 'DEVPKEY_Device_ProblemCode',
                 'DEVPKEY_Device_HardwareIds' `
        -ErrorAction SilentlyContinue |
        Select-Object KeyName,Data |
        Format-List
}
