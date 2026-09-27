using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using DropLite.Models;
using DropLite.Services;
using DropLite.UI;

namespace DropLite;

internal sealed class AppContext : ApplicationContext
{
    private static readonly Color TrayColor = Color.FromArgb(255, 74, 144, 217);

    private readonly NotifyIcon _tray;
    private readonly List<FloatingIconForm> _windows = new();
    private SettingsForm? _settings;
    private bool _paused;

    public AppConfig Config { get; private set; }

    public bool Paused
    {
        get => _paused;
        set
        {
            _paused = value;
            foreach (var w in _windows)
            {
                w.RefreshVisual();
            }
        }
    }

    public AppContext()
    {
        bool firstRun = !ConfigStore.Exists;
        Config = ConfigStore.Load();
        if (firstRun)
        {
            EnsureDefaultProfile();
        }

        _tray = new NotifyIcon
        {
            Icon = IconFactory.CreateIcon(TrayColor),
            Text = "DropLite — 把文件拖到悬浮图标上",
            Visible = true,
        };
        _tray.ContextMenuStrip = BuildTrayMenu();
        _tray.DoubleClick += (_, _) => ShowSettings();

        SpawnProfileWindows();

        if (firstRun)
        {
            ShowSettings();
        }
    }

    private void EnsureDefaultProfile()
    {
        string pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var area = Screen.PrimaryScreen!.WorkingArea;

        var profile = new Profile
        {
            Name = "Main",
            IconColorArgb = unchecked((int)0xFF4A90D9),
            X = area.Right - Config.IconSize - 40,
            Y = area.Top + 80,
            Destinations =
            {
                new Destination
                {
                    Name = "收集图片",
                    Action = DropAction.Move,
                    Pattern = "*.jpg;*.jpeg;*.png;*.gif;*.bmp;*.webp",
                    TargetPath = Path.Combine(pictures, "Collected"),
                },
                new Destination
                {
                    Name = "收集文档",
                    Action = DropAction.Move,
                    Pattern = "*.pdf;*.docx;*.doc;*.xlsx;*.pptx;*.txt",
                    TargetPath = Path.Combine(documents, "Collected"),
                },
            },
        };

        Config.Profiles.Add(profile);
        ConfigStore.Save(Config);
    }

    // ---------------------------------------------------------------- windows

    public void SpawnProfileWindows()
    {
        CloseWindows();
        foreach (Profile profile in Config.Profiles)
        {
            if (!profile.ShowIcon)
            {
                continue;
            }
            var form = new FloatingIconForm(this, profile);
            form.Show();
            _windows.Add(form);
        }
    }

    private void CloseWindows()
    {
        foreach (var w in _windows)
        {
            w.Close();
            w.Dispose();
        }
        _windows.Clear();
    }

    public void RefreshVisuals()
    {
        foreach (var w in _windows)
        {
            w.RefreshVisual();
        }
    }

    public void SaveConfig()
    {
        ConfigStore.Save(Config);
    }

    public void ApplyConfig(AppConfig newConfig)
    {
        Config = newConfig;
        ConfigStore.Save(Config);
        AutoStart.SetEnabled(Config.StartWithWindows);
        SpawnProfileWindows();
    }

    // ---------------------------------------------------------------- settings

    public void ShowSettings(Profile? focus = null)
    {
        if (_settings is { IsDisposed: false })
        {
            _settings.FocusProfile(focus);
            _settings.Activate();
            return;
        }

        _settings = new SettingsForm(this, focus?.Id);
        _settings.Show();
        _settings.Activate();
    }

    // ---------------------------------------------------------------- notifications

    public void NotifyResult(string profileName, ProcessResult result)
    {
        if (!Config.ShowNotifications)
        {
            return;
        }

        string title = result.Failed > 0
            ? $"DropLite — {profileName}（处理完成，有错误）"
            : $"DropLite — {profileName}";
        string text = result.Summary();
        if (result.Errors.Count > 0)
        {
            text += Environment.NewLine + string.Join(Environment.NewLine, result.Errors.GetRange(0, Math.Min(3, result.Errors.Count)));
        }

        _tray.ShowBalloonTip(4000, title, text, result.Failed > 0 ? ToolTipIcon.Warning : ToolTipIcon.Info);
    }

    public void NotifyError(string message)
    {
        _tray.ShowBalloonTip(4000, "DropLite", message, ToolTipIcon.Error);
    }

    // ---------------------------------------------------------------- tray

    private ContextMenuStrip BuildTrayMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Opening += (_, _) => RebuildTrayMenu(menu);
        RebuildTrayMenu(menu);
        return menu;
    }

    private void RebuildTrayMenu(ContextMenuStrip menu)
    {
        menu.Items.Clear();

        menu.Items.Add("打开设置…", null, (_, _) => ShowSettings());

        var profilesItem = new ToolStripMenuItem("档案");
        foreach (Profile p in Config.Profiles)
        {
            var item = new ToolStripMenuItem(p.Name) { Checked = p.ShowIcon };
            item.Click += (_, _) =>
            {
                p.ShowIcon = !p.ShowIcon;
                SaveConfig();
                SpawnProfileWindows();
            };
            profilesItem.DropDownItems.Add(item);
        }
        if (Config.Profiles.Count == 0)
        {
            profilesItem.DropDownItems.Add(new ToolStripMenuItem("（无）") { Enabled = false });
        }
        menu.Items.Add(profilesItem);

        var pause = new ToolStripMenuItem("暂停处理") { Checked = _paused };
        pause.Click += (_, _) => { Paused = !Paused; pause.Checked = Paused; };
        menu.Items.Add(pause);

        var autostart = new ToolStripMenuItem("开机自启") { Checked = AutoStart.IsEnabled() };
        autostart.Click += (_, _) =>
        {
            bool enable = !AutoStart.IsEnabled();
            AutoStart.SetEnabled(enable);
            Config.StartWithWindows = enable;
            SaveConfig();
        };
        menu.Items.Add(autostart);

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => ExitApp());
    }

    private void ExitApp()
    {
        CloseWindows();
        _tray.Visible = false;
        _tray.Dispose();
        Application.Exit();
    }
}
