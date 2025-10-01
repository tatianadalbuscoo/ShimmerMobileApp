/* 
 * ShimmerSDK_EXG — Cross-platform wrapper for Shimmer3 EXG devices.
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


namespace ShimmerSDK.EXG
{

    /// <summary>Operating modes for the EXG sensor.</summary>
    public enum ExgMode
    {
        ECG,
        EMG,
        ExGTest,
        Respiration
    }


    /// <summary>
    /// Cross-platform EXG wrapper for Shimmer3 devices:
    /// configures EXG/aux sensors, applies the nearest firmware-supported sampling rate,
    /// manages connect/stream lifecycle, maps CAL signals (EXG CH1/CH2 and optional respiration),
    /// and exposes samples via LatestData and the SampleReceived event.
    /// </summary>
    public partial class ShimmerSDK_EXG
    {

        // Raised for each incoming EXG packet with the latest parsed sample.
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
        private int indexExg1; 
        private int indexExg2;

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
        private int indexExg1;
        private int indexExg2;

        // Awaitables for state/ack/first-packet synchronization
        private TaskCompletionSource<bool>? _androidConnectedTcs;
        private TaskCompletionSource<bool>? _androidStreamingAckTcs;
        private TaskCompletionSource<bool>? _androidFirstPacketTcs;

#endif


        /// <summary>
        /// Constructor: set sensible defaults (≈51.2 Hz, core IMU sensors on,
        /// EXG off by default, ECG mode selected).
        /// </summary>
        public ShimmerSDK_EXG()
        {
            _samplingRate = 51.2;
            _enableLowNoiseAccelerometer = true;
            _enableWideRangeAccelerometer = true;
            _enableGyroscope = true;
            _enableMagnetometer = true;
            _enablePressureTemperature = true;
            _enableBatteryVoltage = true;
            _enableExtA6 = true;
            _enableExtA7 = true;
            _enableExtA15 = true;
            _enableExg = false;
            _exgMode = ExgMode.ECG;
        }


        /// <summary>
        /// Async convenience wrapper that applies the nearest firmware-supported sampling rate
        /// and returns the actual value applied.
        /// On iOS/macOS it delegates to the platform-specific implementation; on Windows/Android
        /// it simply wraps the synchronous method to keep an async-friendly API.
        /// </summary>
        /// <param name="requestedHz">Desired sampling rate in Hz (must be &gt; 0).</param>
        /// <returns>A task resolving to the firmware-quantized rate actually applied.</returns>
        public Task<double> SetFirmwareSamplingRateNearestAsync(double requestedHz)
        {

#if IOS || MACCATALYST

            return SetFirmwareSamplingRateNearestImpl(requestedHz);

#else

            return Task.FromResult(SetFirmwareSamplingRateNearest(requestedHz));
#endif

        }


        /// <summary>
        /// Computes and applies the nearest firmware-representable sampling rate to <paramref name="requestedHz"/>.
        /// Uses the device base clock (32,768 Hz for Shimmer3) to quantize
        /// the request, writes it to the device (platform-specific), updates <c>SamplingRate</c>, and returns
        /// the effective value.
        /// </summary>
        /// <param name="requestedHz">Desired sampling rate in Hz (must be &gt; 0).</param>
        /// <returns>The firmware-quantized rate actually applied (e.g., 51.2 Hz).</returns>
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

#if WINDOWS

            // Windows API accepts the effective frequency directly (double)
            shimmer?.WriteSamplingRate(applied);

#endif

            // Sync the public property with the actually applied rate
            SamplingRate = applied;
            return applied;
        }


#if WINDOWS

        /// <summary>
        /// Configure the Windows backend: persists sensor flags (incl. EXG), rebuilds the
        /// Shimmer3 sensor bitmap, resets index mapping, and (re)subscribes to data events.
        /// Cleans up any previous driver instance to avoid duplicate handlers/streams.
        /// </summary>
        /// <param name="deviceName">Device name.</param>
        /// <param name="comPort">Serial COM port.</param>
        /// <param name="enableLowNoiseAcc">Enable low-noise accelerometer.</param>
        /// <param name="enableWideRangeAcc">Enable wide-range accelerometer.</param>
        /// <param name="enableGyro">Enable gyroscope.</param>
        /// <param name="enableMag">Enable magnetometer.</param>
        /// <param name="enablePressureTemp">Enable pressure/temperature.</param>
        /// <param name="enableBatteryVoltage">Enable battery voltage sensing.</param>
        /// <param name="enableExtA6">Enable external ADC A6.</param>
        /// <param name="enableExtA7">Enable external ADC A7.</param>
        /// <param name="enableExtA15">Enable external ADC A15.</param>
        /// <param name="enableExg">Enable EXG (ECG/EMG) module.</param>
        /// <param name="exgMode">EXG operating mode.</param>
        public void ConfigureWindows(
            string deviceName,
            string comPort,
            bool enableLowNoiseAcc,
            bool enableWideRangeAcc,
            bool enableGyro,
            bool enableMag,
            bool enablePressureTemp,
            bool enableBatteryVoltage,
            bool enableExtA6,
            bool enableExtA7,
            bool enableExtA15,
            bool enableExg,
            ExgMode exgMode
        )
        {
            _reconfigInProgress = true;
            try
            {

                // Detach & dispose previous instance to avoid duplicate event handlers or stale streams
                if (shimmer != null)
                {
                    try 
                    { 
                        shimmer.UICallback -= this.HandleEvent; 
                    } 
                    catch 
                    {
                    }

                    try 
                    { 
                        shimmer.StopStreaming(); 
                    } 
                    catch 
                    {
                    }

                    try 
                    { 
                        shimmer.Disconnect(); 
                    } 
                    catch 
                    { 
                    }

                    shimmer = null;
                }

                // Persist sensor flags
                _enableLowNoiseAccelerometer = enableLowNoiseAcc;
                _enableWideRangeAccelerometer = enableWideRangeAcc;
                _enableGyroscope = enableGyro;
                _enableMagnetometer = enableMag;
                _enablePressureTemperature = enablePressureTemp;
                _enableBatteryVoltage = enableBatteryVoltage;
                _enableExtA6 = enableExtA6;
                _enableExtA7 = enableExtA7;
                _enableExtA15 = enableExtA15;
                _enableExg = enableExg;
                _exgMode = exgMode;

                // Rebuild sensor bitmap
                int enabled = 0;
                if (_enableLowNoiseAccelerometer)
                    enabled |= (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_A_ACCEL;
                if (_enableWideRangeAccelerometer)
                    enabled |= (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_D_ACCEL;
                if (_enableGyroscope)
                    enabled |= (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_MPU9150_GYRO;
                if (_enableMagnetometer)
                    enabled |= (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_LSM303DLHC_MAG;
                if (_enablePressureTemperature)
                    enabled |= (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_BMP180_PRESSURE;
                if (_enableBatteryVoltage)
                    enabled |= (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_VBATT;
                if (_enableExtA6)
                    enabled |= (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_EXT_A6;
                if (_enableExtA7)
                    enabled |= (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_EXT_A7;
                if (_enableExtA15)
                    enabled |= (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_EXT_A15;
                if (_enableExg)
                {
                    enabled |= (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_EXG1_24BIT;
                    enabled |= (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_EXG2_24BIT;
                }

                _winEnabledSensors = enabled;

                // Force index remapping on next packet
                firstDataPacket = true;

                // Create new driver instance and subscribe exactly once
                shimmer = new ShimmerLogAndStreamSystemSerialPortV2(deviceName, comPort);
                try 
                { 
                    shimmer.UICallback -= this.HandleEvent; 
                } 
                catch 
                {
                }
                shimmer.UICallback += this.HandleEvent;
            }
            finally
            {
                _reconfigInProgress = false;
            }
        }


        /// <summary>
        /// Null-safe accessor for a signal in an <see cref="ObjectCluster"/>:
        /// returns the <see cref="SensorData"/> at <paramref name="idx"/> if the index is valid,
        /// otherwise <c>null</c>.
        /// </summary>
        /// <param name="oc">Source <see cref="ObjectCluster"/>.</param>
        /// <param name="idx">Signal index; if negative, no lookup is performed.</param>
        /// <returns><see cref="SensorData"/> when available; otherwise <c>null</c>.</returns>
        private static SensorData? GetSafe(ObjectCluster oc, int idx)
            => idx >= 0 ? oc.GetData(idx) : null;


        /// <summary>
        /// Best-effort resolver that searches a signal by multiple candidate names and formats.
        /// Tries CAL → RAW → UNCAL, then falls back to a format-agnostic lookup.
        /// Returns the first match as a tuple (index, matchedName, matchedFormat);
        /// returns (-1, null, null) when not found.
        /// </summary>
        /// <param name="oc">Source <see cref="ObjectCluster"/>.</param>
        /// <param name="names">Ordered list of candidate signal names to try.</param>
        /// <returns>Tuple (idx, name, fmt) describing the match, or (-1, null, null) if none.</returns>
        private static (int idx, string? name, string? fmt) FindSignal(ObjectCluster oc, string[] names)
        {

            // Preferred explicit formats in descending priority
            string[] formats = new[]
            {
                ShimmerConfiguration.SignalFormats.CAL,
                "RAW",
                "UNCAL"
            };

            // Try each candidate name across CAL/RAW/UNCAL
            foreach (var f in formats)
                foreach (var n in names)
                {
                    int i = oc.GetIndex(n, f);
                    if (i >= 0) return (i, n, f);
                }
            foreach (var n in names)
            {
                int i = oc.GetIndex(n, null);
                if (i >= 0) return (i, n, null);
            }

            // No match found
            return (-1, null, null);
        }


        /// <summary>
        /// Windows event handler: on DATA_PACKET it maps CAL indices (first packet only),
        /// resolves EXG channels by trying multiple possible signal names, builds a snapshot,
        /// and notifies subscribers via <see cref="SampleReceived"/>.
        /// </summary>
        /// <param name="sender">Driver instance raising the event.</param>
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

                    var ch1 = FindSignal(oc, new[]
                    {
                        "EXG_CH1",
                        Shimmer3Configuration.SignalNames.EXG1_CH1,
                        "EXG1_CH1","EXG1 CH1","EXG CH1",
                        "ECG_CH1","ECG CH1","EMG_CH1","EMG CH1",
                        "ECG RA-LL","ECG LL-RA","ECG_RA-LL","ECG_LL-RA"
                    });
                    var ch2 = FindSignal(oc, new[]
                    {
                        "EXG_CH2",
                        Shimmer3Configuration.SignalNames.EXG2_CH1,
                        "EXG2_CH1","EXG2 CH1","EXG CH2",
                        "ECG_CH2","ECG CH2","EMG_CH2","EMG CH2",
                        "ECG LA-RA","ECG_RA-LA","ECG_LA-RA"
                    });

                    // Cache indices for fast retrieval on subsequent packets
                    indexExg1 = ch1.idx;
                    indexExg2 = ch2.idx;

                    firstDataPacket = false;
                }

                // Build snapshot with CAL values (null-safe via GetSafe)
                var latest = new ShimmerSDK_EXGData(
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
                    GetSafe(oc, indexExtA15),

                    // Two EXG traces are exposed when EXG is enabled; otherwise null
                    _enableExg ? GetSafe(oc, indexExg1) : null,
                    _enableExg ? GetSafe(oc, indexExg2) : null

                );

                // Notify subscribers with the latest parsed sample
                try 
                { 
                    SampleReceived?.Invoke(this, latest); 
                } 
                catch 
                {
                }
            }
        }

#endif


#if ANDROID

        /// <summary>
        /// Configures the Android backend for EXG/IMU: persists flags, validates the MAC,
        /// builds the sensor bitmap (incl. EXG), wires callbacks, and resets index mapping.
        /// </summary>
        /// <param name="deviceId">Logical device name shown by the SDK.</param>
        /// <param name="mac">Bluetooth MAC address (validated).</param>
        /// <param name="enableLowNoiseAcc">Enable low-noise accelerometer.</param>
        /// <param name="enableWideRangeAcc">Enable wide-range accelerometer.</param>
        /// <param name="enableGyro">Enable gyroscope.</param>
        /// <param name="enableMag">Enable magnetometer.</param>
        /// <param name="enablePressureTemp">Enable pressure/temperature sensor.</param>
        /// <param name="enableBatteryVoltage">Enable battery voltage channel.</param>
        /// <param name="enableExtA6">Enable external ADC A6.</param>
        /// <param name="enableExtA7">Enable external ADC A7.</param>
        /// <param name="enableExtA15">Enable external ADC A15.</param>
        /// <param name="enableExg">Enable EXG (EXG1/EXG2 24-bit blocks).</param>
        /// <param name="exgMode">EXG operating mode (ECG/EMG/Test/Respiration).</param>
        /// <exception cref="ArgumentException">Thrown if <paramref name="mac"/> is not a valid Bluetooth address.</exception>
        public void ConfigureAndroid(
            string deviceId,
            string mac,
            bool enableLowNoiseAcc,
            bool enableWideRangeAcc,
            bool enableGyro,
            bool enableMag,
            bool enablePressureTemp,
            bool enableBatteryVoltage,
            bool enableExtA6,
            bool enableExtA7,
            bool enableExtA15,
            bool enableExg,
            ExgMode exgMode
        )
        {

            // Persist sensor flags
            _enableLowNoiseAccelerometer = enableLowNoiseAcc;
            _enableWideRangeAccelerometer = enableWideRangeAcc;
            _enableGyroscope = enableGyro;
            _enableMagnetometer = enableMag;
            _enablePressureTemperature = enablePressureTemp;
            _enableBatteryVoltage = enableBatteryVoltage;
            _enableExtA6 = enableExtA6;
            _enableExtA7 = enableExtA7;
            _enableExtA15 = enableExtA15;
            _enableExg = enableExg;
            _exgMode = exgMode;

            // Validate MAC 
            mac = (mac ?? "").Trim();
            if (!global::Android.Bluetooth.BluetoothAdapter.CheckBluetoothAddress(mac))
                throw new ArgumentException($"Invalid MAC '{mac}'");

            // Build sensor bitmap
            int sensors = 0;
            if (_enableLowNoiseAccelerometer) sensors |= (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_A_ACCEL;
            if (_enableWideRangeAccelerometer) sensors |= (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_D_ACCEL;
            if (_enableGyroscope)              sensors |= (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_MPU9150_GYRO;
            if (_enableMagnetometer)           sensors |= (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_LSM303DLHC_MAG;
            if (_enablePressureTemperature)    sensors |= (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_BMP180_PRESSURE;
            if (_enableBatteryVoltage)         sensors |= (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_VBATT;
            if (_enableExtA6)                  sensors |= (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_EXT_A6;
            if (_enableExtA7)                  sensors |= (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_EXT_A7;
            if (_enableExtA15)                 sensors |= (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_EXT_A15;
            if (_enableExg)
            {
                sensors |= (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_EXG1_24BIT;
                sensors |= (int)ShimmerBluetooth.SensorBitmapShimmer3.SENSOR_EXG2_24BIT;
            }

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
            if (shimmerAndroid == null) throw new InvalidOperationException("ConfigureAndroid was not called");

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
                _androidStreamingAckTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                _androidFirstPacketTcs  = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

                firstDataPacketAndroid = true;
                shimmerAndroid.StartStreaming();

                await Task.WhenAny(_androidStreamingAckTcs.Task, Task.Delay(2000));
                await Task.WhenAny(_androidFirstPacketTcs.Task,  Task.Delay(2000));
            }

            return SamplingRate;    // Effective quantized value
        }


        /// <summary>
        /// Null-safe accessor: returns the SensorData at the given index or null if the index is invalid.
        /// </summary>
        /// <param name="oc">Source ObjectCluster.</param>
        /// <param name="idx">Signal index within the cluster (use -1 to indicate “not found”).</param>
        /// <returns>The SensorData at <paramref name="idx"/>; otherwise null.</returns>
        private static SensorData? GetSafeA(ObjectCluster oc, int idx) => idx >= 0 ? oc.GetData(idx) : null;


        /// <summary>
        /// Searches for a signal by trying multiple names across formats (CAL → RAW → UNCAL → default).
        /// </summary>
        /// <param name="oc">Source ObjectCluster to search.</param>
        /// <param name="names">Candidate signal names to probe, in priority order.</param>
        /// <returns>
        /// A tuple with:
        /// <list type="bullet">
        /// <item><description><c>idx</c>: the found index or -1 if not found.</description></item>
        /// <item><description><c>name</c>: the resolved signal name (or null).</description></item>
        /// <item><description><c>fmt</c>: the resolved format (e.g., "CAL", "RAW", "UNCAL", or null).</description></item>
        /// </list>
        /// </returns>
        private static (int idx, string? name, string? fmt) FindSignalA(ObjectCluster oc, string[] names)
        {
            string[] formats = new[] { ShimmerConfiguration.SignalFormats.CAL, "RAW", "UNCAL" };
            foreach (var f in formats)
                foreach (var n in names)
                {
                    int i = oc.GetIndex(n, f);
                    if (i >= 0) return (i, n, f);
                }
            foreach (var n in names)
            {
                int i = oc.GetIndex(n, null);
                if (i >= 0) return (i, n, null);
            }
            return (-1, null, null);
        }


        /// <summary>
        /// Android event sink for EXG: routes state changes, maps CAL indices on the first data packet,
        /// builds the latest sample, and notifies subscribers.
        /// </summary>
        /// <param name="sender">Event source (Android transport).</param>
        /// <param name="args">Event payload (expected <see cref="CustomEventArgs"/>).</param>
        private void HandleEventAndroid(object? sender, EventArgs args)
        {
            try
            {

                // Ensure the expected payload type
                if (args is not CustomEventArgs ev) return;

                int indicator = ev.getIndicator();

                // Fast path: unblock waiters when streaming state is confirmed
                if (indicator == (int)ShimmerBluetooth.ShimmerIdentifier.MSG_IDENTIFIER_STATE_CHANGE)
                {
                    if (ev.getObject() is int st)
                    {
                        if (st == ShimmerBluetooth.SHIMMER_STATE_CONNECTED)
                        {
                            _androidConnectedTcs?.TrySetResult(true);
                            _androidIsStreaming = false;           // connected but not streaming
                        }
                        else if (st == ShimmerBluetooth.SHIMMER_STATE_STREAMING)
                        {
                            _androidStreamingAckTcs?.TrySetResult(true);
                            _androidIsStreaming = true;
                            firstDataPacketAndroid = true;        // remap indices on resume
                        }
                        else
                        {
                            _androidIsStreaming = false;
                        }
                    }
                    return;
                }

                if (indicator != (int)ShimmerBluetooth.ShimmerIdentifier.MSG_IDENTIFIER_DATA_PACKET)
                    return;

                // Extract payload and guard for nulls.
                var oc = ev.getObject() as ObjectCluster;
                if (oc == null) return;

                // Unblock any waiter for the first packet.
                _androidFirstPacketTcs?.TrySetResult(true);

                // First packet: resolve and cache all CAL indices once.
                if (firstDataPacketAndroid)
                {
                    // IMU & miscellaneous (CAL)
                    indexTimeStamp         = oc.GetIndex(ShimmerConfiguration.SignalNames.SYSTEM_TIMESTAMP,          ShimmerConfiguration.SignalFormats.CAL);
                    indexLowNoiseAccX      = oc.GetIndex(Shimmer3Configuration.SignalNames.LOW_NOISE_ACCELEROMETER_X, ShimmerConfiguration.SignalFormats.CAL);
                    indexLowNoiseAccY      = oc.GetIndex(Shimmer3Configuration.SignalNames.LOW_NOISE_ACCELEROMETER_Y, ShimmerConfiguration.SignalFormats.CAL);
                    indexLowNoiseAccZ      = oc.GetIndex(Shimmer3Configuration.SignalNames.LOW_NOISE_ACCELEROMETER_Z, ShimmerConfiguration.SignalFormats.CAL);
                    indexWideAccX          = oc.GetIndex(Shimmer3Configuration.SignalNames.WIDE_RANGE_ACCELEROMETER_X, ShimmerConfiguration.SignalFormats.CAL);
                    indexWideAccY          = oc.GetIndex(Shimmer3Configuration.SignalNames.WIDE_RANGE_ACCELEROMETER_Y, ShimmerConfiguration.SignalFormats.CAL);
                    indexWideAccZ          = oc.GetIndex(Shimmer3Configuration.SignalNames.WIDE_RANGE_ACCELEROMETER_Z, ShimmerConfiguration.SignalFormats.CAL);
                    indexGyroX             = oc.GetIndex(Shimmer3Configuration.SignalNames.GYROSCOPE_X,              ShimmerConfiguration.SignalFormats.CAL);
                    indexGyroY             = oc.GetIndex(Shimmer3Configuration.SignalNames.GYROSCOPE_Y,              ShimmerConfiguration.SignalFormats.CAL);
                    indexGyroZ             = oc.GetIndex(Shimmer3Configuration.SignalNames.GYROSCOPE_Z,              ShimmerConfiguration.SignalFormats.CAL);
                    indexMagX              = oc.GetIndex(Shimmer3Configuration.SignalNames.MAGNETOMETER_X,           ShimmerConfiguration.SignalFormats.CAL);
                    indexMagY              = oc.GetIndex(Shimmer3Configuration.SignalNames.MAGNETOMETER_Y,           ShimmerConfiguration.SignalFormats.CAL);
                    indexMagZ              = oc.GetIndex(Shimmer3Configuration.SignalNames.MAGNETOMETER_Z,           ShimmerConfiguration.SignalFormats.CAL);
                    indexBMP180Temperature = oc.GetIndex(Shimmer3Configuration.SignalNames.TEMPERATURE,              ShimmerConfiguration.SignalFormats.CAL);
                    indexBMP180Pressure    = oc.GetIndex(Shimmer3Configuration.SignalNames.PRESSURE,                 ShimmerConfiguration.SignalFormats.CAL);
                    indexBatteryVoltage    = oc.GetIndex(Shimmer3Configuration.SignalNames.V_SENSE_BATT,             ShimmerConfiguration.SignalFormats.CAL);
                    indexExtA6             = oc.GetIndex(Shimmer3Configuration.SignalNames.EXTERNAL_ADC_A6,          ShimmerConfiguration.SignalFormats.CAL);
                    indexExtA7             = oc.GetIndex(Shimmer3Configuration.SignalNames.EXTERNAL_ADC_A7,          ShimmerConfiguration.SignalFormats.CAL);
                    indexExtA15            = oc.GetIndex(Shimmer3Configuration.SignalNames.EXTERNAL_ADC_A15,         ShimmerConfiguration.SignalFormats.CAL);

                    var ch1 = FindSignalA(oc, new[]
                    {
                        "EXG_CH1", Shimmer3Configuration.SignalNames.EXG1_CH1, "EXG1_CH1", "EXG1 CH1", "EXG CH1",
                        "ECG_CH1", "ECG CH1", "EMG_CH1", "EMG CH1",
                        "ECG RA-LL", "ECG LL-RA", "ECG_RA-LL", "ECG_LL-RA"
                    });
                    var ch2 = FindSignalA(oc, new[]
                    {
                        "EXG_CH2", Shimmer3Configuration.SignalNames.EXG2_CH1, "EXG2_CH1", "EXG2 CH1", "EXG CH2",
                        "ECG_CH2", "ECG CH2", "EMG_CH2", "EMG CH2",
                        "ECG LA-RA", "ECG_RA-LA", "ECG_LA-RA"
                    });

                    indexExg1     = ch1.idx;
                    indexExg2     = ch2.idx;

                    firstDataPacketAndroid = false;
                }

                // Build the latest snapshot with CAL values (null-safe).
                var latest = new ShimmerSDK_EXGData(
                    GetSafeA(oc, indexTimeStamp),
                    GetSafeA(oc, indexLowNoiseAccX),
                    GetSafeA(oc, indexLowNoiseAccY),
                    GetSafeA(oc, indexLowNoiseAccZ),
                    GetSafeA(oc, indexWideAccX),
                    GetSafeA(oc, indexWideAccY),
                    GetSafeA(oc, indexWideAccZ),
                    GetSafeA(oc, indexGyroX),
                    GetSafeA(oc, indexGyroY),
                    GetSafeA(oc, indexGyroZ),
                    GetSafeA(oc, indexMagX),
                    GetSafeA(oc, indexMagY),
                    GetSafeA(oc, indexMagZ),
                    GetSafeA(oc, indexBMP180Temperature),
                    GetSafeA(oc, indexBMP180Pressure),
                    GetSafeA(oc, indexBatteryVoltage),
                    GetSafeA(oc, indexExtA6),
                    GetSafeA(oc, indexExtA7),
                    GetSafeA(oc, indexExtA15),

                    // EXG (only when enabled)
                    _enableExg ? GetSafeA(oc, indexExg1) : null,
                    _enableExg ? GetSafeA(oc, indexExg2) : null
                );

                // Notify subscribers with the latest parsed sample.
                try 
                {
                    SampleReceived?.Invoke(this, latest); 
                }
                catch 
                {
                }
            }

            catch 
            {
            }
        }

#endif


    }
}
