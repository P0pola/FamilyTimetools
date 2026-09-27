# FamilyTimeESP 2.0.0

这是 `FamilyTime` Mono Unity 游戏的 ESP 插件重写项目。项目已经改为 Visual Studio 解决方案，并使用游戏目录中的 `UniverseLib.Mono.dll` 构建 UGUI 设置窗口；目标扫描与屏幕叠加功能沿用原插件的数据源和配置键。

## 项目文件

- 解决方案：`builds\FamilyTimeESP\FamilyTimeESP.sln`
- 项目文件：`builds\FamilyTimeESP\FamilyTimeESP\FamilyTimeESP.csproj`
- 源码目录：`builds\FamilyTimeESP\FamilyTimeESP\`
- UniverseLib 引用：`UserLibs\UniverseLib.Mono.dll`

项目默认目标框架为 `.NET Framework 4.7.2`，并从当前游戏目录引用 MelonLoader、Assembly-CSharp、UnityEngine 和 Unity Input System 程序集。`GameRoot` 默认指向本项目所在游戏根目录，也可以在 Visual Studio 的项目属性中覆盖。

## UniverseLib 窗口

按 `F2` 打开或关闭完整设置窗口。窗口支持标题栏拖动、边缘缩放、右上角关闭和页面切换，共有六页：

1. **总览**：ESP 总开关、最大距离、最多目标、扫描间隔、分类统计和立即扫描。
2. **部件开关**：框体、名称、距离、连线、中心点、屏外指示。
3. **目标筛选**：野猪、鸡、母鸡、公鸡、小鸡、猪、猪崽、狼女、狼妈妈、狼、玩家、鹿、兔子、其他、物品的独立开关，并提供全部启用/全部禁用。
4. **绘制样式**：方框、四角、填充、框加角，距离渐变、仅屏幕内、框体缩放和线宽。
5. **目标列表**：查看当前筛选后的目标、类别和距离。
6. **作弊功能**：免费建造，开启后点击蓝图即可建成且不消耗材料。
7. **生成实体**：读取游戏模板表，在当前角色位置生成指定实体（可设数量与散布半径）。
8. **关于 / 配置**：打开配置目录、立即保存、恢复默认设置、重置窗口位置和关闭窗口。

免费建造通过 Harmony 前缀拦截 `GAT.BuildPlanManager.TryGetConstructionMaterials`，覆盖锤子建造与村民自动施工两条路径。

按 `F1` 可直接开关 ESP。设置修改会自动保存，也可以通过窗口中的保存按钮立即保存。

## 编译与部署

本次只完成源码项目和静态检查，**未执行编译、未启动游戏、未部署 DLL**。

在确认需要构建时，可使用 Visual Studio 打开 `FamilyTimeESP.sln`，或使用本机 VS 的 MSBuild 构建 `Release|AnyCPU`。项目的 `PublishPlugin` 目标会在 Release 构建后把 DLL 复制到 `builds`；只有显式传入 `DeployToMods=true` 时才会额外复制到游戏的 `Mods` 目录。

示例：

```text
D:\VSstudio\vs2026\MSBuild\Current\Bin\MSBuild.exe builds\FamilyTimeESP\FamilyTimeESP.sln /p:Configuration=Release
D:\VSstudio\vs2026\MSBuild\Current\Bin\MSBuild.exe builds\FamilyTimeESP\FamilyTimeESP.sln /p:Configuration=Release /p:DeployToMods=true
```

## 运行时说明

- ESP 绘制仍使用 Unity IMGUI 的 `OnGUI`，只负责屏幕叠加，不再用 IMGUI 自绘设置菜单。
- 设置窗口使用 `UniverseLib.UI.Panels.PanelBase`、`UIFactory` 和 UGUI 控件。
- 中文字体优先读取 `FamilyTime_Data\Fonts\NotoSansCJKsc-Regular.otf`，找不到时回退到系统字体。
- 配置分类仍为 `FamilyTimeESP`，原有配置键保持兼容；新增的显示部件开关也写入同一分类。
