# SpeedTestWidget  

[![GitHub Sponsors](https://img.shields.io/github/sponsors/MSuhaidi?style=plastic&logo=githubsponsors&color=EA4AAA&logoSize=auto&label=Sponsor%20Me)](https://github.com/sponsors/MSuhaidi)
[![Donate via PayPal](https://img.shields.io/badge/Support_Me-PayPal-002991?style=plastic&logo=paypal&logoSize=auto)](https://paypal.me/MSuhaidiM)
![GitHub top language](https://img.shields.io/github/languages/top/MSuhaidi/SpeedTestWidget?style=plastic)
[![GitHub contributors](https://img.shields.io/github/contributors/Msuhaidi/SpeedTestWidget?style=social&logo=refinedgithub&color=9E95B7)](https://github.com/MSuhaidi/SpeedTestWidget)


A lightweight desktop widget that runs speed tests (download/upload) using the **ndt7** protocol.  

---

## 🚀 Features

- Real-time measurement of download and upload speeds  
- Uses official **ndt7** servers for accurate benchmarking  
- Clean, minimal, always-on-top interface  
- Lightweight and resource-efficient  
- Optional data logging for performance analysis 

---

## 🛠️ Getting Started

### Prerequisites  
- **Windows OS**
- **.NET 8.0 Runtime**

### Installation  
1. Clone this repository  
2. Build using **Visual Studio** or run `dotnet build`  
3. Run the compiled executable — the widget will start and perform a speed test automatically  

### Configuration  
- Configure the ndt7 server endpoint in `Ndt7Client.cs`  
- (Optional) Customize the interface through **Skins**  
- (Optional) Bundle the .NET runtime for standalone distribution (see `SpeedTestWidget.csproj` for an example)

---

## 📂 Project Structure

| File / Folder                | Description |
|------------------------------|-------------|
| `WidgetWindow.xaml` / `.cs`  | UI layout and widget logic |
| `Ndt7Client.*`               | Handles ndt7 communication |
| `DatabaseHelper.cs`          | Local storage and history logging |
| `JsonMessageLogger.cs`       | JSON-based logging of ndt7 test results |
| `SecureStorage.cs`           | Secure storage for sensitive configuration |
| `.gitignore`, `LICENSE`, etc.| Supporting project files |

---

## 🧪 Usage Example

- Download the `.exe` file from the [Releases](../../releases) page.  
- Run it directly on your computer — the widget will appear automatically.

---

## 🧭 Version History

| Version | Release Date | Highlights |
|--------|--------------|------------|
| **1.0.1** | 2025-10-16 | • Code cleanup <br> • Removed bundled runtime |
| **1.0.0** | 2025-10-16 | • Initial stable release |

---

## 🔍 Further Improvements (To-Do Ideas)

- Auto-run at system startup  
- Option to periodically retest  
- More configurable UI themes / skins  
- Exporting test logs (CSV, charting)  
- Support for other speed test protocols

---

## 📜 License

This project is licensed under the **MIT License** — see [LICENSE](LICENSE) for details.

---

## 🙏 Support & Sponsorship

If this project helps you, please consider supporting development:

- 💸 [Donate via PayPal](https://paypal.me/MSuhaidiM)  
- 🌟 [Sponsor on GitHub](https://github.com/sponsors/MSuhaidi)

---

**Repository:** [github.com/MSuhaidi/SpeedTestWidget](https://github.com/MSuhaidi/SpeedTestWidget)
