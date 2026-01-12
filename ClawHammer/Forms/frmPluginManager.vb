Imports System
Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.Drawing
Imports System.IO
Imports System.Net.Http
Imports System.Security.Cryptography
Imports System.Text.Json
Imports System.Threading.Tasks
Imports System.Windows.Forms

Public Class frmPluginManager
    Inherits Form

    Private ReadOnly _toolTip As ToolTip
    Private _settings As PluginSettings
    Private _installedPlugins As List(Of PluginInfo) = New List(Of PluginInfo)()
    Private _catalogEntries As List(Of PluginCatalogEntry) = New List(Of PluginCatalogEntry)()
    Private _hasChanges As Boolean
    Private _requiresRestart As Boolean

    Private Const CorePluginAssemblyFileName As String = "ClawHammer.DefaultPlugins.dll"
    Private Const WorkloadIndent As String = "    "
    Private _pluginHeaderFont As Font

    Private Enum InstalledEntryKind
        PluginAssembly
        Workload
    End Enum

    Private Class InstalledEntry
        Public ReadOnly Property Kind As InstalledEntryKind
        Public ReadOnly Property AssemblyPath As String
        Public ReadOnly Property AssemblyName As String
        Public ReadOnly Property AssemblyVersion As Version
        Public ReadOnly Property IsCore As Boolean
        Public ReadOnly Property Workload As PluginInfo
        Public ReadOnly Property WorkloadCount As Integer

        Public Sub New(kind As InstalledEntryKind, assemblyPath As String, assemblyVersion As Version, isCore As Boolean, workload As PluginInfo, workloadCount As Integer)
            Me.Kind = kind
            Me.AssemblyPath = assemblyPath
            Me.AssemblyName = If(String.IsNullOrWhiteSpace(assemblyPath), String.Empty, Path.GetFileName(assemblyPath))
            Me.AssemblyVersion = assemblyVersion
            Me.IsCore = isCore
            Me.Workload = workload
            Me.WorkloadCount = workloadCount
        End Sub
    End Class

    Private tabMain As TabControl
    Private tabInstalled As TabPage
    Private tabCatalog As TabPage

    Private lstInstalled As ListView
    Private btnEnableDisable As Button
    Private btnUninstall As Button
    Private btnInstallLocal As Button
    Private btnOpenFolder As Button
    Private btnRefreshInstalled As Button
    Private lblInstalledStatus As Label

    Private txtCatalogUrl As TextBox
    Private btnLoadCatalog As Button
    Private lstCatalog As ListView
    Private btnInstallCatalog As Button
    Private prgDownload As ProgressBar
    Private lblDownloadStatus As Label

    Public ReadOnly Property HasChanges As Boolean
        Get
            Return _hasChanges
        End Get
    End Property

    Public ReadOnly Property RequiresRestart As Boolean
        Get
            Return _requiresRestart
        End Get
    End Property

    Public Sub New()
        Text = "Plugin Manager"
        StartPosition = FormStartPosition.CenterParent
        FormBorderStyle = FormBorderStyle.Sizable
        MinimumSize = New Size(760, 520)
        Size = New Size(920, 620)

        Try
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath)
        Catch
        End Try
        _toolTip = New ToolTip() With {
            .AutoPopDelay = 12000,
            .InitialDelay = 500,
            .ReshowDelay = 150,
            .ShowAlways = True
        }

        BuildUi()
        UiThemeManager.ApplyTheme(Me)

        AddHandler Me.Load, AddressOf frmPluginManager_Load
    End Sub

    Private Sub BuildUi()
        tabMain = New TabControl() With {
            .Dock = DockStyle.Fill
        }

        tabInstalled = New TabPage("Installed")
        tabCatalog = New TabPage("Online Catalog")

        tabMain.TabPages.Add(tabInstalled)
        tabMain.TabPages.Add(tabCatalog)

        BuildInstalledTab()
        BuildCatalogTab()

        Controls.Add(tabMain)
    End Sub

    Private Sub BuildInstalledTab()
        lstInstalled = New ListView() With {
            .Dock = DockStyle.Fill,
            .View = View.Details,
            .FullRowSelect = True,
            .HideSelection = False,
            .MultiSelect = False
        }
        lstInstalled.Columns.Add("Name", 180)
        lstInstalled.Columns.Add("Id / Type", 230)
        lstInstalled.Columns.Add("Version", 90)
        lstInstalled.Columns.Add("Status", 90)
        lstInstalled.Columns.Add("Description", 300)
        AddHandler lstInstalled.SelectedIndexChanged, AddressOf lstInstalled_SelectedIndexChanged

        btnEnableDisable = New Button() With {.Text = "Disable", .AutoSize = True}
        btnUninstall = New Button() With {.Text = "Uninstall Plugin", .AutoSize = True}
        btnInstallLocal = New Button() With {.Text = "Install From File...", .AutoSize = True}
        btnOpenFolder = New Button() With {.Text = "Open Plugins Folder", .AutoSize = True}
        btnRefreshInstalled = New Button() With {.Text = "Refresh", .AutoSize = True}

        AddHandler btnEnableDisable.Click, AddressOf btnEnableDisable_Click
        AddHandler btnUninstall.Click, AddressOf btnUninstall_Click
        AddHandler btnInstallLocal.Click, AddressOf btnInstallLocal_Click
        AddHandler btnOpenFolder.Click, AddressOf btnOpenFolder_Click
        AddHandler btnRefreshInstalled.Click, AddressOf btnRefreshInstalled_Click

        _toolTip.SetToolTip(lstInstalled, "Plugins grouped by DLL. Select a workload to enable/disable, or a plugin row to uninstall.")
        _toolTip.SetToolTip(btnEnableDisable, "Toggle the selected workload on or off.")
        _toolTip.SetToolTip(btnUninstall, "Remove the selected plugin DLL and all workloads.")
        _toolTip.SetToolTip(btnInstallLocal, "Stage a local plugin package for install on restart.")
        _toolTip.SetToolTip(btnOpenFolder, "Open the plugins folder in Explorer.")
        _toolTip.SetToolTip(btnRefreshInstalled, "Reload the installed plugin list.")

        lblInstalledStatus = New Label() With {
            .AutoSize = True,
            .Text = "Changes to installed plugins may require a restart.",
            .Location = New Point(8, 8)
        }

        Dim buttonPanel As New FlowLayoutPanel() With {
            .Dock = DockStyle.Bottom,
            .Height = 38,
            .Padding = New Padding(8, 4, 8, 4)
        }
        buttonPanel.Controls.AddRange(New Control() {btnEnableDisable, btnUninstall, btnInstallLocal, btnOpenFolder, btnRefreshInstalled})

        Dim bottomPanel As New Panel() With {
            .Dock = DockStyle.Bottom,
            .Height = 62
        }
        bottomPanel.Controls.Add(lblInstalledStatus)
        bottomPanel.Controls.Add(buttonPanel)

        tabInstalled.Controls.Add(lstInstalled)
        tabInstalled.Controls.Add(bottomPanel)
    End Sub

    Private Sub BuildCatalogTab()
        Dim topPanel As New Panel() With {
            .Dock = DockStyle.Top,
            .Height = 36
        }

        Dim lblCatalog As New Label() With {
            .AutoSize = True,
            .Text = "Catalog URL:",
            .Location = New Point(8, 10)
        }

        txtCatalogUrl = New TextBox() With {
            .Location = New Point(92, 7),
            .Width = 520
        }

        btnLoadCatalog = New Button() With {
            .Text = "Load",
            .Location = New Point(620, 5),
            .Width = 80
        }

        _toolTip.SetToolTip(txtCatalogUrl, "URL to a JSON plugin catalog.")
        _toolTip.SetToolTip(btnLoadCatalog, "Fetch the catalog from the URL.")

        AddHandler btnLoadCatalog.Click, AddressOf btnLoadCatalog_Click

        topPanel.Controls.Add(lblCatalog)
        topPanel.Controls.Add(txtCatalogUrl)
        topPanel.Controls.Add(btnLoadCatalog)

        lstCatalog = New ListView() With {
            .Dock = DockStyle.Fill,
            .View = View.Details,
            .FullRowSelect = True,
            .HideSelection = False,
            .MultiSelect = False
        }
        lstCatalog.Columns.Add("Name", 180)
        lstCatalog.Columns.Add("Id", 230)
        lstCatalog.Columns.Add("Version", 90)
        lstCatalog.Columns.Add("Status", 120)
        lstCatalog.Columns.Add("Description", 300)
        AddHandler lstCatalog.SelectedIndexChanged, AddressOf lstCatalog_SelectedIndexChanged
        _toolTip.SetToolTip(lstCatalog, "Online catalog entries.")

        btnInstallCatalog = New Button() With {
            .Text = "Install Selected",
            .AutoSize = True,
            .Enabled = False
        }
        AddHandler btnInstallCatalog.Click, AddressOf btnInstallCatalog_Click
        _toolTip.SetToolTip(btnInstallCatalog, "Download and stage the selected plugin for install on restart.")

        lblDownloadStatus = New Label() With {
            .AutoSize = True,
            .Text = "Ready.",
            .Location = New Point(8, 8)
        }

        prgDownload = New ProgressBar() With {
            .Dock = DockStyle.Bottom,
            .Height = 16,
            .Minimum = 0,
            .Maximum = 100,
            .Value = 0
        }

        Dim bottomPanel As New Panel() With {
            .Dock = DockStyle.Bottom,
            .Height = 66
        }

        Dim buttonPanel As New FlowLayoutPanel() With {
            .Dock = DockStyle.Bottom,
            .Height = 34,
            .Padding = New Padding(8, 4, 8, 4)
        }
        buttonPanel.Controls.Add(btnInstallCatalog)

        bottomPanel.Controls.Add(lblDownloadStatus)
        bottomPanel.Controls.Add(buttonPanel)
        bottomPanel.Controls.Add(prgDownload)

        tabCatalog.Controls.Add(lstCatalog)
        tabCatalog.Controls.Add(bottomPanel)
        tabCatalog.Controls.Add(topPanel)
    End Sub

    Private Sub frmPluginManager_Load(sender As Object, e As EventArgs)
        _settings = PluginSettingsStore.LoadSettings()
        txtCatalogUrl.Text = _settings.CatalogUrl
        RefreshInstalledList()
    End Sub

    Private Shared Function GetAssemblyDisplayName(assemblyPath As String) As String
        If String.IsNullOrWhiteSpace(assemblyPath) Then
            Return "Unknown"
        End If
        Return Path.GetFileName(assemblyPath)
    End Function

    Private Function IsCoreAssembly(assemblyPath As String) As Boolean
        Dim fileName As String = Path.GetFileName(assemblyPath)
        Return String.Equals(fileName, CorePluginAssemblyFileName, StringComparison.OrdinalIgnoreCase)
    End Function

    Private Sub RefreshInstalledList()
        _settings = PluginSettingsStore.LoadSettings()
        Dim disabledIds As New HashSet(Of String)(_settings.DisabledPluginIds, StringComparer.OrdinalIgnoreCase)

        _installedPlugins = PluginDiscovery.DiscoverPlugins(AddressOf LogMessage)

        Dim pluginMap As New Dictionary(Of String, List(Of PluginInfo))(StringComparer.OrdinalIgnoreCase)
        For Each plugin As PluginInfo In _installedPlugins
            Dim assemblyPath As String = If(plugin.AssemblyPath, String.Empty)
            Dim list As List(Of PluginInfo) = Nothing
            If Not pluginMap.TryGetValue(assemblyPath, list) Then
                list = New List(Of PluginInfo)()
                pluginMap(assemblyPath) = list
            End If
            list.Add(plugin)
        Next

        Dim assemblyPaths As New List(Of String)(pluginMap.Keys)
        assemblyPaths.Sort(Function(a, b) StringComparer.OrdinalIgnoreCase.Compare(GetAssemblyDisplayName(a), GetAssemblyDisplayName(b)))

        lstInstalled.BeginUpdate()
        lstInstalled.Items.Clear()

        If _pluginHeaderFont Is Nothing Then
            _pluginHeaderFont = New Font(lstInstalled.Font, FontStyle.Bold)
        End If

        For Each assemblyPath As String In assemblyPaths
            Dim workloads As List(Of PluginInfo) = pluginMap(assemblyPath)
            workloads.Sort(Function(a, b) StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name))

            Dim isCore As Boolean = IsCoreAssembly(assemblyPath)
            Dim assemblyVersion As Version = Nothing
            If workloads.Count > 0 Then
                assemblyVersion = workloads(0).Version
            End If

            Dim pluginEntry As New InstalledEntry(InstalledEntryKind.PluginAssembly, assemblyPath, assemblyVersion, isCore, Nothing, workloads.Count)
            Dim pluginItem As New ListViewItem(GetAssemblyDisplayName(assemblyPath))
            pluginItem.SubItems.Add("Plugin DLL")
            pluginItem.SubItems.Add(If(assemblyVersion IsNot Nothing, assemblyVersion.ToString(), "0.0.0.0"))
            pluginItem.SubItems.Add(If(isCore, "Core", "Installed"))
            pluginItem.SubItems.Add($"Contains {workloads.Count} workload(s).")
            pluginItem.Tag = pluginEntry
            pluginItem.Font = _pluginHeaderFont
            lstInstalled.Items.Add(pluginItem)

            For Each plugin As PluginInfo In workloads
                Dim status As String = If(disabledIds.Contains(plugin.Id), "Disabled", "Enabled")
                Dim versionText As String = If(plugin.Version IsNot Nothing, plugin.Version.ToString(), "0.0.0.0")
                Dim item As New ListViewItem(WorkloadIndent & plugin.Name)
                item.SubItems.Add(plugin.Id)
                item.SubItems.Add(versionText)
                item.SubItems.Add(status)
                item.SubItems.Add(If(plugin.Description, String.Empty))
                item.Tag = New InstalledEntry(InstalledEntryKind.Workload, assemblyPath, assemblyVersion, isCore, plugin, workloads.Count)
                lstInstalled.Items.Add(item)
            Next
        Next

        lstInstalled.EndUpdate()

        UpdateInstalledButtons()
    End Sub

    Private Sub UpdateInstalledButtons()
        Dim selected As InstalledEntry = GetSelectedInstalled()
        btnEnableDisable.Enabled = False
        btnUninstall.Enabled = False
        btnEnableDisable.Text = "Disable"

        If selected Is Nothing Then
            Return
        End If

        If selected.Kind = InstalledEntryKind.PluginAssembly Then
            btnUninstall.Enabled = Not selected.IsCore
            Return
        End If

        Dim disabledIds As New HashSet(Of String)(_settings.DisabledPluginIds, StringComparer.OrdinalIgnoreCase)
        Dim workload As PluginInfo = selected.Workload
        btnEnableDisable.Enabled = True
        btnEnableDisable.Text = If(workload IsNot Nothing AndAlso disabledIds.Contains(workload.Id), "Enable", "Disable")
    End Sub

    Private Function GetSelectedInstalled() As InstalledEntry
        If lstInstalled.SelectedItems.Count = 0 Then
            Return Nothing
        End If
        Return TryCast(lstInstalled.SelectedItems(0).Tag, InstalledEntry)
    End Function

    Private Sub lstInstalled_SelectedIndexChanged(sender As Object, e As EventArgs)
        UpdateInstalledButtons()
    End Sub

    Private Sub btnEnableDisable_Click(sender As Object, e As EventArgs)
        Dim selected As InstalledEntry = GetSelectedInstalled()
        If selected Is Nothing OrElse selected.Kind <> InstalledEntryKind.Workload Then
            Return
        End If

        Dim workload As PluginInfo = selected.Workload
        If workload Is Nothing Then
            Return
        End If

        Dim disabledIds As New HashSet(Of String)(_settings.DisabledPluginIds, StringComparer.OrdinalIgnoreCase)
        If disabledIds.Contains(workload.Id) Then
            disabledIds.Remove(workload.Id)
        Else
            disabledIds.Add(workload.Id)
        End If

        _settings.DisabledPluginIds = New List(Of String)(disabledIds)
        PluginSettingsStore.SaveSettings(_settings)
        _hasChanges = True
        RefreshInstalledList()
    End Sub

    Private Sub btnUninstall_Click(sender As Object, e As EventArgs)
        Dim selected As InstalledEntry = GetSelectedInstalled()
        If selected Is Nothing OrElse selected.Kind <> InstalledEntryKind.PluginAssembly Then
            Return
        End If

        If selected.IsCore Then
            MessageBox.Show(Me, "The core plugin cannot be uninstalled.", "Plugin Manager", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        If String.IsNullOrWhiteSpace(selected.AssemblyPath) Then
            MessageBox.Show(Me, "Plugin path is missing.", "Plugin Manager", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        Dim pluginName As String = If(String.IsNullOrWhiteSpace(selected.AssemblyName), "Unknown", selected.AssemblyName)
        Dim workloadCount As Integer = selected.WorkloadCount
        Dim result As DialogResult = MessageBox.Show(Me, $"Remove plugin '{pluginName}'? This will remove {workloadCount} workload(s). A restart may be required.", "Plugin Manager", MessageBoxButtons.YesNo, MessageBoxIcon.Warning)
        If result <> DialogResult.Yes Then
            Return
        End If

        Try
            File.Delete(selected.AssemblyPath)
        Catch
            PluginInstallManager.ScheduleDelete(selected.AssemblyPath)
            _requiresRestart = True
        End Try

        _hasChanges = True
        RefreshInstalledList()
    End Sub

    Private Sub btnInstallLocal_Click(sender As Object, e As EventArgs)
        Using dlg As New OpenFileDialog()
            dlg.Title = "Install Plugin"
            dlg.Filter = "Plugin Package (*.zip;*.dll)|*.zip;*.dll|All Files (*.*)|*.*"
            dlg.Multiselect = False
            If dlg.ShowDialog(Me) <> DialogResult.OK Then
                Return
            End If

            Try
                PluginInstallManager.StageInstall(dlg.FileName)
                _requiresRestart = True
                _hasChanges = True
                MessageBox.Show(Me, "Plugin staged for install. Restart ClawHammer to load it.", "Plugin Manager", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Catch ex As Exception
                MessageBox.Show(Me, "Install failed: " & ex.Message, "Plugin Manager", MessageBoxButtons.OK, MessageBoxIcon.Error)
            End Try
        End Using
    End Sub

    Private Sub btnOpenFolder_Click(sender As Object, e As EventArgs)
        Dim folder As String = PluginSettingsStore.GetPluginDirectory()
        Directory.CreateDirectory(folder)
        Process.Start(New ProcessStartInfo("explorer.exe", folder) With {.UseShellExecute = True})
    End Sub

    Private Sub btnRefreshInstalled_Click(sender As Object, e As EventArgs)
        RefreshInstalledList()
    End Sub

    Private Async Sub btnLoadCatalog_Click(sender As Object, e As EventArgs)
        Dim url As String = If(txtCatalogUrl.Text, String.Empty).Trim()
        _settings.CatalogUrl = url
        PluginSettingsStore.SaveSettings(_settings)

        If String.IsNullOrWhiteSpace(url) Then
            MessageBox.Show(Me, "Enter a catalog URL first.", "Plugin Manager", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        Await LoadCatalogAsync(url)
    End Sub

    Private Async Function LoadCatalogAsync(url As String) As Task
        lblDownloadStatus.Text = "Loading catalog..."
        prgDownload.Value = 0
        prgDownload.Style = ProgressBarStyle.Marquee

        Try
            Using client As New HttpClient()
                Dim json As String = Await client.GetStringAsync(url)
                Dim options As New JsonSerializerOptions With {
                    .PropertyNameCaseInsensitive = True
                }
                Dim entries As List(Of PluginCatalogEntry) = JsonSerializer.Deserialize(Of List(Of PluginCatalogEntry))(json, options)
                _catalogEntries = If(entries, New List(Of PluginCatalogEntry)())
            End Using
        Catch ex As Exception
            lblDownloadStatus.Text = "Catalog load failed."
            prgDownload.Style = ProgressBarStyle.Continuous
            prgDownload.Value = 0
            MessageBox.Show(Me, "Failed to load catalog: " & ex.Message, "Plugin Manager", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Return
        End Try

        prgDownload.Style = ProgressBarStyle.Continuous
        prgDownload.Value = 0
        lblDownloadStatus.Text = "Catalog loaded."
        RefreshCatalogList()
    End Function

    Private Sub RefreshCatalogList()
        lstCatalog.BeginUpdate()
        lstCatalog.Items.Clear()

        Dim installedById As New Dictionary(Of String, PluginInfo)(StringComparer.OrdinalIgnoreCase)
        For Each plugin As PluginInfo In _installedPlugins
            installedById(plugin.Id) = plugin
        Next

        For Each entry As PluginCatalogEntry In _catalogEntries
            Dim name As String = If(String.IsNullOrWhiteSpace(entry.Name), entry.Id, entry.Name)
            Dim versionText As String = If(String.IsNullOrWhiteSpace(entry.Version), "", entry.Version)
            Dim status As String = "Not installed"

            Dim installed As PluginInfo = Nothing
            If installedById.TryGetValue(entry.Id, installed) Then
                Dim installedVersion As Version = installed.Version
                Dim catalogVersion As Version = ParseVersion(entry.Version)
                If installedVersion IsNot Nothing AndAlso catalogVersion IsNot Nothing AndAlso catalogVersion > installedVersion Then
                    status = "Update available"
                Else
                    status = "Installed"
                End If
            End If

            Dim item As New ListViewItem(name)
            item.SubItems.Add(entry.Id)
            item.SubItems.Add(versionText)
            item.SubItems.Add(status)
            item.SubItems.Add(If(entry.Description, String.Empty))
            item.Tag = entry
            lstCatalog.Items.Add(item)
        Next

        lstCatalog.EndUpdate()
        UpdateCatalogButtons()
    End Sub

    Private Sub lstCatalog_SelectedIndexChanged(sender As Object, e As EventArgs)
        UpdateCatalogButtons()
    End Sub

    Private Sub UpdateCatalogButtons()
        btnInstallCatalog.Enabled = (lstCatalog.SelectedItems.Count > 0)
    End Sub

    Private Function GetSelectedCatalog() As PluginCatalogEntry
        If lstCatalog.SelectedItems.Count = 0 Then
            Return Nothing
        End If
        Return TryCast(lstCatalog.SelectedItems(0).Tag, PluginCatalogEntry)
    End Function

    Private Async Sub btnInstallCatalog_Click(sender As Object, e As EventArgs)
        Dim entry As PluginCatalogEntry = GetSelectedCatalog()
        If entry Is Nothing Then
            Return
        End If

        If String.IsNullOrWhiteSpace(entry.DownloadUrl) Then
            MessageBox.Show(Me, "Selected entry has no download URL.", "Plugin Manager", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        Await DownloadAndStageAsync(entry)
    End Sub

    Private Async Function DownloadAndStageAsync(entry As PluginCatalogEntry) As Task
        lblDownloadStatus.Text = "Downloading..."
        prgDownload.Value = 0
        prgDownload.Style = ProgressBarStyle.Continuous
        btnInstallCatalog.Enabled = False

        Dim targetPath As String = Nothing
        Try
            Dim downloadUrl As String = entry.DownloadUrl
            Dim fileName As String = Path.GetFileName(New Uri(downloadUrl).AbsolutePath)
            If String.IsNullOrWhiteSpace(fileName) Then
                fileName = entry.Id & ".zip"
            End If

            targetPath = PluginInstallManager.GetPendingFilePath(fileName)

            Using client As New HttpClient()
                Using response As HttpResponseMessage = Await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead)
                    response.EnsureSuccessStatusCode()
                    Dim total As Long = If(response.Content.Headers.ContentLength.HasValue, response.Content.Headers.ContentLength.Value, -1)
                    Using inputStream As Stream = Await response.Content.ReadAsStreamAsync()
                        Using outputStream As Stream = File.Create(targetPath)
                            Dim buffer(8191) As Byte
                            Dim totalRead As Long = 0
                            Dim stopwatch As Stopwatch = Stopwatch.StartNew()
                            Dim read As Integer = Await inputStream.ReadAsync(buffer, 0, buffer.Length)
                            While read > 0
                                Await outputStream.WriteAsync(buffer, 0, read)
                                totalRead += read

                                UpdateDownloadProgress(totalRead, total, stopwatch)
                                read = Await inputStream.ReadAsync(buffer, 0, buffer.Length)
                            End While
                        End Using
                    End Using
                End Using
            End Using

            If Not ValidateSha256(entry, targetPath) Then
                Try
                    File.Delete(targetPath)
                Catch
                End Try
                lblDownloadStatus.Text = "Download failed integrity check."
                Return
            End If

            _requiresRestart = True
            _hasChanges = True
            lblDownloadStatus.Text = "Download complete. Restart required."
            MessageBox.Show(Me, "Plugin staged for install. Restart ClawHammer to load it.", "Plugin Manager", MessageBoxButtons.OK, MessageBoxIcon.Information)
        Catch ex As Exception
            lblDownloadStatus.Text = "Download failed."
            MessageBox.Show(Me, "Download failed: " & ex.Message, "Plugin Manager", MessageBoxButtons.OK, MessageBoxIcon.Error)
            If Not String.IsNullOrWhiteSpace(targetPath) Then
                Try
                    File.Delete(targetPath)
                Catch
                End Try
            End If
        Finally
            prgDownload.Style = ProgressBarStyle.Continuous
            If prgDownload.Value < 0 Then
                prgDownload.Value = 0
            End If
            btnInstallCatalog.Enabled = True
        End Try
    End Function

    Private Sub UpdateDownloadProgress(totalRead As Long, total As Long, stopwatch As Stopwatch)
        If total > 0 Then
            Dim percent As Integer = CInt(Math.Min(100, (totalRead * 100L) \ total))
            prgDownload.Value = Math.Max(0, Math.Min(100, percent))
            lblDownloadStatus.Text = $"{FormatBytes(totalRead)} / {FormatBytes(total)}"
        Else
            prgDownload.Style = ProgressBarStyle.Marquee
            lblDownloadStatus.Text = FormatBytes(totalRead)
        End If

        If stopwatch IsNot Nothing AndAlso stopwatch.Elapsed.TotalSeconds > 0.1 Then
            Dim speed As Double = totalRead / stopwatch.Elapsed.TotalSeconds
            lblDownloadStatus.Text &= $" ({FormatBytes(CLng(speed))}/s)"
        End If
    End Sub

    Private Function ValidateSha256(entry As PluginCatalogEntry, filePath As String) As Boolean
        If entry Is Nothing OrElse String.IsNullOrWhiteSpace(entry.Sha256) Then
            Return True
        End If

        Try
            Using sha As SHA256 = SHA256.Create()
                Using stream As FileStream = File.OpenRead(filePath)
                    Dim hash As Byte() = sha.ComputeHash(stream)
                    Dim actual As String = BitConverter.ToString(hash).Replace("-", String.Empty).ToLowerInvariant()
                    Dim expected As String = entry.Sha256.Trim().ToLowerInvariant()
                    Return String.Equals(actual, expected, StringComparison.OrdinalIgnoreCase)
                End Using
            End Using
        Catch
            Return False
        End Try
    End Function

    Private Shared Function ParseVersion(value As String) As Version
        Dim parsed As Version = Nothing
        If String.IsNullOrWhiteSpace(value) Then
            Return Nothing
        End If
        If Version.TryParse(value, parsed) Then
            Return parsed
        End If
        Return Nothing
    End Function

    Private Shared Function FormatBytes(value As Long) As String
        Dim absValue As Double = Math.Abs(CDbl(value))
        Dim suffix As String = "B"
        Dim size As Double = absValue

        If absValue >= 1024 Then
            size = absValue / 1024.0
            suffix = "KB"
        End If
        If absValue >= 1024 * 1024 Then
            size = absValue / (1024.0 * 1024.0)
            suffix = "MB"
        End If
        If absValue >= 1024 * 1024 * 1024 Then
            size = absValue / (1024.0 * 1024.0 * 1024.0)
            suffix = "GB"
        End If

        Return size.ToString("F1") & " " & suffix
    End Function

    Private Sub InitializeComponent()

    End Sub

    Private Sub LogMessage(message As String)
        lblInstalledStatus.Text = message
    End Sub
End Class












