# EAP 设备管理系统

## 项目概述

EAP（Equipment Automation Program）设备管理系统是一个基于 .NET 10 + Windows Forms 的工业设备通信管理平台，支持多种主流工业通信协议（OPC UA、OPC DA、SECS/GEM HSMS、Modbus TCP/RTU/ASCII），提供设备连接管理、数据采集、心跳检测、自动重连、实时监控等功能。

---

## 项目结构

### 解决方案分层架构

```
EAPSystem.sln
├── EAP.Core          # 核心层：接口定义、配置模型、协议基类、日志系统
├── EAP.Adapters      # 适配层：各协议具体实现（工厂模式）
├── EAP.Services      # 服务层：设备管理、连接管理、业务编排
└── EAP.Client        # 客户端：WinForms UI 界面、设备卡片、监控展示
```

### 目录结构

```
d:\EAP\
├── Config/                          # 设备配置文件目录
│   ├── HSMS_DEVICE_01/              # SECS/GEM 设备配置
│   │   └── device.config
│   ├── MODBUS_TCP_DEVICE/           # Modbus TCP 设备配置
│   │   └── device.config
│   ├── OPC_UA_KEPSERVER/            # OPC UA 设备配置
│   │   └── device.config
│   └── OPC_DA_KEPSERVER/            # OPC DA 设备配置
│       └── device.config
├── EAP.Core/
│   ├── Configuration/               # 配置加载器
│   │   └── Configuration.cs
│   ├── DeviceConfig/                # 设备配置模型
│   │   └── DeviceConfig.cs
│   ├── LogConfig/                   # 日志系统
│   │   ├── DeviceLogger.cs
│   │   └── log4net.config
│   └── Protocol/                    # 协议基类与事件模型
│       └── ProtocolBase.cs
├── EAP.Adapters/
│   ├── Factory/                     # 协议客户端工厂
│   │   └── ProtocolClientFactory.cs
│   ├── Hsms/                        # SECS/GEM HSMS 协议实现
│   │   └── HsmsClient.cs
│   ├── Modbus/                      # Modbus 协议实现
│   │   └── ModbusClient.cs
│   ├── OpcDa/                       # OPC DA 协议实现
│   │   └── OpcDaClient.cs
│   └── OpcUa/                       # OPC UA 协议实现
│       └── OpcUaClient.cs
├── EAP.Services/
│   └── DeviceManager.cs             # 设备管理器（核心服务）
└── EAP.Client/
    ├── MainForm.cs                  # 主窗体（设备卡片管理）
    ├── DeviceInfo.cs                # 设备明细窗体（卡片+详情）
    ├── HeartbeatManager.cs          # 心跳状态管理（UI层）
    ├── Program.cs                   # 程序入口
    └── appsettings.json             # 应用配置
```

---

## 核心模块详解

### 1. EAP.Core - 核心层

#### 1.1 配置模型 (`DeviceConfig.cs`)

**设备配置类 `DeviceConfig`**：
- 基本属性：DeviceId、DeviceName、Enabled、ProtocolType、Description
- 通信参数：ConnectionTimeout、UpdateRate
- 心跳参数：HeartbeatInterval、HeartbeatTimeout、HeartbeatFailuresBeforeDisconnect
- 协议专属配置：OpcUaConfig、OpcDaConfig、HsmsConfig、ModbusConfig
- 标签列表：Tags（TagConfig 集合）

**标签配置类 `TagConfig`**：
- Id、Name、Address、NodeId
- DataType（String/Int32/Int64/Float/Double/Boolean/DateTime）
- ReadOnly、ScanRate、MinValue、MaxValue
- **IsHeartbeatTag**：标识是否为心跳专用标签

**协议类型枚举 `ProtocolType`**：
```csharp
OpcUa, OpcDa, Hsms, Modbus
```

#### 1.2 协议基类 (`ProtocolBase.cs`)

**接口 `IProtocolClient`**：
- 属性：ProtocolType、ConnectionId、IsConnected、HeartbeatStatus
- 事件：ConnectionStatusChanged、DataValueChanged、HeartbeatStatusChanged
- 方法：ConnectAsync、DisconnectAsync、ReadNodeAsync、WriteNodeAsync、SubscribeNodeAsync、UnsubscribeNodeAsync

**抽象基类 `ProtocolClientBase`**：

提供了所有协议共用的基础能力：

| 功能模块 | 说明 |
|---------|------|
| **轮询机制** | `StartPolling()` / `StopPolling()` 定时读取订阅的标签数据 |
| **独立心跳** | `StartHeartbeat()` / `StopHeartbeat()` 定时读取心跳标签，连续 N 次失败后断开连接 |
| **心跳状态** | `UpdateHeartbeatStatus()` 管理心跳状态，支持连续失败计数 |
| **事件触发** | `OnConnectionStatusChanged()` / `OnDataValueChanged()` 统一事件封装 |
| **状态去重** | `_lastReportedConnected` 字段，相同状态不重复触发事件 |
| **标签缓存** | `_tagValues` 并发字典存储最新标签值 |
| **订阅管理** | `_subscribedTags` 并发字典管理订阅的标签及更新频率 |
| **异常处理** | 按类型分级（超时/IO/通用），分别记录到设备日志的不同级别 |

**心跳机制设计**：
- 方式一：配置了 `IsHeartbeatTag` 标签 → 独立心跳包定时读取
- 方式二：未配置心跳标签 → 依赖业务数据轮询的成功/失败更新心跳
- 连续 `HeartbeatFailuresBeforeDisconnect` 次失败后自动触发断开连接

**事件去重机制**：
| 状态类型 | 去重位置 | 实现方式 |
|---------|---------|---------|
| 连接状态 | ProtocolClientBase | `_lastReportedConnected` 字段比较 + 锁保护 |
| 心跳状态 | ProtocolClientBase | `oldStatus != _heartbeatStatus` 比较 |
| UI层状态 | DeviceInfo | `StatusChangeCooldownMs = 500ms` 防抖 |

#### 1.3 配置加载器 (`Configuration.cs`)

**`ConfigurationLoader` 静态类**：
- `GetConfiguration()`：获取配置（带缓存）
- `Refresh()`：刷新配置缓存
- `SaveDeviceConfig()`：保存设备配置

**配置目录查找顺序**：
1. appsettings.json 中 `AppSettings:ConfigDirectory` 指定的路径
2. 程序基目录下的 `Config` 文件夹
3. 开发环境的相对路径 `..\..\..\..\Config`

**配置文件格式**：XML 序列化，每个设备一个文件夹，内含 `device.config`

#### 1.4 设备日志系统 (`DeviceLogger.cs`)

**设计决策**：为什么独立于 log4net 单独实现设备日志
- **系统日志**：使用 log4net，保存在运行目录，格式 `Logs/{level}/yyyyMMdd.log`
- **设备日志**：使用 DeviceLogger，保存在自定义路径，按设备分目录
- 原因：log4net 动态 Appender 配置复杂，难以实现灵活的 `{deviceId}/{level}/` 目录结构
- DeviceLogger 独立实现更简单直接，支持动态设备，目录结构完全可控

**`DeviceLogger` 静态类**：
- 按设备 ID 分目录存储日志
- 按日志级别（info/warn/error/debug）分子目录
- 按日期（yyyyMMdd.log）分文件
- 静态方法：Info、Warn、Error、Debug（均支持带 Exception 重载）
- 线程安全：按文件路径加锁（`ConcurrentDictionary<string, object>`）
- 安全过滤：自动替换设备 ID 中的非法文件名字符
- 异常日志格式：分级缩进，区分异常类型/消息/内部异常/堆栈

**日志目录结构**：
```
Logs/                          # 系统日志（log4net）
├── info/
│   └── 20250626.log
├── warn/
│   └── 20250626.log
├── error/
│   └── 20250626.log
└── debug/
    └── 20250626.log

{DeviceLogDirectory}/          # 设备日志（DeviceLogger）
└── {deviceId}/
    ├── info/
    │   └── 20250626.log
    ├── warn/
    │   └── 20250626.log
    ├── error/
    │   └── 20250626.log
    └── debug/
        └── 20250626.log
```

**日志级别使用规范**：
| 级别 | 使用场景 | 示例 |
|-----|---------|------|
| DEBUG | 调试信息、频繁发生的正常异常 | 单标签读取超时、质量不佳 |
| INFO | 重要状态变更、业务操作 | 设备连接/断开、数据更新 |
| WARN | 异常但可恢复、需要关注 | 心跳超时、IO异常、标签读取失败 |
| ERROR | 严重错误、需要排查 | 未知异常、连接失败、轮询循环异常 |

### 2. EAP.Adapters - 适配层

#### 2.1 工厂模式 (`ProtocolClientFactory.cs`)

```csharp
public static IProtocolClient CreateClient(DeviceConfig config)
{
    return config.ProtocolType switch
    {
        ProtocolType.OpcUa => new OpcUaClient(config),
        ProtocolType.OpcDa => new OpcDaClient(config),
        ProtocolType.Hsms => new HsmsClient(config),
        ProtocolType.Modbus => new ModbusClient(config),
        _ => throw new NotSupportedException(...)
    };
}
```

#### 2.2 各协议适配器

| 协议 | 依赖库 | 特点 |
|-----|-------|------|
| **HSMS (SECS/GEM)** | Secs4Net | 支持 Host/Eqp 模式，Active/Passive 连接，T3-T8 超时参数，S1F1 心跳 |
| **Modbus** | NModbus | 支持 TCP/RTU/ASCII，读写保持寄存器、线圈等 |
| **OPC UA** | - | 标准 OPC UA 客户端，支持匿名/用户名密码认证 |
| **OPC DA** | - | 经典 OPC DA，基于 COM/DCOM |

**通用模式**：
- 连接成功 → `UpdateHeartbeatStatus(true)` → `StartPolling()` → `StartHeartbeat()`
- 断开连接 → `StopPolling()` → `StopHeartbeat()` → 清理资源
- 都使用 `SemaphoreSlim` 保证连接操作线程安全

### 3. EAP.Services - 服务层

#### `DeviceManager` 设备管理器

**核心职责**：
- 设备连接的统一管理（连接、断开、重连）
- 事件转发（协议层 → UI层）
- 标签订阅管理
- 配置热重载

**主要 API**：
| 方法 | 说明 |
|-----|------|
| `ConnectDeviceAsync(deviceId)` | 连接单个设备 |
| `DisconnectDeviceAsync(deviceId)` | 断开单个设备 |
| `ConnectAllAsync()` | 连接所有启用的设备 |
| `DisconnectAllAsync()` | 断开所有设备 |
| `ReloadConfigurationAsync(directory)` | 异步重新加载配置（无阻塞） |
| `GetAllDeviceConfigs()` | 获取所有设备配置 |
| `GetClient(deviceId)` | 获取指定设备客户端 |

**并发控制**：
- `SemaphoreSlim _clientLock`：限制并发连接数（CPU 核心数）
- `ConcurrentDictionary<string, IProtocolClient> _clients`：已连接设备缓存
- `ConcurrentDictionary<string, bool> _connectingDevices`：正在连接的设备防重复
- `ConcurrentDictionary<string, CancellationTokenSource> _reconnectCts`：重连任务管理

**自动重连机制**：
- 设备断开后自动启动重连任务
- 重连间隔：5 秒
- 无限次重试直到成功或手动取消
- 可通过 `DisconnectDeviceAsync` 取消重连

**事件转发**：
```
协议客户端事件 → DeviceManager 转发 → UI 层订阅
  ConnectionStatusChanged → ConnectionStatusChanged
  DataValueChanged        → DataValueChanged
  HeartbeatStatusChanged  → HeartbeatStatusChanged
```

### 4. EAP.Client - 客户端层

#### 4.1 主窗体 `MainForm`

**职责**：
- 设备卡片的创建、布局、管理
- 状态栏统计（总数/在线/离线）
- 设备监控定时器（5秒轮询）
- 菜单操作（连接全部、断开全部、刷新）

**监控逻辑（`OnMonitorTick`）**：
- `SyncConfigurationAsync()`：同步配置变化（新增/删除/更新设备）
- `SyncConnectionStatusAsync()`：根据配置控制设备连接状态

**连接控制规则**：
| 条件 | 动作 |
|-----|------|
| 已禁用 + 已连接 | 断开连接 |
| 已启用 + 未连接 | 建立连接 |
| 已启用 + 已连接 + 心跳异常 | 断开重连 |

#### 4.2 设备明细窗体 `DeviceInfo`

**双模式设计**：
- **卡片模式**：嵌入主窗体，360×140 大小，显示关键信息
- **明细模式**：独立窗体，显示完整信息和交互日志

**公共属性**：
- `DeviceConfig`：设备配置
- `IsConnected`：连接状态
- `HeartbeatStatus`：心跳状态
- `IsInsideMainForm`：是否嵌入主窗体

**事件**：
- `ReturnedToMainForm`：从明细返回卡片模式
- `StatusChanged`：连接状态变化（通知 MainForm 更新状态栏）

**订阅的协议事件**：
- `ConnectionStatusChanged`：更新连接状态
- `HeartbeatStatusChanged`：更新心跳图标
- `DataValueChanged`：显示数据更新日志

#### 4.3 心跳管理器 `HeartbeatManager`

**UI 层心跳动画管理器（职责单一化）**：
- 仅负责 UI 心跳图标的动画效果
- **心跳检测逻辑在 Core 层 `ProtocolClientBase` 中实现**（业务核心）
- 通过 `SetNormal(bool isNormal)` 方法由外部设置心跳状态
- 通过 `HeartbeatStatusChanged` 事件同步状态，而不是各自独立判断

**动画效果**：
- WinForms Timer 驱动（UI 线程安全）
- 心跳动画效果（绿↔灰 交替闪烁）
- 状态颜色：
  - 未连接 → 灰色
  - 已连接 + 心跳正常 → 绿色/灰色交替（动画）
  - 已连接 + 心跳异常 → 红色

**职责划分**：
| 层级 | 组件 | 职责 |
|-----|------|------|
| Core层 | ProtocolClientBase | 业务心跳检测（读标签、失败计数、状态判断） |
| Client层 | HeartbeatManager | UI动画展示（颜色切换、闪烁效果） |

---

## 业务流程

### 1. 启动流程

```
Program.Main()
  ├── 构建 Configuration (appsettings.json)
  ├── 初始化 DeviceLogger (创建设备日志目录)
  ├── 创建 DeviceManager
  └── 启动 MainForm
       ├── SubscribeToEvents()
       ├── InitializeMonitor() (启动5秒监控定时器)
       └── OnLoad() → LoadDevicesAsync()
            ├── 加载所有设备配置
            ├── 创建设备卡片
            ├── 排列卡片布局
            └── ConnectAllAsync() (连接所有启用的设备)
```

### 2. 设备连接流程

```
ConnectDeviceAsync(deviceId)
  ├── 防重复检查 (_connectingDevices)
  └── ConnectDeviceInternalAsync()
       ├── 等待信号量 (_clientLock)
       ├── ProtocolClientFactory.CreateClient() 创建客户端
       ├── 订阅客户端事件
       ├── client.ConnectAsync() 建立连接
       ├── 成功：
       │    ├── 加入 _clients 缓存
       │    └── 订阅所有标签 (SubscribeNodeAsync)
       └── 失败：
            ├── 取消事件订阅
            └── StartReconnect() 启动自动重连
```

### 3. 心跳检测流程

```
独立心跳模式 (配置了 IsHeartbeatTag):
  StartHeartbeat()
    └── 定时循环:
         ├── ReadNodeAsync(heartbeatTagNodeId)
         ├── 成功 → UpdateHeartbeatStatus(true)
         ├── 失败 → UpdateHeartbeatStatus(false)
         │    └── 连续失败 >= N 次 → 触发断开连接
         └── 等待 HeartbeatInterval

业务数据模式 (未配置心跳标签):
  StartPolling()
    └── 定时循环:
         ├── 遍历所有订阅标签
         ├── 逐个 ReadNodeAsync
         ├── 任一成功 → UpdateHeartbeatStatus(true)
         └── 全部失败 → UpdateHeartbeatStatus(false)
```

### 4. 配置热重载流程

```
MonitorTimer (5秒)
  └── OnMonitorTick()
       ├── SyncConfigurationAsync()
       │    ├── ConfigurationLoader.Refresh()
       │    ├── 对比当前设备和已有卡片
       │    ├── 新增设备 → CreateCard()
       │    ├── 删除设备 → RemoveCard()
       │    └── 更新配置 → card.UpdateConfiguration()
       └── SyncConnectionStatusAsync()
            ├── 遍历所有设备配置
            └── 根据启用状态和连接状态执行对应操作

手动刷新 (OnRefresh)
  └── ReloadConfigurationAsync()  (异步无阻塞)
       ├── await DisconnectAllAsync()
       ├── 清空客户端缓存
       ├── ConfigurationLoader.Refresh()
       └── 重新加载配置
```

---

## 技术栈

| 类别 | 技术 | 版本 |
|-----|------|------|
| 框架 | .NET | 10.0 |
| UI | Windows Forms | net10.0-windows |
| UI组件库 | AntdUI | 2.4.1 |
| 日志 | log4net | 3.3.1 |
| 依赖注入 | Microsoft.Extensions.DependencyInjection | 10.0 |
| 配置 | Microsoft.Extensions.Configuration | 10.0 |
| Modbus | NModbus | - |
| SECS/GEM | Secs4Net | - |
| JSON | Newtonsoft.Json | 13.0.4 |

---

## 设计模式与架构原则

### 设计模式

| 模式 | 应用位置 | 说明 |
|-----|---------|------|
| **工厂模式** | ProtocolClientFactory | 根据 ProtocolType 创建对应的协议客户端 |
| **观察者模式** | 事件系统 (ConnectionStatusChanged 等) | 设备状态变化时通知所有订阅者 |
| **模板方法模式** | ProtocolClientBase | 定义通用流程，子类实现具体协议细节 |
| **单例模式** | ConfigurationLoader | 配置加载器全局单例 + 缓存 |
| **装饰器模式** | HeartbeatManager | 为设备 UI 增加心跳动画能力 |

### 架构原则

- **单一职责原则**：Core 定义接口，Adapters 实现协议，Services 编排业务，Client 负责 UI
- **开闭原则**：新增协议只需新增 Adapter 类和工厂分支，不修改现有代码
- **依赖倒置原则**：各层依赖抽象接口（IProtocolClient、IDeviceManager）
- **关注点分离**：UI 层不直接操作协议，通过 DeviceManager 间接访问

---

## 合理性分析

### ✅ 优点

1. **分层清晰**：四层架构职责明确，依赖方向合理（Client → Services → Adapters → Core）
2. **协议扩展方便**：工厂模式 + 抽象基类，新增协议只需新增 Adapter
3. **心跳机制完善**：支持独立心跳包和业务数据双模式，连续失败才断开
4. **自动重连可靠**：断开后自动重连，支持手动取消，防重复连接机制完善
5. **配置热更新**：5秒轮询检测配置变化，支持新增/删除/更新设备
6. **日志系统完整**：按设备、级别、日期分类存储，便于排查问题
7. **并发控制到位**：SemaphoreSlim 限流 + ConcurrentDictionary 线程安全集合
8. **事件驱动设计**：各模块通过事件解耦，便于扩展和维护

### ⚠️ 可改进点

#### 1. 心跳管理存在重复逻辑 ✅ 已优化

**当前状态**：已完成
- 保留 Core 层的心跳检测逻辑（业务核心）
- Client 层 HeartbeatManager 只负责 UI 动画展示
- 通过 `SetNormal()` 方法 + `HeartbeatStatusChanged` 事件同步状态
- 职责单一，状态来源唯一

---

#### 2. 轮询间隔未按 Tag 的 ScanRate 区分

**问题**：
- `ProtocolClientBase.StartPolling()` 中固定 `Task.Delay(1000)`
- TagConfig 有 `ScanRate` 字段但未实际使用

**建议**：
- 按 ScanRate 分组标签，不同频率分组轮询
- 或使用基于时间的调度，每个标签记录下次读取时间

---

#### 3. DeviceLogger 与 log4net 功能重叠 ✅ 方案确认

**当前状态**：已确认方案，保持独立实现并优化
- **设计决策**：保留 DeviceLogger 独立实现
- **原因**：log4net 动态 Appender 复杂，难以实现 `{deviceId}/{level}/` 灵活结构
- **优化内容**：增加线程安全、异常格式优化、安全过滤、日志级别规范

---

#### 4. 配置加载使用 .Wait() 阻塞调用 ✅ 已修复

**当前状态**：已完成
- `ReloadConfiguration` → `ReloadConfigurationAsync`（异步方法）
- 所有 `.Wait()` 调用已清除
- 全部使用 `await` + `ConfigureAwait(false)` 模式
- ModbusClient 添加同步 Cleanup 方法，Dispose 不再阻塞

---

#### 5. 缺少单元测试项目

**问题**：
- 当前解决方案只有 4 个项目，没有测试项目
- 协议实现、设备管理等核心逻辑难以验证

**建议**：
- 新增 `EAP.Tests` 单元测试项目
- 使用 xUnit / NUnit + Moq 框架
- 重点测试：DeviceManager 连接管理、心跳逻辑、配置加载

---

#### 6. UI 与业务逻辑耦合较紧

**问题**：
- DeviceInfo 既是窗体又包含设备状态管理逻辑
- 不符合 MVVM 或 MVC 模式，难以测试

**建议**：
- 引入 MVVM 模式，创建 DeviceViewModel
- 窗体只负责绑定和展示，逻辑放在 ViewModel 中

---

#### 7. 异常处理粒度较粗 ✅ 已优化

**当前状态**：已优化
- **心跳检测异常**：区分 `OperationCanceledException` / `TimeoutException` / `IOException` / 通用 Exception
- **轮询标签异常**：单标签异常不影响整体，区分超时/IO/通用
- **日志分级**：调试信息→DEBUG，可恢复异常→WARN，严重错误→ERROR
- 所有异常都记录到设备日志，便于排查

---

#### 8. 配置文件无验证

**问题**：
- 加载配置时只检查 XML 格式是否正确
- 不验证配置内容的合理性（如端口范围、必填字段）

**建议**：
- 增加配置验证逻辑（FluentValidation 或 DataAnnotations）
- 加载失败时给出具体错误信息

---

#### 9. 设备数量多时 UI 可能卡顿

**问题**：
- `LoadDevicesAsync` 中每个卡片创建后 `Task.Delay(50)`
- 设备数量多的话启动时间很长
- 所有卡片都是 Form，资源占用大

**建议**：
- 使用 UserControl 代替 Form 作为卡片
- 使用虚拟化或分页加载
- 卡片创建放到后台线程，UI 更新批量进行

---

#### 10. 状态变化事件可能重复触发 ✅ 已修复

**当前状态**：已完成
- ProtocolClientBase 增加 `_lastReportedConnected` 字段
- `OnConnectionStatusChanged` 中判断状态是否变化
- 相同状态直接返回，不触发事件
- 使用 `_statusLock` 锁保证线程安全
- 三层去重防护：协议层 → 服务层 → UI层

---

## 运行指南

### 环境要求

- .NET 10.0 SDK 或更高
- Windows 操作系统（WinForms 依赖）
- 对应的设备服务端（Modbus Server、OPC UA Server、SECS/GEM 设备等）

### 配置说明

#### appsettings.json

```json
{
  "AppSettings": {
    "ConfigDirectory": "Config",      // 设备配置目录
    "DeviceLogDirectory": "Logs"      // 设备日志目录
  }
}
```

#### 设备配置 (device.config)

每个设备文件夹下放一个 `device.config`，包含：
- 设备基本信息（ID、名称、是否启用、协议类型）
- 心跳参数（间隔、超时、连续失败次数）
- 协议专属配置（IP、端口、认证等）
- 标签列表（地址、数据类型、扫描频率等）

### 启动方式

```bash
# 编译
dotnet build EAPSystem.sln

# 运行
dotnet run --project EAP.Client/EAP.Client.csproj
```

---

## 配置示例

### Modbus TCP 设备心跳配置

```xml
<Tags>
  <Tag>
    <Id>TAG_HEARTBEAT</Id>
    <Name>心跳寄存器</Name>
    <NodeId>40001</NodeId>
    <DataType>Int16</DataType>
    <ReadOnly>true</ReadOnly>
    <IsHeartbeatTag>true</IsHeartbeatTag>
    <Description>用于心跳检测的保持寄存器</Description>
  </Tag>
</Tags>
```

### SECS/GEM 设备心跳配置

```xml
<Tags>
  <Tag>
    <Id>TAG_HSMS_001</Id>
    <Name>设备状态</Name>
    <NodeId>S1F1</NodeId>
    <DataType>String</DataType>
    <ReadOnly>true</ReadOnly>
    <IsHeartbeatTag>true</IsHeartbeatTag>
    <Description>SECS S1F1 Are You There? 心跳检测</Description>
  </Tag>
</Tags>
```

---

## 扩展开发

### 新增协议支持

1. 在 `EAP.Adapters` 中新建文件夹，如 `MyProtocol/`
2. 创建 `MyProtocolClient.cs` 继承 `ProtocolClientBase`
3. 实现 `IProtocolClient` 接口的所有抽象成员
4. 在 `ProtocolClientFactory.cs` 中增加对应分支
5. 在 `ProtocolType` 枚举中增加新类型
6. 在 `DeviceConfig.cs` 中增加对应配置类

### 新增数据处理逻辑

1. 在 `EAP.Core` 中定义数据处理接口
2. 在 `EAP.Services` 中实现具体逻辑
3. 通过 `DeviceManager` 暴露给 UI 层
4. 在 `MainForm` 或 `DeviceInfo` 中调用

---

## 版本历史

| 版本 | 日期 | 说明 |
|-----|------|------|
| 1.0.0 | - | 初始版本，支持 4 种协议，心跳检测，自动重连，配置热更新 |
| 1.1.0 | 2025-06-26 | 代码质量优化版本 |

**v1.1.0 优化内容**：

1. **心跳管理职责单一化**
   - HeartbeatManager 仅负责 UI 动画，心跳检测统一在 Core 层
   - 消除重复逻辑，状态来源唯一

2. **消除所有阻塞调用**
   - `ReloadConfiguration` → `ReloadConfigurationAsync`
   - 清除所有 `.Wait()` 和 `.Result` 调用
   - 全部使用 `await` + `ConfigureAwait(false)` 模式
   - ModbusClient 添加同步 Cleanup 方法

3. **状态事件去重机制**
   - ProtocolClientBase 增加 `_lastReportedConnected` 状态比较
   - 相同状态不触发事件，避免日志重复和 UI 闪烁
   - 三层防护：协议层 → 服务层 → UI层

4. **DeviceLogger 优化**
   - 确认独立实现方案（比 log4net 动态 Appender 更灵活）
   - 增加线程安全（按文件路径加锁）
   - 增加设备 ID 安全过滤
   - 优化异常日志格式（分级缩进）
   - 完善日志级别使用规范

5. **异常处理细化**
   - 心跳检测：区分超时/IO/通用异常
   - 轮询标签：单标签异常不影响整体，分级记录
   - 所有异常都记录到设备日志
   - 日志级别：DEBUG/WARN/ERROR 合理分配

---

## 注意事项

1. **线程安全**：所有跨线程 UI 操作都通过 `InvokeRequired` / `BeginInvoke` 封送到 UI 线程
2. **资源释放**：设备断开时会停止轮询和心跳，释放连接资源
3. **配置生效**：修改设备配置后 5 秒内自动检测并生效
4. **日志路径**：默认在程序运行目录下的 `Logs` 文件夹
5. **并发限制**：同时连接设备数受 `SemaphoreSlim` 限制（默认等于 CPU 核心数）

---

*本文档基于项目当前代码自动生成，如有更新请同步修改。*
