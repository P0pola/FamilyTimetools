# FamilyTimeTools 2.0.2

仓库：https://github.com/P0pola/FamilyTimetools

`Family Time`（Mono / Unity）的 MelonLoader 插件。提供实体 ESP、UGUI 设置窗口、建造作弊与实体生成，基于游戏自带的 GAT 实体系统实现。

## 功能

- **ESP 屏幕叠加**：按类别给世界中的实体与物品绘制框体、名称、距离、连线、中心点和屏外指示。
- **UGUI 设置窗口**：基于 `UniverseLib.Mono` 的完整控制中心，支持拖动、缩放与分页，共 8 页。
- **免费建造**：开启后放下蓝图，用锤子点击一次即可建成，不消耗材料。
- **地板堆叠**：地板可以贴着下一块地板往上摸，间距等于地板自身厚度，不会空出一整格。
- **准星注水**：按住自定义热键，从准星命中的地面持续喷出一股细水柱；水量按游戏水桶标定，默认单格、约 5 桶/秒，热键在菜单里录制。
- **实体生成**：读取游戏模板表，在当前角色位置批量生成指定实体。
- **模板导出**：把游戏 SDK 的实体模板表导出为文本，便于查看模板名与索引。

## 快捷键

| 按键 | 作用 |
| --- | --- |
| `F2` | 开关设置窗口（不影响 ESP 运行） |
| 自定义 | 注水热键，在「作弊功能」页点击按钮后按下你要的键 |

配置修改后自动保存；窗口中的「保存配置」按钮可立即落盘。

## 窗口页面

| 页面 | 内容 |
| --- | --- |
| 总览 | ESP 总开关、最大距离、最多目标、扫描间隔、分类统计、立即重新扫描 |
| 部件开关 | 框体、名称、距离、连线、中心点、屏外指示 |
| 目标筛选 | 各实体类别独立开关，另提供全部启用 / 全部禁用 |
| 绘制样式 | 方框、四角、填充、框加角，距离渐变、仅屏幕内、框体缩放、线宽 |
| 目标列表 | 当前筛选后按距离排序的目标、类别与距离 |
| 作弊功能 | 免费建造、地板堆叠、准星注水（热键自定义）、导出实体模板列表 |
| 生成实体 | 刷新模板列表、选择模板、设置数量与散布半径后生成 |
| 关于 / 配置 | 打开配置目录、立即保存、恢复默认、重置窗口位置、关闭窗口 |

## 项目文件

| 内容 | 路径 |
| --- | --- |
| 解决方案 | `FamilyTimeTools.sln` |
| 项目文件 | `FamilyTimeTools/FamilyTimeTools.csproj` |
| 源码目录 | `FamilyTimeTools/` |
| 输出目录 | `FamilyTimeTools/bin/<Configuration>/` |

目标框架为 `.NET Framework 4.7.2`，编译时从游戏目录引用 MelonLoader、UniverseLib、Assembly-CSharp 和 UnityEngine 系列程序集。这些引用只在编译期使用（`Private=false`），不会随 DLL 一起拷贝。

## 编译

`GameRoot` 指向游戏根目录，默认值为项目目录的上三级（`$(MSBuildThisFileDirectory)..\..\..\`）。标准布局是把仓库放在 `<游戏根>\builds\FamilyTimeTools\`，此时默认值即可命中；否则需要在命令行或项目属性里显式覆盖。

### Windows

用 Visual Studio 打开 `FamilyTimeTools.sln`，或直接用 MSBuild 构建 `Release|AnyCPU`：

```text
MSBuild.exe FamilyTimeTools.sln /p:Configuration=Release /p:GameRoot="D:\Steam\steamapps\common\Family Time\"
MSBuild.exe FamilyTimeTools.sln /p:Configuration=Release /p:GameRoot="D:\Steam\steamapps\common\Family Time\" /p:DeployToMods=true
```

### macOS / Linux

编译只需要托管程序集，不需要运行游戏，因此 `mono` + `msbuild` 即可：

```bash
brew install mono
cd /path/to/FamilyTimetools
msbuild FamilyTimeTools/FamilyTimeTools.csproj \
  /p:Configuration=Release \
  /p:GameRoot="/path/to/Family Time"
```

如果 msbuild 对老式 csproj 报错，也可以直接把源码和 `-r:` 引用交给 `csc`。项目没有 NuGet 依赖，这条路更直接。

### 部署

`Release` 构建结束后，`PublishPlugin` 目标会把 DLL 复制到 `$(GameRoot)builds`；只有显式传入 `/p:DeployToMods=true` 时才会额外复制到游戏的 `Mods` 目录。Debug 构建不会复制。

## 实体类别

所有实体类别集中定义在 `FamilyTimeTools/WorldScanner.cs` 的 `EspMod.Kinds` 表里，一行代表一个类别：

```csharp
new EntityKind("WolfGirl", "狼女", new Color(0.75f, 0.55f, 1.00f), match: "wolf_girl", "wolfgirl"),
```

字段含义：

| 字段 | 说明 |
| --- | --- |
| `Key` | 配置键后缀，写为 `Cat_<Key>`，需与旧配置保持一致 |
| `Name` | 界面显示与目标标签使用的中文名 |
| `Color` | 该类别在 ESP 与目标列表中的颜色 |
| `match` | 模板名包含的关键字（不区分大小写），可给多个 |
| `defaultEnabled` | 恢复默认设置时的开关状态 |
| `isItem` | 标记该类别走 `rigidTransformManager` 而不是 GAT |

匹配按表顺序取**第一个**命中的关键字，因此更具体的关键字必须排在更宽泛的关键字前面（如 `baby_chick` 在 `chicken` 前、`piglet` 在 `pig` 前、`wolf_girl` 在 `wolf` 前）。

**新增一类实体只需在表里加一行**，模板匹配、命名、配色、筛选开关和总览统计都会自动生效，不需要改动其他文件。

当前启用的类别：小鸡、公鸡、母鸡、鸡、猪崽、猪、野猪、狼女、狼妈妈、狼、玩家，以及兜底的「其他」和「物品」。鹿与兔子的匹配行已在表中保留但被注释，需要时取消注释即可。

物品不参与模板名匹配：`rigidTransformManager` 里的每个刚体都算作「物品」，标签取该对象的 GameObject 名。

## 运行时说明

- ESP 绘制使用 Unity IMGUI 的 `OnGUI`，只负责屏幕叠加，不使用 IMGUI 自绘设置菜单。
- 设置窗口基于 `UniverseLib.UI.Panels.PanelBase`、`UIFactory` 与 UGUI 控件。
- 目标扫描默认 0.15 秒一次，可在总览页调整；扫描结果按距离排序并受「最多目标」限制。
- 中文字体直接取系统字体，优先 `Microsoft YaHei`（微软雅黑），依次回退到 `SimHei` / `SimSun` / `Noto Sans CJK SC` 等；不依赖游戏目录里的字体文件。
- 免费建造通过 Harmony 前缀拦截 `GAT.BuildPlanManager.TryGetConstructionMaterials`，覆盖锤子建造与村民自动施工两条路径。
- 地板堆叠在 `FloorStacking.cs`。游戏原本为什么堆不了：`BuildPart.GetPlacementCell` 把命中点四舍五入成整数格，地板是薄板，瞄准顶面取整后 Y 仍然落回同一格，而 `BuildPartManager.TryCreateBuildPart` 对同（模板 + 格子 + 朝向）直接返回 false，表现就是蓝图凭空消失；而向上顺延一整格（1 米）又远高于地板实际厚度。
- 开启后，若正在放地板、且瞄中已建成的另一块地板、且命中面朝上，新地板高度直接取“命中面高度 − 自身网格包围盒下沿”，底面正好贴住命中面，间距等于地板厚度。X / Z 仍然按最小格子吸附，不会出现半格。
- 判定“是不是地板”用 `BuildPart.displayName`（Hay Floor / Wood Floor），模板名不可靠（wood_planks01 里没有 floor）。只有地板走这条通道，其余建筑完全不受影响。
- 连续高度绕不开 `Cell.Pack`（x 12 位 / y 8 位 / z 12 位），所以整数格只当存放键：先向上顺延到未占用的格，再用差值等量的浮点偏移把矩阵平移拉回真正高度（`GetPlacementMatrix` 后置），预览 / 蓝图 / 成品 / 碰撞盒一致。
- 偏移随游戏自带世界存档一起保存：游戏的存档格式（byte + short×3）写不下小数，
  所以本模块用 `WorldSerializer` 的自定义段机制另存一段 `build.floor-stack`（每条 15 字节：templateIndex + cell xyz + rotation + offset）。
  游戏自带的 `build.parts` 是 order 80，本段排 **79**，
  于是读档时偏移表先就位，建筑创建时矩阵一次就算对，不会先落网格再跳位。
  仅保存真正建成的零件（预览阶段预写的偏移不落盘）；旧存档没有这个段，读档时会先清空偏移表，不会互相污染。
  读档期间 `BuildPartManager.LoadFrom` 会先删光旧零件，此时按 `WorldSerializer.isLoadingInProgress` 判断并保留偏移，
  否则刚装好的表会被自己的移除钩子清掉。
- 准星注水在 `WaterTool.cs`。调用游戏自带的 `WaterManager.SpawnWaterAtCell` / `RemoveWaterAtCell`（水桶倒水与闪电洪水都走这两个入口），
  `waterLevel` 是 0~1 的归一化水量。游戏里一桶水 = `waterBucketCapacity / 65535 = 250 / 65535 ≈ 0.0038`，菜单里的“每次注水量”直接按桶填写（默认 0.25 桶/脉冲、20Hz），喷射半径 0 表示单格细水柱。准星取点用 `World.Raycast`，打中建筑或实体则不注水，只认地形。
- 注水只在游戏的水模拟窗口（256×256 格，跟着玩家走）内生效，超出窗口 `SpawnWaterAtCell` 直接返回 false。
- 注水热键在菜单里录制（存 `Key` 枚举名），支持键盘与鼠标；录制期间会禁用游戏输入（同 `InputMenu` 的做法），避免误触发。
- 实体生成直接调用 `GAT.GAT.CreateInstances`，与游戏内部 `SpawnOffspringForFemales` 用法一致；模板索引只在进入世界后有效，每次进入世界需要重新刷新。
- 配置分类为 `FamilyTimeESP`，保存于游戏的 `UserData` 目录。配置键与旧版本保持一致，升级后原有设置继续有效。
- 「导出实体模板列表」会写出 `UserData\FamilyTimeTools_templates.txt`。
