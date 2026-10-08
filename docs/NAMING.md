# 名称选择与检索记录

检索日期：**2026-10-08（Asia/Shanghai）**。结论是选择 **ParcelCrop 4x6**，中文副标题 **2:3 面单裁切**，仓库名建议 `parcelcrop-4x6`，可执行文件为 `ParcelCrop.exe`。

`Parcel` 表示包裹场景，`Crop` 表示裁切，`4x6` 对应输出的 4×6 英寸尺寸；比例在副标题明确写为宽:高 = 2:3。1600×2400 px / 400 DPI 的物理尺寸是 101.6×152.4 mm，不能把它误写为精确的 100×150 mm。

## 检索范围与结果

使用公开网页精确名称/功能词搜索、GitHub 未登录公开仓库搜索 API（`in:name`），以及三个公开包注册表的精确包名接口。公开网页索引可能滞后，GitHub 搜索结果也会变化；以下为本次查询观察，不是品牌独占性证明。

| 名称 | 本次观察 | 判断 |
| --- | --- | --- |
| LabelTrim | [LabelTrim 网站](https://labletrim.com/)已用于同类面单裁切；GitHub `LabelTrim in:name` 返回 1 个仓库：[beingnitishh/LabelTrim](https://github.com/beingnitishh/LabelTrim) | 存在明显同名，停止沿用 |
| RatioLabel | [RatioLabel International GmbH](https://www.etiketten.de/)用于标签和物流相关业务；GitHub 返回 1 个含词仓库，属于无关的图表项目 | 不采用 |
| LabelCrop | [labelcrop.app](https://labelcrop.app/)及 [labelcrop.cloud](https://www.labelcrop.cloud/)等已用于同类裁切；GitHub `LabelCrop in:name` 返回 14 | 同类重名密集，不采用 |
| LabelFit | [App Store 的 LabelFit](https://apps.apple.com/de/app/labelfit-n%C3%A4hrwert-scanner/id6791918085)是营养标签扫描应用；GitHub `LabelFit in:name` 返回 3 | 基础名已用于软件，不采用 |
| LabelFit46 | GitHub `LabelFit46 in:name` 返回 0，网页未检出明显精确同名产品 | 与已有 LabelFit 品牌接近，不优先 |
| ParcelCrop | GitHub `ParcelCrop in:name` 返回 0，网页未检出明显同名面单软件 | 作为主名称 |
| ParcelCrop46 / ParcelCrop-4x6 | 两个 GitHub `in:name` 查询均返回 0；网页未检出明显精确同名产品 | 显示名采用更易读的 ParcelCrop 4x6 |

GitHub 主名称查询均返回 HTTP 200、`incomplete_results: false`。`in:name` 是仓库名称匹配，不等于精确品牌或商标查询；例如 RatioLabel 的一个结果是 `WeightedTreemaps-RatioLabels`。

对变体 `"parcel-crop" in:name` 的追加查询返回 2 个仓库，其中 [carlbomsdata/postnord-parcel-cropper](https://github.com/carlbomsdata/postnord-parcel-cropper)也是面单裁切工具，属于描述性近名，不能隐去。另两个更宽的追加查询（`"parcelcrop"`、`"parcel_crop" in:name`）遇到 API 403 限流，未将其计为零结果。`"ParcelCrop 4x6"` 公开仓库查询返回 0。

网页检索包含 `"LabelTrim"`、`"RatioLabel" software`、`"LabelCrop" software`、`"LabelFit" software`、`"LabelFit46"`、`"ParcelCrop"`、`"ParcelCrop" software`、`"ParcelCrop" label`、`"ParcelCrop 4x6"`、`"ParcelCrop46"`、`"parcel-crop" label` 及 `"parcel crop" software`。ParcelCrop 的部分返回是农业地块、GIS 论文变量等无关内容，没有据此认定存在同名面单产品。

精确包名 `parcelcrop` 的以下接口均返回 HTTP 404：

- [npm](https://registry.npmjs.org/parcelcrop)
- [PyPI](https://pypi.org/pypi/parcelcrop/json)
- [NuGet](https://api.nuget.org/v3-flatcontainer/parcelcrop/index.json)

这只说明查询时这些接口未返回该包，项目也不因此承诺向这些注册表发布。

## 结论边界

本次未查询各国商标注册库、公司名称登记、全部软件商店或域名注册状态，也未查询私有及未被索引项目。因此可表述为“在上述范围内未检出 ParcelCrop 4x6 的明显精确同类重名”，不能表述为“全球唯一”“商标已可用”或“名称已经注册”。后续公开发布或更大范围商业使用前可以扩大检索范围。

项目与本文列出的任何同名或近名第三方均无已确认的隶属、合作或授权关系。更名是避免混淆并说明功能，不代表取得第三方名称权利。
