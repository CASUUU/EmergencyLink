# EmergencyLink

EmergencyLink 是一个面向小范围比赛现场的 Windows 紧急连麦告警工具。

第一版是单 exe 设计：同一个 `EmergencyLink.exe` 可以启动本机服务器，也可以作为主办方、管理者、选手或队友客户端连接房间。

## 当前能力

- 主办方电脑兼服务器
- 管理者电脑兜底服务器
- 启动前角色选择，按角色显示精简界面
- 房间名 + 密码加入
- 赛前测试提醒和回执
- 比赛中正式告警
- 队友端二次确认防误触
- 选手端透明置顶悬浮件、声音提醒、拖动位置、按钮回执
- 主办方/管理者收到正式告警时弹出置顶提醒
- 开发者 API，可从终端查询通讯状态
- 选手回执正式请求后立即扣减连麦次数，审批不重复扣减
- 剩余次数为 0 时允许超额紧急请求
- 同一目标选手、同类提醒在配置时间窗内合并为同一批次
- 本地日志落盘

## 编译

当前仓库提供了不依赖 NuGet 的构建脚本：

```powershell
.\build\build.ps1
```

编译结果：

```text
dist\EmergencyLink.exe
```

也可以用 Visual Studio 打开：

```text
EmergencyLink.sln
```

## 自检

```powershell
$p = Start-Process -FilePath .\dist\EmergencyLink.exe -ArgumentList '--self-test' -PassThru -Wait
$p.ExitCode
```

返回码为 `0` 表示核心通信链路通过。

窗体初始化自检：

```powershell
$p = Start-Process -FilePath .\dist\EmergencyLink.exe -ArgumentList '--form-smoke-test' -PassThru -Wait
$p.ExitCode
```

## 文档

- `docs\使用说明书.md`
- `docs\测试流程.md`
