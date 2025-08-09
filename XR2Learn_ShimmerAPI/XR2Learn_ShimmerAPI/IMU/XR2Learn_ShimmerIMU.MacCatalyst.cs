#if MACCATALYST
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using CoreBluetooth;
using CoreFoundation;
using Foundation;

namespace XR2Learn_ShimmerAPI.IMU
{
    /// <summary>
    /// macOS (Mac Catalyst) BLE-backed implementation for Shimmer3 IMU.
    /// Notes:
    /// - This file adds BLE support under the MACCATALYST symbol. Windows code remains untouched.
    /// - Replace the UUID placeholders with the actual Shimmer service & characteristic UUIDs.
    /// - The packet parser is a stub; map actual payload fields to XR2Learn_ShimmerIMUData.
    /// </summary>
    public partial class XR2Learn_ShimmerIMU
    {
        // ======== Configure these to match your Shimmer3 BLE profile ========
        // Example 128-bit UUIDs in canonical form; replace with real ones.
        private static readonly CBUUID SHIMMER_SERVICE_UUID      = CBUUID.FromString("6E400001-B5A3-F393-E0A9-E50E24DCCA9E");
        private static readonly CBUUID SHIMMER_RX_CHAR_UUID       = CBUUID.FromString("6E400003-B5A3-F393-E0A9-E50E24DCCA9E"); // notifications from device
        private static readonly CBUUID SHIMMER_TX_CHAR_UUID       = CBUUID.FromString("6E400002-B5A3-F393-E0A9-E50E24DCCA9E"); // writes to device

        // Optional: filter by advertised local name
        private string _bleDeviceName = "Shimmer3"; // set via Configure for Mac if desired

        // BLE state
        private CBCentralManager _central;
        private ShimmerCentralDelegate _centralDelegate;
        private CBPeripheral _peripheral;
        private CBCharacteristic _rxChar; // notifications (device -> host)
        private CBCharacteristic _txChar; // writes (host -> device)
        private readonly DispatchQueue _bleQueue = new DispatchQueue("XR2Learn_ShimmerIMU.BLE");

        private TaskCompletionSource<bool> _tcsPoweredOn;
        private TaskCompletionSource<bool> _tcsConnected;
        private TaskCompletionSource<bool> _tcsDiscoveredServices;
        private TaskCompletionSource<bool> _tcsDiscoveredCharacteristics;

        private volatile bool _isStreaming;

        /// <summary>
        /// Configure for Mac Catalyst (BLE). Callers can still use the same signature;
        /// we store flags and (optionally) a BLE name hint in place of COM port.
        /// </summary>
        public void Configure(
            string deviceName, string bleNameOrHint, bool enableLowNoiseAcc,
            bool enableWideRangeAcc, bool enableGyro, bool enableMag,
            bool enablePressureTemp, bool enableBattery,
            bool enableExtA6, bool enableExtA7, bool enableExtA15)
        {
            // Save flags (same as Windows path)
            _enableLowNoiseAccelerometer = enableLowNoiseAcc;
            _enableWideRangeAccelerometer = enableWideRangeAcc;
            _enableGyroscope = enableGyro;
            _enableMagnetometer = enableMag;
            _enablePressureTemperature = enablePressureTemp;
            _enableBattery = enableBattery;
            _enableExtA6 = enableExtA6;
            _enableExtA7 = enableExtA7;
            _enableExtA15 = enableExtA15;

            // Use the "comPort" parameter slot as BLE name hint on Mac
            if (!string.IsNullOrWhiteSpace(bleNameOrHint))
                _bleDeviceName = bleNameOrHint;
        }

        public void Connect()
        {
            if (IsConnected()) return;

            _tcsPoweredOn = new TaskCompletionSource<bool>();
            _tcsConnected = new TaskCompletionSource<bool>();
            _tcsDiscoveredServices = new TaskCompletionSource<bool>();
            _tcsDiscoveredCharacteristics = new TaskCompletionSource<bool>();

            _centralDelegate = new ShimmerCentralDelegate(this);
            _central = new CBCentralManager(_centralDelegate, _bleQueue);

            // Wait for PoweredOn
            _tcsPoweredOn.Task.Wait();

            // Start scanning for Service UUID to reduce noise; we also filter by name in delegate
            _central.ScanForPeripherals(new CBUUID[] { SHIMMER_SERVICE_UUID });

            // Block until connected (or timeout)
            if (!_tcsConnected.Task.Wait(TimeSpan.FromSeconds(20)))
                throw new TimeoutException("BLE connect timeout: peripheral not found/connected.");

            // Discover services
            _peripheral.DiscoverServices(new CBUUID[] { SHIMMER_SERVICE_UUID });
            if (!_tcsDiscoveredServices.Task.Wait(TimeSpan.FromSeconds(10)))
                throw new TimeoutException("BLE service discovery timeout.");

            // Discover characteristics
            foreach (var svc in _peripheral.Services)
            {
                if (svc.UUID.Equals(SHIMMER_SERVICE_UUID))
                    _peripheral.DiscoverCharacteristics(new CBUUID[] { SHIMMER_RX_CHAR_UUID, SHIMMER_TX_CHAR_UUID }, svc);
            }
            if (!_tcsDiscoveredCharacteristics.Task.Wait(TimeSpan.FromSeconds(10)))
                throw new TimeoutException("BLE characteristic discovery timeout.");

            // Subscribe to notifications on RX characteristic
            if (_rxChar == null) throw new InvalidOperationException("RX characteristic not found.");
            _peripheral.SetNotifyValue(true, _rxChar);
        }

        public async void Disconnect()
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

        public async void StartStreaming()
        {
            if (_txChar == null || _peripheral == null) return;

            // TODO: replace with the actual Shimmer command to start streaming.
            // Common patterns are 0x07,0x00,... or ASCII like "start" depending on firmware.
            var startCmd = NSData.FromArray(new byte[] { (byte)0x07 }); // PacketTypeShimmer.startStreamingCommand
            _peripheral.WriteValue(startCmd, _txChar, CBCharacteristicWriteType.WithResponse);
            await DelayWork(100);
            _isStreaming = true;
        }

        public async void StopStreaming()
        {
            if (_txChar == null || _peripheral == null) return;
            // TODO: replace with the actual Shimmer command to stop streaming.
            var stopCmd = NSData.FromArray(new byte[] { (byte)0x20 }); // PacketTypeShimmer.stopStreamingCommand
            _peripheral.WriteValue(stopCmd, _txChar, CBCharacteristicWriteType.WithResponse);
            await DelayWork(100);
            _isStreaming = false;
        }

        public bool IsConnected()
        {
            return _peripheral != null && _peripheral.State == CBPeripheralState.Connected;
        }

        // ======== Packet parsing ========
        private void OnPacketReceived(byte[] payload)
        {
            // TODO: parse Shimmer BLE payload into calibrated units.
            // Below is a minimal-safe example that sets only the timestamp.
            // Replace with your real mapping (endianness, scales, units...).

            try
            {
                // Example: first 4 bytes = uint32 timestamp in ms
                uint ts = 0;
                if (payload.Length >= 4)
                    ts = (uint)(payload[0] | (payload[1] << 8) | (payload[2] << 16) | (payload[3] << 24));

#if WINDOWS
                // On Mac we don't have SensorData type; we keep the same model but we cannot use it here.
#else
                // Non-Windows stub path (as in your current code) uses object; create a simple frame
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
#endif
            }
            catch
            {
                // Swallow malformed frames until mapping is finalized.
            }
        }

        // ======== Central delegate ========
        private class ShimmerCentralDelegate : CBCentralManagerDelegate
        {
            private readonly XR2Learn_ShimmerIMU _owner;

            public ShimmerCentralDelegate(XR2Learn_ShimmerIMU owner)
            {
                _owner = owner;
            }

            public override void UpdatedState(CBCentralManager central)
            {
                if (central.State == CBCentralManagerState.PoweredOn)
                {
                    _owner?._tcsPoweredOn?.TrySetResult(true);
                }
                else if (central.State == CBCentralManagerState.Unauthorized)
                {
                    _owner?._tcsPoweredOn?.TrySetException(new UnauthorizedAccessException("Bluetooth unauthorized. Check Info.plist & entitlements."));
                }
                else if (central.State == CBCentralManagerState.Unsupported)
                {
                    _owner?._tcsPoweredOn?.TrySetException(new PlatformNotSupportedException("Bluetooth LE unsupported on this Mac."));
                }
            }

            public override void DiscoveredPeripheral(CBCentralManager central, CBPeripheral peripheral, NSDictionary advertisementData, NSNumber RSSI)
            {
                // Filter by name and service; name may be null depending on advertisement
                var name = peripheral.Name ?? advertisementData?[CBAdvertisement.DataLocalNameKey]?.ToString();
                if (!string.IsNullOrEmpty(_owner._bleDeviceName) && !string.IsNullOrEmpty(name))
                {
                    if (!name.Contains(_owner._bleDeviceName, StringComparison.OrdinalIgnoreCase))
                        return;
                }

                // Stop scanning and connect to the first match
                central.StopScan();
                _owner._peripheral = peripheral;
                peripheral.Delegate = new ShimmerPeripheralDelegate(_owner);
                central.ConnectPeripheral(peripheral);
            }

            public override void ConnectedPeripheral(CBCentralManager central, CBPeripheral peripheral)
            {
                _owner?._tcsConnected?.TrySetResult(true);
            }

            public override void FailedToConnectPeripheral(CBCentralManager central, CBPeripheral peripheral, NSError error)
            {
                _owner?._tcsConnected?.TrySetException(new Exception($"Failed to connect: {error?.LocalizedDescription}"));
            }

            public override void DisconnectedPeripheral(CBCentralManager central, CBPeripheral peripheral, NSError error)
            {
                _owner._isStreaming = false;
            }
        }

        // ======== Peripheral delegate ========
        private class ShimmerPeripheralDelegate : CBPeripheralDelegate
        {
            private readonly XR2Learn_ShimmerIMU _owner;

            public ShimmerPeripheralDelegate(XR2Learn_ShimmerIMU owner)
            {
                _owner = owner;
            }

            public override void DiscoveredService(CBPeripheral peripheral, NSError error)
            {
                if (error != null)
                {
                    _owner?._tcsDiscoveredServices?.TrySetException(new Exception(error.LocalizedDescription));
                    return;
                }

                // Signal that services are available
                _owner?._tcsDiscoveredServices?.TrySetResult(true);
            }

            public override void DiscoveredCharacteristic(CBPeripheral peripheral, CBService service, NSError error)
            {
                if (error != null)
                {
                    _owner?._tcsDiscoveredCharacteristics?.TrySetException(new Exception(error.LocalizedDescription));
                    return;
                }

                foreach (var c in service.Characteristics ?? Array.Empty<CBCharacteristic>())
                {
                    if (c.UUID.Equals(SHIMMER_RX_CHAR_UUID))
                        _owner._rxChar = c;
                    else if (c.UUID.Equals(SHIMMER_TX_CHAR_UUID))
                        _owner._txChar = c;
                }

                if (_owner._rxChar != null && _owner._txChar != null)
                    _owner?._tcsDiscoveredCharacteristics?.TrySetResult(true);
            }

            public override void UpdatedCharacterteristicValue(CBPeripheral peripheral, CBCharacteristic characteristic, NSError error)
            {
                if (error != null) return;
                if (characteristic?.Value == null) return;

                var bytes = characteristic.Value.ToArray();
                _owner.OnPacketReceived(bytes);
            }
        }
    }
}
#endif
