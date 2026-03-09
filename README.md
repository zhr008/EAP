# CIM (Computer Integrated Manufacturing) Architecture

## 项目概述

基于 **.NET 9.0** 构建的分布式 CIM 架构系统，采用 **Master-Agent** 模式，集成 **SECS/GEM** 通信协议、**gRPC** 服务通信和 **Kafka** 消息队列。

---

## 架构图

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│                              CIM Architecture                                   │
├─────────────────────────────────────────────────────────────────────────────────┤
│                                                                                  │
│  ┌──────────────┐                    ┌──────────────┐                           │
│  │   Master     │◄──── gRPC ───────►│    Agent     │                           │
│  │   Service    │                    │   Service    │                           │
│  │  (Port 7000) │                    │  (Port 5001) │                           │
│  └──────┬───────┘                    └──────┬───────┘                           │
│         │                                   │                                    │
│         │ REST API                          │ SECS/GEM (HSMS)                   │
│         │ Swagger UI                        │ TCP/IP                             │
│         ▼                                   ▼                                    │
│  ┌──────────────┐                    ┌──────────────┐      ┌──────────────┐     │
│  │  Equipment   │                    │   Device     │──────│  Equipment   │     │
│  │  Controller  │                    │  Connector   │ HSMS │   EQP001     │     │
│  └──────────────┘                    │  (SECS/GEM)  │      └──────────────┘     │
│         │                            └──────────────┘      ┌──────────────┐     │
│         │ gRPC Services                                    │   Equipment   │     │
│         ▼                                                  │   EQP002     │     │
│  ┌──────────────┐                                          └──────────────┘     │
│  │  Equipment   │                                                                    │
│  │  State Svc   │                                                                    │
│  └──────┬───────┘                                                                    │
│         │                                                                            │
│         ▼                                                                            │
│  ┌──────────────┐     Kafka Topics      ┌──────────────┐                           │
│  │    Kafka     │──────────────────────►│    Event     │                           │
│  │   Publisher  │                       │  Processor   │                           │
│  └──────────────┘                       └──────────────┘                           │
│                                                                                  │
└─────────────────────────────────────────────────────────────────────────────────┘
```

---

## 解决方案结构

```
CimArchitecture.sln
├── src/
│   ├── Cim.Core/                    # 核心领域模型和接口
│   │   ├── Models/
│   │   │   └── EquipmentModels.cs   # 设备、报警、配方、事件模型
│   │   ├── Interfaces/
│   │   │   └── Services.cs          # 服务接口定义
│   │   ├── Events/
│   │   │   └── DomainEvents.cs      # 领域事件定义
│   │   └── Cim.Core.csproj
│   │
│   ├── Cim.GrpcContracts/           # gRPC 契约定义
│   │   ├── Protos/
│   │   │   └── cim.proto            # Protocol Buffers 定义
│   │   └── Cim.GrpcContracts.csproj
│   │
│   ├── Cim.MasterService/           # Master 主服务
│   │   ├── Program.cs               # 服务入口和配置
│   │   ├── Controllers/
│   │   │   └── EquipmentController.cs  # REST API 控制器
│   │   ├── Services/
│   │   │   ├── EquipmentGrpcService.cs    # gRPC 设备服务
│   │   │   ├── EquipmentStateService.cs   # 设备状态管理
│   │   │   └── KafkaMessagePublisher.cs   # Kafka 消息发布
│   │   ├── appsettings.json
│   │   └── Cim.MasterService.csproj
│   │
│   ├── Cim.AgentService/            # Agent 代理服务
│   │   ├── Program.cs               # 服务入口
│   │   ├── Services/
│   │   │   └── DeviceAgentService.cs    # 设备代理后台服务
│   │   ├── appsettings.json
│   │   └── Cim.AgentService.csproj
│   │
│   ├── Cim.DeviceConnector/         # 设备通信连接器
│   │   ├── SecsGem/
│   │   │   └── HsmsConnection.cs    # HSMS (SECS/GEM) 实现
│   │   └── Cim.DeviceConnector.csproj
│   │
│   └── Cim.EventProcessor/          # 事件处理器
│       ├── Cim.EventProcessor.csproj
│
└── README.md
```

---

## 核心组件说明

### 1. Cim.Core (核心层)

**职责**: 定义领域模型、枚举和服务接口

#### 主要模型:
- **EquipmentInfo**: 设备信息 (ID、类型、地址、状态、变量等)
- **AlarmInfo**: 报警信息 (ID、代码、描述、严重程度、时间戳)
- **RecipeInfo**: 配方信息 (ID、名称、版本、参数、内容)
- **EventReport**: 事件报告 (事件 ID、类型、数据项)
- **DataCollectionItem**: 数据采集项 (变量 ID、值、数据类型、单位)
- **SecsGemMessage**: SECS/GEM 消息 (流、函数、系统字节、数据)

#### 设备状态枚举 (EquipmentState):
| 状态 | 值 | 说明 |
|------|-----|------|
| Offline | 0 | 离线 |
| Online | 1 | 在线 |
| Running | 2 | 运行中 |
| Idle | 3 | 空闲 |
| Paused | 4 | 暂停 |
| Stopped | 5 | 停止 |
| Error | 6 | 错误 |
| Maintenance | 7 | 维护中 |

#### 服务接口:
- `IEquipmentStateService`: 设备状态管理
- `IRecipeService`: 配方管理
- `IAlarmService`: 报警管理
- `IEventReportService`: 事件报告
- `IDataCollectionService`: 数据采集
- `ISecsGemCommunication`: SECS/GEM 通信
- `IMessagePublisher`: 消息队列发布
- `IAgentCoordinator`: Agent 协调器

---

### 2. Cim.GrpcContracts (gRPC 契约层)

**职责**: 定义 gRPC 服务和消息契约

#### gRPC 服务:

| 服务 | 方法 | 说明 |
|------|------|------|
| **EquipmentService** | GetEquipment | 获取设备信息 |
| | GetAllEquipments | 获取所有设备 |
| | UpdateEquipmentState | 更新设备状态 |
| | GetVariable/SetVariable | 变量读写 |
| **RecipeService** | GetRecipe/GetAllRecipes | 获取配方 |
| | CreateRecipe/UpdateRecipe/DeleteRecipe | 配方 CRUD |
| | DownloadRecipe/UploadRecipe | 配方上下传 |
| | SelectRecipe | 选择配方 |
| **AlarmService** | GetAlarm/GetActiveAlarms/GetAllAlarms | 报警查询 |
| | ClearAlarm/AcknowledgeAlarm | 报警清除/确认 |
| **EventReportService** | RegisterEvent | 注册事件 |
| | GetEvents | 获取事件历史 |
| **DataCollectionService** | CollectData | 采集数据 |
| | GetData | 查询历史数据 |
| | ConfigureCollection | 配置采集 |

---

### 3. Cim.MasterService (主服务)

**职责**: 系统核心协调者，提供 gRPC 和 REST API，管理设备状态，发布消息到 Kafka

#### 端口配置:
- **REST API**: 7000 (HTTP) / 7001 (HTTPS)
- **gRPC**: 7001

#### 核心服务:

##### EquipmentGrpcService
- 实现 gRPC 设备管理服务
- 调用 IEquipmentStateService 处理业务逻辑
- 返回 Protocol Buffer 格式响应

##### EquipmentStateService
- 内存存储设备状态 (ConcurrentDictionary)
- 支持设备注册、状态更新、变量读写
- 发布状态变更事件到 Kafka

##### KafkaMessagePublisher
- 发布消息到 Kafka 主题:
  - `cim.equipment.state`: 设备状态变更
  - `cim.alarm.events`: 报警事件
  - `cim.event.reports`: 事件报告
  - `cim.data.collection`: 数据采集

##### REST Controllers:
- **EquipmentController**: 设备管理 REST API
- **AgentsController**: Agent 注册和心跳

---

### 4. Cim.AgentService (代理服务)

**职责**: 部署在现场，直接连接设备，执行 Master 指令

#### 端口配置:
- **HTTP**: 5001

#### 核心功能:

##### DeviceAgentService (BackgroundService)
1. **启动流程**:
   - 向 Master 注册 (如果配置了 Master 地址)
   - 加载设备配置并建立 HSMS 连接
   - 发送心跳到 Master
   - 持续监控连接状态

2. **设备连接管理**:
   - 使用 `HsmsConnection` 建立 TCP 连接
   - 处理设备消息接收和解析
   - 支持重连机制

3. **SECS/GEM 消息处理**:
   | Stream.Function | 说明 | 处理方法 |
   |----------------|------|----------|
   | S1F2 | Access Request Ack | HandleAccessRequestAck |
   | S2F14 | Variable Data Response | HandleVariableDataResponse |
   | S2F42 | Process Program List | HandleProcessProgramList |
   | S5F2 | Alarm Ack | HandleAlarmAck |
   | S6F2 | Event Report | HandleEventReport |

4. **支持的 SECS/GEM 操作**:
   - S1F1: Access Request (连接请求)
   - S2F13: Variable Request (变量读取)
   - S2F41: Process Program Request (配方列表请求)
   - S2F43: Process Program Select (配方选择)
   - S2F47: Process Program Create (配方下载)
   - S5F1: Alarm Display (报警上报)

---

### 5. Cim.DeviceConnector (设备连接器)

**职责**: 实现 SECS/GEM (HSMS) 通信协议

#### HsmsConnection 类:

##### 核心方法:
- `ConnectAsync()`: 建立 TCP 连接到设备
- `DisconnectAsync()`: 断开连接
- `SendAsync()`: 发送 SECS 消息
- `ReceiveLoopAsync()`: 异步接收消息循环

##### HSMS 消息格式:
```
┌────────────────────────────────────────┐
│  Length (4 bytes, Big Endian)          │
├────────────────────────────────────────┤
│  Device ID (2 bytes)                   │
│  Stream (1 byte)                       │
│  Function (1 byte)                     │
│  Type (1 byte, Primary/Secondary bit)  │
│  System Bytes (2 bytes)                │
│  Data (TLV format)                     │
└────────────────────────────────────────┘
```

##### 事件:
- `MessageReceived`: 接收到 SECS 消息时触发
- `ConnectionChanged`: 连接状态变化时触发

---

### 6. Cim.EventProcessor (事件处理器)

**职责**: 消费 Kafka 消息，处理业务事件 (待实现)

#### 预期功能:
- 消费 `cim.*` 主题消息
- 持久化事件到数据库
- 触发业务规则
- 生成报表

---

## 通信流程

### 设备状态管理流程
```
Agent                      Master                    Kafka
  │                          │                         │
  │──Device Connected──────► │                         │
  │                          │──State Changed──────►   │
  │                          │   (Publish)             │
  │                          │                         │
  │◄────gRPC Query─────────  │                         │
  │   (GetEquipment)         │                         │
  │                          │                         │
```

### 配方下载流程
```
Client                    Master                    Agent                   Equipment
  │                         │                         │                         │
  │──DownloadRecipe──────► │                         │                         │
  │   (gRPC)                │                         │                         │
  │                         │──Command─────────────► │                         │
  │                         │   (S2F47)               │                         │
  │                         │                         │──PP_CREATE───────────► │
  │                         │                         │   (SECS/GEM)            │
  │                         │◄────Result──────────── │                         │
  │◄────Response────────── │                         │                         │
  │                         │                         │                         │
```

### 报警上报流程
```
Equipment                 Agent                     Master                  Kafka
  │                         │                         │                         │
  │──ALMD (S5F1)─────────► │                         │                         │
  │                         │──Process Message─────► │                         │
  │                         │                         │──Publish─────────────► │
  │                         │                         │   cim.alarm.events      │
  │                         │                         │                         │
```

### 数据采集流程
```
Equipment                 Agent                     Master                  Kafka
  │                         │                         │                         │
  │◄────VREQ (S2F13)────── │                         │                         │
  │──Variable Data───────► │                         │                         │
  │   (S2F14)               │                         │                         │
  │                         │──CollectData─────────► │                         │
  │                         │                         │──Publish─────────────► │
  │                         │                         │   cim.data.collection   │
  │                         │                         │                         │
```

---

## Kafka 主题设计

| 主题 | 消息类型 | 说明 |
|------|---------|------|
| `cim.equipment.state` | EquipmentStateChanged | 设备状态变更事件 |
| `cim.alarm.events` | AlarmRaised/AlarmCleared | 报警触发/清除事件 |
| `cim.event.reports` | EventReport | SECS/GEM 事件报告 |
| `cim.data.collection` | DataCollection | 采集的数据点 |

---

## 技术栈

| 组件 | 技术 | 版本 |
|------|------|------|
| 运行时 | .NET | 9.0 |
| gRPC | Grpc.Tools | 2.67.0 |
| Web 框架 | ASP.NET Core | 9.0.13 |
| 消息队列 | Apache Kafka | (待集成 Confluent.Kafka) |
| 通信协议 | SECS/GEM (HSMS) | 自定义实现 |
| 架构模式 | Microservices + Master-Agent | - |

---

## 部署架构

```
┌─────────────────────────────────────────────────────────────┐
│                      Kubernetes Cluster                      │
│                                                              │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐         │
│  │   Master    │  │   Master    │  │   Master    │         │
│  │  Service    │  │  Service    │  │  Service    │  ...    │
│  │  (Replica)  │  │  (Replica)  │  │  (Replica)  │         │
│  └─────────────┘  └─────────────┘  └─────────────┘         │
│                                                              │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐         │
│  │   Agent     │  │   Agent     │  │   Agent     │         │
│  │  Service    │  │  Service    │  │  Service    │  ...    │
│  │  (Per Site) │  │  (Per Site) │  │  (Per Site) │         │
│  └─────────────┘  └─────────────┘  └─────────────┘         │
│                                                              │
│  ┌─────────────────────────────────────────────────────┐   │
│  │              Apache Kafka Cluster                    │   │
│  │  (Zookeeper + Brokers + Topics)                      │   │
│  └─────────────────────────────────────────────────────┘   │
│                                                              │
└─────────────────────────────────────────────────────────────┘
         │                    │                    │
         ▼                    ▼                    ▼
   ┌──────────┐         ┌──────────┐         ┌──────────┐
   │ Factory A│         │ Factory B│         │ Factory C│
   │ Devices  │         │ Devices  │         │ Devices  │
   └──────────┘         └──────────┘         └──────────┘
```

---

## 关键特性

### 1. 设备状态管理 (State Management)
- 实时跟踪设备状态 (Offline/Online/Running/Idle/Error 等)
- 支持状态变更事件发布
- 心跳检测机制

### 2. 配方管理 (Recipe Management)
- 配方上传/下载 (S2F47 PP_CREATE)
- 配方选择 (S2F43 PP_SELECT)
- 配方版本控制
- 参数验证

### 3. 事件报告 (Event Report)
- SECS/GEM 事件订阅 (S6F1 ER_REQ)
- 事件数据收集 (CEID, Data Items)
- 实时事件发布到 Kafka

### 4. 报警管理 (Alarm Management)
- 报警上报 (S5F1 ALMD)
- 报警确认和清除
- 报警分级 (Critical/Major/Minor/Warning/Information)
- 报警历史记录

### 5. 数据采集 (Data Collection)
- 变量请求 (S2F13 VREQ)
- 周期性数据采集
- 支持多种数据类型 (Boolean, Integer, Float, String, Array)
- 数据发布到 Kafka 供下游分析

---

## 扩展方向

1. **数据库集成**: 添加 Entity Framework Core 支持，持久化设备状态、报警历史、事件日志
2. **Kafka 完整集成**: 启用 Confluent.Kafka 生产者/消费者
3. **认证授权**: 添加 JWT 认证和基于角色的访问控制
4. **监控告警**: 集成 Prometheus + Grafana 监控系统健康
5. **容器化**: 添加 Dockerfile 和 Helm Charts 支持 K8s 部署
6. **SECS/GEM 完善**: 实现完整的 SECS-II 消息编解码和 GEM 规范

---

## 快速开始

### 启动 Master Service
```bash
cd src/Cim.MasterService
dotnet run --urls="http://localhost:7000;https://localhost:7001"
```

### 启动 Agent Service
```bash
cd src/Cim.AgentService
dotnet run --urls="http://localhost:5001"
```

### API 测试
- Swagger UI: `https://localhost:7000/swagger`
- Health Check: `https://localhost:7000/health`
- gRPC Endpoint: `https://localhost:7001`

---

## 联系方式

如有问题或建议，请联系开发团队。