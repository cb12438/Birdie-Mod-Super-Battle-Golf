using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Windows.Forms;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new BirdieModInstallerForm());
    }
}

internal sealed class BirdieModInstallerForm : Form
{
    private const string GameExecutableName = "Super Battle Golf.exe";
    private const string PayloadResourceName = "BirdieMod.PayloadBundle.zip";

    private static readonly string[] BackupEntries =
    {
        "version.dll",
        "dobby.dll",
        "NOTICE.txt",
        "MelonLoader",
        "Dependencies",
        Path.Combine("Mods", "BirdieMod.dll"),
        Path.Combine("Mods", "BirdieMod.cfg")
    };

    private readonly TextBox pathTextBox;
    private readonly Button browseButton;
    private readonly Button installButton;
    private readonly TextBox logTextBox;

    internal BirdieModInstallerForm()
    {
        Text = "Birdie Mod Installer v1.0";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new System.Drawing.Size(640, 380);

        Label titleLabel = new Label();
        titleLabel.Left = 16;
        titleLabel.Top = 16;
        titleLabel.Width = 608;
        titleLabel.Height = 24;
        titleLabel.Text = "Select the folder that contains Super Battle Golf.exe";
        Controls.Add(titleLabel);

        pathTextBox = new TextBox();
        pathTextBox.Left = 16;
        pathTextBox.Top = 48;
        pathTextBox.Width = 500;
        Controls.Add(pathTextBox);

        browseButton = new Button();
        browseButton.Left = 524;
        browseButton.Top = 46;
        browseButton.Width = 100;
        browseButton.Height = 26;
        browseButton.Text = "Browse";
        browseButton.Click += BrowseButton_Click;
        Controls.Add(browseButton);

        Label statusLabel = new Label();
        statusLabel.Left = 16;
        statusLabel.Top = 78;
        statusLabel.Width = 608;
        statusLabel.Height = 18;

        string detected = AutoDetectGameDirectory();
        if (detected != null)
        {
            pathTextBox.Text = detected;
            statusLabel.Text = "Steam installation detected automatically.";
            statusLabel.ForeColor = System.Drawing.Color.Green;
        }
        else
        {
            statusLabel.Text = "Game directory not found automatically - browse to select.";
            statusLabel.ForeColor = System.Drawing.Color.DarkGoldenrod;
        }
        Controls.Add(statusLabel);

        installButton = new Button();
        installButton.Left = 16;
        installButton.Top = 104;
        installButton.Width = 120;
        installButton.Height = 30;
        installButton.Text = "Install";
        installButton.Click += InstallButton_Click;
        Controls.Add(installButton);

        logTextBox = new TextBox();
        logTextBox.Left = 16;
        logTextBox.Top = 150;
        logTextBox.Width = 608;
        logTextBox.Height = 208;
        logTextBox.Multiline = true;
        logTextBox.ScrollBars = ScrollBars.Vertical;
        logTextBox.ReadOnly = true;
        Controls.Add(logTextBox);
    }

    private static string AutoDetectGameDirectory()
    {
        const string GameSubPath = @"steamapps\common\Super Battle Golf";

        string pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        string pf64 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

        string[] defaultSteamRoots = new[]
        {
            Path.Combine(pf86, "Steam"),
            Path.Combine(pf64, "Steam"),
            @"C:\Steam",
            @"D:\Steam",
            @"E:\Steam",
        };

        foreach (string root in defaultSteamRoots)
        {
            string candidate = Path.Combine(root, GameSubPath);
            if (IsValidGameDirectory(candidate))
                return candidate;
        }

        // Parse libraryfolders.vdf to find additional Steam library locations
        foreach (string steamRoot in new[] { Path.Combine(pf86, "Steam"), Path.Combine(pf64, "Steam") })
        {
            string vdfPath = Path.Combine(steamRoot, @"steamapps\libraryfolders.vdf");
            if (!File.Exists(vdfPath))
                continue;

            try
            {
                string[] lines = File.ReadAllLines(vdfPath);
                foreach (string line in lines)
                {
                    string trimmed = line.Trim();
                    if (!trimmed.StartsWith("\"path\"", StringComparison.OrdinalIgnoreCase))
                        continue;

                    int first = trimmed.IndexOf('"', 6);
                    int last = trimmed.LastIndexOf('"');
                    if (first < 0 || last <= first)
                        continue;

                    string libraryPath = trimmed.Substring(first + 1, last - first - 1).Replace("\\\\", "\\");
                    string candidate = Path.Combine(libraryPath, GameSubPath);
                    if (IsValidGameDirectory(candidate))
                        return candidate;
                }
            }
            catch
            {
            }
        }

        return null;
    }

    private void BrowseButton_Click(object sender, EventArgs e)
    {
        using (FolderBrowserDialog dialog = new FolderBrowserDialog())
        {
            dialog.Description = "Choose your Super Battle Golf game folder";
            dialog.ShowNewFolderButton = false;
            if (Directory.Exists(pathTextBox.Text))
            {
                dialog.SelectedPath = pathTextBox.Text;
            }

            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                pathTextBox.Text = dialog.SelectedPath;
            }
        }
    }

    private void InstallButton_Click(object sender, EventArgs e)
    {
        string gameDirectory = pathTextBox.Text.Trim();
        if (!IsValidGameDirectory(gameDirectory))
        {
            MessageBox.Show(this, "Select a valid folder that contains Super Battle Golf.exe.", "Invalid folder", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        bool isUpdate = File.Exists(Path.Combine(gameDirectory, "Mods", "BirdieMod.dll"));

        SetBusyState(true);
        logTextBox.Clear();

        if (isUpdate)
            Log("Existing installation detected — updating...");

        try
        {
            InstallIntoDirectory(gameDirectory);
            Log(isUpdate ? "Update complete." : "Install complete.");
            string msg = isUpdate
                ? "Birdie Mod was updated successfully!"
                : "Birdie Mod was installed successfully!";
            MessageBox.Show(this, msg, "Done", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            Log("Install failed: " + ex.Message);
            MessageBox.Show(this, ex.Message, "Install failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetBusyState(false);
        }
    }

    private void SetBusyState(bool isBusy)
    {
        UseWaitCursor = isBusy;
        browseButton.Enabled = !isBusy;
        installButton.Enabled = !isBusy;
        pathTextBox.Enabled = !isBusy;
        Cursor.Current = isBusy ? Cursors.WaitCursor : Cursors.Default;
    }

    private void InstallIntoDirectory(string gameDirectory)
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "BirdieModInstaller_" + Guid.NewGuid().ToString("N"));
        string payloadDirectory = Path.Combine(tempRoot, "Payload");

        try
        {
            Directory.CreateDirectory(tempRoot);
            ExtractEmbeddedPayload(tempRoot, payloadDirectory);
            BackupExistingFiles(gameDirectory);
            CopyDirectoryContents(payloadDirectory, gameDirectory);
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    private void ExtractEmbeddedPayload(string tempRoot, string payloadDirectory)
    {
        Assembly assembly = Assembly.GetExecutingAssembly();
        using (Stream resourceStream = assembly.GetManifestResourceStream(PayloadResourceName))
        {
            if (resourceStream == null)
            {
                throw new InvalidOperationException("Embedded payload was not found. Rebuild the installer with build_gui_installer.bat.");
            }

            string payloadZipPath = Path.Combine(tempRoot, "PayloadBundle.zip");
            using (FileStream outputStream = File.Create(payloadZipPath))
            {
                resourceStream.CopyTo(outputStream);
            }

            Directory.CreateDirectory(payloadDirectory);
            ZipFile.ExtractToDirectory(payloadZipPath, payloadDirectory);
            Log("Payload extracted.");
        }
    }

    private void BackupExistingFiles(string gameDirectory)
    {
        string backupRoot = Path.Combine(gameDirectory, ".birdiemod_backup");
        Directory.CreateDirectory(backupRoot);
        Log("Backup folder: " + backupRoot);

        for (int i = 0; i < BackupEntries.Length; i++)
        {
            string relativePath = BackupEntries[i];
            string sourcePath = Path.Combine(gameDirectory, relativePath);
            string backupPath = Path.Combine(backupRoot, relativePath);

            if (File.Exists(sourcePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(backupPath));
                File.Copy(sourcePath, backupPath, true);
                Log("Backed up file: " + relativePath);
                continue;
            }

            if (Directory.Exists(sourcePath))
            {
                TryDeleteDirectory(backupPath);
                CopyDirectoryContents(sourcePath, backupPath);
                Log("Backed up folder: " + relativePath);
            }
        }
    }

    private void CopyDirectoryContents(string sourceDirectory, string targetDirectory)
    {
        Directory.CreateDirectory(targetDirectory);

        string[] directories = Directory.GetDirectories(sourceDirectory, "*", SearchOption.AllDirectories);
        for (int i = 0; i < directories.Length; i++)
        {
            string targetPath = directories[i].Replace(sourceDirectory, targetDirectory);
            Directory.CreateDirectory(targetPath);
        }

        string[] files = Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories);
        for (int i = 0; i < files.Length; i++)
        {
            string targetPath = files[i].Replace(sourceDirectory, targetDirectory);
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath));
            File.Copy(files[i], targetPath, true);
        }
    }

    private void TryDeleteDirectory(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            return;
        }

        try
        {
            Directory.Delete(directoryPath, true);
        }
        catch
        {
        }
    }

    private static bool IsValidGameDirectory(string gameDirectory)
    {
        if (string.IsNullOrWhiteSpace(gameDirectory) || !Directory.Exists(gameDirectory))
        {
            return false;
        }

        return File.Exists(Path.Combine(gameDirectory, GameExecutableName));
    }

    private void Log(string message)
    {
        logTextBox.AppendText(message + Environment.NewLine);
    }
}
