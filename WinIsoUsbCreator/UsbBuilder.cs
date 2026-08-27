using System.Diagnostics;
using System.IO;

namespace WinIsoUsbCreator;
internal static class UsbBuilder
{
    public static void Build(int diskIndex, string isoPath, Action<int,string> progress)
    {
        progress(5, "Creating two USB partitions...");
        string script = $"select disk {diskIndex}\nclean\nconvert gpt\ncreate partition primary size=4096\nformat quick fs=fat32 label=\"BOOT\"\nassign letter=B\ncreate partition primary\nformat quick fs=exfat label=\"WINISO\"\nassign letter=I\nexit\n";
        string scriptFile = Path.Combine(Path.GetTempPath(), "WinIsoUsbCreator-" + Guid.NewGuid() + ".txt");
        try
        {
            File.WriteAllText(scriptFile, script); Run("diskpart.exe", $"/s \"{scriptFile}\"");
            progress(35, "Copying Windows ISO to second partition..."); File.Copy(isoPath, @"I:\Windows10.iso", true);
            progress(75, "Copying prepared WinPE boot files if available...");
            string winPe = Path.Combine(AppContext.BaseDirectory, "WinPE"); if (Directory.Exists(winPe)) CopyDirectory(winPe, @"B:\");
            progress(95, "Finalizing USB...");
        }
        finally { try { File.Delete(scriptFile); } catch { } }
    }
    private static void Run(string file, string args)
    {
        using var p = Process.Start(new ProcessStartInfo(file, args) { UseShellExecute=false, CreateNoWindow=true, RedirectStandardOutput=true, RedirectStandardError=true }) ?? throw new InvalidOperationException("Could not start " + file);
        string output=p.StandardOutput.ReadToEnd(), error=p.StandardError.ReadToEnd(); p.WaitForExit();
        if (p.ExitCode != 0) throw new InvalidOperationException($"{file} failed ({p.ExitCode}): {output}\n{error}");
    }
    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (string dir in Directory.GetDirectories(source, "*", SearchOption.AllDirectories)) Directory.CreateDirectory(dir.Replace(source,destination,StringComparison.OrdinalIgnoreCase));
        foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories)) { string target=file.Replace(source,destination,StringComparison.OrdinalIgnoreCase); Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.Copy(file,target,true); }
    }
}
