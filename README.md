# TCA 3.8.0.1 简体中文汉化

本汉化以 [allista 官方 v3.8.0.1](https://github.com/allista/ThrottleControlledAvionics/tree/v3.8.0.1) 为功能基础，覆盖主界面、编辑器、HUD、宏、科研模块及游戏内手册。中文缺失时回退到英文；内部 ID、用户自定义名称及单位保留原样。汉化与构建检查不代表所有飞行控制及自动驾驶场景均已通过实飞测试。

感谢社区译者 **Aebestach** 及 [Aebestach/ThrottleControlledAvionics](https://github.com/Aebestach/ThrottleControlledAvionics) 项目的本地化工作。本版本复用了其 [本地化提交 `8e769dcd653b2864054642df3692ec789bd2aed4`](https://github.com/Aebestach/ThrottleControlledAvionics/commit/8e769dcd653b2864054642df3692ec789bd2aed4)，并补充翻译与显示适配。项目及本汉化遵循 [CC BY-SA 4.0](https://creativecommons.org/licenses/by-sa/4.0/)；原作者、贡献者署名及版权原文保留于下方和源码中。

## 构建与验证

`build.ps1` 使用本机 KSP 1.12.5 与已安装 AT-Utils 的程序集编译官方基线和汉化版本，并生成 `build/TCA-3.8.0.1-zh-CN.1.zip`。`verify.ps1` 检查双语键、格式占位符、翻译引用、公开接口、航点坐标绑定及基线差异；这类检查不能替代实际游戏中的界面检查与测试飞行。两个脚本默认使用 `D:\SteamLibrary\steamapps\common\Kerbal Space Program`，也可通过 `-KspPath` 指定安装路径。

源码仓库中的 `GameData/ThrottleControlledAvionics/tca_ui.bundle` 是指向其他仓库资源的符号链接，在部分 Windows 检出中会表现为 **50 字节的链接文本**。制作发行包时，必须保留或使用现装官方 3.8.0.1 的真实 `tca_ui.bundle` 资源文件，不能用这段链接文本覆盖它。

## 安装

退出游戏后再覆盖 `GameData/ThrottleControlledAvionics`。安装前保留用户的 `config.xml`、`TCA.conf`、全局宏文件 `TCA.macro` 及存档中的宏与配置，并保留上述官方真实 `tca_ui.bundle`。确认 ModuleManager 和兼容的 AT-Utils 已安装。

---

#Throttle Controlled Avionics

*Originally created by
[qfeys](http://forum.kerbalspaceprogram.com/members/45099-qfeys) and
[Zenka](http://forum.kerbalspaceprogram.com/members/108134-Zenka)*

**Current development and maintenance:
[allista](http://forum.kerbalspaceprogram.com/members/102693-allista)**

***

##Introduction

TCA is a plugin that enhances the attitude control of a ship by dynamically changing the output of its engines and RCS thrusters. Even with most unbalanced designs TCA maintains stable flight and quick precise response to user or autopilot input. TCA also provides many sophisticated autopilot programs based on this core functionality, including waypoint navigation and automatic landing. It is also fast enough to simultaneously control a whole squadron of ships without much overhead.

##Features

* Functionality:
    * Simultaneous control over multiple vessels.
    * Realtime RCS and engine balancing.
    * Multiple engine roles and engine profiles.
    * Full support of slow engines (jets, propellers, etc.)
    * Thrust attitude control.
    * Hovering control.
    * Many sophisticated autopilot programs.
    * Orbital maneuvering independent of command module orientation.
    * User defined macros.    
* Technical:
    * Both the Toolbar and the stock AppLauncher are supported.
    * Per-vessel/per-save configuration profiles and custom presets.
    * In-game configurable key binding.
    * Full career mode integration.

##[TCA Manual](https://github.com/qfeys/ThrottleControlledAvionics/blob/master/GameData/ThrottleControlledAvionics/INSTRUCTIONS.md)
    
##Downloads and Installation

* **[SpaceDock](http://spacedock.info/mod/198/Throttle%20Controlled%20Avionics)**
* [GitHub](https://github.com/qfeys/ThrottleControlledAvionics/releases)
* [Source code](https://github.com/qfeys/ThrottleControlledAvionics)

**TCA requires [ModuleManager](http://forum.kerbalspaceprogram.com/index.php?/topic/50533-105-module-manager) to work.**

If you're upgrading, **DELETE** the old version before installing a new one. **BUT** do not delete the **TCA.conf** and **config.xml** files to preserve your settings.

If you like TCA you may [buy me a cup of coffee](https://www.paypal.me/Allista) to help the development.

##[ChangeLog](https://github.com/qfeys/ThrottleControlledAvionics/blob/master/ChangeLog.md)

##Acknowledgments:

First of all, many thanks to **qfeys** and **Zenka** for creating the original concept and implementation. Without them TCA would not exist.

And I want to express extreme gratitude to **[smjjames](http://forum.kerbalspaceprogram.com/index.php?/profile/134004-smjjames/)**, who helped me so much with the testing of TCA3 and proposed countless improvements. Without **smjjames** this version would never have come to a release!

Also, here are the mods whose code and ideas were used in one way or another:

* [Hangar](http://forum.kerbalspaceprogram.com/threads/88933)
* [MechJeb2](http://forum.kerbalspaceprogram.com/threads/12384)
