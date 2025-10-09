# SpeedTestWidget  

[![Donate via PayPal](https://img.shields.io/badge/Donate-PayPal-blue?style=flat-square&logo=paypal)](https://paypal.me/MSuhaidiM)  

A lightweight desktop widget that runs speed tests (download/upload) using the **ndt7** protocol.  

---

## 🚀 Features

- Real-time network speed measurement (download + upload)  
- Uses **ndt7** server for accurate results  
- Minimal UI — always visible in a small window  
- Easy to integrate / embed into your workflow  

---

## 🛠️ Getting Started

### Prerequisites  
- Windows OS 

### Installation  
1. Clone this repository  
2. Build using Visual Studio or via CLI (`dotnet build`)  
3. Run the executable — a widget window will appear showing the speed results  

### Configuration  
- You can configure the ndt7 server endpoint in `Ndt7Client.cs`  
- (Optional) Adjust widget appearance via the XAML / UI files  

---

## 📂 Project Structure

| Folder / File                  | Purpose |
|-------------------------------|---------|
| `WidgetWindow.xaml` / `.cs`   | UI layout & window logic |
| `Ndt7Client.*`                 | Core logic for communicating with ndt7 servers |
| `DatabaseHelper.cs`           | Local storage / logging (if enabled) |
| `JsonMessageLogger.cs`         | Logging of JSON data from ndt7 servers|
| `SecureStorage.cs`              | Encrypted result storage |
| `.gitignore`, `LICENSE`, etc.  | Standard project files |

---

## 🧪 Usage Example

- Download the exe file from release page and run on your computer.

---

## 📜 License

This project is licensed under the **MIT License** — see [LICENSE](LICENSE) for details.

---

## 🙏 Support / Donations

If you find this useful and want to support continued development, you can donate via PayPal: [paypal.me/MSuhaidiM](https://paypal.me/MSuhaidiM)

---

## 🔍 Further Improvements (To-Do Ideas)

- Auto-run at system startup  
- Option to periodically retest  
- More configurable UI themes / skins  
- Exporting test logs (CSV, charting)  
- Support for other speed test protocols
