#if MACCATALYST
using System;
using System.Threading.Tasks;
using CoreBluetooth;
using CoreFoundation;
using Foundation;

namespace XR2Learn_ShimmerAPI.IMU
{
    /// <summary>
    /// Implementazione BLE per Mac Catalyst.
    /// SOLO helper privati e stato BLE: nessun metodo pubblico duplicato.
    /// </summary>
    public partial class XR2Learn_ShimmerIMU
    {
        // UUID (NUS-like)
        private static readonly CBUUID SHIMMER_SERVICE_UUID = CBUUID.FromString("6E400001-B5A3-F393-E0A9-E50E24DCCA9E");
        private static readonly CBUUID SHIMMER_RX_CHAR_UUID  = CBUUID.FromString("6E400003-B5A3-F393-E0A9-E50E24DCCA9E"); // notify
        private static readonly CBUUID SHIMMER_TX_CHAR_UUID  = CBUUID.FromString("6E400002-B5A3-F393-E0A9-E50E24DCCA9E"); // write

        // Hint nome periferica (impostato in Configure nel ramo MACCATALYST)
        private string _bleDeviceName = "Shimmer3";

        // Stato BLE
        private CBCentralManager _central;
        private ShimmerCentralDelegate _centralDelegate;
        private CBPeripheral _peripheral;
        private CBCharacteristic _rxChar;
        private CBCharacteristic _txChar;
        private readonly DispatchQueue _bleQueue = new DispatchQueue("XR2Learn_ShimmerIMU.BLE");

        // TCS per sincronizzazione
        private TaskCompletionSource<bool> _tcsPoweredOn;
        private TaskCompletionSource<bool> _tcsConnected;
        private TaskCompletionSource<bool> _tcsDiscoveredServices;
        private TaskCompletionSource<bool> _tcsDiscoveredCharacteristics;

        private volatile bool _isStreaming;

        // ===== Helper privati usati dai metodi pubblici (nel ramo #elif MACCATALYST) =====

        private void ConnectMac()
        {
            _tcsPoweredOn = new TaskCompletionSource<bool>();
            _tcsConnected = new TaskCompletionSource<bool>();
            _tcsDiscoveredServices = new TaskCompletionSource<bool>();
            _tcsDiscoveredCharacteristics = new TaskCompletionSource<bool>();

            _centralDelegate = new ShimmerCentralDelegate(this);
            _central = new CBCentralManager(_centralDelegate, _bleQueue);

            // Attendi BT PoweredOn
            _tcsPoweredOn.Task.Wait();

            // Scan (filtriamo per service, e per nome nel delegate)
            _central.ScanForPeripherals(new[] { SHIMMER_SERVICE_UUID });

            if (!_tcsConnected.Task.Wait(TimeSpan.FromSeconds(20)))
                throw new TimeoutException("BLE connect timeout: peripheral not found/connected.");

            _peripheral.DiscoverServices(new[] { SHIMMER_SERVICE_UUID });
            if (!_tcsDiscoveredServices.Task.Wait(TimeSpan.FromSeconds(10)))
                throw new TimeoutException("BLE service discovery timeout.");

            foreach (var svc in _peripheral.Services ?? Array.Empty<CBService>())
            {
                if (svc?.UUID != null && svc.UUID.Equals(SHIMMER_SERVICE_UUID))
                    _peripheral.DiscoverCharacteristics(new[] { SHIMMER_RX_CHAR_UUID, SHIMMER_TX_CHAR_UUID }, svc);
            }

            if (!_tcsDiscoveredCharacteristics.Task.Wait(TimeSpan.FromSeconds(10)))
                throw new TimeoutException("BLE characteristic discovery timeout.");

            if (_rxChar == null)
                throw new InvalidOperationException("RX characteristic not found.");

            _peripheral.SetNotifyValue(true, _rxChar);
        }

        private async Task DisconnectMacAsync()
        {
            try
            {
                if (_peripheral != null && _central != null)
                {
                    _central.CancelPeripheralConnection(_peripheral);
                    await DelayWork(400);
                }
            }
            finally
            {
                _isStreaming = false;
                _rxChar = null;
                _txChar = null;
                _peripheral = null;
                _centralDelegate = null;
                _central = null;
            }
        }

        private async Task StartStreamingMacAsync()
        {
            if (_txChar == null || _peripheral == null) return;
            SendCmd(0x07); // startStreamingCommand
            await DelayWork(100);
            _isStreaming = true;
        }

        private async Task StopStreamingMacAsync()
        {
            if (_txChar == null || _peripheral == null) return;
            SendCmd(0x20); // stopStreamingCommand
            await DelayWork(100);
            _isStreaming = false;
        }

        private bool IsConnectedMac() =>
            _peripheral != null && _peripheral.State == CBPeripheralState.Connected;

        private void SendCmd(byte b)
        {
            if (_txChar == null || _peripheral == null) return;
            _peripheral.WriteValue(NSData.FromArray(new byte[] { b }), _txChar, CBCharacteristicWriteType.WithResponse);
        }

        // ===== Parser (stub: da completare secondo il protocollo Shimmer) =====
        private void OnPacketReceived(byte[] payload)
        {
            try
            {
                uint ts = 0;
                if (payload.Length >= 4)
                    ts = (uint)(payload[0] | (payload[1] << 8) | (payload[2] << 16) | (payload[3] << 24));

                LatestData = new XR2Learn_ShimmerIMUData(
                    timeStamp: ts,
                    accelerometerX: null, accelerometerY: null, accelerometerZ: null,
                    wideAccelerometerX: null, wideAccelerometerY: null, wideAccelerometerZ: null,
                    gyroscopeX: null, gyroscopeY: null, gyroscopeZ: null,
                    magnetometerX: null, magnetometerY: null, magnetometerZ: null,
                    temperatureBMP180: null, pressureBMP180: null,
                    batteryVoltage: null,
                    extADC_A6: null, extADC_A7: null, extADC_A15: null
                );
            }
            catch
            {
                // ignora frame corrotti finché il parser non è completo
            }
        }

        // ===== Delegates BLE =====

        private class ShimmerCentralDelegate : CBCentralManagerDelegate
        {
            private readonly XR2Learn_ShimmerIMU _owner;
            public ShimmerCentralDelegate(XR2Learn_ShimmerIMU owner) => _owner = owner;

            public override void UpdatedState(CBCentralManager central)
            {
                if (central.State == CBCentralManagerState.PoweredOn)
                    _owner?._tcsPoweredOn?.TrySetResult(true);
                else if (central.State == CBCentralManagerState.Unauthorized)
                    _owner?._tcsPoweredOn?.TrySetException(new UnauthorizedAccessException("Bluetooth unauthorized."));
                else if (central.State == CBCentralManagerState.Unsupported)
                    _owner?._tcsPoweredOn?.TrySetException(new PlatformNotSupportedException("BLE unsupported."));
            }

            public override void DiscoveredPeripheral(CBCentralManager central, CBPeripheral peripheral, NSDictionary advertisementData, NSNumber RSSI)
            {
                var name = peripheral?.Name ?? advertisementData?[CBAdvertisement.DataLocalNameKey]?.ToString();
                if (!string.IsNullOrEmpty(_owner._bleDeviceName) && !string.IsNullOrEmpty(name))
                {
                    if (!name.Contains(_owner._bleDeviceName, StringComparison.OrdinalIgnoreCase))
                        return;
                }

                central.StopScan();
                _owner._peripheral = peripheral;
                peripheral.Delegate = new ShimmerPeripheralDelegate(_owner);
                central.ConnectPeripheral(peripheral);
            }

            public override void ConnectedPeripheral(CBCentralManager central, CBPeripheral peripheral) =>
                _owner?._tcsConnected?.TrySetResult(true);

            public override void FailedToConnectPeripheral(CBCentralManager central, CBPeripheral peripheral, NSError error) =>
                _owner?._tcsConnected?.TrySetException(new Exception($"Failed to connect: {error?.LocalizedDescription}"));

            public override void DisconnectedPeripheral(CBCentralManager central, CBPeripheral peripheral, NSError error)
            {
                _owner._isStreaming = false;
            }
        }

       private class ShimmerPeripheralDelegate : CBPeripheralDelegate
{
    private readonly XR2Learn_ShimmerIMU _owner;
    public ShimmerPeripheralDelegate(XR2Learn_ShimmerIMU owner) => _owner = owner;

    // OK: singolare
    public override void DiscoveredService(CBPeripheral peripheral, NSError error)
    {
        if (error != null)
        {
            _owner?._tcsDiscoveredServices?.TrySetException(new Exception(error.LocalizedDescription));
            return;
        }
        _owner?._tcsDiscoveredServices?.TrySetResult(true);
    }

    // OK: singolare
    public override void DiscoveredCharacteristic(CBPeripheral peripheral, CBService service, NSError error)
    {
        if (error != null)
        {
            _owner?._tcsDiscoveredCharacteristics?.TrySetException(new Exception(error.LocalizedDescription));
            return;
        }

        foreach (var c in service.Characteristics ?? Array.Empty<CBCharacteristic>())
        {
            if (c.UUID.Equals(SHIMMER_RX_CHAR_UUID)) _owner._rxChar = c;
            else if (c.UUID.Equals(SHIMMER_TX_CHAR_UUID)) _owner._txChar = c;
        }

        if (_owner._rxChar != null && _owner._txChar != null)
            _owner?._tcsDiscoveredCharacteristics?.TrySetResult(true);
    }

    // ✅ firma ESATTA in Xamarin/MacCatalyst (con "terter")
    public override void UpdatedCharacterteristicValue(CBPeripheral peripheral, CBCharacteristic characteristic, NSError error)
    {
        if (error != null || characteristic?.Value == null) return;
        var bytes = characteristic.Value.ToArray();
        _owner.OnPacketReceived(bytes);
    }
}

    }
}
#endif
