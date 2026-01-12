Imports System.IO
Imports System.IO.Compression
Imports System.Diagnostics
Imports System.Text
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Windows.Forms

Friend Partial Class frmUpdate
    Inherits Form

    Private ReadOnly _release As UpdateReleaseInfo
    Private ReadOnly _asset As UpdateAssetInfo
    Private ReadOnly _currentVersion As Version
    Private ReadOnly _palette As UiThemePalette
    Private _cts As CancellationTokenSource
    Private _tempRoot As String
    Private _downloadPath As String

    Public Sub New(release As UpdateReleaseInfo, currentVersion As Version)
        InitializeComponent()

        _release = release
        _currentVersion = currentVersion
        _asset = UpdateService.SelectBestAsset(release)
        _palette = UiThemeManager.Palette

        Try
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath)
        Catch
        End Try

        AddHandler btnDownload.Click, AddressOf DownloadClicked
        AddHandler btnCancel.Click, AddressOf CancelClicked
        AddHandler btnRelease.Click, AddressOf OpenReleaseClicked

        PopulateReleaseInfo()
        ApplyTheme()

        If _asset Is Nothing Then
            btnDownload.Enabled = False
            lblStatus.Text = "No compatible update package was found."
        End If
    End Sub

    Private Sub PopulateReleaseInfo()
        lblCurrentValue.Text = GetAppVersionDisplay()
        lblLatestValue.Text = If(_release IsNot Nothing, FormatVersionDisplay(_release.Version), "Unknown")
        lblPackageValue.Text = If(_asset IsNot Nothing, _asset.Name, "No compatible asset found")
        txtReleaseNotes.Text = If(_release IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(_release.Notes), _release.Notes, "No release notes provided.")
        btnRelease.Enabled = (_release IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(_release.HtmlUrl))
    End Sub

    Private Sub ApplyTheme()
        BackColor = _palette.Background
        ForeColor = _palette.Text

        tableRoot.BackColor = _palette.Background
        tableDetails.BackColor = _palette.Background
        panelProgress.BackColor = _palette.Background
        tableProgress.BackColor = _palette.Background
        tableStatus.BackColor = _palette.Background
        flowButtons.BackColor = _palette.Background

        txtReleaseNotes.BackColor = _palette.Surface
        txtReleaseNotes.ForeColor = _palette.Text

        lblTitle.ForeColor = _palette.Text
        lblCurrentTitle.ForeColor = _palette.TextMuted
        lblLatestTitle.ForeColor = _palette.TextMuted
        lblPackageTitle.ForeColor = _palette.TextMuted
        lblNotesTitle.ForeColor = _palette.TextMuted
        lblCurrentValue.ForeColor = _palette.Text
        lblLatestValue.ForeColor = _palette.Text
        lblPackageValue.ForeColor = _palette.Text
        lblStatus.ForeColor = _palette.Text
        lblProgress.ForeColor = _palette.TextMuted
        lblSpeed.ForeColor = _palette.TextMuted

        ConfigureButton(btnDownload)
        ConfigureButton(btnCancel)
        ConfigureButton(btnRelease)

        UiThemeManager.ApplyTheme(Me)
    End Sub

    Private Sub ConfigureButton(button As Button)
        If button Is Nothing Then
            Return
        End If

        button.UseVisualStyleBackColor = False
        button.BackColor = _palette.Surface
        button.ForeColor = _palette.Text
        button.FlatStyle = FlatStyle.Flat
        button.FlatAppearance.BorderColor = _palette.Border
        button.FlatAppearance.BorderSize = 1
    End Sub

    Private Async Sub DownloadClicked(sender As Object, e As EventArgs)
        If _asset Is Nothing OrElse _cts IsNot Nothing Then
            Return
        End If

        _cts = New CancellationTokenSource()
        btnDownload.Enabled = False
        btnCancel.Text = "Cancel"
        progressDownload.Value = 0
        lblStatus.Text = "Downloading update..."

        Try
            _tempRoot = CreateTempRoot()
            Directory.CreateDirectory(_tempRoot)
            _downloadPath = Path.Combine(_tempRoot, _asset.Name)

            Dim progress As New Progress(Of DownloadProgressInfo)(AddressOf UpdateProgress)
            Await UpdateService.DownloadAssetAsync(_asset, _downloadPath, progress, _cts.Token)

            If _cts.IsCancellationRequested Then
                lblStatus.Text = "Download canceled."
                btnCancel.Text = "Close"
                btnDownload.Enabled = True
                _cts = Nothing
                Return
            End If

            lblStatus.Text = "Preparing update..."
            Await ApplyUpdateAsync(_downloadPath)
        Catch ex As OperationCanceledException
            lblStatus.Text = "Download canceled."
            btnCancel.Text = "Close"
            btnDownload.Enabled = True
        Catch ex As Exception
            lblStatus.Text = "Update failed: " & ex.Message
            btnCancel.Text = "Close"
            btnDownload.Enabled = True
        Finally
            _cts = Nothing
        End Try
    End Sub

    Private Sub CancelClicked(sender As Object, e As EventArgs)
        If _cts IsNot Nothing Then
            lblStatus.Text = "Canceling..."
            _cts.Cancel()
            Return
        End If
        Close()
    End Sub

    Private Sub OpenReleaseClicked(sender As Object, e As EventArgs)
        If _release Is Nothing OrElse String.IsNullOrWhiteSpace(_release.HtmlUrl) Then
            Return
        End If
        Try
            Process.Start(New ProcessStartInfo(_release.HtmlUrl) With {.UseShellExecute = True})
        Catch
        End Try
    End Sub

    Private Sub UpdateProgress(info As DownloadProgressInfo)
        progressDownload.Value = Math.Max(0, Math.Min(100, info.Percent))
        Dim totalText As String = If(info.TotalBytes > 0, UpdateService.FormatBytes(info.TotalBytes), "Unknown")
        lblProgress.Text = $"{UpdateService.FormatBytes(info.BytesReceived)} / {totalText}"
        Dim speedText As String = If(info.SpeedBytesPerSec > 0, UpdateService.FormatBytes(CLng(info.SpeedBytesPerSec)) & "/s", "N/A")
        lblSpeed.Text = "Speed: " & speedText
    End Sub

    Private Async Function ApplyUpdateAsync(downloadPath As String) As Task
        If String.IsNullOrWhiteSpace(downloadPath) OrElse Not File.Exists(downloadPath) Then
            Throw New FileNotFoundException("Downloaded update package was not found.")
        End If

        Dim targetDir As String = AppContext.BaseDirectory
        Dim exeName As String = Path.GetFileName(Application.ExecutablePath)
        Dim extension As String = Path.GetExtension(downloadPath).ToLowerInvariant()

        If Not CanWriteToFolder(targetDir) Then
            Throw New InvalidOperationException("The application folder is not writable. Please run as administrator.")
        End If

        If extension = ".exe" Then
            Process.Start(New ProcessStartInfo(downloadPath) With {.UseShellExecute = True})
            Application.Exit()
            Return
        End If

        If extension <> ".zip" Then
            Throw New InvalidOperationException("Unsupported update package format.")
        End If

        Dim extractRoot As String = Path.Combine(_tempRoot, "payload")
        If Directory.Exists(extractRoot) Then
            Directory.Delete(extractRoot, True)
        End If
        Directory.CreateDirectory(extractRoot)

        Await Task.Run(Sub() ZipFile.ExtractToDirectory(downloadPath, extractRoot))

        Dim exePath As String = Directory.GetFiles(extractRoot, exeName, SearchOption.AllDirectories).FirstOrDefault()
        Dim sourceDir As String = If(String.IsNullOrWhiteSpace(exePath), extractRoot, Path.GetDirectoryName(exePath))

        If String.IsNullOrWhiteSpace(sourceDir) OrElse Not Directory.Exists(sourceDir) Then
            Throw New InvalidOperationException("Unable to locate update files.")
        End If

        LaunchUpdateScript(sourceDir, targetDir, exeName, _tempRoot)
        Application.Exit()
    End Function

    Private Function CreateTempRoot() As String
        Dim baseDir As String = Path.Combine(Path.GetTempPath(), "ClawHammerUpdate")
        Dim id As String = Guid.NewGuid().ToString("N")
        Return Path.Combine(baseDir, id)
    End Function

    Private Function CanWriteToFolder(folder As String) As Boolean
        Try
            Dim testPath As String = Path.Combine(folder, "write-test.tmp")
            File.WriteAllText(testPath, "x")
            File.Delete(testPath)
            Return True
        Catch
            Return False
        End Try
    End Function

    Private Sub LaunchUpdateScript(sourceDir As String, targetDir As String, exeName As String, tempRoot As String)
        Dim scriptPath As String = Path.Combine(tempRoot, "apply-update.cmd")
        Dim pid As Integer = Process.GetCurrentProcess().Id
        Dim exePath As String = Path.Combine(targetDir, exeName)

        Dim sb As New StringBuilder()
        sb.AppendLine("@echo off")
        sb.AppendLine("setlocal")
        sb.AppendLine($"set ""PID={pid}""")
        sb.AppendLine($"set ""SOURCE={sourceDir}""")
        sb.AppendLine($"set ""TARGET={targetDir.TrimEnd(Path.DirectorySeparatorChar)}""")
        sb.AppendLine(":wait")
        sb.AppendLine("tasklist /FI ""PID eq %PID%"" | find /I ""%PID%"" >nul")
        sb.AppendLine("if not errorlevel 1 (")
        sb.AppendLine("  timeout /t 1 /nobreak >nul")
        sb.AppendLine("  goto wait")
        sb.AppendLine(")")
        sb.AppendLine("robocopy ""%SOURCE%"" ""%TARGET%"" /E /COPY:DAT /R:2 /W:1 /NFL /NDL /NJH /NJS /NP >nul")
        sb.AppendLine($"start """" ""{exePath}""")
        sb.AppendLine("endlocal")

        File.WriteAllText(scriptPath, sb.ToString(), Encoding.ASCII)
        Dim startInfo As New ProcessStartInfo(scriptPath) With {
            .WorkingDirectory = tempRoot,
            .UseShellExecute = True,
            .WindowStyle = ProcessWindowStyle.Hidden
        }
        Process.Start(startInfo)
    End Sub
End Class
