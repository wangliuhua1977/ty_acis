# 天翼视联-能力开放门户

> Saved from [https://open.ctseelink.cn/portal/document-open/jump?group=1876872930746007554&child=1876874942127009793&ability=1658134828321959937#1658141288305729537](https://open.ctseelink.cn/portal/document-open/jump?group=1876872930746007554&child=1876874942127009793&ability=1658134828321959937#1658141288305729537) on 2026-03-08

# 天翼视联-能力开放门户

[![](https://open.ctseelink.cn/portal/static/img/header-logo.7def4c92.png)][1]

王\*\*

基础视频流

-   简介
-   调用方式
-   相关能力
    -   获取访问令牌（勿删-...
    -   查询监控目录
    -   查询我的设备
    -   查询行业树目录
    -   自定义分组查询
    -   查询设备基础信息
    -   查询设备能力集
    -   查询视联百川盒子能力...
    -   查询AI开通信息
    -   查询AI标品车牌布控...
    -   查询设备AI告警信息...
    -   查询设备告警信息
    -   查询设备标签
    -   查询授权用户信息
    -   获取标准直播视频流
    -   获取云回看视频流
    -   云回看文件下载
    -   查询视频流信息
    -   查询设备云存开通信息...
    -   IOT基础查询能力
    -   设备基础控制
    -   应用级消息推送
    -   响应状态码字典
    -   示例代码
-   常见问题

相关接口

查询AI消息列表

### **2、查询AI消息列表**

注意事项: 只能通过云眼APP开启的AI标品告警消息 30 天内的告警消息。

字段标识

getAlertInfoList

接口描述

分类查询消息列表

承载协议

HTTPS

承载网络

公网

请求方式

POST

数据格式

请求参数以 form 表单方式提交（application/x-www-form-urlencoded）响应结果为 JSON 格式

约束

无

接口URL

https://vcp.21cn.com/open/token/AIAlarm/getAlertInfoList

业务方向

接入方→能力开放

请求参数

字段标识

说明

数据类型

可空

accessToken

鉴权令牌

String

N

enterpriseUser

企业主(访问令牌获取方式为“用户无感知获取令牌”，需要传此参数)

String

Y

parentUser

父账号(enterpriseUser是子账号，则parentUser必填)

String

Y

pageSize

每页设备数量

Integer

Y

pageNo

页号

String

Y

startTime

查询的起始时间,格式“2021-08-16 18:40:01:001”,注意日期和时间中间有空格

String

Y

endTime

查询的终止时间,格式“2021-08-16 18:40:01:001”,注意日期和时间中间有空格

String

Y

alertTypeList

告警类型列表(3画面异常巡检，4时光缩影，5区域入侵，12客流统计，13厨帽识别，14抽烟识别，15口罩识别，16玩手机识别，17火情识别，7人脸布控，6车牌布控，18平安慧眼人脸布控，19平安慧眼车牌布控，20平安慧眼区域入侵; 21大象识别，22电动车识别，23水域监控-区域入侵24水域监控-滞留告警，25-人群聚集检测，26-医用防护服检测 ,27 高空抛物不传表示查全部  
说明：多个类型通过逗号分隔 “3,15,21”

String

Y

deviceCode

精确的设备code

String

Y

alertSource

消息来源: 1-端侧，2-云化，3-云测-ai能力中台，4-平安慧眼

Integer

Y

响应参数

字段标识

说明

数据类型

可空

code

响应码

Integer

N

msg

响应信息

String

N

data

响应数据

Object

N

data

字段标识

说明

数据类型

可空

List

告警信息列表(注意:前端展示时，滚动分页，下一页要通过上页最后一条记录的时间和id,来过滤下一页时间和id相同的记录)

AlertInfoDto

N

pageNo

页数

Integer

N

pageSize

每页数量

Integer

N

total

总数

Integer

N

AlertInfoDto

字段标识

说明

数据类型

可空

id

告警信息主键id

String

N

deviceCode

设备码

String

N

alertType

告警类型(13画面异常巡检，4时光缩影，5区域入侵，12客流统计，13厨帽识别，14抽烟识别，15口罩识别，16玩手机识别，17火情识别，7人脸布控，6车牌布控，18平安慧眼人脸布控，19平安慧眼车牌布控，20平安慧眼区域入侵; 21大象识别，22电动车识别，23水域监控-区域入侵24水域监控-滞留告警，25-人群聚集检测，26-医用防护服检测)

Integer

N

msgReqNo

消息流水号

Integer

Y

content

告警内容

String

Y

alertTypeList

告警类型列表

String

Y

createTime

告警时间

Data

Y

updateTime

更新时间

Data

Y

deviceName

设备名称

String

Y

featureId

消息联动功能类型

Integer

Y

alertSource

消息来源: 1-端侧，2-云化，3-云测-ai能力中台，4-平安慧眼

Integer

Y

结果示例

```javascript
//正常情况下，平台返回下述 JSON 数据包：
{
  "code": 0,
  "msg": "成功",
  "data":[{
    .....
}]
}

```

获取AI告警消息详情

### **3、查询AI告警消息详情**

注意事项：区域入侵的抓拍图应使用‘获取告警消息抓拍图片地址接口’，图片地址链接有效时间为 1 天

字段标识

getAlertInfoDetail

接口描述

获取AI告警消息详情

承载协议

HTTPS

承载网络

公网

请求方式

POST

数据格式

请求参数以 form 表单方式提交（application/x-www-form-urlencoded）响应结果为 JSON 格式

约束

无

接口URL

https://vcp.21cn.com/open/token/AIAlarm/getAlertInfoDetail

业务方向

接入方→能力开放

请求参数

字段标识

说明

数据类型

可空

accessToken

鉴权令牌

String

N

enterpriseUser

企业主(访问令牌获取方式为“用户无感知获取令牌”，需要传此参数)

String

Y

parentUser

父账号(enterpriseUser是子账号，则parentUser必填)

String

Y

msgId

告警消息主键id

Long

N

alertType

告警类型列表(3画面异常巡检，4时光缩影，5区域入侵，12客流统计，13厨帽识别，14抽烟识别，15口罩识别，16玩手机识别，17火情识别，7人脸布控，6车牌布控，18平安慧眼人脸布控，19平安慧眼车牌布控，20平安慧眼区域入侵; 21大象识别，22电动车识别，23水域监控-区域入侵24水域监控-滞留告警，25-人群聚集检测，26-医用防护服检测 ,27 高空抛物

Integer

N

deviceCode

设备编码（消息ID对应的设备编码）

String

N

响应参数

字段标识

说明

数据类型

可空

code

响应码

Integer

N

msg

响应信息

String

N

data

响应数据

Object

N

data

字段标识

说明

数据类型

可空

id

主键id

String

N

deviceSource

设备来源

Integer

N

treeType

树类型

Integer

N

deviceCode

设备码

String

N

params

告警信息

AlertParamsDto

N

AlertParamsDto

字段标识

说明

数据类型

可空

featureId

消息联动功能类型

Integer

Y

catchPatImageUrl

图片地址, 人脸布控、车牌布控等

String

Y

imageUrl

视频缩略图(车牌没有这个图，目前只有人脸有)

String

Y

bgImageUrl

设备抓拍背景图Url

String

Y

cloudFileId

云存文件id

String

Y

cloudFileDownUrl

云存文件下载地址

String

Y

cloudFileIconUrl

云存文件icon地址(视频的底图)

String

Y

cloudFileName

云存名称

String

Y

similarity

人脸抓拍，相似度

String

Y

carNum

车牌号

String

Y

carType

车牌类型，1-黑名单，2-陌生车牌

String

Y

userName

人脸布控(底图上传保存)

String

Y

remark

备注(底图上传保存)

String

Y

webUrl

网页地址

String

Y

结果示例

```javascript
//正常情况下，平台返回下述 JSON 数据包：
//告警图片链接地址有效时间为1天，图片保存时间与账号开通云存套餐时间相同
{
  "code": 0,
  "msg": "成功",
  "data":[{
    .....
}]
}

```

获取区域入侵告警消息抓拍图片地址

### **4、获取区域入侵告警消息抓拍图片地址**

注意事项：只供区域入侵这个告警类型使用

接口名称

getSnapImgUrl

接口描述

新获取告警消息抓拍图片地址

承载协议

HTTP/HTTPS

请求方式

POST

数据格式

请求参数以 form 表单方式提交（application/x-www-form-urlencoded），响应结果为 JSON 格式

接口URL

https://vcp.21cn.com/open/token/AIAlarm/getSnapImgUrl

加密方式

无

请求参数

字段标识

说明

数据类型

可空

accessToken

鉴权令牌

String

N

enterpriseUser

企业主(访问令牌获取方式为“用户无感知获取令牌”，需要传此参数)

String

Y

parentUser

父账号(enterpriseUser是子账号，则parentUser必填)

String

Y

msgId

告警消息id

String

N

alertType

告警类型

Integer

N

deviceCode

设备编码（消息ID对应的设备编码）

String

N

响应参数

字段标识

说明

数据类型

可空

code

响应码

Integer

N

msg

响应信息

String

N

data

响应数据(抓拍图片地址，可能返回为空表示没有抓拍图片)

String

Y

结果示例

```javascript
//接口成功返回如下
{
    "code": 0,
    "msg": "成功",
    "data": "http://media-jiangxi-uz-yijia.jxoss.xstore.ctyun.cn/LF07/FACECONTROLPACK/3KSCA56022012W8/14ce1cc78aa827cdec551e5ae0cda8cd?Signature=jUxFDrf1PrvLB%2BCl75tUx3VCsLY%3D&AWSAccessKeyId=t4tyhIuBkPHxmt5B69oy&Expires=1674009241"
}

```

AI告警图片刷新接口

### **5、AI告警图片刷新接口**

字段标识

refreshDownloadUrl

接口描述

AI告警图片刷新接口

承载协议

HTTPS

承载网络

公网

请求方式

POST

数据格式

请求参数以 form 表单方式提交（application/x-www-form-urlencoded）响应结果为 JSON 格式

约束

无

接口URL

https://vcp.21cn.com/open/token/ai/task/source/refreshDownloadUrl

业务方向

接入方→能力开放

请求参数

字段标识

说明

数据类型

可空

srcToken

文件token

String

N

accessToken

鉴权令牌

String

N

enterpriseUser

企业主(访问令牌获取方式为“用户无感知获取令牌”，需要传此参数)

String

Y

parentUser

父账号(enterpriseUser是子账号，则parentUser必填)

String

Y

响应参数

字段标识

说明

数据类型

可空

code

响应码

Integer

N

msg

响应信息

String

N

data

加密数据，data 采用 RSA 加密或 XXTea 加密（加密选择见 6.2. 公共请求参数）解密后参见后文示例

String

N

返回示例

```javascript
// 正常情况下，平台返回下述 JSON 数据包：
{
   "code": 0,
   "msg": "成功",
   "data": "F3A4261D18CABD4E7C9926105CD9287F45393458D7F9DCBC3BE9650CCC4D25D33657DEE9034339747D398516FB0DB6A75581AB3D1D308AE27FBA2552486972C267D************BDA153ACAE918AFEF1AE369F9981CDF78730724E83FC53A2222800A7EC9FB2039E928FB7250904F927552E22F64DD2E73CD7CE8209681BFC9105"
} 
// data 解密后数据包为：
{
"code":0,
"msg":"成功",
"data":{
"srcUrl":"http://example.jpg"
}
}

// 错误时，平台返回错误码等信息，JSON 数据包示例如下：
{
"code": -1,
"msg":"系统错误"
}

```

AI智能播报语音模板

### **1、播报获取播报模板列表**

字段标识

pageList

接口描述

获取播报模板列表

承载协议

HTTPS

承载网络

公网

请求方式

POST

数据格式

请求参数以 form 表单方式提交（application/x-www-form-urlencoded）响应结果为 JSON 格式

约束

无

接口URL

https://vcp.21cn.com/open/u/aiTemplate/pageList

业务方向

接入方→能力开放

请求参数

字段标识

说明

数据类型

可空

aiType

1、区域入侵：2、客流统计：3、厨帽识别：4、抽烟识别：5、口罩识别：6、店员玩手机：7、火情预警：8、时光缩影9、人脸布控10、车牌布控、11、画面异常巡检

String

Y

templateStatus

模板状态：1-上架，2-下架

Integer

Y

audioStatus

音频状态：1-成功，2-失败

Integer

Y

pageNo

当前页

Integer

N

pageSize

显示条数

Integer

N

templateName

名称搜索

String

Y

accessToken

鉴权令牌

String

N

enterpriseUser

企业主(访问令牌获取方式为“用户无感知获取令牌”，需要传此参数)

String

Y

parentUser

父账号(enterpriseUser是子账号，则parentUser必填)

String

Y

响应参数

字段标识

说明

数据类型

可空

code

响应码

Integer

N

msg

响应信息

String

N

data

加密数据，data 采用 RSA 加密或 XXTea 加密（加密选择见 6.2. 公共请求参数）

String

Y

data 字段解密后说明

字段标识

说明

数据类型

可空

list

列表

Object\[\]

N

total

总数

Integer

N

list 内容字段说明

字段标识

说明

数据类型

可空

templateName

模板名称

String

N

aiName

ai名称

String

N

aiType

ai类型

Integer

N

id

id

Integer

N

templateContent

内容

String

N

audioStatus

音频状态：0-生成中，1-成功，2-失败

Integer

N

templateStatus

模板状态：1-上架，2-下架

Integer

N

audioId

音频id

String

N

mediaFormat

1-音频，2-录音，3-文本

Integer

N

wordSpeed

语速（正常-0，快速-250，慢速->-250）

String

N

playDuration

播放时长

Double

N

soundType

声音类型（yifeng-男生，chongchong-女生）

String

N

结果示例

```javascript
正常情况下，平台返回下述 JSON 数据包：
{
  	"code": 0,
  	"msg": "成功",
  	"data": "F3A4261D18CABD4E7C9926105CD9287F45393458D7F9DCBC3BE9650CCC4D25D33657DEE9034339747D398516FB0DB6A75581AB3D1D308AE27FBA2552486972C267D************BDA153ACAE918AFEF1AE369F9981CDF78730724E83FC53A2222800A7EC9FB2039E928FB7250904F927552E22F64DD2E73CD7CE8209681BFC9105"
} 
data 解密后数据包为：
{
  "code": 0,
  "msg": "成功",
  "data": {
    "total": 161,
    "list": [
      {
        "templateName": "森林防火****男",
        "aiName": "客流统计",
        "aiType": 2,
        "id": 3,
        "templateContent": "您已进入森林防火*******男",
        "audioStatus": 0,
        "templateStatus": 2,
        "audioId": "339",
        "mediaFormat": 3,
        "wordSpeed": "0",
        "playDuration": 0,
        "audioUrl": null,
        "soundType": null
      }
    ]
  }
}
错误时，平台返回错误码等信息，JSON 数据包示例如下：
{
	“code”: -1,
	“msg“:”系统错误”
}

```

### **2、播报获取播报详情**

字段标识

getInfo

接口描述

获取播报详情

承载协议

HTTPS

承载网络

公网

请求方式

POST

数据格式

请求参数以 form 表单方式提交（application/x-www-form-urlencoded）响应结果为 JSON 格式

约束

无

接口URL

https://vcp.21cn.com/open/u/aiTemplate/getInfo

业务方向

接入方→能力开放

请求参数

字段标识

说明

数据类型

可空

id

模板ID

Integer

N

响应参数

字段标识

说明

数据类型

可空

code

响应码

Integer

N

msg

响应信息

String

N

data

加密数据，data 采用 RSA 加密或 XXTea 加密

String

Y

data 字段解密后说明

字段标识

说明

数据类型

可空

templateName

模板名称

String

N

aiName

ai名称

String

N

aiType

ai类型

Integer

N

id

id

Integer

N

templateContent

内容

String

N

audioStatus

音频状态：0-生成中，1-成功，2-失败

Integer

N

templateStatus

模板状态：1-上架，2-下架

Integer

N

audioId

音频id

Integer

N

audioUrl

音频Url

String

N

mediaFormat

1-音频，2-录音，3-文本

Integer

N

soundType

声音类型（yifeng-男生，chongchong-女生）

String

N

wordSpeed

语速（正常-0，快速-250，慢速->-250）

String

N

playDuration

播放时长

Double

N

结果示例

```javascript
正常情况下，平台返回下述 JSON 数据包：
{
  	"code": 0,
  	"msg": "成功",
  	"data": "F3A4261D18CABD4E7C9926105CD9287F45393458D7F9DCBC3BE9650CCC4D25D33657DEE9034339747D398516FB0DB6A75581AB3D1D308AE27FBA2552486972C267D************BDA153ACAE918AFEF1AE369F9981CDF78730724E83FC53A2222800A7EC9FB2039E928FB7250904F927552E22F64DD2E73CD7CE8209681BFC9105"
} 
data 解密后数据包为：
{
  "code": 0,
  "msg": "成功",
  "data": {
    "templateName": "垃圾分类**-女",
    "aiName": "客流统计",
    "aiType": 2,
    "id": 2,
    "templateContent": "现在是非投放时间****-女",
    "audioStatus": 1,
    "templateStatus": 1,
    "audioId": "2",
    "mediaFormat": 3,
    "wordSpeed": "0",
    "playDuration": 6,
    "audioUrl": null,
    "soundType": "chongchong"
  }
}
错误时，平台返回错误码等信息，JSON 数据包示例如下：
{
	“code”: -1,
	“msg“:”系统错误”
}

```

### **3、播报获取音频详情**

字段标识

getAudioInfo

接口描述

获取音频详情

承载协议

HTTPS

承载网络

公网

请求方式

POST

数据格式

请求参数以 form 表单方式提交（application/x-www-form-urlencoded）响应结果为 JSON 格式

约束

无

接口URL

https://vcp.21cn.com/open/token/aiTemplate/getAudioInfo

业务方向

接入方→能力开放

请求参数

字段标识

说明

数据类型

可空

audioId

音频ID

Integer

N

响应参数

字段标识

说明

数据类型

可空

code

响应码

Integer

N

msg

响应信息

String

N

data

加密数据，data 采用 RSA 加密或 XXTea 加密（加密选择见 6.2. 公共请求参数）

String

Y

data 字段解密后说明

字段标识

说明

数据类型

可空

id

音频id

String

N

fileName

文件名称

String

N

containerId

桶ID，获取上传地址时返回的containerId

Integer

N

objectId

对象ID，获取上传地址时返回的objectId

Integer

N

content

内容

String

N

mediaFormat

媒体格式

Integer

N

soundType

声音类型

Integer

N

wordSpeed

语速

Integer

N

fileSize

文件大小

String

N

audioUrl

音频URL

String

N

playDuration

播放时长

Double

N

结果示例

```javascript
正常情况下，平台返回下述 JSON 数据包：
{
  	"code": 0,
  	"msg": "成功",
  	"data": "F3A4261D18CABD4E7C9926105CD9287F45393458D7F9DCBC3BE9650CCC4D25D33657DEE9034339747D398516FB0DB6A75581AB3D1D308AE27FBA2552486972C267D************BDA153ACAE918AFEF1AE369F9981CDF78730724E83FC53A2222800A7EC9FB2039E928FB7250904F927552E22F64DD2E73CD7CE8209681BFC9105"
} 
data 解密后数据包为：
{
  "code": 0,
  "msg": "成功",
  "data": {
    "id": 2,
    "fileName": "垃圾***女",
    "containerId": 5036,
    "objectId": "LF181/CLOUDBROADCAST/2cd664****c068ea",
    "content": "现在是非投放时间****女",
    "mediaFormat": 3,
    "soundType": "chongchong",
    "wordSpeed": 0,
    "fileSize": "201.3252",
    "audioUrl": "http://url/LF181/CLOUDBROADCAST/2cd664****ac068ea?response-content-disposition=at*****5%25B3.wav&Signature=LBa******xFc%3D&AWSAccessKeyId=L3GZ7WJ***YEW&Expires=166****81",
    "playDuration": 6.44
  }
}
错误时，平台返回错误码等信息，JSON 数据包示例如下：
{
	“code”: -1,
	“msg“:”系统错误”
}

```

AI客服

[1]: https://open.ctseelink.cn/portal/