using System;
using System.IO;
using System.Windows.Forms;

internal static class UninstallerProgram
{
    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new BirdieModUninstallerForm());
    }
}

internal sealed class BirdieModUninstallerForm : Form
{
    private const string GameExecutableName = "Super Battle Golf.exe";
    private const string BackupFolderName = ".birdiemod_backup";

    private static readonly string[] RemoveEntries =
    {
        Path.Combine("Mods", "BirdieMod.dll"),
        Path.Combine("Mods", "BirdieMod.cfg"),
        "version.dll",
        "dobby.dll",
        "NOTICE.txt",
        "MelonLoader",
        "Dependencies",
    };

    private readonly TextBox pathTextBox;
    private readonly Button browseButton;
    private readonly Button uninstallButton;
    private readonly TextBox logTextBox;

    internal BirdieModUninstallerForm()
    {
        Text = "Birdie Mod Uninstaller v1.0";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new System.Drawing.Size(560, 340);

        Label titleLabel = new Label();
        titleLabel.Left = 16;
        titleLabel.Top = 16;
        titleLabel.Width = 528;
        titleLabel.Height = 24;
        titleLabel.Text = "Select the folder that contains Super Battle Golf.exe";
        Controls.Add(titleLabel);

        pathTextBox = new TextBox();
        pathTextBox.Left = 16;
        pathTextBox.Top = 48;
        pathTextBox.Width = 430;
        Controls.Add(pathTextBox);

        browseButton = new Button();
        browseButton.Left = 454;
        browseButton.Top = 46;
        browseButton.Width = 90;
        browseButton.Height = 26;
        browseButton.Text = "Browse";
        browseButton.Click += BrowseButton_Click;
        Controls.Add(browseButton);

        Label statusLabel = new Label();
        statusLabel.Left = 16;
        statusLabel.Top = 78;
        statusLabel.Width = 528;
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

        uninstallButton = new Button();
        uninstallButton.Left = 16;
        uninstallButton.Top = 104;
        uninstallButton.Width = 120;
        uninstallButton.Height = 30;
        uninstallButton.Text = "Uninstall";
        uninstallButton.Click += UninstallButton_Click;
        Controls.Add(uninstallButton);

        logTextBox = new TextBox();
        logTextBox.Left = 16;
        logTextBox.Top = 150;
        logTextBox.Width = 528;
        logTextBox.Height = 170;
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
                dialog.SelectedPath = pathTextBox.Text;

            if (dialog.ShowDialog(this) == DialogResult.OK)
                pathTextBox.Text = dialog.SelectedPath;
        }
    }

    private void UninstallButton_Click(object sender, EventArgs e)
    {
        string gameDirectory = pathTextBox.Text.Trim();
        if (!IsValidGameDirectory(gameDirectory))
        {
            MessageBox.Show(this, "Select a valid folder that contains Super Battle Golf.exe.", "Invalid folder", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        DialogResult confirm = MessageBox.Show(this,
            "This will remove Birdie Mod and MelonLoader from:\n\n" + gameDirectory + "\n\nContinue?",
            "Confirm uninstall", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (confirm != DialogResult.Yes)
            return;

        SetBusyState(true);
        logTextBox.Clear();

        try
        {
            string backupRoot = Path.Combine(gameDirectory, BackupFolderName);
            bool hasBackup = Directory.Exists(backupRoot);

            RemoveInstalledFiles(gameDirectory);

            if (hasBackup)
            {
                DialogResult restoreResult = MessageBox.Show(this,
                    "A backup from the original installation was found.\nRestore pre-install files?",
                    "Restore backup?", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                if (restoreResult == DialogResult.Yes)
                {
                    RestoreFromBackup(backupRoot, gameDirectory);
                    Log("Backup restored.");
                }

                TryDeleteDirectory(backupRoot);
                Log("Backup folder removed.");
            }

            Log("Uninstall complete.");
            MessageBox.Show(this, "Birdie Mod was uninstalled successfully.", "Done", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            Log("Uninstall failed: " + ex.Message);
            MessageBox.Show(this, ex.Message, "Uninstall failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetBusyState(false);
        }
    }

    private void RemoveInstalledFiles(string gameDirectory)
    {
        for (int i = 0; i < RemoveEntries.Length; i++)
        {
            string fullPath = Path.Combine(gameDirectory, RemoveEntries[i]);

            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
                Log("Removed: " + RemoveEntries[i]);
            }
            else if (Directory.Exists(fullPath))
            {
                Directory.Delete(fullPath, true);
                Log("Removed folder: " + RemoveEntries[i]);
            }
        }
    }

    private void RestoreFromBackup(string backupRoot, string gameDirectory)
    {
        string[] files = Directory.GetFiles(backupRoot, "*", SearchOption.AllDirectories);
        for (int i = 0; i < files.Length; i++)
        {
            string relativePath = files[i].Substring(backupRoot.Length)
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string targetPath = Path.Combine(gameDirectory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath));
            File.Copy(files[i], targetPath, true);
            Log("Restored: " + relativePath);
        }
    }

    private void SetBusyState(bool isBusy)
    {
        UseWaitCursor = isBusy;
        browseButton.Enabled = !isBusy;
        uninstallButton.Enabled = !isBusy;
        pathTextBox.Enabled = !isBusy;
        Cursor.Current = isBusy ? Cursors.WaitCursor : Cursors.Default;
    }

    private void TryDeleteDirectory(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
            return;

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
            return false;

        return File.Exists(Path.Combine(gameDirectory, GameExecutableName));
    }

    private void Log(string message)
    {
        logTextBox.AppendText(message + Environment.NewLine);
    }
}
