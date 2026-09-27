# DropLite

受 [DropIt](https://www.dropitproject.com/) 启发、为 Windows 打造的悬浮式文件自动处理工具。
把文件拖到桌面悬浮图标上，DropLite 会按你定义的规则自动**移动 / 复制 / 回收 / 压缩 / 解压 / 重命名 / 打开**它们。

![build](https://github.com/issakk/dropit/actions/workflows/build.yml/badge.svg)

## 特性

- 🎯 **悬浮图标**：常驻桌面、置顶、可拖动、不抢焦点（`WS_EX_NOACTIVATE`），空闲时零 CPU（纯事件驱动，无轮询）
- 👥 **多档案（Profile）**：每个档案一个独立悬浮图标，各自的归属颜色、位置与规则集
- 📦 **8 种动作**：移动、复制、回收站删除（走 Shell，可还原）、压缩 ZIP、解压、重命名、打开、忽略
- 🔀 **规则自上而下匹配**：支持文件掩码（`*.jpg;*.png`），空掩码匹配一切
- ⚡ **高性能**：多核并行处理、批量文件一次 Shell 调用、1 MB 缓冲流式压缩、原子写配置、单实例
- 📁 **同名冲突策略**：自动重命名 / 覆盖 / 跳过
- 🧮 **模板变量**：ZIP 名与重命名模板支持 `{name}` `{ext}` `{date:yyyy-MM-dd}` `{n}`
- 📜 **处理日志**：每次拖放的明细与错误按日写入日志文件（保留 14 天），托盘一键打开；全局异常兜底不再静默崩溃
- 👀 **拖入预览**：拖着文件悬停在图标上时，提示将命中的规则与目标位置
- 🛡 **越界保护**：显示器变更后图标自动拉回可见区域，托盘可一键重置图标位置
- 🗂 **常用分类一键添加**：内置图片/视频/音频/文档/压缩包/安装包/字体/代码等 12 类常用掩码，设置里一键生成规则
- 🇨🇳 **中文界面**：设置窗口、规则编辑、托盘菜单与通知全部为简体中文
- 🖥 **零依赖部署**：.NET 9 自包含单文件 exe（ReadyToRun 预编译），无需安装运行时

## 下载

1. 打开 [Actions](https://github.com/issakk/dropit/actions) 页面，选择最新的 **build** 运行
2. 在 **Artifacts** 区域下载 `DropLite-win-x64-<run>` 得到 `DropLite.exe`
3. 或者：打 `v*` 标签（如 `git tag v1.0.0 && git push --tags`）会自动创建 GitHub Release 并附带 zip

## 快速上手

1. 运行 `DropLite.exe` —— 首次启动会在屏幕右上方出现一个蓝色悬浮图标，并打开设置窗口
2. 默认档案带两条示例规则（图片 → 图片库\Collected，文档 → 文档库\Collected），按需修改
3. 从资源管理器拖文件/文件夹到悬浮图标上松手 —— 规则立即执行，托盘弹出结果气泡
4. 左键按住悬浮图标可拖动其位置；双击打开设置；右键有快捷菜单

### 托盘菜单

| 菜单项 | 说明 |
| --- | --- |
| 打开设置… | 打开设置窗口 |
| 档案 | 显示 / 隐藏各档案的悬浮图标 |
| 重置图标位置 | 把所有悬浮图标拉回屏幕右上角（找不到图标时用） |
| 暂停处理 | 暂停所有拖放处理 |
| 开机自启 | 写入 HKCU Run 注册表 |
| 打开日志文件夹 | 打开 %APPDATA%\DropLite\logs（按日滚动，保留 14 天） |
| 退出 | 退出 |

## 规则语法

- **File mask**：分号分隔的通配符，`*` 匹配任意字符、`?` 匹配单个字符，不区分大小写。
  示例：`*.jpg;*.jpeg;*.png`、`report-202?.*`。留空 = 匹配所有文件。
- **匹配顺序**：规则自上而下，第一条命中的规则生效。可把 `Ignore` 放在最后做兜底。
- **重命名模板变量**：

  | 变量 | 含义 |
  | --- | --- |
  | `{name}` | 原文件名（不含扩展名） |
  | `{ext}` | 扩展名（含点，如 `.jpg`） |
  | `{date:yyyy-MM-dd}` | 当前日期时间（任意 .NET 格式） |
  | `{n}` | 本次批次内的序号（从 0 开始） |

  示例模板：`{name}_{date:yyyyMMdd}{ext}` → `photo.jpg` 变 `photo_20260927.jpg`

## 动作说明

| 动作 | 行为 |
| --- | --- |
| Move | 移动到目标文件夹（跨卷目录自动复制后删除源） |
| Copy | 复制到目标文件夹，原文件保留 |
| Recycle (delete) | 发送到回收站（整个批次一次 Shell 调用） |
| Compress to ZIP | 追加进目标文件夹下的 ZIP；已存在的压缩包会继续追加，条目重名则跳过 |
| Extract ZIP | 解压到目标文件夹（默认为压缩包同目录的同名文件夹），带 zip-slip 防护 |
| Rename | 按模板重命名 |
| Open | 用系统默认程序打开 |
| Ignore | 不做任何处理 |

## 配置文件

`%APPDATA%\DropLite\config.json`（UTF-8，可直接编辑；程序运行时由设置窗口原子覆写）

## 本地构建（可选）

```bash
dotnet publish src/DropLite/DropLite.csproj -c Release -o publish
```

产物为 `publish/DropLite.exe`（自包含单文件，约几十 MB，ReadyToRun + 压缩）。
本项目以 GitHub Actions 为准：推送后 CI 自动构建并上传 artifact。

## 项目结构

```
src/DropLite/
├── Program.cs                  入口：单实例互斥、HiDPI 初始化
├── AppContext.cs               托盘图标、档案窗口管理、通知、首次运行默认档案
├── Models/AppModels.cs         Profile / Destination / AppConfig
├── Services/
│   ├── ConfigStore.cs          JSON 配置读写（原子写）与深拷贝
│   ├── FileProcessor.cs        规则匹配 + 并行动作引擎（8 种动作）
│   ├── ShellDelete.cs          SHFileOperation 回收站删除
│   ├── IconFactory.cs          GDI+ 矢量绘制悬浮图标 / 托盘图标
│   └── AutoStart.cs            开机自启（HKCU Run）
└── UI/
    ├── FloatingIconForm.cs     悬浮图标窗（拖放接收 / 拖动 / 忙碌动画）
    ├── SettingsForm.cs         设置窗口（档案与规则管理）
    ├── DestinationEditorForm.cs 规则编辑器
    └── Dialogs.cs              通用输入对话框
```

## 致谢与许可

灵感来自 [DropIt](https://www.dropitproject.com/)（本仓库与其无代码关系）。
代码以 [MIT](LICENSE) 许可发布。
