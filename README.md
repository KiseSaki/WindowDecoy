# WindowDecoy (Win伪装)

> 一款基于 WPF (.NET 10) 开发的 Windows 窗口伪装与防窥保护工具。

![Platform](https://img.shields.io/badge/Platform-Windows-blue.svg)
![Target](https://img.shields.io/badge/.NET-10.0--windows-purple.svg)
![Language](https://img.shields.io/badge/Language-C%23%2013-green.svg)
![License](https://img.shields.io/badge/License-MIT-orange.svg)

---

## 📖 项目简介

**WindowDecoy** 专为保护桌面隐私与防窥设计。在需要临时离开座位、他人靠近或需要快速隐藏敏感内容时，WindowDecoy 能够通过全局快捷键迅速将目标窗口进行伪装（替换虚假标题、替换窗口图标或覆盖伪装画面/假截图），并在需要时一键无缝还原。

---

## ✨ 核心特性

- 🎯 **灵活的窗口选择**
  - 内置窗口/进程选择器（ProcessPicker），快速抓取当前运行的程序与可视窗口。
- 🎭 **多重伪装方案**
  - **标题替换**：自定义显示伪装文本（如各类工作软件、系统设置等）。
  - **图标替换**：支持从可执行文件（.exe）、图标文件（.ico）等提取并替换目标窗口图标。
  - **伪装背景/截图**：支持配置伪装图片或静态工作界面截图覆盖。
- ⚡ **全局快捷键响应**
  - **一键激活伪装**：默认快捷键 `Ctrl + Alt + Q`。
  - **一键恢复原状**：默认快捷键 `Ctrl + Alt + \``。
  - 支持在主界面实时修改并动态重新注册热键。
- 🛡️ **完备的故障安全（Fail-Safe）保护**
  - **异常自动还原**：若程序发生未捕获异常或主调度器异常，自动触发紧急还原，避免目标窗口遗留假态。
  - **系统关机/注销保护**：监听到系统会话结束信号时自动释放所有伪装。
  - **安全退出**：软件正常退出或强制退出时确保所有被控窗口恢复原始状态。
- 📌 **系统托盘与静默运行**
  - 支持关闭窗口时最小化到系统托盘，后台持续响应全局快捷键。
  - 托盘右键菜单支持一键还原所有窗口、唤出主界面或退出程序。

---

## 🛠️ 技术栈

- **框架**：.NET 10.0 (WPF - Windows Presentation Foundation)
- **语言**：C# 13 (Nullable & ImplicitUsings 开启)
- **底层技术**：Win32 P/Invoke API (`user32.dll`, `kernel32.dll`, `dwmapi.dll` 等)
  - 窗口句柄枚举与状态保存 (`EnumWindows`, `GetWindowText`, `GetWindowLongPtr`)
  - 窗口样式与属性动态调整 (`SetWindowLongPtr`, `SetWindowPos`, `SetWindowText`)
  - 全局热键注册与消息泵循环 (`RegisterHotKey`, `UnregisterHotKey`, `HwndSource`)
  - 图标提取与内存图像转换 (`ExtractIconEx`, `GetIconInfo`)

---

## 📁 目录结构

```
Win伪装/
├── Assets/                 # 静态资源文件（程序图标等）
├── Converters/             # WPF XAML 数据转换器
├── Interop/                # Win32 原生 API P/Invoke 签名与结构体定义
├── Models/                 # 数据模型（会话配置、窗口状态、枚举等）
├── Services/               # 核心服务（DecoyManager, HotkeyManager, WindowController等）
├── ViewModels/             # MVVM 视图模型（MainViewModel, RelayCommand等）
├── Views/                  # WPF 界面（MainWindow, DecoyWindow, ProcessPickerWindow）
├── App.xaml / App.xaml.cs  # 应用程序入口与全局容灾逻辑
├── app.manifest            # Windows DPI 识别与权限声明清单
├── WindowDecoy.csproj      # 项目配置文件 (.NET 10.0-windows)
├── .gitattributes          # Git 属性配置
├── .gitignore              # Git 忽略文件规则
├── LICENSE                 # 开源许可证 (MIT)
└── README.md               # 项目说明文档
```

---

## 🚀 快速开始

### 前置要求

- **操作系统**：Windows 10 / 11 64位
- **开发运行环境**：[.NET 10.0 SDK](https://dotnet.microsoft.com/)

### 编译与运行

1. 克隆或下载代码到本地：
   ```bash
   git clone <repository-url>
   cd Win伪装
   ```

2. 恢复依赖并编译项目：
   ```bash
   dotnet build -c Release
   ```

3. 运行应用程序：
   ```bash
   dotnet run -c Release
   ```

4. 或发布为单文件可执行程序：
   ```bash
   dotnet publish -c Release -r win-x64 --self-contained false -o ./Output
   ```

---

## ⌨️ 快捷键说明

| 功能 | 默认快捷键 | 说明 |
| :--- | :--- | :--- |
| **激活伪装** | `Ctrl + Alt + Q` | 立即激活已配置的窗口伪装模式 |
| **恢复原状** | `Ctrl + Alt + \`` | 立即还原所有窗口至正常状态 |

> 💡 可以在程序主界面中根据个人使用习惯自由配置快捷键组合。

---

## 📄 开源许可证

本项目采用 [MIT License](LICENSE) 授权开源。
