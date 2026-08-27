using System.Diagnostics;
using System.IO;

namespace WinIsoUsbCreator;

internal static class UsbBuilder
{
    public static void Build(int diskIndex, string isoPath, Action<int, string> progress)
    {
        if (diskIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(diskIndex));

        if (!File.Exists(isoPath))
            throw new FileNotFoundException("The selected Windows ISO could not be found.", isoPath);

        char bootLetter = FindFreeDriveLetter('B', 'H');
        char isoLetter = FindFreeDriveLetter((char)(bootLetter + 1), 'Z', bootLetter);

        progress(5, $"Cleaning USB disk {diskIndex}...");
        RunDiskPart($"""
select disk {diskIndex}
clean
exit
""");

        progress(12, "Preparing MBR partition table...");
        EnsureMbr(diskIndex);

        progress(18, "Creating BOOT and WINISO partitions...");
        RunDiskPart($"""
select disk {diskIndex}
create partition primary size=4096
format quick fs=fat32 label="BOOT"
active
assign letter={bootLetter}
create partition primary
format quick fs=exfat label="WINISO"
assign letter={isoLetter}
exit
""");

        string bootRoot = $@"{bootLetter}:\";
        string isoRoot = $@"{isoLetter}:\";

        WaitForDrive(bootRoot);
        WaitForDrive(isoRoot);

        progress(35, $"Copying Windows ISO to {isoLetter}: ...");
        string destinationIso = Path.Combine(isoRoot, Path.GetFileName(isoPath));
        File.Copy(isoPath, destinationIso, true);

        progress(78, $"Preparing boot partition {bootLetter}: ...");
        string winPe = Path.Combine(AppContext.BaseDirectory, "WinPE");
        if (Directory.Exists(winPe))
        {
            CopyDirectory(winPe, bootRoot);
        }
        else
        {
            progress(85, "ISO copied. WinPE folder was not found, so BOOT currently contains no boot environment.");
        }

        progress(95, "Flushing copied files...");
        GC.Collect();
        GC.WaitForPendingFinalizers();

        progress(100, $"Finished. BOOT={bootLetter}:, WINISO={isoLetter}:, ISO={Path.GetFileName(destinationIso)}");
    }

    private static void EnsureMbr(int diskIndex)
    {
        var result = RunDiskPartCapture($"""
select disk {diskIndex}
convert mbr
exit
""");

        if (result.ExitCode == 0)
            return;

        string combined = (result.Output + "\n" + result.Error).ToLowerInvariant();

        // DiskPart returns an error when the disk is already MBR. In that case
        // there is nothing to convert and it is safe to continue.
        bool alreadyMbr =
            combined.Contains("not gpt formatted") ||
            combined.Contains("already mbr") ||
            combined.Contains("mbr disk");

        if (!alreadyMbr)
            throw new InvalidOperationException(
                $"Unable to prepare disk {diskIndex} as MBR.\n\n{result.Output}\n{result.Error}");
    }

    private static char FindFreeDriveLetter(char start, char end, params char[] excluded)
    {
        var used = DriveInfo.GetDrives()
            .Select(d => char.ToUpperInvariant(d.Name[0]))
            .ToHashSet();

        int first = char.ToUpperInvariant(start);
        int last = char.ToUpperInvariant(end);

        for (int value = first; value <= last; value++)
        {
            char letter = (char)value;
            if (!used.Contains(letter) && !excluded.Contains(letter))
                return letter;
        }

        throw new InvalidOperationException("No free drive letters are available for the USB partitions.");
    }

    private static void WaitForDrive(string root)
    {
        for (int i = 0; i < 60; i++)
        {
            if (Directory.Exists(root))
                return;

            Thread.Sleep(250);
        }

        throw new IOException($"Windows did not mount the new USB partition at {root}");
    }

    private static void RunDiskPart(string script)
    {
        var result = RunDiskPartCapture(script);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"diskpart.exe failed ({result.ExitCode}).\n\n{result.Output}\n{result.Error}");
        }
    }

    private static ProcessResult RunDiskPartCapture(string script)
    {
        string scriptFile = Path.Combine(
            Path.GetTempPath(),
            "WinIsoUsbCreator-" + Guid.NewGuid().ToString("N") + ".txt");

        try
        {
            File.WriteAllText(scriptFile, script);
            return RunCapture("diskpart.exe", $"/s \"{scriptFile}\"");
        }
        finally
        {
            try { File.Delete(scriptFile); } catch { }
        }
    }

    private static ProcessResult RunCapture(string file, string args)
    {
        using var process = Process.Start(new ProcessStartInfo(file, args)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        }) ?? throw new InvalidOperationException("Could not start " + file);

        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        return new ProcessResult(process.ExitCode, output, error);
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

    private sealed record ProcessResult(int ExitCode, string Output, string Error);
}
