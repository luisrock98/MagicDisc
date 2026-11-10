# MagicDisc 1.0.1

面向 Apple 芯片 Mac 的本地图片、动图、视频和 PDF 工具。

## 功能

- 图片格式转换、压缩、裁剪、旋转、去背景、合并及尺寸调整。
- GIF、APNG 与视频互转，序列图生成动图，视频剪辑和提取帧。
- PDF 合并、页面处理、图片转 PDF；OCR 优先使用 macOS Apple Vision。
- AI 超分辨率：支持 2×、3×、4×，模型列表提供简短用途说明。
- 可自定义的正反面环形工具栏、批处理、自动转换文件夹。

## 安装和使用

1. 从 [Releases](https://github.com/luisrock98/MagicDisc/releases) 下载 `MagicDisc-1.0.1-macOS-arm64.dmg`，将 MagicDisc 放入“应用程序”。更新前从菜单栏退出旧版本。
2. 在“常规 → 转换引擎”按需安装 FFmpeg、Upscayl 或选择已有程序。应用包不预装这些引擎；AI 模型首次使用时下载并校验，完成后可离线处理。
3. 在“常规 → 基本设置”控制登录启动、菜单栏图标及关闭窗口后的行为。
4. 环形工具栏中，将右侧预设拖到圆环即可添加；将扇区拖回右侧列表即可移除。通过“编辑图片”选择缩放百分比或宽高。

用户设置、预设、历史记录和已下载的引擎位于 `~/Library/Application Support/MagicDisc`，工程清理不涉及这个目录。

## 构建

需要 .NET 10 SDK、Python 3、macOS 命令行工具。

```sh
python3 build/package-magicdisc.py
python3 build/package-magicdisc-dmg.py
```

输出 `dist/MagicDisc.app`。`MAGICDISC_DOTNET` 可指定 SDK，`MAGICDISC_BUNDLE_OUTPUT` 可指定应用输出路径。当前版本使用 ad-hoc 签名，未完成 Developer ID 签名与公证。

功能验证：

```sh
dotnet run --project tools/MagicDisc.Smoke -c Release -r osx-arm64
```

## 项目结构

- `src/`：应用、共享转换实现与平台依赖。
- `tests/`、`tools/`：验证与打包检查。
- `build/`、`installer/`：构建脚本和打包资源。
- `dist/`：本地打包输出，不纳入 Git 仓库。
- `docs/VALIDATION.md`：当前验证记录。

## 来源与许可

基于 [Filee](https://github.com/KnifeLemon/Filee)，详见 [来源说明](UPSTREAM.md)、[MIT 许可](LICENSE) 和 [第三方许可](THIRD-PARTY-NOTICES.md)。目前未配置在线更新服务。
