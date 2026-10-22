# Visual Diff Lab --- Windows 原生应用开发任务书

> **文档版本：** 0.1.0\
> **决策基准日期：** 2026-10-22\
> **执行对象：** 本地 AI Coding Agent（Pi）\
> **产品状态：**
> 已完成初步产品探索；本文件用于实施，不代表未经验证的技术假设已经成立。\
> **核心原则：**
> 先验证技术风险，再逐阶段实现；编译成功不等于完成，必须真实运行并提供测试与视觉证据。

------------------------------------------------------------------------

## 0. 给 Pi 的执行指令

你负责实际创建工程、编码、构建、调试、测试、运行和整理证据。严格遵守以下规则：

1.  **先检查环境和官方文档，再开始编码。** 不要假设本机已经安装所需
    SDK、模板或运行时。
2.  按本文档的 Phase 0 → Phase 5
    顺序执行。每个阶段先定义验收条件，完成后提供证据，再进入下一阶段。
3.  不要擅自扩展 MVP。遇到不在范围内的功能，记录到
    `docs/decisions/DEFERRED.md`，不自行加入。
4.  不要用模拟界面、静态图片或占位按钮冒充已完成的功能。关键功能必须能在真实
    Windows 窗口中操作。
5.  不要仅凭编译成功宣告完成。每个核心交互都必须有对应的测试、运行验证或明确记录的人工验收。
6.  不使用 Preview、Experimental 或已弃用的 API
    作为产品基础。确实需要某项预览能力时，先说明必要性并等待产品决策。
7.  依赖应尽量少且版本固定。新增 NuGet/npm
    依赖前，记录用途、许可、维护状态、版本和替代方案。
8.  不要伪造测试、性能数字、截图、设备信息或官方支持结论。无法执行的检查要明确标记为
    `NOT RUN`，并说明原因。
9.  保持变更小而可审查。每个阶段更新
    README、测试结果和决策记录；不要在一个提交中混合大量无关改动。
10. 如环境不具备 Windows GUI，允许继续完成可独立验证的 Core、文档和 CI
    工作，但必须把 UI 运行验收列为阻塞项，不能声称已经完成。

### 阶段报告格式

每个阶段结束时输出：

-   **完成内容**
-   **修改文件**
-   **执行的命令**
-   **测试结果**：通过数、失败数、未运行数
-   **真实运行验证**：操作步骤、结果、截图路径
-   **性能数据**（如适用）：机器配置、OS
    build、测试图片、重复次数和测量方式
-   **已知问题与风险**
-   **下一阶段计划**

------------------------------------------------------------------------

## 1. 产品定义

### 1.1 产品名称

**Visual Diff Lab**

英文描述：A local-first native Windows workspace for inspecting visual
differences between web UI screenshots.

### 1.2 产品定位

面向前端开发者和 QA 工程师的原生 Windows 桌面应用，用于检查两张网页/UI
截图之间的视觉变化。应用负责让用户准确查看、定位、过滤和导出差异，不负责自动运行网页或判断某项变化是不是
Bug。

### 1.3 典型用户任务

1.  用户修改 CSS 或前端组件后，打开修改前和修改后的截图。
2.  使用并排、滑动揭示、透明度叠加或差异热力图观察变化。
3.  放大并平移到具体 UI 区域。
4.  对时间戳、随机头像等非关键动态区域建立忽略矩形。
5.  检查差异像素数量和比例。
6.  导出差异图或并排图，附到 Issue、PR 或测试报告中。

### 1.4 产品原则

-   本地优先：源图默认不上传、不联网、不需要账户。
-   画布优先：图片是视觉中心，控件不应挤占主要工作区。
-   精确而可预测：坐标、缩放、阈值和忽略区域行为有明确定义。
-   Windows 原生：使用 WinUI 3 与 Fluent Design 的平台惯例，不模仿
    macOS。
-   透明反馈：长任务、失败、取消、权限错误均有明确状态。
-   可测试：算法独立于 UI；所有核心行为可重复验证。
-   小而完整：不因技术兴趣增加不必要功能。

### 1.5 MVP 必须包含

-   导入两张本地图片：文件选择与拖放。
-   支持 PNG、JPEG、BMP；不支持的格式显示可理解的错误。
-   交换 A/B 图片、重新选择图片、清空当前对比。
-   四种模式：并排、滑动揭示、透明度叠加、差异热力图。
-   同步缩放与平移、适应窗口、原始像素比例。
-   可调逐像素差异阈值。
-   差异像素数、参与比较像素数、差异比例。
-   创建、选择、删除忽略矩形；支持对区域操作撤销。
-   导出差异叠加 PNG 与并排对比 PNG。
-   深色、浅色和跟随系统主题。
-   键盘快捷键、拖放、无障碍名称和可见焦点。
-   错误处理、自动化测试、性能记录、真实视觉验收。
-   README、许可证、CI、版本发布准备。

### 1.6 明确非目标

MVP 不实现：

-   浏览器自动化、网页抓取、Playwright 执行或自动截图。
-   AI 分析、云端处理、账号、同步、遥测。
-   视频、GIF 时间轴或动画比较。
-   文件夹批量比较、项目管理或历史数据库。
-   PSD、RAW、PDF 等专业格式。
-   自动平移/缩放/旋转配准。
-   通用图像编辑器功能。
-   OCR、自动缺陷分类或自动判断视觉变化是否正确。
-   插件系统、脚本系统或复杂的扩展架构。

新增功能必须经过产品决策，不得以"实现很简单"为由擅自加入。

------------------------------------------------------------------------

## 2. 技术基线与 Phase 0 核验

### 2.1 当前参考基线

以 2026-10-22 的官方资料为参考：

-   Visual Studio 2026：18.11.1（官方发布日期 2026-10-20）。
-   C# / .NET：.NET 10 LTS；官方当前补丁为 10.0.13。
-   Windows App SDK：2.5.1 Stable。
-   Windows SDK：优先验证 10.0.26100.9457
    兼容性基线；若实际依赖或工具链需要更新 SDK，可验证
    10.0.28000.2957，但不得仅为追新而升级。
-   UI 框架：WinUI 3 + XAML。
-   候选画布：Win2D（`Microsoft.Graphics.Win2D`），须在 Phase 0 验证与
    WinUI 3、Windows App SDK 及目标系统的兼容性。
-   UI 自动化：优先评估 Appium Windows Driver；微软文档指出传统
    WinAppDriver 不再积极开发。
-   发布：优先 MSIX；Store 与 GitHub Release 分别准备。

**重要：**
上述版本是核验起点，不是绕过验证的许可。开始编码前再次访问官方来源，检查是否有更新的稳定补丁、兼容性限制、已知问题或支持状态变化。不要使用
Preview/Experimental 版本。

### 2.2 目标操作系统

产品正式支持策略：**Windows 11 25H2 及以上版本**。

测试至少覆盖：

-   Windows 11 25H2（最低正式支持版本）。
-   Windows 11 26H2（当前新版本验证环境）。

必须在 Phase 0 验证 WinUI 模板、Windows App SDK、SDK target version 与
MSIX manifest 的实际约束。不要把"文档中支持某个
API"误认为"应用包的最低系统版本可以任意设置"。如果打包工具链不允许所选
manifest 版本，应记录实际限制并给出方案，不得静默降低产品支持承诺。

### 2.3 官方资料

开始工作前检查以下资料：

-   Windows App SDK 稳定/预览/实验发布通道：\
    https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/release-channels
-   Windows App SDK 下载：\
    https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/downloads
-   Windows SDK 下载：\
    https://learn.microsoft.com/en-us/windows/apps/windows-sdk/downloads
-   Visual Studio 2026 发布说明：\
    https://learn.microsoft.com/en-us/visualstudio/releases/2026/release-notes
-   .NET 支持政策：\
    https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core
-   WinUI 3：\
    https://learn.microsoft.com/en-us/windows/apps/winui/
-   Win2D 快速入门：\
    https://learn.microsoft.com/en-us/windows/apps/develop/win2d/quick-start
-   Windows 图像处理：\
    https://learn.microsoft.com/en-us/windows/apps/develop/media-authoring-processing/imaging
-   WinUI 应用测试：\
    https://learn.microsoft.com/en-us/windows/apps/develop/testing/
-   Windows 应用分发选择：\
    https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/choose-distribution-path
-   Windows 支持版本：\
    https://learn.microsoft.com/en-us/windows/release-health/supported-versions-windows-client
-   Playwright 截图断言（作为概念参考，不作为运行依赖）：\
    https://playwright.dev/docs/test-snapshots

### 2.4 环境检查命令（PowerShell）

先创建 `artifacts/phase-0/`，把命令输出保存到文本文件。

``` powershell
$ErrorActionPreference = "Stop"

New-Item -ItemType Directory -Force artifacts/phase-0 | Out-Null

dotnet --info 2>&1 |
  Tee-Object artifacts/phase-0/dotnet-info.txt

dotnet --list-sdks 2>&1 |
  Tee-Object artifacts/phase-0/dotnet-sdks.txt

dotnet --list-runtimes 2>&1 |
  Tee-Object artifacts/phase-0/dotnet-runtimes.txt

dotnet new list 2>&1 |
  Tee-Object artifacts/phase-0/dotnet-templates.txt

Get-ComputerInfo |
  Select-Object WindowsProductName, WindowsVersion, OsBuildNumber, OsArchitecture |
  Format-List |
  Out-File artifacts/phase-0/windows-info.txt

Get-Command msbuild -ErrorAction SilentlyContinue |
  Format-List * |
  Out-File artifacts/phase-0/msbuild-command.txt
```

另外记录：

-   Visual Studio 完整版本与已安装工作负载。
-   Windows SDK 实际安装版本。
-   当前用户权限与是否可以运行桌面 GUI。
-   当前 Git、Node.js（仅在决定使用 Appium 后需要）版本。
-   Win2D 官方包的最新稳定版本、许可证和兼容性。

### 2.5 Phase 0 技术 Spike

在开始产品完整实现前，制作最小可丢弃的验证原型，验证：

1.  WinUI 3 窗口可以正常创建、启动、关闭。
2.  Win2D `CanvasControl` 能显示一张图片。
3.  两张 4K PNG 能够解码、显示、缩放和平移。
4.  画布可以绘制图像叠加层与矩形选区。
5.  在窗口调整大小、切换主题和关闭窗口时，没有明显资源泄漏或渲染错误。
6.  能执行一个后台差异计算任务，计算期间 UI 仍响应操作。
7.  可以在目标 Windows 版本上构建并运行 MSIX 测试包，或记录阻塞原因。

如果 Win2D 在目标组合上有兼容性或维护风险，先评估 WinUI 自身可用的渲染
API，再决定是否换方案。不要在未经验证时同时引入多个绘图框架。

Phase 0 交付：

-   `docs/decisions/ADR-0001-ui-framework.md`
-   `docs/decisions/ADR-0002-rendering.md`
-   `artifacts/phase-0/` 环境记录、运行截图和性能基线
-   `docs/reports/phase-0-report.md`

------------------------------------------------------------------------

## 3. UI/UX 设计规范

### 3.1 设计方向：Quiet Precision

关键词：安静、精确、低干扰、清晰、可信。

视觉原则：

-   以中性色构成基础界面。
-   差异高亮使用固定且一致的强调色。
-   图片画布是视觉中心，工具栏和检查面板为其服务。
-   优先复用 WinUI 原生控件和 Fluent Design。
-   不自绘系统标题栏，除非有明确且已验证的必要性。
-   不滥用渐变、阴影、玻璃材质、卡片和大圆角。
-   深浅主题都要完整设计，不能只在深色主题上完成。
-   不依赖颜色作为唯一状态指示；选择状态应有轮廓、标签或其他提示。

### 3.2 参考应用与用途

-   **WinUI 3 Gallery / Fluent Design：**
    控件、主题、焦点、菜单、弹出层和平台一致性。
-   **Microsoft PowerToys：** 专业工具的设置组织、状态反馈和紧凑布局。
-   **Figma 等画布应用：**
    缩放、平移、选区和工具状态；只借鉴交互原则，不复制复杂工作流。
-   **OICAT / Image Compare：**
    同类对比模式、差异图、阈值和导出；用于识别基础功能与差异化空间。
-   **Windows Photos：** 图片查看与基础命令的 Windows 使用习惯。

参考资源：

-   WinUI Gallery：https://github.com/microsoft/WinUI-Gallery
-   PowerToys：https://github.com/microsoft/PowerToys
-   OICAT：https://github.com/MalumaDev/OICAT
-   Image Compare：https://github.com/gimletlove/imagecompare

所有截图和参考说明放在
`docs/design/references/`。记录参考应用、具体借鉴点、不可照搬之处和来源
URL。不要将参考截图当成最终产品界面。

### 3.3 主窗口结构

主窗口由以下部分组成：

1.  **标题栏：**
    原生窗口行为；显示应用名，正常响应最小化、最大化、关闭。
2.  **命令栏：** Open A、Open B、交换图片、模式切换、导出。
3.  **主画布：**
    占据窗口主要面积，负责图像、叠加、热力图、滑动线和忽略区域。
4.  **Inspector 检查面板：**
    图片尺寸、格式、阈值、差异统计、忽略区域列表；可折叠。
5.  **状态栏：** 缩放比例、计算状态、差异摘要和操作反馈。

初始设计尺寸以 1280×800 有效像素为参考；最低窗口尺寸暂定
1024×640。必须实测窄窗口下布局，不得出现控件重叠、裁切关键命令或无法关闭面板的情况。若测试表明最低尺寸不合理，应在设计记录中修订并给出证据。

### 3.4 空状态

首次打开时：

-   主画布显示简洁的拖放区域。
-   提供 `Open Image A` 与 `Open Image B` 操作。
-   说明支持 PNG、JPEG、BMP。
-   不显示没有数据的图表、虚假统计或装饰性卡片。
-   拖入单张图片后明确显示 A 或 B 已载入，并提示下一步。
-   如果用户只导入一张图片，应用仍然可操作，不显示错误对话框。

### 3.5 四种比较模式

**Side-by-side（并排）** - A 与 B 以自然像素尺寸显示在各自视口内。 -
使用共同的缩放比例和同步平移。 -
图片不拉伸；尺寸不同必须明确显示边界差异。

**Wipe（滑动揭示）** - 两图使用同一坐标原点和共同缩放。 -
分割线可以通过鼠标与键盘调整。 -
分割线不能遮挡到无法操作；拖动期间实时更新。 -
切换模式后尽可能保留当前视野。

**Overlay（透明度叠加）** - A、B 叠加在同一坐标系。 -
提供透明度滑块和明确的当前值。 -
叠加比例变化应立即反馈，不应重新解码原始图片。

**Heatmap（差异热力图）** - 以可识别的基准图为底，叠加差异颜色。 -
支持查看差异和原图之间的关系。 -
颜色、透明度和阈值规则固定并写入文档。 -
不同阈值重新计算时，显示计算状态；旧结果不得冒充新结果。

### 3.6 画布交互

-   滚轮缩放以指针位置为锚点。
-   `Space + Drag` 平移画布。
-   提供 `Fit` 与 `100%` 两种明确不同的命令。
-   双击画布是否重置视图须与快捷键行为一致；不要引入隐藏手势。
-   视图变换使用统一的数学模型；渲染坐标与图像坐标不得混为一谈。
-   处理高 DPI、窗口尺寸变化和屏幕缩放变化。
-   快速连续缩放/平移不能导致坐标漂移或忽略区域错位。

### 3.7 忽略区域

-   用户进入明确的区域选择模式后，在画布上拖动创建矩形。
-   区域保存在比较画布坐标系中，而不是屏幕坐标。
-   支持选中、删除和撤销区域添加/删除。
-   多个区域允许重叠；算法使用区域的并集进行屏蔽。
-   区域越界时裁剪到比较画布范围。
-   区域列表显示序号或简洁标签，并提供删除操作。
-   选区必须有足够对比度；不能仅依靠填充色表达选择状态。
-   取消创建时不得留下半成品区域。
-   忽略区域只排除差异计算，不修改原始图片。

### 3.8 主题、字体和无障碍

-   主题：Light、Dark、System。
-   字体优先使用 Segoe UI Variable 与系统默认文本样式。
-   遵守 Fluent 控件默认间距、圆角和交互状态，避免无依据地重写样式。
-   所有命令和输入控件必须有 AutomationProperties.Name
    或等效的可访问名称。
-   焦点顺序符合视觉顺序；焦点指示清晰。
-   关键功能可以通过键盘完成。
-   支持系统文字缩放和高对比度场景。
-   对比高亮必须有可辨认的非颜色提示或统计辅助。
-   禁止只在鼠标悬停时才显示核心命令。

### 3.9 快捷键

以下是应用约定，必须实现并测试：

  快捷键           行为
  ---------------- --------------------------------------------------------
  `Ctrl+O`         打开图片（先打开 A 或 B 的选择流程，避免覆盖当前图片）
  `Ctrl+E`         导出结果
  `1`              Side-by-side
  `2`              Wipe
  `3`              Overlay
  `4`              Heatmap
  `F`              Fit to view
  `0`              100% 原始像素比例
  `Space + Drag`   平移画布
  `Esc`            取消当前选区或退出当前工具模式
  `Ctrl+Z`         撤销最近的可逆区域操作

模式数字快捷键仅在画布焦点下、且焦点不在文本输入控件中时生效。所有快捷键必须在
Help 或菜单中可发现；如果与系统或控件冲突，需在 ADR 中记录并统一修改。

------------------------------------------------------------------------

## 4. 图像与差异算法规范

### 4.1 输入与规范化

-   首版格式：PNG、JPEG、BMP。
-   读取文件失败、文件被占用、权限拒绝、文件损坏和超大图像都必须有可理解的反馈。
-   解码后转成统一的像素格式，优先评估 `SoftwareBitmap` 与 BGRA8 / 预乘
    Alpha 的路径。
-   颜色空间约定要明确；MVP 按规范化的 sRGB 颜色处理。
-   不修改源文件，不覆盖输入图片。
-   对 EXIF 方向、ICC profile、透明通道的实际 API
    行为要通过测试样本验证并记录。

### 4.2 比较坐标系与不同尺寸

设 A 尺寸为 `WA × HA`，B 尺寸为 `WB × HB`。

-   比较画布尺寸：`W = max(WA, WB)`，`H = max(HA, HB)`。
-   A、B 都以比较画布的左上角 `(0,0)` 对齐。
-   不自动缩放、平移或配准输入图片。
-   对坐标 `(x,y)`：
    -   两图都有像素：按像素差异规则比较。
    -   只有一图有像素：该位置视为差异。
    -   两图都没有像素：不属于比较画布。
-   忽略区域的并集从比较统计中排除。
-   `ComparedPixelCount` 是比较画布中未被忽略的像素数量。
-   `DifferentPixelCount` 是未被忽略且被判定为不同的像素数量。
-   `DiffRatio = DifferentPixelCount / ComparedPixelCount`。若分母为
    0，显示 `N/A`，不能除零。

### 4.3 MVP 差异规则

-   先使用确定性的逐像素比较，不引入 AI 或自动配准。
-   规范化为统一通道顺序和 Alpha 表示后，对 R/G/B/A 通道差值取最大值。
-   如果最大通道差值大于用户阈值，则标记该像素为差异。
-   阈值范围：0--64（以 8-bit 通道值为单位）。
-   默认阈值建议先设为
    16，但必须用真实截图样本验证；如验证结果不合理，可在报告中提出调整。
-   阈值 0 应检测所有规范化通道存在差异的像素。
-   对不可见 RGB 数据的行为必须通过预乘 Alpha
    规范化测试，防止透明像素隐藏的 RGB 垃圾值制造假差异。
-   热力图、统计数字与导出结果必须来自同一份
    `DiffResult`，避免算法规则不一致。

### 4.4 导出定义

MVP 支持两种导出：

1.  **Difference Overlay PNG**
    -   尺寸为比较画布大小。
    -   基底使用 A；在差异像素上叠加固定差异颜色。
    -   B 超出 A 的新增区域需要以可辨识颜色标记。
    -   A 超出 B 的缺失区域需要以可辨识颜色标记。
    -   忽略区域不得显示为普通差异。
    -   输出必须不包含应用窗口边框、鼠标光标或工具栏。
2.  **Side-by-side PNG**
    -   左右并排呈现 A 与 B。
    -   使用自然像素尺寸，不拉伸图片。
    -   尺寸不同的部分以中性背景补齐。
    -   可以包含简洁的 `A` / `B`
        标签栏，但标签样式必须固定且不会遮挡图像。
    -   输出尺寸在预览或保存前明确说明；遇到超大输出时应提示内存风险或采用安全的处理策略。

保存使用系统保存文件对话框。导出失败时保留当前会话状态并允许重试。

### 4.5 参考实现测试向量

必须建立程序生成的测试图片，至少包含：

-   A 与 B 完全相同。
-   只有一个像素不同。
-   差值恰好等于阈值。
-   差值刚好超过阈值。
-   颜色通道不同但 Alpha 为 0 的像素。
-   Alpha 不同、RGB 相同的像素。
-   A 大于 B。
-   B 大于 A。
-   两图都为 0×0 的无效输入（必须拒绝，不得崩溃）。
-   忽略区域覆盖全部画布。
-   忽略区域部分越界。
-   多个重叠忽略区域。
-   极端宽高比与边界像素。
-   解码损坏文件和不支持的格式。

所有测试使用确定性数据，不能依赖人工目测作为算法唯一验收方式。

------------------------------------------------------------------------

## 5. 工程架构与目录

建议采用以下结构；允许在技术验证后做有记录的调整：

``` text
VisualDiffLab/
├─ VisualDiffLab.sln
├─ global.json
├─ Directory.Build.props
├─ Directory.Packages.props
├─ src/
│  ├─ VisualDiffLab.App/
│  │  ├─ App.xaml
│  │  ├─ MainWindow.xaml
│  │  ├─ Views/
│  │  ├─ ViewModels/
│  │  ├─ Commands/
│  │  ├─ Services/
│  │  ├─ Assets/
│  │  └─ Package.appxmanifest
│  ├─ VisualDiffLab.Core/
│  │  ├─ Models/
│  │  ├─ Comparison/
│  │  ├─ IgnoreRegions/
│  │  └─ Validation/
│  ├─ VisualDiffLab.Imaging/
│  │  ├─ Decoding/
│  │  ├─ PixelBuffers/
│  │  └─ Export/
│  └─ VisualDiffLab.Rendering/
│     ├─ Canvas/
│     ├─ ViewTransform/
│     └─ Overlays/
├─ tests/
│  ├─ VisualDiffLab.Core.Tests/
│  ├─ VisualDiffLab.Imaging.Tests/
│  └─ VisualDiffLab.UITests/
├─ docs/
│  ├─ product/
│  ├─ design/
│  │  └─ references/
│  ├─ decisions/
│  ├─ testing/
│  └─ reports/
├─ scripts/
├─ assets/
│  └─ test-images/
├─ artifacts/
├─ README.md
├─ LICENSE
├─ CONTRIBUTING.md
├─ CODE_OF_CONDUCT.md
└─ .github/
   └─ workflows/
```

### 5.1 模块职责

-   **App：** WinUI 窗口、控件、命令绑定、生命周期和用户反馈。
-   **Core：**
    比较模型、阈值算法、忽略区域、统计和可逆编辑操作；不得引用 WinUI 或
    Win2D。
-   **Imaging：** 图片解码、规范化、像素缓冲、编码与导出。
-   **Rendering：** 画布绘制、图像变换、裁剪、叠加、热力图和选区显示。
-   **Tests：** 可重复的单元测试和集成测试。
-   **UITests：** 通过真实 UI Automation 树执行用户工作流。

使用 MVVM 组织 UI 状态，但不要为了模式本身引入大型框架。若原生
`ObservableObject`、`ICommand` 或轻量自实现足够，就不必增加 MVVM
第三方包。

### 5.2 核心模型建议

-   `ImageDocument`：来源、尺寸、格式、规范化像素数据、必要的元信息。
-   `ComparisonSession`：A、B、模式、阈值、忽略区域与当前会话状态。
-   `ComparisonSettings`：阈值和显示设置。
-   `IgnoreRegion`：稳定 ID、矩形坐标、可选标签。
-   `DiffResult`：比较画布尺寸、差异掩码或差异像素、比较数、差异数、比例、所用阈值和会话版本号。
-   `ViewTransform`：缩放比例、平移偏移、适应窗口计算。
-   `ExportOptions`：导出类型、路径和标签选项。

不要把大型位图反复复制到多个
ViewModel。明确资源所有权、缓冲区生命周期和释放策略。后台任务取消后，旧结果不得覆盖较新阈值或新图片的结果；可使用递增版本号或取消令牌验证结果是否仍有效。

### 5.3 构建质量

-   使用 `global.json` 固定所需 .NET SDK feature band（基于 Phase 0
    实测版本）。
-   使用 `Directory.Packages.props` 统一管理 NuGet 版本。
-   开启 Nullable Reference Types。
-   适当启用分析器和警告；不要通过全局禁用警告来追求绿色构建。
-   不提交本地绝对路径、用户目录、密钥或测试机器私有配置。
-   资源必须在窗口关闭、任务取消和设备丢失时正确释放。

------------------------------------------------------------------------

## 6. 分阶段实施与验收

### Phase 0 --- 环境与技术验证

**任务** - 核验官方稳定版本和本机开发环境。 - 建立 WinUI 3 窗口。 - 验证
Win2D、图片加载、绘制、缩放、平移和后台计算。 - 测量两张 4K PNG
的初次加载、首次比较、内存和基本交互。 - 验证目标 OS、SDK
target、manifest 与 MSIX 兼容性。

**验收** - 应用可启动和正常退出。 - 两张 4K 图片可以显示。 -
缩放、平移、叠加可实际操作。 - 后台计算不会冻结 UI。 -
输出环境报告、截图和性能基线。 - 对渲染方案做出 ADR 决策。

### Phase 1 --- Core 算法与测试样本

**任务** - 建立独立 Core 类库。 - 实现比较画布、尺寸差异、阈值、Alpha
规则、忽略区域掩码和统计。 - 建立确定性测试向量。 -
为耗时计算添加取消与版本检查。

**验收** - 所有算法测试通过。 - 测试不依赖 UI 线程。 -
对边界条件有明确断言。 - 输出测试报告和算法规则文档。 -
同一输入与设置反复运行，结果一致。

### Phase 2 --- 主工作区与基础交互

**任务** - 完成窗口布局、空状态、文件选择和拖放。 - 实现 A/B
交换、重新选择、清空和错误提示。 - 实现并排、滑动揭示、叠加、热力图。 -
实现同步缩放、平移、Fit、100%。 - 实现阈值与统计显示。 -
实现主题切换和检查面板折叠。

**验收** - 核心操作全部可在真实窗口执行。 - 模式切换保留视图状态。 -
不支持或损坏文件不会使应用崩溃。 - 窗口缩放不出现明显布局破坏。 -
保存每个模式的真实运行截图。

### Phase 3 --- 忽略区域与导出

**任务** - 实现矩形区域创建、选择、删除和撤销。 -
实现区域坐标映射和越界处理。 - 将忽略掩码应用到统计、热力图和导出。 -
实现 Difference Overlay PNG 与 Side-by-side PNG 导出。 -
处理保存失败、权限拒绝和取消操作。

**验收** - 忽略区域不会影响源图。 -
所有视图变换下，区域都保持在正确图像坐标。 -
导出像素结果与当前比较设置一致。 - 输出 PNG
可由外部图片查看器正常打开。 - 导出失败后可重试，当前会话状态不丢失。

### Phase 4 --- 质量、性能和可访问性

**任务** - 完成 UI Automation IDs 与可访问名称。 - 建立 Appium Windows
Driver UI 测试或记录合适的替代方案。 - 测试深浅主题、高
DPI、最小窗口、最大化与恢复。 -
测试快速连续切换模式、阈值变化、取消计算和关闭窗口。 - 使用真实 4K
与大尺寸图片进行性能测量。 -
检查资源释放、异常处理、静态分析和依赖安全。

**验收** - 单元测试和集成测试通过。 - UI
自动化覆盖核心流程，或明确标注未运行原因。 -
没有已知的阻塞级崩溃或数据错误。 - 性能报告包含设备、OS
build、图片尺寸、重复次数和测量方法。 - 所有核心状态有真实截图证据。

### Phase 5 --- 发布准备

**任务** - 完成 MSIX 打包与干净环境安装验证。 -
验证安装、启动、卸载和升级路径。 - 建立 GitHub Actions
构建与测试流程。 - 编写 README、贡献指南、许可证、隐私说明和发布说明。 -
建立版本号和 Release checklist。

**验收** - Release 构建可重复完成。 - 包可以在目标 Windows
环境安装、启动和卸载。 - CI 能构建应用并运行可执行的测试。 -
不包含用户私人图片、路径、凭据或机器专属配置。 -
文档明确区分已验证与尚未验证的功能。

------------------------------------------------------------------------

## 7. 具体构建、测试与诊断命令

以下命令在 PowerShell 中执行。项目名和路径应保持一致；如果 WinUI
模板生成的项目结构不同，更新本文档中的命令和报告，不要假装命令已成功执行。

### 7.1 还原与构建

``` powershell
$ErrorActionPreference = "Stop"

dotnet restore .\VisualDiffLab.sln

dotnet build .\VisualDiffLab.sln `
  --configuration Debug `
  --no-restore

dotnet build .\VisualDiffLab.sln `
  --configuration Release `
  --no-restore
```

### 7.2 测试

``` powershell
dotnet test .\VisualDiffLab.sln `
  --configuration Release `
  --no-build `
  --logger "trx;LogFileName=core-tests.trx" `
  --results-directory .\artifacts\test-results
```

如果 solution 中包含无法通过通用 `dotnet test` 执行的 UI 测试项目，应将
Core、Imaging 和 UI 自动化测试分开执行，并在 README 中记录正确命令。

### 7.3 静态分析与依赖

``` powershell
dotnet list .\VisualDiffLab.sln package --vulnerable --include-transitive
dotnet list .\VisualDiffLab.sln package --outdated
```

依赖审查必须记录结果。若命令因网络或 NuGet feed
问题失败，应记录错误并重试，不得把失败当成"没有漏洞"。

### 7.4 真实运行检查

在实际目标 Windows 环境中：

1.  启动 Release 构建的应用。
2.  导入两张真实网页/UI 截图。
3.  依次操作四种比较模式。
4.  缩放到 100%，放大、平移并回到 Fit。
5.  创建忽略区域并观察差异统计变化。
6.  导出两种 PNG，并用外部查看器打开。
7.  切换浅色/深色主题。
8.  调整窗口大小、最大化和恢复。
9.  尝试无效文件和不可写入的导出位置。
10. 关闭应用并确认无异常退出或资源残留。

将截图和日志存入
`artifacts/acceptance/`，文件名要包含阶段、场景和主题，例如：

``` text
artifacts/acceptance/
├─ phase-2-empty-light.png
├─ phase-2-side-by-side-dark.png
├─ phase-2-wipe-dark.png
├─ phase-2-overlay-light.png
├─ phase-2-heatmap-dark.png
├─ phase-3-ignore-region.png
├─ phase-3-export-diff.png
├─ phase-4-high-dpi.png
└─ phase-4-performance-report.md
```

这些文件名是建议的证据布局；只有真实创建的文件才可列为完成证据。

------------------------------------------------------------------------

## 8. 性能与资源管理

### 8.1 暂定目标

以两张 3840×2160 PNG 为首要基准：

-   首次加载并生成差异结果：目标 ≤ 2.5 秒。
-   已生成结果的对比模式切换：目标 ≤ 100 ms。
-   单次差异计算：目标 ≤ 1.5 秒。
-   正常拖动与缩放过程中不出现持续性卡顿。
-   常规工作集目标 ≤ 500 MB。

这些是待实测的目标，不是保证。若未达到，先记录基线、分析瓶颈并优化；不要通过降低测试质量、隐藏延迟或删掉功能让报告变绿。

### 8.2 测量规范

每份性能报告必须记录：

-   CPU、GPU、RAM、OS build、DPI 缩放。
-   输入图片的格式、宽高、文件大小。
-   是否冷启动、是否已解码、是否重复运行。
-   每个场景至少进行 5 次正式测量；报告中位数与最大值。
-   使用的测量工具、日志位置和版本。
-   任何预热步骤与排除项。

### 8.3 资源生命周期

-   大型像素缓冲不得在 UI 每次刷新时重新分配。
-   画布重绘不应触发重新解码。
-   阈值变化应取消或淘汰过期计算结果。
-   关闭窗口时取消正在执行的任务。
-   正确释放 Win2D / Direct2D 相关资源。
-   不允许后台任务将已过期的结果覆盖当前会话。
-   对极大尺寸图片设置明确的资源上限或失败策略，不能因为内存不足而直接崩溃。

------------------------------------------------------------------------

## 9. 数据安全与错误处理

-   默认本地处理，不联网上传图像。
-   不实现遥测、账号或后台同步。
-   不记录图片内容或图像像素到日志。
-   日志只记录必要的操作类型、耗时、异常类型和可选的文件格式/尺寸；默认不记录完整用户路径。
-   不请求管理员权限。
-   不修改输入图片。
-   导出不覆盖输入文件，除非用户明确在保存对话框中选择同一路径并确认覆盖。
-   对取消、权限拒绝、磁盘空间不足、损坏文件、解码失败、计算取消和应用关闭进行测试。
-   用户可理解的错误提示与诊断日志分离。
-   崩溃或异常不能使源文件被修改。

------------------------------------------------------------------------

## 10. CI/CD、GitHub 与发布

### 10.1 GitHub Actions

CI 至少执行：

1.  检出代码。
2.  安装锁定版本的 .NET SDK。
3.  还原依赖。
4.  Release 构建。
5.  Core / Imaging 单元测试。
6.  依赖漏洞检查。
7.  上传 TRX 测试报告和构建日志。
8.  在适当的 Release 或手动触发工作流中生成 MSIX 测试包。

UI 自动化可能依赖交互式桌面会话、Appium server 和 Windows Driver
环境。不要假设每种托管 GitHub runner 都能稳定执行桌面 UI 测试。先验证
runner；若不可靠，把 UI 测试放到专用自托管 Windows runner
或明确的本地验收流程中，并在 CI 中清楚显示该门槛。

### 10.2 建议的工作流文件

-   `.github/workflows/ci.yml`：构建、单元测试、静态分析。
-   `.github/workflows/package.yml`：手动或标签触发的 MSIX 打包。
-   `.github/ISSUE_TEMPLATE/bug_report.yml`
-   `.github/ISSUE_TEMPLATE/feature_request.yml`

### 10.3 发布渠道

**Microsoft Store** - 首选 MSIX。 - 需要真实完成 Partner Center
账号、商店资料、图标、截图、隐私声明和认证流程。 - Store
提交前执行干净环境安装和升级验证。 - Store
是否最终发布由项目维护者决定。

**GitHub Releases** - 发布源码、版本说明、测试摘要和安装包。 -
不要承诺未经签名或验证的安装程序有无警告体验。 - 不在 Release
中包含个人路径、机器信息或测试用户图片。 - 明确列出最低支持 Windows
版本和已知问题。

### 10.4 开源文件

必须准备：

-   `README.md`
-   `LICENSE`（由项目所有者选择合适的开源许可证；不要擅自宣称许可证已确定）
-   `CONTRIBUTING.md`
-   `CODE_OF_CONDUCT.md`
-   `SECURITY.md`
-   `CHANGELOG.md` 或 GitHub Releases 版本说明
-   `docs/privacy.md`

------------------------------------------------------------------------

## 11. 最终验收清单

只有全部必要项目通过，才可以将 MVP 标记为完成。

### 功能

-   [ ] 导入两张图片、交换、重新选择和清空。
-   [ ] 四种比较模式可实际操作。
-   [ ] 同步缩放、平移、Fit 与 100% 正常。
-   [ ] 阈值改变后统计、热力图和导出一致更新。
-   [ ] 忽略区域可创建、选择、删除、撤销。
-   [ ] 尺寸不一致时行为符合规范。
-   [ ] 两种 PNG 导出均正确。

### 质量

-   [ ] Core 与 Imaging 单元测试通过。
-   [ ] 关键 UI 流程通过 UI 自动化或有明确的人工验收证据。
-   [ ] 损坏文件、权限拒绝、取消和失败状态经过测试。
-   [ ] 没有已知阻塞级崩溃或数据破坏问题。
-   [ ] 依赖、许可和漏洞检查结果已记录。

### 视觉与可访问性

-   [ ] 空状态、四种模式、忽略区域和导出状态均有真实截图。
-   [ ] 深色与浅色主题通过验收。
-   [ ] 窄窗口、最大化/恢复和高 DPI 通过验收。
-   [ ] 键盘操作、焦点状态和可访问名称通过检查。
-   [ ] 没有明显的控件重叠、裁切、布局跳动或无法解释的空白。

### 性能与发布

-   [ ] 4K 性能报告包含可复现的测试方法。
-   [ ] 资源释放与取消行为通过测试。
-   [ ] Release 构建和 MSIX 打包成功。
-   [ ] 在干净目标环境完成安装、启动、卸载验证。
-   [ ] README、许可证、贡献指南、隐私说明与版本说明完整。
-   [ ] CI 结果和测试证据可供审查。

### 证据目录

最终提交前，确保下列文件存在并内容真实：

``` text
docs/reports/
├─ phase-0-report.md
├─ phase-1-report.md
├─ phase-2-report.md
├─ phase-3-report.md
├─ phase-4-report.md
└─ release-readiness.md

artifacts/
├─ phase-0/
├─ test-results/
└─ acceptance/
```

------------------------------------------------------------------------

## 12. 官方资料与竞品参考

### Microsoft 官方资料

1.  Windows App SDK 发布通道\
    https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/release-channels
2.  Windows App SDK 下载\
    https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/downloads
3.  Windows SDK 下载\
    https://learn.microsoft.com/en-us/windows/apps/windows-sdk/downloads
4.  Visual Studio 2026 发布说明\
    https://learn.microsoft.com/en-us/visualstudio/releases/2026/release-notes
5.  .NET 支持政策\
    https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core
6.  WinUI 3\
    https://learn.microsoft.com/en-us/windows/apps/winui/
7.  Windows 设计原则\
    https://learn.microsoft.com/en-us/windows/apps/design/design-principles
8.  Windows 应用最佳实践\
    https://learn.microsoft.com/en-us/windows/apps/get-started/best-practices
9.  Win2D 快速入门\
    https://learn.microsoft.com/en-us/windows/apps/develop/win2d/quick-start
10. Windows 图像处理\
    https://learn.microsoft.com/en-us/windows/apps/develop/media-authoring-processing/imaging
11. WinUI / Windows App SDK 测试\
    https://learn.microsoft.com/en-us/windows/apps/develop/testing/
12. Windows 应用分发路径\
    https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/choose-distribution-path
13. Windows 支持版本\
    https://learn.microsoft.com/en-us/windows/release-health/supported-versions-windows-client
14. Playwright Screenshot Assertions\
    https://playwright.dev/docs/test-snapshots

### 竞品与设计参考

-   WinUI 3 Gallery：https://github.com/microsoft/WinUI-Gallery
-   Microsoft PowerToys：https://github.com/microsoft/PowerToys
-   OICAT：https://github.com/MalumaDev/OICAT
-   Image Compare：https://github.com/gimletlove/imagecompare

------------------------------------------------------------------------

## 13. 开发完成的定义

Visual Diff Lab 只有在以下条件同时满足时，才算完成 MVP：

1.  产品核心工作流可以从导入到导出完整运行。
2.  差异算法由确定性测试覆盖，边界行为有文档。
3.  WinUI 界面符合设计规范，核心操作可通过键盘完成。
4.  所有核心功能都在真实 Windows 环境中运行验证。
5.  有可复现的性能数据、自动化测试报告和视觉截图。
6.  安装包能在目标环境安装、运行和卸载。
7.  开源文档、CI 和发布准备齐全。
8.  所有未完成项与已知限制都如实记录。

**最终原则：不要把"代码写完"当成"产品完成"。只有真实界面、实际交互、测试结果和发布流程都经过验证，Visual
Diff Lab 才算达到交付标准。**
