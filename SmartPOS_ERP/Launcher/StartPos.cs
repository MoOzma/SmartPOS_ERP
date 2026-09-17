using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading;

internal static class StartPos
{
    private const string ShortcutName = "\u0627\u0644\u0642\u062F\u0633";

    [STAThread]
    private static int Main(string[] args)
    {
        var root = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Production");
        try
        {
            InstallShortcuts(root);
        }
        catch
        {
        }

        if (args != null && args.Length > 0 && string.Equals(args[0], "--shortcuts-only", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        var app = Path.Combine(root, "SmartPOS_ERP.exe");
        if (File.Exists(app))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = app,
                WorkingDirectory = root,
                UseShellExecute = true
            });
        }

        const string url = "http://127.0.0.1:5202";
        WaitForHttp(url, 30000);
        OpenBrowser(url);
        return 0;
    }

    private static void InstallShortcuts(string root)
    {
        var target = Path.Combine(root, "Start-POS.exe");
        if (!File.Exists(target))
        {
            target = Path.Combine(root, "start-pos.cmd");
        }

        var icon = Path.Combine(root, "app.ico");
        if (!File.Exists(icon))
        {
            icon = Path.Combine(root, "wwwroot", "favicon.ico");
        }

        if (!File.Exists(icon))
        {
            icon = target;
        }

        var folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddFolder(folders, Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
        AddFolder(folders, Environment.GetFolderPath(Environment.SpecialFolder.Desktop));
        AddFolder(folders, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Desktop"));
        AddFolder(folders, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "OneDrive", "Desktop"));
        AddFolder(folders, Environment.GetFolderPath(Environment.SpecialFolder.Startup));

        var shellType = Type.GetTypeFromProgID("WScript.Shell");
        if (shellType == null)
        {
            return;
        }

        var shell = Activator.CreateInstance(shellType);
        foreach (var folder in folders)
        {
            try
            {
                CreateShortcut(shellType, shell, Path.Combine(folder, ShortcutName + ".lnk"), target, root, icon);
            }
            catch
            {
            }
        }
    }

    private static void CreateShortcut(Type shellType, object shell, string linkPath, string target, string workDir, string icon)
    {
        var shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { linkPath });
        var t = shortcut.GetType();
        t.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { target });
        t.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { workDir });
        t.InvokeMember("WindowStyle", BindingFlags.SetProperty, null, shortcut, new object[] { 1 });
        t.InvokeMember("IconLocation", BindingFlags.SetProperty, null, shortcut, new object[] { icon });
        t.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut, new object[] { "POS" });
        t.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
    }

    private static void AddFolder(HashSet<string> folders, string path)
    {
        if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
        {
            folders.Add(path);
        }
    }

    private static void WaitForHttp(string url, int timeoutMs)
    {
        var started = DateTime.UtcNow;
        while ((DateTime.UtcNow - started).TotalMilliseconds < timeoutMs)
        {
            try
            {
                var request = (HttpWebRequest)WebRequest.Create(url);
                request.Timeout = 1500;
                request.AllowAutoRedirect = false;
                using (request.GetResponse())
                {
                    return;
                }
            }
            catch
            {
                Thread.Sleep(400);
            }
        }
    }

    private static void OpenBrowser(string url)
    {
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Microsoft\Edge\Application\msedge.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Microsoft\Edge\Application\msedge.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Google\Chrome\Application\chrome.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Google\Chrome\Application\chrome.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Google\Chrome\Application\chrome.exe")
        };

        foreach (var browser in candidates)
        {
            if (!File.Exists(browser))
            {
                continue;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = browser,
                Arguments = "--start-fullscreen --new-window " + url,
                UseShellExecute = true
            });
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
    }
}

