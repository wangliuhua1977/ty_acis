# 天翼视联 AI 智能巡检系统 - Codex 极简上下文

> 用途：给 Codex 新线程提供最小但够用的背景，尽量减少自动压缩耗时。  
> 原则：只保留会反复影响代码实现的规则、接口、字段和排障结论；原始门户文档不要整包塞入。

## 1. 项目定位
这是一个 .NET 8 Windows 桌面端慢直播运维巡检系统，不是播放器，不是纯大屏，也不是通用工单平台。

固定主线：
接口判定 → 点位体检 → 可播放性判断 → 截图/播放复核 → 故障派单 → 定时复检 → 恢复销警 → 报表沉淀

首版核心模块：
1. AI智能巡检
2. 故障派单处理
3. 报表中心

## 2. 技术冻结
- 技术栈：.NET 8
- 分层：Core / Infrastructure / Services / UI / App
- 地图：高德
- 展示坐标：GCJ-02
- 本地持久化：JSON / 配置 / 轻量缓存
- 通知：企业微信群 webhook / 量子密信 webhook
- 底层接入参数不进界面，只读本地配置

## 3. 接入公共规则
- 域名：`https://vcp.21cn.com`
- 协议：HTTPS
- 方法：POST
- 提交：`application/x-www-form-urlencoded`
- Header：`apiVersion: 2.0`
- clientType：`3`
- 版本口径：`v1.0` 与 `1.1` 都可能出现，不能把 data 解密逻辑写死

统一封装以下能力，不允许在各接口服务里复制：
- 请求参数加密器
- 签名器
- 响应解密器
- 按接口/版本决定 data 是否解密

加密签名基线：
- 私有参数整体加密为 `params`
- 参数加密：XXTea
- 签名：HMAC-SHA256
- 响应可能是普通 JSON，也可能 `data` 为加密串

## 4. 令牌策略
当前项目固定按“用户无感知获取令牌”实现，不走扫码登录。

接口：
- `POST https://vcp.21cn.com/open/oauth/getAccessToken`

关键规则：
- 首次：`grantType = vcp_189`
- 刷新：`grantType = refresh_token`
- accessToken 有效期：7天
- refreshToken 有效期：30天

落地策略：
1. 首次成功后本地缓存
2. 接口调用前检查 accessToken 是否有效
3. 有效则直接复用
4. 过期则检查 refreshToken
5. refreshToken 有效则刷新并覆盖缓存
6. refreshToken 失效才重新申请

禁止：
- 每调一次接口就重取 token
- 在多个 ViewModel 散落 token 状态
- 让 UI 直接处理 token 刷新

建议服务：
- `TokenService`
- `TokenCache`

## 5. 当前最重要接口
### 目录与设备
- `/open/token/device/getReginWithGroupList`：目录树，按 `hasChildren` 递归
- `/open/token/device/getDeviceList`：当前目录设备列表
- `/open/token/device/getAllDeviceListNew`：账号下全量设备分页
- `/open/token/device/showDevice`：设备详情
- `/open/token/vpaas/device/batchDeviceStatus`：批量在线状态，单次最多 200
- `/open/token/vpaas/device/getDeviceStatus`：单设备在线状态

开发建议：
- 设备清单拉取与在线状态刷新分开
- 目录树本地递归缓存
- 状态查询优先批量接口
- 单设备详情只在详情页或复核时补查

### 普通告警
- `/open/token/device/getDeviceAlarmMessage`

用途：
- 离线、上线、移动侦测等基础异常
- 适合作为底层状态与恢复统计来源

### AI 告警
- `/open/token/AIAlarm/getAlertInfoList`
- `/open/token/AIAlarm/getAlertInfoDetail`
- `/open/token/AIAlarm/getSnapImgUrl`
- `/open/token/ai/task/source/refreshDownloadUrl`

当前项目优先关注：
- 画面异常巡检
- 区域入侵
- 火情
- 客流
- 人脸/车牌布控

注意：
- 告警详情上下文至少保留 `msgId + alertType + deviceCode`
- 图片/抓拍/音频链接都视为临时资源，必须支持过期后刷新

### 直播流
常见接口：
- `/open/token/cloud/getDeviceMediaUrlHls`
- `/open/token/cloud/getDeviceMediaUrlRtmp`
- `/open/token/cloud/getDeviceMediaUrlFlv`
- `/open/token/vpaas/getDeviceMediaWebrtcUrl`
- `/open/token/vpaas/getH5StreamUrl`

关键结论：
- 流地址是临时地址，不能长期缓存
- 播放检查不能只绑一种协议
- 需要做协议降级重试
- 需要识别 H.264 / H.265
- 播放失败要区分：地址过期、设备离线、编码不支持、协议/网关问题

### 云回看
- `/open/token/cloud/getCloudFolderList`
- `/open/token/cloud/getCloudFileList`
- `/open/token/cloud/getFileUrlById`
- `/open/token/cloud/streamUrlRtmp`
- `/open/token/cloud/streamUrlHls`

用途：
- 告警复盘
- 人工复核
- 截图留痕

## 6. 地图与坐标
- 展示统一使用 GCJ-02
- 接口若返回 BD-09，展示前转 GCJ-02
- 本地人工补录坐标只影响本地展示与本地统计，不回写平台
- 手工坐标按地图展示坐标填写
- UI 必须明确提示“按高德 GCJ-02 填写”

## 7. UI固定约束
一级导航固定：
- 首页
- AI智能巡检
- 故障派单处理
- 报表中心
- 系统设置

AI智能巡检页固定布局：
- 左侧任务区
- 中间地图中台
- 右侧详情/视频预览区

派单处理页固定布局：
- 左侧筛选区
- 中间工单列表
- 右侧工单详情

地图要求：
- 地图是核心工作区，不是装饰
- 列表与地图双向联动
- 当前巡检点位高亮
- 故障点位红灯闪烁
- 点位点击支持视频预览或状态说明

## 8. 派单极简规则
- 派单模式：自动 / 人工
- 通知：企业微信群 webhook
- 同一点位同一故障重复出现时合并
- 工单状态：待派单 / 已派单
- 恢复状态：未恢复 / 已恢复
- 恢复后自动发送恢复通知
- 管理员可调整处理单位、维护人、负责人及手机号

## 9. 报表首版范围
只做：
- 巡检执行报表
- 故障统计报表
- 派单处置报表
- 责任归属报表
- 重点未恢复故障清单

## 10. 代码服务边界建议
至少拆成：
- `TokenService`
- `OpenApiSigner`
- `RequestEncryptor`
- `ResponseDecryptor`
- `DirectoryService`
- `DeviceService`
- `LiveStreamService`
- `CloudReplayService`
- `AlarmService`
- `AiAlarmService`
- `MapCoordinateService`
- `NotificationService`

## 11. 新线程使用规则
新线程只放三样：
1. `AGENTS.md`
2. 本文档
3. 当前轮要改的页面/模块清单

不要再把门户原始 md、排障手册、流程图、接口原文整包塞进背景。需要某个接口细节时，再单独补一份原始文档。
