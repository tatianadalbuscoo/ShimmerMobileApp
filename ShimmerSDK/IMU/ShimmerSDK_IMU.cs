/* 
 * ShimmerSDK_IMU — Cross-platform wrapper for Shimmer3 IMU devices.
 * Handles sensor configuration, firmware-quantized sampling rate, connection/stream lifecycle,
 * CAL index mapping, and event-driven delivery of samples via LatestData/SampleReceived.
 * Backends: Windows (serial V2) and Android (Bluetooth RFCOMM V2).
 */


using System;
using System.Threading;
using System.Threading.Tasks;

#if WINDOWS || ANDROID
using ShimmerAPI;
#endif

#if ANDROID
using ShimmerSDK.Android;
#endif


namespace ShimmerSDK.IMU
{

    /// <summary>
    /// Cross-platform IMU wrapper for Shimmer3 devices:
    /// configures sensors, sets the firmware-quantized sampling rate,
    /// handles connect/stream lifecycle, maps CAL signals, and exposes data
    /// via LatestData and the SampleReceived event.
    /// </summary>
    public partial class ShimmerSDK_IMU
    {

        // Raised for each incoming IMU packet with the latest parsed sample.
        public event EventHandler<dynamic>? SampleReceived;


#if WINDOWS
        
        // Enabled sensor bitmap
        private int _winEnabledSensors;

        // Windows buffered serial driver 
        private ShimmerLogAndStreamSystemSerialPortV2? shimmer;

        // Guards reconfiguration; ignore events while true
        private volatile bool _reconfigInProgress = false;

        // Map indices on first received packet
        private bool firstDataPacket = true;

        // Signal indices in ObjectCluster (CAL), set on first packet
        private int indexTimeStamp;
        private int indexLowNoiseAccX;
        private int indexLowNoiseAccY;
        private int indexLowNoiseAccZ;
        private int indexWideAccX;
        private int indexWideAccY;
        private int indexWideAccZ;
        private int indexGyroX;
        private int indexGyroY;
        private int indexGyroZ;
        private int indexMagX;
        private int indexMagY;
        private int indexMagZ;
        private int indexBMP180Temperature;
        private int indexBMP180Pressure;
        private int indexBatteryVoltage;
        private int indexExtA6;
        private int indexExtA7;
        private int indexExtA15;

#endif

#if ANDROID

        // Android Bluetooth wrapper
        private ShimmerLogAndStreamAndroidBluetoothV2 shimmerAndroid;

        // Enabled sensor bitmap
        private int _androidEnabledSensors;

        // True while device is streaming
        private volatile bool _androidIsStreaming = false;

        // Map indices on first received packet
        private bool firstDataPacketAndroid = true;

        // Signal indices in ObjectCluster (CAL), set on first packet
        private int indexTimeStamp;
        private int indexLowNoiseAccX;
        private int indexLowNoiseAccY;
        private int indexLowNoiseAccZ;
        private int indexWideAccX;
        private int indexWideAccY;
        private int indexWideAccZ;
        private int indexGyroX;
        private int indexGyroY;
        private int indexGyroZ;
        private int indexMagX;
        private int indexMagY;
        private int indexMagZ;
        private int indexBMP180Temperature;
        private int indexBMP180Pressure;
        private int indexBatteryVoltage;
        private int indexExtA6;
        private int indexExtA7;
        private int indexExtA15;

        // Awaitables for state/ack/first-packet synchronization
        private System.Threading.Tasks.TaskCompletionSource<bool>? _androidConnectedTcs;
        private System.Threading.Tasks.TaskCompletionSource<bool>? _androidStreamingAckTcs;
        private System.Threading.Tasks.TaskCompletionSource<bool>? _androidFirstPacketTcs;

#endif


        // Last parsed IMU sample (CAL values for enabled sensors)
        public ShimmerSDK_IMUData? LatestData { get; private set; }


        /// <summary>
        /// Constructor: Initializes the IMU wrapper with sensible defaults:
        /// sampling at ~51.2 Hz and all key sensors enabled (LNA, WRA, gyro, mag,
        /// pressure/temperature, battery, and external ADCs A6/A7/A15).
        /// </summary>
        public ShimmerSDK_IMU()
        {
            _samplingRate = 51.2;
            _enableLowNoiseAccelerometer = true;
            _enableWideRangeAccelerometer = true;
            _enableGyroscope = true;
            _enableMagnetometer = true;
            _enablePressureTemperature = true;
            _enableBattery = true;
            _enableExtA6 = true;
            _enableExtA7 = true;
            _enableExtA15 = true;
        }


        /// <summary>
        /// Sets the nearest firmware-representable sampling rate to <paramref name="requestedHz"/>,
        /// writes it to the device, updates <c>SamplingRate</c>, and returns the applied value.
        /// </summary>
        /// <param name="requestedHz">Desired sampling rate in Hz (must be &gt; 0).</param>
        /// <returns>The actual rate applied after firmware quantization (e.g., 51.2 Hz).</returns>
        public double SetFirmwareSamplingRateNearest(double requestedHz)
        {
            if (requestedHz <= 0) throw new ArgumentOutOfRangeException(nameof(requestedHz));

            // Base clock: Shimmer3 = 32768
            double clock = 32768.0;

#if IOS || MACCATALYST

            // Avoid blocking
            if (!string.IsNullOrWhiteSpace(BridgeTargetMac))
                return SetFirmwareSamplingRateNearestAsync(requestedHz).GetAwaiter().GetResult();

#endif

            // Quantize to nearest: divider = round(clock / f_request), f_applied = clock / divider
            int divider = Math.Max(1, (int)Math.Round(clock / requestedHz, MidpointRounding.AwayFromZero));
            double applied = clock / divider;

            // Write to firmware

#if WINDOWS

            // Windows API accepts the effective frequency directly (double)
            shimmer?.WriteSamplingRate(applied);

#elif ANDROID

            // Android wrapper uses a divider-based API
            shimmerAndroid?.WriteSamplingRate(divider);     

#endif

            // Sync the public property with the actually applied rate
            SamplingRate = applied;
            return applied;
        }


#if WINDOWS

        /// <summary>
        /// Configure the Windows serial backend: apply sensor flags, rebuild the bitmap,
        /// reset index mapping, and subscribe to data events.
        /// </summary>
        /// <param name="deviceName">Device name.</param>
        /// <param name="comPort">Serial COM port.</param>
        /// <param name="enableLowNoiseAcc">Enable low-noise accelerometer.</param>
        /// <param name="enableWideRangeAcc">Enable wide-range accelerometer.</param>
        /// <param name="enableGyro">Enable gyroscope.</param>
        /// <param name="enableMag">Enable magnetometer.</param>
        /// <param name="enablePressureTemp">Enable pressure/temperature sensor.</param>
        /// <param name="enableBattery">Enable battery voltage sensing.</param>
        /// <param name="enableExtA6">Enable external ADC A6.</param>
        /// <param name="enableExtA7">Enable external ADC A7.</param>
        /// <param name="enableExtA15">Enable external ADC A15.</param>
        public void ConfigureWindows(
            string deviceName,
            string comPort,
            bool enableLowNoiseAcc,
            bool enableWideRangeAcc,
            bool enableGyro,
            bool enableMag,
            bool enablePressureTemp,
            bool enableBattery,
            bool enableExtA6,
            bool enableExtA7,
            bool enableExtA15)
        {

            _reconfigInProgress = true;
            try
            {
                // Detach & dispose previous instance to avoid duplicate event handlers or stale streams
                if (shimmer != null)
                {
                    try { shimmer.UICallback -= this.HandleEvent; } catch {}
                    try { shimmer.StopStreaming(); } catch {}
                    try { shimmer.Disconnect(); } catch {}
                    shimmer = null;
                }

                // Persist sensor flags
                _enableLowNoiseAccelerometer = enableLowNoiseAcc;
                _enableWideRangeAccelerometer = enableWideRangeAcc;
                _enableGyroscope = enableGyro;
                _enableMagnetometer = enableMag;
                _enablePressureTemperature = enablePressureTemp;
                _enableBattery = enableBattery;
                _enableExtA6 = enableExtA6;
                _enableExtA7 = enableExtA7;
                _enableExtA15 = enableExtA15;

                // Rebuild sensor bitmap
                int enabledSensors = 0;
                if (_enableLowNoiseAccelerometer)
                    enabledSensors |= (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_A_ACCEL;
                if (_enableWideRangeAccelerometer)
                    enabledSensors |= (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_D_ACCEL;
                if (_enableGyroscope)
                    enabledSensors |= (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_MPU9150_GYRO;
                if (_enableMagnetometer)
                    enabledSensors |= (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_LSM303DLHC_MAG;
                if (_enablePressureTemperature)
                    enabledSensors |= (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_BMP180_PRESSURE;
                if (_enableBattery)
                    enabledSensors |= (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_VBATT;
                if (_enableExtA6)
                    enabledSensors |= (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_EXT_A6;
                if (_enableExtA7)
                    enabledSensors |= (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_EXT_A7;
                if (_enableExtA15)
                    enabledSensors |= (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_EXT_A15;

                _winEnabledSensors = enabledSensors;

                // Force index remapping on next packet
                firstDataPacket = true;

                // Create new driver instance and subscribe exactly once
                shimmer = new ShimmerLogAndStreamSystemSerialPortV2(deviceName, comPort);
                try { shimmer.UICallback -= this.HandleEvent; } catch {}
                shimmer.UICallback += this.HandleEvent;
            }
            finally
            {
                _reconfigInProgress = false;
            }
        }


        /// <summary>Safely gets a signal by index.</summary>
        /// <param name="oc">Source <see cref="ObjectCluster"/>.</param>
        /// <param name="idx">Signal index, returns null if &lt; 0.</param>
        /// <returns><see cref="SensorData"/> if present; otherwise null.</returns>
        private static SensorData? GetSafe(ObjectCluster oc, int idx)
        {
            return idx >= 0 ? oc.GetData(idx) : null;
        }


        /// <summary>
        /// Windows event sink: on DATA_PACKET, map indices (first packet), build <see cref="LatestData"/>,
        /// and raise <see cref="SampleReceived"/>.
        /// </summary>
        /// <param name="sender">Event source (driver instance).</param>
        /// <param name="args">Event payload (<see cref="CustomEventArgs"/>).</param>
        private void HandleEvent(object? sender, EventArgs args)
        {

            // Ignore packets while reconfiguring
            if (_reconfigInProgress) return;

            // Cast event, check for DATA_PACKET, and extract the ObjectCluster payload
            var eventArgs = (CustomEventArgs)args;
            if (eventArgs.getIndicator() == (int)ShimmerBluetooth.ShimmerIdentifier.MSG_IDENTIFIER_DATA_PACKET)
            {
                ObjectCluster oc = (ObjectCluster)eventArgs.getObject();

                if (firstDataPacket)
                {

                    // Map CAL indices once (signal name → column index)
                    indexTimeStamp = oc.GetIndex(ShimmerConfiguration.SignalNames.SYSTEM_TIMESTAMP, ShimmerConfiguration.SignalFormats.CAL);
                    indexLowNoiseAccX = oc.GetIndex(Shimmer3Configuration.SignalNames.LOW_NOISE_ACCELEROMETER_X, ShimmerConfiguration.SignalFormats.CAL);
                    indexLowNoiseAccY = oc.GetIndex(Shimmer3Configuration.SignalNames.LOW_NOISE_ACCELEROMETER_Y, ShimmerConfiguration.SignalFormats.CAL);
                    indexLowNoiseAccZ = oc.GetIndex(Shimmer3Configuration.SignalNames.LOW_NOISE_ACCELEROMETER_Z, ShimmerConfiguration.SignalFormats.CAL);
                    indexWideAccX     = oc.GetIndex(Shimmer3Configuration.SignalNames.WIDE_RANGE_ACCELEROMETER_X, ShimmerConfiguration.SignalFormats.CAL);
                    indexWideAccY     = oc.GetIndex(Shimmer3Configuration.SignalNames.WIDE_RANGE_ACCELEROMETER_Y, ShimmerConfiguration.SignalFormats.CAL);
                    indexWideAccZ     = oc.GetIndex(Shimmer3Configuration.SignalNames.WIDE_RANGE_ACCELEROMETER_Z, ShimmerConfiguration.SignalFormats.CAL);
                    indexGyroX        = oc.GetIndex(Shimmer3Configuration.SignalNames.GYROSCOPE_X, ShimmerConfiguration.SignalFormats.CAL);
                    indexGyroY        = oc.GetIndex(Shimmer3Configuration.SignalNames.GYROSCOPE_Y, ShimmerConfiguration.SignalFormats.CAL);
                    indexGyroZ        = oc.GetIndex(Shimmer3Configuration.SignalNames.GYROSCOPE_Z, ShimmerConfiguration.SignalFormats.CAL);
                    indexMagX         = oc.GetIndex(Shimmer3Configuration.SignalNames.MAGNETOMETER_X, ShimmerConfiguration.SignalFormats.CAL);
                    indexMagY         = oc.GetIndex(Shimmer3Configuration.SignalNames.MAGNETOMETER_Y, ShimmerConfiguration.SignalFormats.CAL);
                    indexMagZ         = oc.GetIndex(Shimmer3Configuration.SignalNames.MAGNETOMETER_Z, ShimmerConfiguration.SignalFormats.CAL);
                    indexBMP180Temperature = oc.GetIndex(Shimmer3Configuration.SignalNames.TEMPERATURE, ShimmerConfiguration.SignalFormats.CAL);
                    indexBMP180Pressure    = oc.GetIndex(Shimmer3Configuration.SignalNames.PRESSURE, ShimmerConfiguration.SignalFormats.CAL);
                    indexBatteryVoltage    = oc.GetIndex(Shimmer3Configuration.SignalNames.V_SENSE_BATT, ShimmerConfiguration.SignalFormats.CAL);
                    indexExtA6             = oc.GetIndex(Shimmer3Configuration.SignalNames.EXTERNAL_ADC_A6, ShimmerConfiguration.SignalFormats.CAL);
                    indexExtA7             = oc.GetIndex(Shimmer3Configuration.SignalNames.EXTERNAL_ADC_A7, ShimmerConfiguration.SignalFormats.CAL);
                    indexExtA15            = oc.GetIndex(Shimmer3Configuration.SignalNames.EXTERNAL_ADC_A15, ShimmerConfiguration.SignalFormats.CAL);

                    firstDataPacket = false;
                }

                // Build snapshot with CAL values (null-safe via GetSafe)
                LatestData = new ShimmerSDK_IMUData(
                    GetSafe(oc, indexTimeStamp),
                    GetSafe(oc, indexLowNoiseAccX),
                    GetSafe(oc, indexLowNoiseAccY),
                    GetSafe(oc, indexLowNoiseAccZ),
                    GetSafe(oc, indexWideAccX),
                    GetSafe(oc, indexWideAccY),
                    GetSafe(oc, indexWideAccZ),
                    GetSafe(oc, indexGyroX),
                    GetSafe(oc, indexGyroY),
                    GetSafe(oc, indexGyroZ),
                    GetSafe(oc, indexMagX),
                    GetSafe(oc, indexMagY),
                    GetSafe(oc, indexMagZ),
                    GetSafe(oc, indexBMP180Temperature),
                    GetSafe(oc, indexBMP180Pressure),
                    GetSafe(oc, indexBatteryVoltage),
                    GetSafe(oc, indexExtA6),
                    GetSafe(oc, indexExtA7),
                    GetSafe(oc, indexExtA15)
                );

                // Notify subscribers with the latest parsed sample
                if (LatestData != null)
                SampleReceived?.Invoke(this, LatestData);
            }
        }

#endif


#if ANDROID

        /// <summary>
        /// Configure the Android Bluetooth backend: apply sensor flags, validate MAC,
        /// compute the sensor bitmap, create the V2 wrapper, and (re)wire callbacks.
        /// </summary>
        /// <param name="deviceId">Device name.</param>
        /// <param name="mac">Bluetooth MAC address (validated).</param>
        /// <param name="enableLowNoiseAcc">Enable low-noise accelerometer.</param>
        /// <param name="enableWideRangeAcc">Enable wide-range accelerometer.</param>
        /// <param name="enableGyro">Enable gyroscope.</param>
        /// <param name="enableMag">Enable magnetometer.</param>
        /// <param name="enablePressureTemp">Enable pressure/temperature.</param>
        /// <param name="enableBattery">Enable battery sensing.</param>
        /// <param name="enableExtA6">Enable external ADC A6.</param>
        /// <param name="enableExtA7">Enable external ADC A7.</param>
        /// <param name="enableExtA15">Enable external ADC A15.</param>
        public void ConfigureAndroid(
            string deviceId,
            string mac,
            bool enableLowNoiseAcc,
            bool enableWideRangeAcc,
            bool enableGyro,
            bool enableMag,
            bool enablePressureTemp,
            bool enableBattery,
            bool enableExtA6,
            bool enableExtA7,
            bool enableExtA15)
        {

            // Persist sensor flags
            _enableLowNoiseAccelerometer = enableLowNoiseAcc;
            _enableWideRangeAccelerometer = enableWideRangeAcc;
            _enableGyroscope = enableGyro;
            _enableMagnetometer = enableMag;
            _enablePressureTemperature = enablePressureTemp;
            _enableBattery = enableBattery;
            _enableExtA6 = enableExtA6;
            _enableExtA7 = enableExtA7;
            _enableExtA15 = enableExtA15;

            // Validate MAC 
            mac = (mac ?? "").Trim();
            if (!global::Android.Bluetooth.BluetoothAdapter.CheckBluetoothAddress(mac))
                throw new ArgumentException($"Invalid MAC '{mac}'");

            // Build sensor bitmap
            int sensors = 0;
            if (_enableLowNoiseAccelerometer) sensors |= (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_A_ACCEL;
            if (_enableWideRangeAccelerometer) sensors |= (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_D_ACCEL;
            if (_enableGyroscope)             sensors |= (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_MPU9150_GYRO;
            if (_enableMagnetometer)          sensors |= (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_LSM303DLHC_MAG;
            if (_enablePressureTemperature)   sensors |= (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_BMP180_PRESSURE;
            if (_enableBattery)               sensors |= (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_VBATT;
            if (_enableExtA6)                 sensors |= (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_EXT_A6;
            if (_enableExtA7)                 sensors |= (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_EXT_A7;
            if (_enableExtA15)                sensors |= (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_EXT_A15;

            _androidEnabledSensors = sensors;

            // Create V2-style Android wrapper (RFCOMM transport)
            shimmerAndroid = new ShimmerLogAndStreamAndroidBluetoothV2(deviceId, mac);

            // Subscribe once to events (avoid duplicates)
            shimmerAndroid.UICallback -= HandleEventAndroid;
            shimmerAndroid.UICallback += HandleEventAndroid;

            // Force index remap on next DATA_PACKET
            firstDataPacketAndroid = true;
            indexTimeStamp = indexLowNoiseAccX = indexLowNoiseAccY = indexLowNoiseAccZ =
            indexWideAccX = indexWideAccY = indexWideAccZ =
            indexGyroX = indexGyroY = indexGyroZ =
            indexMagX = indexMagY = indexMagZ =
            indexBMP180Temperature = indexBMP180Pressure =
            indexBatteryVoltage = indexExtA6 = indexExtA7 = indexExtA15 = -1;
        }


        /// <summary>
        /// Connects the Android Bluetooth transport and reports the connection state.
        /// </summary>
        /// <returns><c>true</c> if connected; otherwise <c>false</c>.</returns>
        public bool ConnectInternalAndroid()
        {
            if (shimmerAndroid == null)
                throw new InvalidOperationException("Shimmer Android is not configured. Call ConfigureAndroid() first.");

            shimmerAndroid.Connect();
            return shimmerAndroid.IsConnected();
        }


        /// <summary>
        /// Applies a new sampling rate on Android safely: stops streaming if needed,
        /// disables sensors, sets the nearest firmware rate, restores config, and resumes.
        /// </summary>
        /// <param name="requestedHz">Desired sampling rate in Hz (must be &gt; 0).</param>
        /// <returns>The effective (quantized) sampling rate now in use.</returns>
        public async Task<double> ApplySamplingRateWithSafeRestartAsync(double requestedHz)
        {
            if (requestedHz <= 0) throw new ArgumentOutOfRangeException(nameof(requestedHz));
            if (shimmerAndroid == null) throw new InvalidOperationException("ConfigureAndroid was not called.");

            var wasStreaming = _androidIsStreaming;

            // If streaming, stop first
            if (wasStreaming)
            {
                shimmerAndroid.StopStreaming();
                await Task.Delay(150);
                _androidIsStreaming = false;
            }

            // Disable sensors to avoid partial packets
            shimmerAndroid.WriteSensors(0);
            await Task.Delay(150);

            // Apply SR in firmware
            var applied = SetFirmwareSamplingRateNearest(requestedHz);
            await Task.Delay(180);

            // Re-apply ranges/power defaults
            shimmerAndroid.WriteAccelRange(0);
            shimmerAndroid.WriteGyroRange(0);
            shimmerAndroid.SetLowPowerAccel(false);
            shimmerAndroid.SetLowPowerGyro(false);
            shimmerAndroid.WriteInternalExpPower(0);
            await Task.Delay(150);

            // Re-enable selected sensors and refresh metadata
            shimmerAndroid.WriteSensors(_androidEnabledSensors);
            await Task.Delay(180);

            shimmerAndroid.Inquiry();
            await Task.Delay(350);

            // Reload calibrations
            shimmerAndroid.ReadCalibrationParameters("All");
            await Task.Delay(250);

            // If we were streaming, start again and wait for ACK + first packet
            if (wasStreaming)
            {
                _androidStreamingAckTcs = new System.Threading.Tasks.TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                _androidFirstPacketTcs  = new System.Threading.Tasks.TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

                firstDataPacketAndroid = true;
                shimmerAndroid.StartStreaming();

                await Task.WhenAny(_androidStreamingAckTcs.Task, Task.Delay(2000));
                await Task.WhenAny(_androidFirstPacketTcs.Task,  Task.Delay(2000));
            }

            return SamplingRate; // Effective quantized value
        }


        /// <summary>Android event sink: route state changes, map indices on first packet, update <see cref="LatestData"/>, and raise <see cref="SampleReceived"/>.</summary>
        /// <param name="sender">Event source.</param>
        /// <param name="args">Event payload (<see cref="CustomEventArgs"/>).</param>
        private void HandleEventAndroid(object? sender, EventArgs args)
        {
            try
            {

                // Ensure the expected payload type
                if (args is not CustomEventArgs ev)
                    return;

                int indicator = ev.getIndicator();

                // Fast path: unblock waiters when streaming state is confirmed
                if (indicator == (int)ShimmerBluetooth.ShimmerIdentifier.MSG_IDENTIFIER_STATE_CHANGE)
                {
                    if (ev.getObject() is int st && st == 3 /* SHIMMER_STATE_STREAMING */)
                        _androidStreamingAckTcs?.TrySetResult(true);
                }

                // Route non-data events that do not require further handling
                switch (indicator)
                {
                    case (int)ShimmerBluetooth.ShimmerIdentifier.MSG_IDENTIFIER_STATE_CHANGE:
                        if (ev.getObject() is int st)
                        {
                            if (st == ShimmerBluetooth.SHIMMER_STATE_CONNECTED)
                            {
                                _androidConnectedTcs?.TrySetResult(true);
                                _androidIsStreaming = false; // connected, not streaming
                            }
                            else if (st == ShimmerBluetooth.SHIMMER_STATE_STREAMING)
                            {
                                _androidStreamingAckTcs?.TrySetResult(true);
                                _androidIsStreaming = true;
                                firstDataPacketAndroid = true; // remap indices on resume
                            }
                            else
                            {
                                _androidIsStreaming = false;
                            }
                        }
                        return;

                    case (int)ShimmerBluetooth.ShimmerIdentifier.MSG_IDENTIFIER_NOTIFICATION_MESSAGE:
                    case (int)ShimmerBluetooth.ShimmerIdentifier.MSG_IDENTIFIER_PACKET_RECEPTION_RATE:
                        return;

                    case (int)ShimmerBluetooth.ShimmerIdentifier.MSG_IDENTIFIER_DATA_PACKET:
                        _androidFirstPacketTcs?.TrySetResult(true);
                        break; // handle packet below

                    default:
                        return;
                }

                // ----- DATA_PACKET -----

                var oc = ev.getObject() as ObjectCluster;
                if (oc == null) return;

                // Local helpers: safe index lookup and null-safe data access
                static int SafeIdx(ObjectCluster c, string name, string fmt)
                {
                    var i = c.GetIndex(name, fmt);
                    return i < 0 ? -1 : i;
                }
                static SensorData? SafeGet(ObjectCluster c, int idx) => idx >= 0 ? c.GetData(idx) : null;

                // On first packet, resolve and cache CAL indices once
                if (firstDataPacketAndroid)
                {
                    indexTimeStamp         = SafeIdx(oc, ShimmerConfiguration.SignalNames.SYSTEM_TIMESTAMP, "CAL");
                    indexLowNoiseAccX      = SafeIdx(oc, Shimmer3Configuration.SignalNames.LOW_NOISE_ACCELEROMETER_X, "CAL");
                    indexLowNoiseAccY      = SafeIdx(oc, Shimmer3Configuration.SignalNames.LOW_NOISE_ACCELEROMETER_Y, "CAL");
                    indexLowNoiseAccZ      = SafeIdx(oc, Shimmer3Configuration.SignalNames.LOW_NOISE_ACCELEROMETER_Z, "CAL");
                    indexWideAccX          = SafeIdx(oc, Shimmer3Configuration.SignalNames.WIDE_RANGE_ACCELEROMETER_X, "CAL");
                    indexWideAccY          = SafeIdx(oc, Shimmer3Configuration.SignalNames.WIDE_RANGE_ACCELEROMETER_Y, "CAL");
                    indexWideAccZ          = SafeIdx(oc, Shimmer3Configuration.SignalNames.WIDE_RANGE_ACCELEROMETER_Z, "CAL");
                    indexGyroX             = SafeIdx(oc, Shimmer3Configuration.SignalNames.GYROSCOPE_X, "CAL");
                    indexGyroY             = SafeIdx(oc, Shimmer3Configuration.SignalNames.GYROSCOPE_Y, "CAL");
                    indexGyroZ             = SafeIdx(oc, Shimmer3Configuration.SignalNames.GYROSCOPE_Z, "CAL");
                    indexMagX              = SafeIdx(oc, Shimmer3Configuration.SignalNames.MAGNETOMETER_X, "CAL");
                    indexMagY              = SafeIdx(oc, Shimmer3Configuration.SignalNames.MAGNETOMETER_Y, "CAL");
                    indexMagZ              = SafeIdx(oc, Shimmer3Configuration.SignalNames.MAGNETOMETER_Z, "CAL");
                    indexBMP180Temperature = SafeIdx(oc, Shimmer3Configuration.SignalNames.TEMPERATURE, "CAL");
                    indexBMP180Pressure    = SafeIdx(oc, Shimmer3Configuration.SignalNames.PRESSURE, "CAL");
                    indexBatteryVoltage    = SafeIdx(oc, Shimmer3Configuration.SignalNames.V_SENSE_BATT, "CAL");
                    indexExtA6             = SafeIdx(oc, Shimmer3Configuration.SignalNames.EXTERNAL_ADC_A6, "CAL");
                    indexExtA7             = SafeIdx(oc, Shimmer3Configuration.SignalNames.EXTERNAL_ADC_A7, "CAL");
                    indexExtA15            = SafeIdx(oc, Shimmer3Configuration.SignalNames.EXTERNAL_ADC_A15, "CAL");

                    firstDataPacketAndroid = false;
                }

                // Update the latest data snapshot (CAL values), null-safe
                LatestData = new ShimmerSDK_IMUData(
                    SafeGet(oc, indexTimeStamp),
                    SafeGet(oc, indexLowNoiseAccX), SafeGet(oc, indexLowNoiseAccY), SafeGet(oc, indexLowNoiseAccZ),
                    SafeGet(oc, indexWideAccX),    SafeGet(oc, indexWideAccY),    SafeGet(oc, indexWideAccZ),
                    SafeGet(oc, indexGyroX),       SafeGet(oc, indexGyroY),       SafeGet(oc, indexGyroZ),
                    SafeGet(oc, indexMagX),        SafeGet(oc, indexMagY),        SafeGet(oc, indexMagZ),
                    SafeGet(oc, indexBMP180Temperature), SafeGet(oc, indexBMP180Pressure),
                    SafeGet(oc, indexBatteryVoltage),
                    SafeGet(oc, indexExtA6), SafeGet(oc, indexExtA7), SafeGet(oc, indexExtA15)
                );

                // Notify subscribers with the latest parsed sample
                try { SampleReceived?.Invoke(this, LatestData); } catch {}
            }
            catch {}
        }

#endif

    }
}
