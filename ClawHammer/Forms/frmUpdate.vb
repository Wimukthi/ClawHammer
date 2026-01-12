Imports System.IO
Imports System.IO.Compression
Imports System.Diagnostics
Imports System.Text
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Windows.Forms

Friend Class frmUpdate
    Inherits Form

    Private ReadOnly _release As UpdateReleaseInfo
    Private ReadOnly _asset As UpdateAssetInfo
    Private ReadOnly _currentVersion As Version
    Private ReadOnly _palette As UiThemePalette
    Private _cts As CancellationTokenSource
    Private _tempRoot As String
    Private _downloadPath As String

    Private ReadOnly _lblStatus As Label
    Private ReadOnly _lblSpeed As Label
    Private ReadOnly _lblProgress As Label
    Private ReadOnly _progressBar As ProgressBar
    Private ReadOnly _btnDownload As Button
    Private ReadOnly _btnCancel As Button

    Public Sub New(release As UpdateReleaseInfo, currentVersion As Version)
        _release = release
        _currentVersion = currentVersion
        _asset = UpdateService.SelectBestAsset(release)
        _palette = UiThemeManager.Palette

        AutoScaleMode = AutoScaleMode.Dpi
        AutoScaleDimensions = New SizeF(96.0F, 96.0F)
        Text = "ClawHammer Updater"
        StartPosition = FormStartPosition.CenterParent
        MinimumSize = New Size(640, 520)
        Size = New Size(740, 600)

        BackColor = _palette.Background
        ForeColor = _palette.Text

        Dim root As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 2,
            .RowCount = 8,
            .Padding = New Padding(16),
            .BackColor = _palette.Background
        }
        root.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))
        root.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))

        Dim title As New Label() With {
            .Text = "Update Available",
            .AutoSize = True,
            .Font = New Font(Font, FontStyle.Bold),
            .ForeColor = _palette.Text
        }
        root.Controls.Add(title, 0, 0)
        root.SetColumnSpan(title, 2)

        Dim lblCurrentTitle As New Label() With {.Text = "Current:", .AutoSize = True, .ForeColor = _palette.TextMuted}
        Dim lblCurrentValue As New Label() With {.Text = GetAppVersionDisplay(), .AutoSize = True, .ForeColor = _palette.Text}
        root.Controls.Add(lblCurrentTitle, 0, 1)
        root.Controls.Add(lblCurrentValue, 1, 1)

        Dim lblLatestTitle As New Label() With {.Text = "Latest:", .AutoSize = True, .ForeColor = _palette.TextMuted}
        Dim latestText As String = If(_release IsNot Nothing, FormatVersionDisplay(_release.Version), "Unknown")
        Dim lblLatestValue As New Label() With {.Text = latestText, .AutoSize = True, .ForeColor = _palette.Text}
        root.Controls.Add(lblLatestTitle, 0, 2)
        root.Controls.Add(lblLatestValue, 1, 2)

        Dim lblAssetTitle As New Label() With {.Text = "Package:", .AutoSize = True, .ForeColor = _palette.TextMuted}
        Dim assetText As String = If(_asset IsNot Nothing, _asset.Name, "No compatible asset found")
        Dim lblAssetValue As New Label() With {.Text = assetText, .AutoSize = True, .ForeColor = _palette.Text}
        root.Controls.Add(lblAssetTitle, 0, 3)
        root.Controls.Add(lblAssetValue, 1, 3)

        Dim notesTitle As New Label() With {.Text = "Release notes:", .AutoSize = True, .ForeColor = _palette.TextMuted}
        root.Controls.Add(notesTitle, 0, 4)
        root.SetColumnSpan(notesTitle, 2)

        Dim notesBox As New TextBox() With {
            .Multiline = True,
            .ReadOnly = True,
            .ScrollBars = ScrollBars.Vertical,
            .Dock = DockStyle.Fill,
            .BackColor = _palette.Surface,
            .ForeColor = _palette.Text,
            .BorderStyle = BorderStyle.FixedSingle
        }
        notesBox.Text = If(_release IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(_release.Notes), _release.Notes, "No release notes provided.")
        root.Controls.Add(notesBox, 0, 5)
        root.SetColumnSpan(notesBox, 2)

        _progressBar = New ProgressBar() With {.Dock = DockStyle.Fill, .Minimum = 0, .Maximum = 100, .Value = 0}
        root.Controls.Add(_progressBar, 0, 6)
        root.SetColumnSpan(_progressBar, 2)

        Dim statusPanel As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 3}
        statusPanel.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50.0F))
        statusPanel.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 25.0F))
        statusPanel.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 25.0F))
        _lblStatus = New Label() With {.Text = "Ready to download.", .AutoSize = True, .ForeColor = _palette.Text}
        _lblProgress = New Label() With {.Text = "0 / 0", .AutoSize = True, .ForeColor = _palette.TextMuted, .TextAlign = ContentAlignment.MiddleRight}
        _lblSpeed = New Label() With {.Text = "Speed: N/A", .AutoSize = True, .ForeColor = _palette.TextMuted, .TextAlign = ContentAlignment.MiddleRight}
        statusPanel.Controls.Add(_lblStatus, 0, 0)
        statusPanel.Controls.Add(_lblProgress, 1, 0)
        statusPanel.Controls.Add(_lblSpeed, 2, 0)
        root.Controls.Add(statusPanel, 0, 7)
        root.SetColumnSpan(statusPanel, 2)

        Dim buttonPanel As New FlowLayoutPanel() With {
            .Dock = DockStyle.Bottom,
            .FlowDirection = FlowDirection.RightToLeft,
            .Padding = New Padding(0, 12, 0, 0),
            .AutoSize = True,
            .BackColor = _palette.Background
        }

        _btnDownload = New Button() With {.Text = "Download && Install", .AutoSize = True, .BackColor = _palette.Surface, .ForeColor = _palette.Text, .FlatStyle = FlatStyle.Flat}
        _btnDownload.FlatAppearance.BorderColor = _palette.Border
        _btnDownload.FlatAppearance.BorderSize = 1
        AddHandler _btnDownload.Click, AddressOf DownloadClicked

        _btnCancel = New Button() With {.Text = "Close", .AutoSize = True, .BackColor = _palette.Surface, .ForeColor = _palette.Text, .FlatStyle = FlatStyle.Flat}
        _btnCancel.FlatAppearance.BorderColor = _palette.Border
        _btnCancel.FlatAppearance.BorderSize = 1
        AddHandler _btnCancel.Click, AddressOf CancelClicked

        Dim btnRelease As New Button() With {.Text = "View Release", .AutoSize = True, .BackColor = _palette.Surface, .ForeColor = _palette.Text, .FlatStyle = FlatStyle.Flat}
        btnRelease.FlatAppearance.BorderColor = _palette.Border
        btnRelease.FlatAppearance.BorderSize = 1
        AddHandler btnRelease.Click, AddressOf OpenReleaseClicked

        buttonPanel.Controls.Add(_btnCancel)
        buttonPanel.Controls.Add(_btnDownload)
        buttonPanel.Controls.Add(btnRelease)

        Dim container As New Panel() With {.Dock = DockStyle.Fill, .BackColor = _palette.Background}
        container.Controls.Add(root)
        container.Controls.Add(buttonPanel)

        Controls.Add(container)

        UiThemeManager.ApplyTheme(Me)

        If _asset Is Nothing Then
            _btnDownload.Enabled = False
            _lblStatus.Text = "No compatible update package was found."
        End If
    End Sub

    Private Async Sub DownloadClicked(sender As Object, e As EventArgs)
        If _asset Is Nothing OrElse _cts IsNot Nothing Then
            Return
        End If

        _cts = New CancellationTokenSource()
        _btnDownload.Enabled = False
        _btnCancel.Text = "Cancel"
        _progressBar.Value = 0
        _lblStatus.Text = "Downloading update..."

        Try
            _tempRoot = CreateTempRoot()
            Directory.CreateDirectory(_tempRoot)
            _downloadPath = Path.Combine(_tempRoot, _asset.Name)

            Dim progress As New Progress(Of DownloadProgressInfo)(AddressOf UpdateProgress)
            Await UpdateService.DownloadAssetAsync(_asset, _downloadPath, progress, _cts.Token)

            If _cts.IsCancellationRequested Then
                _lblStatus.Text = "Download canceled."
                _btnCancel.Text = "Close"
                _btnDownload.Enabled = True
                _cts = Nothing
                Return
            End If

            _lblStatus.Text = "Preparing update..."
            Await ApplyUpdateAsync(_downloadPath)
        Catch ex As OperationCanceledException
            _lblStatus.Text = "Download canceled."
            _btnCancel.Text = "Close"
            _btnDownload.Enabled = True
        Catch ex As Exception
            _lblStatus.Text = "Update failed: " & ex.Message
            _btnCancel.Text = "Close"
            _btnDownload.Enabled = True
        Finally
            _cts = Nothing
        End Try
    End Sub

    Private Sub CancelClicked(sender As Object, e As EventArgs)
        If _cts IsNot Nothing Then
            _lblStatus.Text = "Canceling..."
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
        _progressBar.Value = Math.Max(0, Math.Min(100, info.Percent))
        Dim totalText As String = If(info.TotalBytes > 0, UpdateService.FormatBytes(info.TotalBytes), "Unknown")
        _lblProgress.Text = $"{UpdateService.FormatBytes(info.BytesReceived)} / {totalText}"
        Dim speedText As String = If(info.SpeedBytesPerSec > 0, UpdateService.FormatBytes(CLng(info.SpeedBytesPerSec)) & "/s", "N/A")
        _lblSpeed.Text = "Speed: " & speedText
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
