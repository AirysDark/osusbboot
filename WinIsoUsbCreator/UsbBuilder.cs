using System.Diagnostics;
using System.IO;

namespace WinIsoUsbCreator;

internal static class UsbBuilder
{
    public static void Build(int diskIndex, string isoPath, Action<int, string> progress)
    {
        char bootLetter = FindFreeDriveLetter('B', 'H');
        char isoLetter = FindFreeDriveLetter((char)(bootLetter + 1), 'Z', bootLetter);

        progress(5, $"Creating two USB partitions on disk {diskIndex}...");

        string script = $"""
select disk {diskIndex}
clean
convert mbr
create partition primary size=4096
format quick fs=fat32 label="BOOT"
active
assign letter={bootLetter}
create partition primary
format quick fs=exfat label="WINISO"
assign letter={isoLetter}
exit
""";

        string scriptFile = Path.Combine(Path.GetTempPath(), "WinIsoUsbCreator-" + Guid.NewGuid() + ".txt");
        try
        {
            File.WriteAllText(scriptFile, script);
            Run("diskpart.exe", $"/s \"{scriptFile}\"");

            string bootRoot = $@"{bootLetter}:\";
            string isoRoot = $@"{isoLetter}:\";

            WaitForDrive(bootRoot);
            WaitForDrive(isoRoot);

            progress(35, $"Copying Windows ISO to {isoLetter}: ...");
            File.Copy(isoPath, Path.Combine(isoRoot, "Windows10.iso"), true);

            progress(75, $"Copying prepared WinPE boot files to {bootLetter}: if available...");
            string winPe = Path.Combine(AppContext.BaseDirectory, "WinPE");
            if (Directory.Exists(winPe))
            {
                CopyDirectory(winPe, bootRoot);
            }

            progress(95, "Finalizing USB...");
        }
        finally
        {
            try { File.Delete(scriptFile); } catch { }
        }
    }

    private static char FindFreeDriveLetter(char start, char end, params char[] excluded)
    {
        var used = DriveInfo.GetDrives()
            .Select(d => char.ToUpperInvariant(d.Name[0]))
            .ToHashSet();

        foreach (char letter in Enumerable.Range(char.ToUpperInvariant(start), char.ToUpperInvariant(end) - char.ToUpperInvariant(start) + 1).Select(i => (char)i))
        {
            if (!used.Contains(letter) && !excluded.Contains(letter))
                return letter;
        }

        throw new InvalidOperationException("No free drive letters are available for the USB partitions.");
    }

    private static void WaitForDrive(string root)
    {
        for (int i = 0; i < 40; i++)
        {
            if (Directory.Exists(root))
                return;

            Thread.Sleep(250);
        }

        throw new IOException($"Windows did not mount the new USB partition at {root}");
    }

    private static void Run(string file, string args)
    {
        using var p = Process.Start(new ProcessStartInfo(file, args)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        }) ?? throw new InvalidOperationException("Could not start " + file);

        string output = p.StandardOutput.ReadToEnd();
        string error = p.StandardError.ReadToEnd();
        p.WaitForExit();

        if (p.ExitCode != 0)
            throw new InvalidOperationException($"{file} failed ({p.ExitCode}): {output}\n{error}");
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);

        foreach (string dir in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(source, dir);
            Directory.CreateDirectory(Path.Combine(destination, relative));
        }

        foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(source, file);
            string target = Path.Combine(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, true);
        }
    }
}
