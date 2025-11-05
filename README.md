# ShimmerMobileApp

A cross-platform **.NET MAUI** app that visualizes live data from **Shimmer3** sensors.  
It runs on **Android, iOS, Mac (MacCatalyst), and Windows** and can consume data either:

1. **Directly on Android or Windows** or  
2. **From iOS/Mac** over **WebSocket** using the companion bridge:
   **BridgeForMacAndIOS** -> https://github.com/tatianadalbuscoo/BridgeForMacAndIOS

> **Why the bridge?** iOS/MacCatalyst apps only expose CoreBluetooth (BLE/GATT). Shimmer3 uses Bluetooth Classic (SPP).  
> The Android bridge connects to Shimmer over SPP and republishes the stream over WebSocket on your LAN, so this app can reuse the same UI/business logic on iOS/Mac.

---

## Features

- Live streaming from **Shimmer3**
  - **Android**: Bluetooth Classic (**SPP/RFCOMM**)
  - **Windows**: Bluetooth-serial **COM port** (SPP/RFCOMM)
  - **iOS/Mac**: **WebSocket** via Android bridge
- **Multi-device:** connect **multiple Shimmer devices** and switch between them (one device displayed at a time via per-device pages/tabs)
- Real-time charts for multiple sensors (Accelerometers, Gyroscope, Magnetometer, Battery, etc.)
- Runtime configuration of sensors and sample rate
- Single codebase for Android, iOS, MacCatalyst, and Windows
- - **Y-axis control:** automatic scaling or manual range selection per series
- **Axis layouts:** show accelerometer **X/Y/Z** on a single combined chart **or** split into **three separate charts**
- **Sensor families:** supports both **IMU** (e.g., Accel/Gyro/Mag) and **EXG** (e.g., EMG/ECG) streams
- **Label cadence:** configurable X-axis label interval (e.g., every *n* samples or seconds)
- **Time window:** adjustable X-axis duration (e.g., show last *N* seconds)

---

## Prerequisites

- **.NET 8 SDK**
- **.NET workloads**
  ```bash
  dotnet workload install android
  dotnet workload install maui
  ```
- **Git**
- **For iOS/Mac**: a running **Android Bridge** on the same network  
  -> https://github.com/tatianadalbuscoo/BridgeForMacAndIOS

---

## Getting Started

---

Clone and restore:
```bash
git clone https://github.com/tatianadalbuscoo/ShimmerMobileApp
dotnet restore
```

---

#### Where to set **iOS/Mac Bridge IP**

---


Only iOS/Mac need an IP. In this repo it’s defined **in code**:

**File:** `ShimmerInterface/App.xaml.cs` (inside the `#if IOS || MACCATALYST` block)

```csharp
// iOS/macOS bridge endpoint (edit these)
const string BridgeHost = "172.20.10.2"; // <- change to your Android bridge IP
const int    BridgePort = 8787;          // <- change if you use a non-default port
const string BridgePath = "/";           // <- change only if your WS server uses a path

---

## Run the app

### Android (device or emulator)
- Pair **Shimmer3** in **Android Bluetooth**.
- Then run the command:
```bash
dotnet build -t:Run -f net8.0-android -c Debug
```
Or use your IDE (Visual Studio 2022 / VS Code with C# Dev Kit).

> On a physical device, enable **Developer options → USB debugging** and accept the **RSA fingerprint**.  

### iOS / Mac (MacCatalyst)
- Ensure the **Android Bridge** is running and reachable.
- set Android Bridge IP (see above where to change in the App.xaml.cs file)
- Build & run the **iOS** or **MacCatalyst** target from your IDE.
  - **On a Mac:** use Xcode with the .NET workloads installed.
  - **From Windows:** use **Visual Studio 2022** with **Hot Restart** to deploy to a **physical iOS device** without a Mac.  
  Requires a **paid Apple Developer account**.
  - **macOS/MacCatalyst from Windows via VM:** you can use a **macOS Sequoia** VM with **VMware**.  

### Windows
- Pair **Shimmer3** in **Windows Bluetooth**.
- Use your IDE (Visual Studio 2022 / VS Code with C# Dev Kit)

## Run Tests

Use the provided command to execute the test suite:
```bash
dotnet test tests/tests.csproj -c Debug
```
