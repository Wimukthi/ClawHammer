Imports LibreHardwareMonitor
Imports LibreHardwareMonitor.Hardware
Imports System.Timers
Imports System.Numerics
Imports System.Reflection
Imports System.Threading
Imports System.Diagnostics
Imports System.Globalization
Imports System.Drawing
Imports System.IO
Imports System.Net.Http
Imports System.Text.Json
Imports System.Threading.Tasks
Imports System.Linq
' ----------------------------------------------------------------------------------------
' Author:                    Wimukthi Bandara
' Company:                   Grey Element Software
' Assembly version:          1.2.0.160
' ----------------------------------------------------------------------------------------
' This program is free software: you can redistribute it and/or modify
' it under the terms of the GNU General Public License as published by
' the Free Software Foundation, either version 3 of the License, or
' any later version.
' This program is distributed in the hope that it will be useful,
' but WITHOUT ANY WARRANTY; without even the implied warranty of
' MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
' GNU General Public License for more details.
' You should have received a copy of the GNU General Public License
' along with this program.  If not, see http://www.gnu.org/licenses/.
' ----------------------------------------------------------------------------------------



Public Class frmMain

    Public Sub New()
        ' Initialize UI components but keep startup alive if designer metadata is corrupt.
        Try
            InitializeComponent()
        Catch ex As Exception
            System.Diagnostics.Debug.WriteLine("!!! EXCEPTION DURING InitializeComponent !!!")
            System.Diagnostics.Debug.WriteLine($"Message: {ex.Message}")
            System.Diagnostics.Debug.WriteLine($"StackTrace: {ex.StackTrace}")
        End Try
        UiThemeManager.ApplyTheme(Me)
    End Sub

    Private cts As Threading.CancellationTokenSource
    Private Const MinValuePrime As Long = 2
    Private Const MaxValuePrime As Long = 25000000
    Public ThreadsArray As List(Of Threading.Thread) = New List(Of Threading.Thread)
    Private _stressTester As New StressTester()
    Private perfCPU As New _
        System.Diagnostics.PerformanceCounter(
            "Processor", "% Processor Time", "_Total")
    Private _cpuDataTimer As System.Timers.Timer
    Private _cpuUsageTimer As System.Timers.Timer
    Private _computer As Computer
    Private _cpuDataPollInProgress As Integer = 0
    Private _cpuUiUpdatePending As Integer = 0
    Private _cpuUsagePollInProgress As Integer = 0
    Private _cpuUsageUiUpdatePending As Integer = 0
    Public logs As New ClawLog
    Public coreCount As Integer = 0

    ' Throughput tracking.
    Private _operationsCompleted As Long = 0
    Private _stopwatch As New System.Diagnostics.Stopwatch()
    Private _throughputTimer As New System.Windows.Forms.Timer()
    Private _runOptions As New RunOptions()
    Private _telemetryTimer As System.Timers.Timer
    Private _telemetryWriter As StreamWriter
    Private _telemetryFilePath As String
    Private _runStartUtc As DateTime = DateTime.UtcNow
    Private _telemetryLock As New Object()
    Private _telemetryPollInProgress As Integer = 0
    Private _timedRunTimer As System.Windows.Forms.Timer
    Private _timedRunRemainingSeconds As Integer = 0
    Private _lastAvgTemp As Single = Single.NaN
    Private _lastMaxTemp As Single = Single.NaN
    Private _lastCpuUsage As Integer = 0
    Private _lastThroughput As Double = 0
    Private _stopInProgress As Integer = 0
    Private _validationCts As Threading.CancellationTokenSource
    Private _validationThread As Threading.Thread
    Private Const CpuPollIntervalMs As Integer = 200
    Private Const UiSnappyCpuPollIntervalMs As Integer = 750
    Private _tempPlotForm As TempPlotForm
    Private _plotUpdatePending As Integer = 0
    Private _sensorImageList As ImageList
    Private Shared ReadOnly CelsiusSuffix As String = ChrW(&HB0) & "C"
    Private _uiLayout As UiLayoutStore
    Private _uiLayoutApplied As Boolean = False
    Private _pendingMainSplitterDistance As Integer = -1
    Private Const SensorIconFolderName As String = "icons"
    Private Const IconCpu As String = "cpu"
    Private Const IconGpu As String = "gpu"
    Private Const IconMotherboard As String = "motherboard"
    Private Const IconMemory As String = "memory"
    Private Const IconStorage As String = "storage"
    Private Const IconController As String = "controller"
    Private Const IconDefault As String = "temp"
    Private Const DefaultProfileName As String = "Default"
    Private Shared ReadOnly BuiltInProfileOrder As String() = {
        DefaultProfileName,
        "OC Quick Thermal",
        "OC Sustained Heat",
        "OC AVX Torture",
        "OC Mixed Stress",
        "OC Integer Stability"
    }
    Private _profiles As Dictionary(Of String, ProfileData) = New Dictionary(Of String, ProfileData)(StringComparer.OrdinalIgnoreCase)
    Private _profileStorePath As String
    Private _currentProfileName As String = DefaultProfileName
    Private _loadingProfile As Boolean = False
    Private _profilesInitialized As Boolean = False
    Private _plotTimeWindowSeconds As Single = 120.0F
    Private _cpuVendor As CpuVendor = CpuVendor.Unknown
    Private _cpuVendorResolved As Boolean = False


    Public Shared Sub SetDoubleBuffered(ByVal control As Control)
        GetType(Control).InvokeMember("DoubleBuffered", BindingFlags.SetProperty Or BindingFlags.Instance Or BindingFlags.NonPublic, Nothing, control, New Object() {True})
    End Sub

    Private Shared Function FormatCelsius(value As Single) As String
        Return value.ToString("F1") & CelsiusSuffix
    End Function

    Private Structure SensorReading
        Public ReadOnly Label As String
        Public ReadOnly ValueText As String
        Public ReadOnly IconKey As String

        Public Sub New(label As String, valueText As String, iconKey As String)
            Me.Label = label
            Me.ValueText = valueText
            Me.IconKey = iconKey
        End Sub
    End Structure

    Private Enum CpuVendor
        Unknown
        Intel
        Amd
    End Enum

    Private Enum CpuTempKind
        Unknown
        Excluded
        Package
        CoreAverage
        Core
        Tdie
        Tctl
        Ccd
        DieAverage
        Socket
        Other
    End Enum

    Private Structure CpuTempReading
        Public ReadOnly Name As String
        Public ReadOnly Value As Single
        Public ReadOnly Kind As CpuTempKind

        Public Sub New(name As String, value As Single, kind As CpuTempKind)
            Me.Name = name
            Me.Value = value
            Me.Kind = kind
        End Sub
    End Structure

    Private Sub InitializeHardwareMonitor()
        If _computer IsNot Nothing Then
            Return
        End If

        Try
            _computer = New Computer() With {
                .IsCpuEnabled = True,
                .IsGpuEnabled = True,
                .IsMotherboardEnabled = True,
                .IsStorageEnabled = True,
                .IsMemoryEnabled = True,
                .IsControllerEnabled = True
            }
            _computer.Open()
        Catch ex As Exception
            If _computer IsNot Nothing Then
                Try
                    _computer.Close()
                Catch
                End Try
                _computer = Nothing
            End If
            Dim el As New ErrorLogger
            el.WriteToErrorLog(ex.Message, ex.StackTrace, My.Application.Info.AssemblyName.ToString & " Encountered an Error")
        End Try
    End Sub

    Private Sub InitializeSensorIcons()
        If lstvCoreTemps Is Nothing Then
            Return
        End If

        Dim iconFolder As String = Path.Combine(AppContext.BaseDirectory, SensorIconFolderName)
        If Not Directory.Exists(iconFolder) Then
            Return
        End If

        Dim imageList As New ImageList() With {
            .ColorDepth = ColorDepth.Depth32Bit,
            .ImageSize = New Size(16, 16)
        }

        Dim iconFiles As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {
            {IconCpu, "cpu.png"},
            {IconGpu, "gpu.png"},
            {IconMotherboard, "motherboard.png"},
            {IconMemory, "memory.png"},
            {IconStorage, "storage.png"},
            {IconController, "controller.png"},
            {IconDefault, "temp.png"}
        }

        For Each entry In iconFiles
            Dim iconPath As String = Path.Combine(iconFolder, entry.Value)
            If Not File.Exists(iconPath) Then
                Continue For
            End If

            Try
                Dim bytes As Byte() = File.ReadAllBytes(iconPath)
                Using ms As New MemoryStream(bytes)
                    Using temp As Image = Image.FromStream(ms)
                        Dim bmp As New Bitmap(temp)
                        imageList.Images.Add(entry.Key, bmp)
                    End Using
                End Using
            Catch
            End Try
        Next

        If imageList.Images.Count = 0 Then
            Return
        End If

        _sensorImageList = imageList
        lstvCoreTemps.SmallImageList = _sensorImageList
        UiThemeManager.ApplyTheme(lstvCoreTemps)
    End Sub

    Private Function GetSensorIconKey(hardwareType As HardwareType, isCpuBranch As Boolean) As String
        If isCpuBranch Then
            Return IconCpu
        End If

        Select Case hardwareType
            Case HardwareType.Cpu
                Return IconCpu
            Case HardwareType.GpuAmd, HardwareType.GpuIntel, HardwareType.GpuNvidia
                Return IconGpu
            Case HardwareType.Motherboard
                Return IconMotherboard
            Case HardwareType.Memory
                Return IconMemory
            Case HardwareType.Storage
                Return IconStorage
            Case HardwareType.EmbeddedController, HardwareType.SuperIO
                Return IconController
            Case Else
                Return IconDefault
        End Select
    End Function

    Private Sub ApplyIconToItem(item As ListViewItem, iconKey As String)
        Dim list As ImageList = lstvCoreTemps.SmallImageList
        If list Is Nothing Then
            item.ImageIndex = -1
            Return
        End If

        Dim keyToUse As String = iconKey
        If Not list.Images.ContainsKey(keyToUse) Then
            keyToUse = IconDefault
        End If

        If list.Images.ContainsKey(keyToUse) Then
            item.ImageKey = keyToUse
        Else
            item.ImageIndex = -1
        End If
    End Sub

    Private Sub LoadUiLayout()
        _uiLayout = UiLayoutManager.LoadLayout()
        If _uiLayout Is Nothing Then
            _uiLayout = New UiLayoutStore()
        End If

        ApplyWindowLayoutIfAvailable()
        ApplyMainListLayout(_uiLayout.MainWindow)
    End Sub

    Private Sub ApplyWindowLayoutIfAvailable()
        If _uiLayout Is Nothing OrElse _uiLayout.MainWindow Is Nothing Then
            Return
        End If

        If _uiLayoutApplied Then
            Return
        End If

        If _uiLayout.MainWindow.Width <= 0 OrElse _uiLayout.MainWindow.Height <= 0 Then
            Return
        End If

        UiLayoutManager.ApplyWindowLayout(Me, _uiLayout.MainWindow)
        _uiLayoutApplied = True
    End Sub

    Private Sub ApplyMainListLayout(layout As UiWindowLayout)
        If layout Is Nothing Then
            Return
        End If

        If lstvCoreTemps IsNot Nothing AndAlso layout.ColumnWidths IsNot Nothing AndAlso layout.ColumnWidths.Count > 0 Then
            Dim count As Integer = Math.Min(layout.ColumnWidths.Count, lstvCoreTemps.Columns.Count)
            For i As Integer = 0 To count - 1
                Dim width As Integer = layout.ColumnWidths(i)
                If width > 30 Then
                    lstvCoreTemps.Columns(i).Width = width
                End If
            Next
        End If

        _pendingMainSplitterDistance = layout.SplitterDistance
    End Sub

    Private Sub ApplyPendingMainSplitter()
        If _pendingMainSplitterDistance <= 0 Then
            Return
        End If

        If SplitContainer1 Is Nothing OrElse Not IsHandleCreated Then
            Return
        End If

        Dim distance As Integer = _pendingMainSplitterDistance
        _pendingMainSplitterDistance = -1
        BeginInvoke(Sub() UiLayoutManager.ApplySplitterDistanceSafe(SplitContainer1, distance))
    End Sub

    Private Sub SaveUiLayout()
        If _uiLayout Is Nothing Then
            _uiLayout = New UiLayoutStore()
        End If

        UiLayoutManager.CaptureWindowLayout(Me, _uiLayout.MainWindow)

        If SplitContainer1 IsNot Nothing Then
            _uiLayout.MainWindow.SplitterDistance = SplitContainer1.SplitterDistance
        End If

        _uiLayout.MainWindow.ColumnWidths = New List(Of Integer)()
        If lstvCoreTemps IsNot Nothing Then
            For Each column As ColumnHeader In lstvCoreTemps.Columns
                _uiLayout.MainWindow.ColumnWidths.Add(column.Width)
            Next
        End If

        If _tempPlotForm IsNot Nothing AndAlso Not _tempPlotForm.IsDisposed Then
            _tempPlotForm.CaptureLayout(_uiLayout.TempPlotWindow)
        End If

        UiLayoutManager.SaveLayout(_uiLayout)
    End Sub

    Private Sub SaveTempPlotLayout()
        If _tempPlotForm Is Nothing OrElse _tempPlotForm.IsDisposed Then
            Return
        End If

        If _uiLayout Is Nothing Then
            _uiLayout = UiLayoutManager.LoadLayout()
        End If

        _tempPlotForm.CaptureLayout(_uiLayout.TempPlotWindow)
        UiLayoutManager.SaveLayout(_uiLayout)
    End Sub


    Private Sub QueueCoreTempUiUpdate(readings As List(Of SensorReading), avgText As String)
        If Me.IsDisposed OrElse Not Me.IsHandleCreated Then
            Return
        End If

        If Interlocked.Exchange(_cpuUiUpdatePending, 1) = 1 Then
            Return
        End If

        Dim updateAction As Action = Sub()
                                         Try
                                             UpdateCoreTempUi(readings, avgText)
                                         Finally
                                             Interlocked.Exchange(_cpuUiUpdatePending, 0)
                                         End Try
                                     End Sub

        Try
            If Me.InvokeRequired Then
                Me.BeginInvoke(updateAction)
            Else
                updateAction()
            End If
        Catch
            Interlocked.Exchange(_cpuUiUpdatePending, 0)
        End Try
    End Sub

    Private Sub QueuePlotUpdate(samples As List(Of TempSensorSample), snapshotUtc As DateTime)
        If samples Is Nothing Then
            Return
        End If

        Dim plotForm As TempPlotForm = _tempPlotForm
        If plotForm Is Nothing OrElse plotForm.IsDisposed Then
            Return
        End If

        If Me.IsDisposed OrElse Not Me.IsHandleCreated Then
            Return
        End If

        If Interlocked.Exchange(_plotUpdatePending, 1) = 1 Then
            Return
        End If

        Dim updateAction As Action = Sub()
                                         Try
                                             If plotForm.IsDisposed OrElse Not plotForm.Visible OrElse plotForm.WindowState = FormWindowState.Minimized Then
                                                 Return
                                             End If
                                             plotForm.UpdateSnapshot(samples, snapshotUtc)
                                         Finally
                                             Interlocked.Exchange(_plotUpdatePending, 0)
                                         End Try
                                     End Sub

        Try
            If Me.InvokeRequired Then
                Me.BeginInvoke(updateAction)
            Else
                updateAction()
            End If
        Catch
            Interlocked.Exchange(_plotUpdatePending, 0)
        End Try
    End Sub

    Private Sub UpdateCoreTempUi(readings As List(Of SensorReading), avgText As String)
        If lstvCoreTemps Is Nothing Then
            Return
        End If

        lstvCoreTemps.BeginUpdate()
        Try
            If lstvCoreTemps.Items.Count <> readings.Count Then
                lstvCoreTemps.Items.Clear()
                For Each reading In readings
                    Dim item As New ListViewItem(reading.Label)
                    item.SubItems.Add(reading.ValueText)
                    ApplyIconToItem(item, reading.IconKey)
                    lstvCoreTemps.Items.Add(item)
                Next
            Else
                For i As Integer = 0 To readings.Count - 1
                    Dim item As ListViewItem = lstvCoreTemps.Items(i)
                    item.Text = readings(i).Label
                    If item.SubItems.Count > 1 Then
                        item.SubItems(1).Text = readings(i).ValueText
                    Else
                        item.SubItems.Add(readings(i).ValueText)
                    End If
                    ApplyIconToItem(item, readings(i).IconKey)
                Next
            End If
        Finally
            lstvCoreTemps.EndUpdate()
        End Try

        cputemp.Text = avgText
    End Sub

    Private Sub QueueCpuUsageUiUpdate(cpuUsageValue As Integer)
        If Me.IsDisposed OrElse Not Me.IsHandleCreated Then
            Return
        End If

        If Interlocked.Exchange(_cpuUsageUiUpdatePending, 1) = 1 Then
            Return
        End If

        Dim updateAction As Action = Sub()
                                         Try
                                             Dim clampedValue As Integer = Math.Max(0, Math.Min(100, cpuUsageValue))
                                             progCPUUsage.Value = clampedValue
                                             lblusage.Text = "CPU Usage: " & clampedValue.ToString() & "%"
                                         Finally
                                             Interlocked.Exchange(_cpuUsageUiUpdatePending, 0)
                                         End Try
                                     End Sub

        Try
            If Me.InvokeRequired Then
                Me.BeginInvoke(updateAction)
            Else
                updateAction()
            End If
        Catch
            Interlocked.Exchange(_cpuUsageUiUpdatePending, 0)
        End Try
    End Sub

    ' CPU temperature polling callback.
    Sub SubCPUDatTimer(ByVal sender As Object, ByVal e As ElapsedEventArgs)
        If Interlocked.Exchange(_cpuDataPollInProgress, 1) = 1 Then
            Return
        End If

        Try
            If _computer Is Nothing Then
                InitializeHardwareMonitor()
            End If

            Dim readings As New List(Of SensorReading)()
            Dim samples As List(Of TempSensorSample) = Nothing
            Dim avgText As String = "CPU Temp: N/A"
            Dim autoStopThreshold As Single = _runOptions.AutoStopTempC
            Dim snapshotUtc As DateTime = DateTime.UtcNow
            Dim shouldCheckThrottle As Boolean = _runOptions.AutoStopOnThrottle AndAlso btnStart.Text = "Stop"
            Dim throttleIndicators As List(Of String) = If(shouldCheckThrottle, New List(Of String)(), Nothing)

            If _tempPlotForm IsNot Nothing AndAlso Not _tempPlotForm.IsDisposed Then
                samples = New List(Of TempSensorSample)()
            End If

            If _computer IsNot Nothing Then
                Dim cpuTemps As New List(Of CpuTempReading)()
                CollectTemperatureReadings(readings, samples, cpuTemps, throttleIndicators)

                Dim avgTemp As Single = Single.NaN
                Dim maxTemp As Single = Single.NaN
                Dim avgLabel As String = String.Empty
                If ComputeCpuTempSummary(cpuTemps, avgTemp, maxTemp, avgLabel) Then
                    avgText = avgLabel & ": " & FormatCelsius(avgTemp)
                    _lastAvgTemp = avgTemp
                    _lastMaxTemp = maxTemp
                Else
                    avgText = "CPU Temp: N/A"
                    _lastAvgTemp = Single.NaN
                    _lastMaxTemp = Single.NaN
                End If
            Else
                _lastAvgTemp = Single.NaN
                _lastMaxTemp = Single.NaN
            End If

            If readings.Count = 0 Then
                readings.Add(New SensorReading("Temperatures", "N/A", IconDefault))
            Else
                readings.Sort(Function(a, b) StringComparer.OrdinalIgnoreCase.Compare(a.Label, b.Label))
            End If

            QueueCoreTempUiUpdate(readings, avgText)
            If samples IsNot Nothing Then
                QueuePlotUpdate(samples, snapshotUtc)
            End If
            If shouldCheckThrottle AndAlso throttleIndicators IsNot Nothing AndAlso throttleIndicators.Count > 0 Then
                Dim distinctIndicators As List(Of String) = throttleIndicators.Distinct(StringComparer.OrdinalIgnoreCase).ToList()
                Dim detailText As String = String.Join(", ", distinctIndicators)
                TriggerAutoStop($"Auto-stop triggered: CPU throttling detected ({detailText})")
            ElseIf btnStart.Text = "Stop" AndAlso autoStopThreshold > 0 AndAlso Not Single.IsNaN(_lastMaxTemp) AndAlso _lastMaxTemp >= autoStopThreshold Then
                TriggerAutoStop($"Auto-stop triggered at {_lastMaxTemp:F1} C")
            End If
        Catch ex As Exception
            Dim el As New ErrorLogger
            el.WriteToErrorLog(ex.Message, ex.StackTrace, My.Application.Info.AssemblyName.ToString & " Encountered an Error")
        Finally
            Interlocked.Exchange(_cpuDataPollInProgress, 0)
        End Try
    End Sub

    Sub SubCPUUsage(ByVal sender As Object, ByVal e As ElapsedEventArgs)
        If Interlocked.Exchange(_cpuUsagePollInProgress, 1) = 1 Then
            Return
        End If

        Try
            Dim cpuUsageValue As Integer
            Try
                cpuUsageValue = CInt(Fix(perfCPU.NextValue()))
            Catch
                cpuUsageValue = 0
            End Try

            _lastCpuUsage = cpuUsageValue
            QueueCpuUsageUiUpdate(cpuUsageValue)
        Catch ex As Exception
            Dim el As New ErrorLogger
            el.WriteToErrorLog(ex.Message, ex.StackTrace, My.Application.Info.AssemblyName.ToString & " Encountered an Error")
        Finally
            Interlocked.Exchange(_cpuUsagePollInProgress, 0)
        End Try
    End Sub
    Private Sub frmMain_FormClosing(sender As Object, e As FormClosingEventArgs) Handles Me.FormClosing
        If chkSaveLog.Checked Then
            logs.WriteLog(rhtxtlog.Text)
        End If
        SaveUiLayout()
        If _throughputTimer IsNot Nothing Then
            RemoveHandler _throughputTimer.Tick, AddressOf _throughputTimer_Tick
            _throughputTimer.Stop()
            _throughputTimer.Dispose()
            _throughputTimer = Nothing
        End If

        StopTimedRun()
        StopTelemetry()
        StopValidation()

        If _tempPlotForm IsNot Nothing AndAlso Not _tempPlotForm.IsDisposed Then
            _tempPlotForm.Close()
            _tempPlotForm = Nothing
        End If

        If _cpuDataTimer IsNot Nothing Then
            RemoveHandler _cpuDataTimer.Elapsed, AddressOf SubCPUDatTimer
            _cpuDataTimer.Stop()
            _cpuDataTimer.Dispose()
            _cpuDataTimer = Nothing
        End If

        If _cpuUsageTimer IsNot Nothing Then
            RemoveHandler _cpuUsageTimer.Elapsed, AddressOf SubCPUUsage
            _cpuUsageTimer.Stop()
            _cpuUsageTimer.Dispose()
            _cpuUsageTimer = Nothing
        End If

        If cts IsNot Nothing Then
            cts.Cancel()
            cts.Dispose()
            cts = Nothing
        End If

        If perfCPU IsNot Nothing Then
            perfCPU.Dispose()
        End If

        If _computer IsNot Nothing Then
            _computer.Close()
            _computer = Nothing
        End If

        If _profilesInitialized Then
            SaveProfileStore()
        End If
    End Sub

    Private Sub frmMain_Shown(sender As Object, e As EventArgs) Handles Me.Shown
        ApplyWindowLayoutIfAvailable()
        ApplyPendingMainSplitter()
    End Sub

    Private Async Sub frmMain_Load(sender As System.Object, e As System.EventArgs) Handles MyBase.Load

        Dim initForm As frmInit = Nothing
        My.Application.SaveMySettingsOnExit = False
        LoadUiLayout()
        Try
            initForm = New frmInit()
            initForm.Show(Me)
            initForm.SetStatus("Starting...")
            initForm.AddDetail("Launching ClawHammer.")
            Await Task.Yield()

            If IsAdmin() = False Then
                initForm.SetStatus("Checking privileges...")
                initForm.AddDetail("Admin privileges not detected. Prompting for elevation.")
                uacprompt.ShowDialog(Me)
            Else
                initForm.SetStatus("Checking privileges...")
                initForm.AddDetail("Admin privileges detected.")
            End If
            If lstvCoreTemps Is Nothing Then
                System.Diagnostics.Debug.WriteLine("!!! ERROR: lstvCoreTemps is Nothing immediately before SetDoubleBuffered call in frmMain_Load !!!")
            End If

            initForm.SetStatus("Preparing UI...")
            initForm.AddDetail("Enabling double buffering.")
            SetDoubleBuffered(lstvCoreTemps)
            initForm.AddDetail("Loading sensor icons.")
            InitializeSensorIcons()
            initForm.AddDetail("Restoring window layout.")
            ApplyPendingMainSplitter()

            initForm.SetStatus("Initializing sensors...")
            initForm.AddDetail("Opening LibreHardwareMonitor.")
            Dim detectedHardware As List(Of String) = Nothing
            Await Task.Run(Sub()
                               InitializeHardwareMonitor()
                               If _computer Is Nothing Then
                                   Return
                               End If
                               Dim names As New List(Of String)()
                               For Each hw As IHardware In _computer.Hardware
                                   names.Add($"{hw.HardwareType}: {hw.Name}")
                               Next
                               detectedHardware = names
                           End Sub)
            initForm.AddDetail("LibreHardwareMonitor ready.")
            If detectedHardware IsNot Nothing AndAlso detectedHardware.Count > 0 Then
                For Each line As String In detectedHardware
                    initForm.AddDetail("Detected " & line)
                Next
            End If

            initForm.SetStatus("Starting sensor timers...")
            _cpuDataTimer = New System.Timers.Timer(CpuPollIntervalMs)
            AddHandler _cpuDataTimer.Elapsed, New ElapsedEventHandler(AddressOf SubCPUDatTimer)
            _cpuDataTimer.Start()
            initForm.AddDetail("CPU temperature polling started.")

            _cpuUsageTimer = New System.Timers.Timer(CpuPollIntervalMs)
            AddHandler _cpuUsageTimer.Elapsed, New ElapsedEventHandler(AddressOf SubCPUUsage)
            _cpuUsageTimer.Start()
            initForm.AddDetail("CPU usage polling started.")

            ApplyUiSnappyMode()

            initForm.SetStatus("Gathering system info...")
            initForm.AddDetail("Querying physical core count.")
            coreCount = Await Task.Run(Function() QueryPhysicalCoreCount())

            Me.Text = "ClawHammer v" + My.Application.Info.Version.ToString + " - [Idle]"
            NumThreads.Maximum = Environment.ProcessorCount
            NumThreads.Value = Environment.ProcessorCount
            CmbThreadPriority.Text = "Normal"
            lblProcessorCount.Text = Environment.ProcessorCount.ToString + " Hardware Threads"
            lblcores.Text = coreCount & " Physical Cores"
            cmbStressType.Items.Clear()
            cmbStressType.DataSource = System.Enum.GetValues(GetType(StressTestType))
            cmbStressType.SelectedItem = StressTestType.FloatingPoint

            initForm.SetStatus("Loading profiles...")
            initForm.AddDetail("Loading saved profiles.")
            InitializeProfiles()
            initForm.AddDetail($"Active profile: {_currentProfileName}.")

            LogMessage("ClawHammer Startup Successful")
            initForm.AddDetail("Starting system info snapshot (background).")
            Threading.ThreadPool.QueueUserWorkItem(Sub(state) LogSystemInfo())
            lblThroughput.Text = "Throughput: N/A"
        Catch ex As Exception
            Dim el As New ErrorLogger
            LogMessage("ClawHammer Encountered Errors while starting up!")
            el.WriteToErrorLog(ex.Message, ex.StackTrace, My.Application.Info.AssemblyName.ToString & " Encountered an Error")
            If initForm IsNot Nothing Then
                initForm.AddDetail("Initialization failed: " & ex.Message)
            End If
        Finally
            If initForm IsNot Nothing Then
                initForm.SetStatus("Ready.")
                initForm.AddDetail("Initialization complete.")
                initForm.Close()
                initForm.Dispose()
            End If
        End Try

    End Sub

    Private Sub btnStart_Click(sender As System.Object, e As System.EventArgs) Handles btnStart.Click

        Try

            If btnStart.Text = "Start" Then
                StartStressTest()
            ElseIf btnStart.Text = "Stop" Then

                StopStressTest("Stopped by user.")

            End If

        Catch ex As Exception
            Dim el As New ErrorLogger
            el.WriteToErrorLog(ex.Message, ex.StackTrace, My.Application.Info.AssemblyName.ToString & " Encountered an Error")
        End Try

    End Sub

    ' Throughput UI update timer.
    Private Sub _throughputTimer_Tick(sender As Object, e As EventArgs)
        If _stopwatch IsNot Nothing AndAlso _stopwatch.IsRunning Then
            Dim elapsedSeconds As Double = _stopwatch.Elapsed.TotalSeconds
            Dim currentOperations As Long = Interlocked.Read(_operationsCompleted)
            Dim throughput As Double = If(elapsedSeconds > 0.01, currentOperations / elapsedSeconds, 0)

            lblThroughput.Text = $"Throughput: {throughput:F2} ops/sec"
            _lastThroughput = throughput
        End If
    End Sub

    ' Thread-safe log append.
    Private Sub LogMessage(message As String)
        Dim timestampedMessage As String = $"[{Date.Now:yyyy-MM-dd HH:mm:ss}] {message}{vbCrLf}"
        If rhtxtlog.InvokeRequired Then
            rhtxtlog.BeginInvoke(Sub() rhtxtlog.AppendText(timestampedMessage))
        Else
            rhtxtlog.AppendText(timestampedMessage)
        End If
    End Sub

    ' Thread-safe title bar update.
    Private Sub SetTitleBarText(status As String)
        Dim effectiveStatus As String = status
        If _timedRunRemainingSeconds > 0 AndAlso status.Contains("Running") Then
            Dim remaining As TimeSpan = TimeSpan.FromSeconds(_timedRunRemainingSeconds)
            effectiveStatus = $"{status} [{remaining:hh\:mm\:ss} left]"
        End If

        Dim extras As New List(Of String)()
        If _runOptions.UiSnappyMode Then extras.Add("UI Snappy")
        If _runOptions.TimedRunMinutes > 0 Then extras.Add("Timed")
        If _runOptions.AutoStopTempC > 0 Then extras.Add("Auto-stop")
        If _runOptions.AutoStopOnThrottle Then extras.Add("Throttle-stop")
        If _runOptions.UseAffinity AndAlso _runOptions.AffinityCores IsNot Nothing AndAlso _runOptions.AffinityCores.Count > 0 Then extras.Add("Affinity")
        If _runOptions.TelemetryEnabled Then extras.Add("CSV Log")
        If _runOptions.ValidationEnabled Then extras.Add("Validation")

        Dim extraText As String = If(extras.Count > 0, " [" & String.Join(", ", extras) & "]", String.Empty)
        Dim title As String = $"ClawHammer v{My.Application.Info.Version} - {effectiveStatus}{extraText}"
        If Me.InvokeRequired Then
            Me.BeginInvoke(Sub() Me.Text = title)
        Else
            Me.Text = title
        End If
    End Sub

    ' Thread-safe status bar update.
    Private Sub UpdateActiveThreadCount()
        Dim countText As String = $"{ThreadsArray.Count} Threads Active"
        Dim parent As Control = LblActiveThreads.GetCurrentParent()
        If parent IsNot Nothing AndAlso parent.InvokeRequired Then
            parent.BeginInvoke(Sub() LblActiveThreads.Text = countText)
        Else
            LblActiveThreads.Text = countText
        End If
    End Sub

    Private Sub CollectTemperatureReadings(readings As List(Of SensorReading), samples As List(Of TempSensorSample), cpuTemps As List(Of CpuTempReading), throttleIndicators As List(Of String))
        For Each hw As IHardware In _computer.Hardware
            AddHardwareTemperatureReadings(hw, readings, samples, cpuTemps, throttleIndicators, hw.HardwareType = HardwareType.Cpu, Nothing)
        Next
    End Sub

    Private Sub AddHardwareTemperatureReadings(hw As IHardware, readings As List(Of SensorReading), samples As List(Of TempSensorSample), cpuTemps As List(Of CpuTempReading), throttleIndicators As List(Of String), isCpuBranch As Boolean, parentLabel As String)
        hw.Update()
        Dim baseLabel As String = BuildHardwareLabel(hw, parentLabel)
        Dim iconKey As String = GetSensorIconKey(hw.HardwareType, isCpuBranch)

        For Each sensor As ISensor In hw.Sensors
            If isCpuBranch AndAlso throttleIndicators IsNot Nothing Then
                Dim throttleName As String = EvaluateThrottleSensor(sensor)
                If Not String.IsNullOrWhiteSpace(throttleName) Then
                    throttleIndicators.Add(throttleName)
                End If
            End If

            If sensor.SensorType <> SensorType.Temperature Then
                Continue For
            End If

            Dim label As String = $"{baseLabel} - {sensor.Name}"
            Dim hasValue As Boolean = sensor.Value.HasValue
            Dim valueText As String
            Dim sampleValue As Single = 0

            If hasValue Then
                sampleValue = sensor.Value.Value
                valueText = FormatCelsius(sampleValue)
                If isCpuBranch AndAlso cpuTemps IsNot Nothing Then
                    Dim kind As CpuTempKind = ClassifyCpuTempSensor(sensor.Name)
                    If kind <> CpuTempKind.Excluded Then
                        cpuTemps.Add(New CpuTempReading(sensor.Name, sampleValue, kind))
                    End If
                End If
            Else
                valueText = "N/A"
            End If

            readings.Add(New SensorReading(label, valueText, iconKey))
            If samples IsNot Nothing Then
                samples.Add(New TempSensorSample(label, sampleValue, hasValue))
            End If
        Next

        For Each subHardware As IHardware In hw.SubHardware
            AddHardwareTemperatureReadings(subHardware, readings, samples, cpuTemps, throttleIndicators, isCpuBranch OrElse hw.HardwareType = HardwareType.Cpu, baseLabel)
        Next
    End Sub

    Private Function BuildHardwareLabel(hw As IHardware, parentLabel As String) As String
        Dim label As String = $"{hw.HardwareType}: {hw.Name}"
        If String.IsNullOrWhiteSpace(parentLabel) Then
            Return label
        End If
        Return parentLabel & " - " & hw.Name
    End Function

    Private Function ComputeCpuTempSummary(cpuTemps As List(Of CpuTempReading), ByRef avgTemp As Single, ByRef maxTemp As Single, ByRef avgLabel As String) As Boolean
        avgTemp = Single.NaN
        maxTemp = Single.NaN
        avgLabel = "CPU Temp"
        If cpuTemps Is Nothing OrElse cpuTemps.Count = 0 Then
            Return False
        End If

        Dim coreAvg As New List(Of Single)()
        Dim package As New List(Of Single)()
        Dim cores As New List(Of Single)()
        Dim tdie As New List(Of Single)()
        Dim tctl As New List(Of Single)()
        Dim ccd As New List(Of Single)()
        Dim dieAvg As New List(Of Single)()
        Dim socket As New List(Of Single)()
        Dim other As New List(Of Single)()

        For Each reading As CpuTempReading In cpuTemps
            If Single.IsNaN(reading.Value) Then
                Continue For
            End If

            Select Case reading.Kind
                Case CpuTempKind.CoreAverage
                    coreAvg.Add(reading.Value)
                Case CpuTempKind.Package
                    package.Add(reading.Value)
                Case CpuTempKind.Core
                    cores.Add(reading.Value)
                Case CpuTempKind.Tdie
                    tdie.Add(reading.Value)
                Case CpuTempKind.Tctl
                    tctl.Add(reading.Value)
                Case CpuTempKind.Ccd
                    ccd.Add(reading.Value)
                Case CpuTempKind.DieAverage
                    dieAvg.Add(reading.Value)
                Case CpuTempKind.Socket
                    socket.Add(reading.Value)
                Case Else
                    other.Add(reading.Value)
            End Select
        Next

        Dim candidate As List(Of Single) = Nothing
        Dim vendor As CpuVendor = GetCpuVendor()

        Select Case vendor
            Case CpuVendor.Intel
                If coreAvg.Count > 0 Then
                    candidate = coreAvg
                    avgLabel = "CPU Avg"
                ElseIf package.Count > 0 Then
                    candidate = package
                    avgLabel = "CPU Pkg"
                ElseIf cores.Count > 0 Then
                    candidate = cores
                    avgLabel = "CPU Avg"
                ElseIf tdie.Count > 0 Then
                    candidate = tdie
                    avgLabel = "CPU Die"
                ElseIf tctl.Count > 0 Then
                    candidate = tctl
                    avgLabel = "CPU Tctl"
                ElseIf socket.Count > 0 Then
                    candidate = socket
                    avgLabel = "CPU Socket"
                ElseIf other.Count > 0 Then
                    candidate = other
                    avgLabel = "CPU Temp"
                End If
            Case CpuVendor.Amd
                If dieAvg.Count > 0 Then
                    candidate = dieAvg
                    avgLabel = "CPU Die"
                ElseIf ccd.Count > 0 Then
                    candidate = ccd
                    avgLabel = "CPU CCD Avg"
                ElseIf tdie.Count > 0 Then
                    candidate = tdie
                    avgLabel = "CPU Die"
                ElseIf tctl.Count > 0 Then
                    candidate = tctl
                    avgLabel = "CPU Tctl"
                ElseIf package.Count > 0 Then
                    candidate = package
                    avgLabel = "CPU Pkg"
                ElseIf cores.Count > 0 Then
                    candidate = cores
                    avgLabel = "CPU Avg"
                ElseIf socket.Count > 0 Then
                    candidate = socket
                    avgLabel = "CPU Socket"
                ElseIf other.Count > 0 Then
                    candidate = other
                    avgLabel = "CPU Temp"
                End If
            Case Else
                If coreAvg.Count > 0 Then
                    candidate = coreAvg
                    avgLabel = "CPU Avg"
                ElseIf package.Count > 0 Then
                    candidate = package
                    avgLabel = "CPU Pkg"
                ElseIf cores.Count > 0 Then
                    candidate = cores
                    avgLabel = "CPU Avg"
                ElseIf tdie.Count > 0 Then
                    candidate = tdie
                    avgLabel = "CPU Die"
                ElseIf tctl.Count > 0 Then
                    candidate = tctl
                    avgLabel = "CPU Tctl"
                ElseIf ccd.Count > 0 Then
                    candidate = ccd
                    avgLabel = "CPU CCD Avg"
                ElseIf socket.Count > 0 Then
                    candidate = socket
                    avgLabel = "CPU Socket"
                ElseIf other.Count > 0 Then
                    candidate = other
                    avgLabel = "CPU Temp"
                End If
        End Select

        If candidate Is Nothing OrElse candidate.Count = 0 Then
            Return False
        End If

        avgTemp = ComputeAverage(candidate)
        maxTemp = ComputeMax(candidate)
        Return True
    End Function

    Private Function ComputeAverage(values As List(Of Single)) As Single
        Dim total As Double = 0
        For Each value As Single In values
            total += value
        Next
        Return CSng(total / values.Count)
    End Function

    Private Function ComputeMax(values As List(Of Single)) As Single
        Dim maxValue As Single = Single.MinValue
        For Each value As Single In values
            If value > maxValue Then
                maxValue = value
            End If
        Next
        Return maxValue
    End Function

    Private Function ClassifyCpuTempSensor(sensorName As String) As CpuTempKind
        If String.IsNullOrWhiteSpace(sensorName) Then
            Return CpuTempKind.Unknown
        End If

        Dim nameLower As String = sensorName.Trim().ToLowerInvariant()
        If IsExcludedCpuTempSensor(nameLower) Then
            Return CpuTempKind.Excluded
        End If

        If nameLower.Contains("core average") OrElse nameLower.Contains("core avg") Then
            Return CpuTempKind.CoreAverage
        End If
        If nameLower.Contains("die average") Then
            Return CpuTempKind.DieAverage
        End If
        If nameLower.Contains("package") Then
            Return CpuTempKind.Package
        End If
        If nameLower.Contains("tctl") AndAlso nameLower.Contains("tdie") Then
            Return CpuTempKind.Tctl
        End If
        If nameLower.Contains("tctl") Then
            Return CpuTempKind.Tctl
        End If
        If nameLower.Contains("tdie") Then
            Return CpuTempKind.Tdie
        End If
        If nameLower.Contains("ccd") Then
            Return CpuTempKind.Ccd
        End If
        If nameLower.Contains("socket") Then
            Return CpuTempKind.Socket
        End If
        If IsCoreReadingName(nameLower) Then
            Return CpuTempKind.Core
        End If
        If nameLower = "cpu" OrElse nameLower.StartsWith("cpu ") Then
            Return CpuTempKind.Package
        End If

        Return CpuTempKind.Other
    End Function

    Private Function IsExcludedCpuTempSensor(nameLower As String) As Boolean
        If nameLower.Contains("distance to tjmax") Then
            Return True
        End If
        If nameLower.Contains("tjmax") AndAlso nameLower.Contains("distance") Then
            Return True
        End If
        If nameLower.Contains("delta") Then
            Return True
        End If
        If nameLower.Contains("hot spot") OrElse nameLower.Contains("hotspot") Then
            Return True
        End If
        If nameLower.Contains("limit") OrElse nameLower.Contains("critical") OrElse nameLower.Contains("throttle") Then
            Return True
        End If
        If nameLower.Contains("vrm") OrElse nameLower.Contains("mos") Then
            Return True
        End If
        If nameLower.Contains("soc") Then
            Return True
        End If
        If nameLower.Contains("i/o") Then
            Return True
        End If
        If nameLower.Contains("max") OrElse nameLower.Contains("min") Then
            If Not nameLower.Contains("core average") AndAlso Not nameLower.Contains("core avg") AndAlso Not nameLower.Contains("die average") Then
                Return True
            End If
        End If
        Return False
    End Function

    Private Function IsCoreReadingName(nameLower As String) As Boolean
        Dim marker As String = "core #"
        Dim index As Integer = nameLower.IndexOf(marker, StringComparison.Ordinal)
        If index >= 0 Then
            Dim digitIndex As Integer = index + marker.Length
            If digitIndex < nameLower.Length AndAlso Char.IsDigit(nameLower(digitIndex)) Then
                Return True
            End If
        End If

        marker = "core "
        index = nameLower.IndexOf(marker, StringComparison.Ordinal)
        If index >= 0 Then
            Dim digitIndex As Integer = index + marker.Length
            If digitIndex < nameLower.Length AndAlso Char.IsDigit(nameLower(digitIndex)) Then
                Return True
            End If
        End If

        Return False
    End Function

    Private Function EvaluateThrottleSensor(sensor As ISensor) As String
        If sensor Is Nothing Then
            Return Nothing
        End If

        Dim name As String = sensor.Name
        If String.IsNullOrWhiteSpace(name) Then
            Return Nothing
        End If

        Dim nameLower As String = name.Trim().ToLowerInvariant()
        If Not IsThrottleSensorName(nameLower) Then
            Return Nothing
        End If

        If Not sensor.Value.HasValue Then
            Return Nothing
        End If

        Dim value As Single = sensor.Value.Value
        If IsThrottleActive(sensor.SensorType, value, nameLower) Then
            Return name
        End If

        Return Nothing
    End Function

    Private Function IsThrottleSensorName(nameLower As String) As Boolean
        If nameLower.Contains("throttl") Then
            Return True
        End If
        If nameLower.Contains("prochot") Then
            Return True
        End If
        If nameLower.Contains("power limit exceeded") OrElse nameLower.Contains("current limit exceeded") OrElse nameLower.Contains("thermal limit exceeded") Then
            Return True
        End If
        If nameLower.Contains("limit exceeded") Then
            Return True
        End If
        If nameLower.Contains("power limit") OrElse nameLower.Contains("current limit") OrElse nameLower.Contains("thermal limit") Then
            Return True
        End If
        If nameLower.Contains("edp") Then
            Return True
        End If
        If nameLower.Contains("ppt") OrElse nameLower.Contains("tdc") OrElse nameLower.Contains("edc") Then
            Return True
        End If
        Return False
    End Function

    Private Function IsThrottleActive(sensorType As SensorType, value As Single, nameLower As String) As Boolean
        Select Case sensorType
            Case SensorType.Control
                Return value >= 1.0F
            Case SensorType.Level, SensorType.Load, SensorType.Factor
                Return value >= 99.0F
            Case Else
                If IsThrottleBooleanName(nameLower) Then
                    Return value >= 1.0F
                End If
        End Select
        Return False
    End Function

    Private Function IsThrottleBooleanName(nameLower As String) As Boolean
        Return nameLower.Contains("throttl") OrElse nameLower.Contains("exceed") OrElse nameLower.Contains("prochot")
    End Function

    Private Function GetCpuVendor() As CpuVendor
        If _cpuVendorResolved Then
            Return _cpuVendor
        End If

        Dim vendor As CpuVendor = CpuVendor.Unknown
        Try
            For Each item In New System.Management.ManagementObjectSearcher("Select Manufacturer, Name from Win32_Processor").[Get]()
                Dim manufacturer As String = item("Manufacturer")?.ToString()
                vendor = DetectCpuVendorFromName(manufacturer)
                If vendor <> CpuVendor.Unknown Then
                    Exit For
                End If

                Dim name As String = item("Name")?.ToString()
                vendor = DetectCpuVendorFromName(name)
                If vendor <> CpuVendor.Unknown Then
                    Exit For
                End If
            Next
        Catch
        End Try

        If vendor = CpuVendor.Unknown Then
            Try
                If _computer IsNot Nothing Then
                    For Each hw As IHardware In _computer.Hardware
                        If hw.HardwareType = HardwareType.Cpu Then
                            vendor = DetectCpuVendorFromName(hw.Name)
                            If vendor <> CpuVendor.Unknown Then
                                Exit For
                            End If
                        End If
                    Next
                End If
            Catch
            End Try
        End If

        _cpuVendor = vendor
        _cpuVendorResolved = True
        Return vendor
    End Function

    Private Function DetectCpuVendorFromName(name As String) As CpuVendor
        If String.IsNullOrWhiteSpace(name) Then
            Return CpuVendor.Unknown
        End If

        Dim lower As String = name.ToLowerInvariant()
        If lower.Contains("authenticamd") OrElse lower.Contains("amd") OrElse lower.Contains("ryzen") OrElse lower.Contains("threadripper") OrElse lower.Contains("epyc") OrElse lower.Contains("athlon") Then
            Return CpuVendor.Amd
        End If
        If lower.Contains("genuineintel") OrElse lower.Contains("intel") OrElse lower.Contains("xeon") OrElse lower.Contains("pentium") OrElse lower.Contains("celeron") OrElse lower.Contains("atom") Then
            Return CpuVendor.Intel
        End If

        Return CpuVendor.Unknown
    End Function

    Private Function QueryPhysicalCoreCount() As Integer
        Dim total As Integer = 0
        Try
            For Each item In New System.Management.ManagementObjectSearcher("Select NumberOfCores from Win32_Processor").[Get]()
                Dim value As String = item("NumberOfCores")?.ToString()
                Dim cores As Integer
                If Integer.TryParse(value, cores) Then
                    total += cores
                End If
            Next
        Catch
        End Try

        If total <= 0 Then
            total = Environment.ProcessorCount
        End If

        Return total
    End Function

    Private Sub ApplyUiSnappyMode()
        Dim interval As Integer = If(_runOptions.UiSnappyMode, UiSnappyCpuPollIntervalMs, CpuPollIntervalMs)
        If _cpuDataTimer IsNot Nothing Then
            _cpuDataTimer.Interval = interval
        End If
        If _cpuUsageTimer IsNot Nothing Then
            _cpuUsageTimer.Interval = interval
        End If
    End Sub

    Private Function GetEffectiveThreadCount() As Integer
        Dim count As Integer = Math.Max(1, CInt(NumThreads.Value))
        If _runOptions.UiSnappyMode AndAlso count > 1 Then
            count -= 1
        End If
        Return count
    End Function

    Private Function SelectMixedWorkload(threadIndex As Integer, avxAvailable As Boolean) As StressTestType
        If avxAvailable Then
            Select Case threadIndex Mod 3
                Case 0
                    Return StressTestType.FloatingPoint
                Case 1
                    Return StressTestType.IntegerPrimes
                Case Else
                    Return StressTestType.AVX
            End Select
        End If

        If threadIndex Mod 2 = 0 Then
            Return StressTestType.FloatingPoint
        End If
        Return StressTestType.IntegerPrimes
    End Function

    Private Sub StartStressTest()
        btnStart.Text = "Stop"
        btnStart.Image = My.Resources._stop

        SetTitleBarText("[Running]")

        ' Initialize throughput tracking.
        _operationsCompleted = 0
        lblThroughput.Text = "Throughput: N/A"
        _telemetryFilePath = Nothing
        _stopwatch = System.Diagnostics.Stopwatch.StartNew()
        _throughputTimer = New System.Windows.Forms.Timer()
        _throughputTimer.Interval = 1000
        AddHandler _throughputTimer.Tick, AddressOf _throughputTimer_Tick
        _throughputTimer.Start()

        Dim selectedTestType As StressTestType
        If cmbStressType.SelectedItem IsNot Nothing AndAlso [Enum].TryParse(cmbStressType.SelectedItem.ToString(), selectedTestType) Then
            LogMessage($"Starting {selectedTestType} Stress Test...")
        Else
            LogMessage("Error: No valid stress test type selected. Defaulting to FloatingPoint.")
            selectedTestType = StressTestType.FloatingPoint
            cmbStressType.SelectedItem = selectedTestType
        End If

        Dim avxAvailable As Boolean = Vector.IsHardwareAccelerated
        If selectedTestType = StressTestType.AVX AndAlso Not avxAvailable Then
            LogMessage("AVX selected but SIMD acceleration is not available. Falling back to FloatingPoint.")
            selectedTestType = StressTestType.FloatingPoint
            cmbStressType.SelectedItem = selectedTestType
        End If

        cts = New Threading.CancellationTokenSource()
        Dim token As Threading.CancellationToken = cts.Token
        ThreadsArray.Clear()

        Dim threadCount As Integer = GetEffectiveThreadCount()
        If _runOptions.UiSnappyMode AndAlso threadCount < NumThreads.Value Then
            LogMessage($"UI snappy mode enabled. Threads reduced to {threadCount}.")
        End If

        Dim affinityCores As New List(Of Integer)()
        Dim useAffinity As Boolean = _runOptions.UseAffinity AndAlso _runOptions.AffinityCores IsNot Nothing
        If useAffinity Then
            For Each coreIndex In _runOptions.AffinityCores.Distinct()
                If coreIndex >= 0 AndAlso coreIndex < Environment.ProcessorCount Then
                    affinityCores.Add(coreIndex)
                End If
            Next
            If affinityCores.Count = 0 Then
                useAffinity = False
                LogMessage("Core affinity enabled but no valid cores were selected. Ignoring affinity.")
            End If
        End If

        For i = 0 To threadCount - 1
            Dim reportProgressAction As Action(Of Integer) = Sub(ops) Interlocked.Add(_operationsCompleted, ops)
            Dim workloadType As StressTestType = selectedTestType
            If selectedTestType = StressTestType.Mixed Then
                workloadType = SelectMixedWorkload(i, avxAvailable)
            End If

            Dim coreIndex As Integer? = Nothing
            If useAffinity Then
                coreIndex = affinityCores(i Mod affinityCores.Count)
            End If

            Dim threadStart As Threading.ThreadStart = Sub()
                                                           If coreIndex.HasValue Then
                                                               If Not ThreadAffinity.TrySetCurrentThreadAffinity(coreIndex.Value) Then
                                                                   LogMessage($"Affinity set failed for core {coreIndex.Value}.")
                                                               End If
                                                           End If

                                                           Select Case workloadType
                                                               Case StressTestType.IntegerPrimes
                                                                   _stressTester.FindPrimesInRange(MinValuePrime, MaxValuePrime, token, reportProgressAction)
                                                               Case StressTestType.FloatingPoint
                                                                   _stressTester.PerformFpWorkload(token, reportProgressAction)
                                                               Case StressTestType.AVX
                                                                   _stressTester.PerformAvxWorkload(token, reportProgressAction)
                                                               Case Else
                                                                   _stressTester.PerformFpWorkload(token, reportProgressAction)
                                                           End Select
                                                       End Sub

            Dim t As Threading.Thread = New Threading.Thread(threadStart)
            ThreadsArray.Add(t)

            Select Case CmbThreadPriority.Text
                Case "Normal"
                    t.Priority = System.Threading.ThreadPriority.Normal
                Case "Above Normal"
                    t.Priority = System.Threading.ThreadPriority.AboveNormal
                Case "Below Normal"
                    t.Priority = System.Threading.ThreadPriority.BelowNormal
                Case "Lowest"
                    t.Priority = System.Threading.ThreadPriority.Lowest
                Case "Highest"
                    t.Priority = System.Threading.ThreadPriority.Highest
            End Select

            t.IsBackground = True
            t.Start()

            Dim affinityInfo As String = If(coreIndex.HasValue, $" Core {coreIndex.Value}", String.Empty)
            LogMessage($"Thread Created ({workloadType}){affinityInfo} [Thread ID] : {t.ManagedThreadId}")
        Next

        UpdateActiveThreadCount()
        StartTelemetry()
        StartTimedRun()
        StartValidation()
        If _runOptions.AutoShowTempPlot Then
            ShowTemperaturePlotWindow()
        End If
    End Sub

    Private Sub StopStressTest(Optional reason As String = Nothing)
        If Interlocked.Exchange(_stopInProgress, 1) = 1 Then
            Return
        End If

        Try
            If Not String.IsNullOrWhiteSpace(reason) Then
                LogMessage(reason)
            End If
            LogMessage("Sending cancellation signal to all threads...")

            StopTimedRun()
            StopTelemetry()
            StopValidation()

            If _throughputTimer IsNot Nothing Then
                RemoveHandler _throughputTimer.Tick, AddressOf _throughputTimer_Tick
                _throughputTimer.Stop()
                _throughputTimer.Dispose()
                _throughputTimer = Nothing
            End If
            If _stopwatch IsNot Nothing Then _stopwatch.Stop()
            lblThroughput.Text = "Throughput: N/A"

            If cts IsNot Nothing Then
                cts.Cancel()
                cts.Dispose()
                cts = Nothing
            End If

            If Not String.IsNullOrWhiteSpace(_telemetryFilePath) Then
                LogMessage($"Telemetry saved: {_telemetryFilePath}")
            End If

            SetTitleBarText("[Idle]")
            ThreadsArray.Clear()
            UpdateActiveThreadCount()

            btnStart.Text = "Start"
            btnStart.Image = My.Resources.arrow_right_3
        Finally
            Interlocked.Exchange(_stopInProgress, 0)
        End Try
    End Sub

    Private Sub StartTimedRun()
        If _runOptions.TimedRunMinutes <= 0 Then
            Return
        End If

        _timedRunRemainingSeconds = _runOptions.TimedRunMinutes * 60
        _timedRunTimer = New System.Windows.Forms.Timer()
        _timedRunTimer.Interval = 1000
        AddHandler _timedRunTimer.Tick, AddressOf TimedRunTimer_Tick
        _timedRunTimer.Start()
        SetTitleBarText("[Running]")
    End Sub

    Private Sub StopTimedRun()
        If _timedRunTimer IsNot Nothing Then
            RemoveHandler _timedRunTimer.Tick, AddressOf TimedRunTimer_Tick
            _timedRunTimer.Stop()
            _timedRunTimer.Dispose()
            _timedRunTimer = Nothing
        End If
        _timedRunRemainingSeconds = 0
    End Sub

    Private Sub TimedRunTimer_Tick(sender As Object, e As EventArgs)
        If _timedRunRemainingSeconds <= 0 Then
            StopStressTest("Timed run completed.")
            Return
        End If

        _timedRunRemainingSeconds -= 1
        If _timedRunRemainingSeconds < 0 Then
            _timedRunRemainingSeconds = 0
        End If
        SetTitleBarText("[Running]")
    End Sub

    Private Sub StartTelemetry()
        If Not _runOptions.TelemetryEnabled Then
            Return
        End If

        StopTelemetry()

        Dim logsPath As String = Path.Combine(Application.StartupPath, "Logs")
        Directory.CreateDirectory(logsPath)
        _telemetryFilePath = Path.Combine(logsPath, $"ClawHammer_Telemetry_{Date.Now:yyyyMMdd_HHmmss}.csv")
        _telemetryWriter = New StreamWriter(_telemetryFilePath, False, System.Text.Encoding.UTF8)
        _telemetryWriter.WriteLine("Timestamp,ElapsedSec,CpuUsage,AvgTempC,MaxTempC,ThroughputOpsPerSec")
        _telemetryWriter.Flush()
        _runStartUtc = DateTime.UtcNow

        _telemetryTimer = New System.Timers.Timer(Math.Max(250, _runOptions.TelemetryIntervalMs))
        AddHandler _telemetryTimer.Elapsed, AddressOf TelemetryTimerElapsed
        _telemetryTimer.Start()
    End Sub

    Private Sub StopTelemetry()
        If _telemetryTimer IsNot Nothing Then
            RemoveHandler _telemetryTimer.Elapsed, AddressOf TelemetryTimerElapsed
            _telemetryTimer.Stop()
            _telemetryTimer.Dispose()
            _telemetryTimer = Nothing
        End If

        SyncLock _telemetryLock
            If _telemetryWriter IsNot Nothing Then
                _telemetryWriter.Flush()
                _telemetryWriter.Dispose()
                _telemetryWriter = Nothing
            End If
        End SyncLock
    End Sub

    Private Sub TelemetryTimerElapsed(sender As Object, e As ElapsedEventArgs)
        If Interlocked.Exchange(_telemetryPollInProgress, 1) = 1 Then
            Return
        End If

        Try
            WriteTelemetrySample()
        Finally
            Interlocked.Exchange(_telemetryPollInProgress, 0)
        End Try
    End Sub

    Private Sub WriteTelemetrySample()
        Dim writer As StreamWriter = Nothing
        SyncLock _telemetryLock
            writer = _telemetryWriter
        End SyncLock

        If writer Is Nothing Then
            Return
        End If

        Dim avgTemp As Single = _lastAvgTemp
        Dim maxTemp As Single = _lastMaxTemp
        Dim cpuUsage As Integer = _lastCpuUsage
        Dim throughput As Double = _lastThroughput
        Dim elapsed As Double = (DateTime.UtcNow - _runStartUtc).TotalSeconds
        Dim timestamp As String = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)

        Dim avgText As String = If(Single.IsNaN(avgTemp), String.Empty, avgTemp.ToString("F1", CultureInfo.InvariantCulture))
        Dim maxText As String = If(Single.IsNaN(maxTemp), String.Empty, maxTemp.ToString("F1", CultureInfo.InvariantCulture))
        Dim line As String = String.Join(",", timestamp, elapsed.ToString("F1", CultureInfo.InvariantCulture), cpuUsage.ToString(CultureInfo.InvariantCulture), avgText, maxText, throughput.ToString("F2", CultureInfo.InvariantCulture))

        SyncLock _telemetryLock
            If _telemetryWriter IsNot Nothing Then
                _telemetryWriter.WriteLine(line)
            End If
        End SyncLock
    End Sub

    Private Sub StartValidation()
        If Not _runOptions.ValidationEnabled Then
            Return
        End If

        StopValidation()

        _validationCts = New Threading.CancellationTokenSource()
        Dim token As Threading.CancellationToken = _validationCts.Token
        Dim reportError As Action(Of String) = Sub(message)
                                                   LogMessage(message)
                                                   TriggerAutoStop(message)
                                               End Sub

        _validationThread = New Threading.Thread(Sub() _stressTester.RunValidationLoop(token, reportError))
        _validationThread.IsBackground = True
        _validationThread.Start()
    End Sub

    Private Sub StopValidation()
        If _validationCts IsNot Nothing Then
            _validationCts.Cancel()
            _validationCts.Dispose()
            _validationCts = Nothing
        End If
        _validationThread = Nothing
    End Sub

    Private Sub TriggerAutoStop(reason As String)
        If Me.IsDisposed OrElse Not Me.IsHandleCreated Then
            Return
        End If

        If Me.InvokeRequired Then
            Me.BeginInvoke(Sub() StopStressTest(reason))
        Else
            StopStressTest(reason)
        End If
    End Sub

    Private Function CloneRunOptions(source As RunOptions) As RunOptions
        Dim copy As New RunOptions() With {
            .TimedRunMinutes = source.TimedRunMinutes,
            .AutoStopTempC = source.AutoStopTempC,
            .AutoStopOnThrottle = source.AutoStopOnThrottle,
            .UiSnappyMode = source.UiSnappyMode,
            .TelemetryEnabled = source.TelemetryEnabled,
            .TelemetryIntervalMs = source.TelemetryIntervalMs,
            .ValidationEnabled = source.ValidationEnabled,
            .AutoShowTempPlot = source.AutoShowTempPlot,
            .UseAffinity = source.UseAffinity,
            .AffinityCores = If(source.AffinityCores IsNot Nothing, New List(Of Integer)(source.AffinityCores), New List(Of Integer)())
        }
        Return copy
    End Function

    Private Function BuildProfileFromUi() As ProfileData
        Dim stressName As String = StressTestType.FloatingPoint.ToString()
        If cmbStressType.SelectedItem IsNot Nothing Then
            stressName = cmbStressType.SelectedItem.ToString()
        End If

        Dim profile As New ProfileData() With {
            .Threads = CInt(NumThreads.Value),
            .StressType = stressName,
            .ThreadPriority = CmbThreadPriority.Text,
            .SaveLogOnExit = chkSaveLog.Checked,
            .RunOptions = CloneRunOptions(_runOptions),
            .PlotTimeWindowSeconds = _plotTimeWindowSeconds
        }
        Return profile
    End Function

    Private Sub ApplyProfile(profile As ProfileData)
        If profile Is Nothing Then
            Return
        End If

        _loadingProfile = True
        Try
            Dim threadValue As Decimal = NumThreads.Value
            If profile.Threads > 0 Then
                Dim clamped As Integer = Math.Max(CInt(NumThreads.Minimum), Math.Min(CInt(NumThreads.Maximum), profile.Threads))
                threadValue = clamped
            End If
            NumThreads.Value = threadValue

            Dim parsedType As StressTestType
            If Not String.IsNullOrWhiteSpace(profile.StressType) AndAlso [Enum].TryParse(profile.StressType, parsedType) Then
                cmbStressType.SelectedItem = parsedType
            End If

            If Not String.IsNullOrWhiteSpace(profile.ThreadPriority) Then
                CmbThreadPriority.Text = profile.ThreadPriority
            End If

            chkSaveLog.Checked = profile.SaveLogOnExit

            If profile.RunOptions IsNot Nothing Then
                _runOptions = profile.RunOptions
                If _runOptions.AffinityCores Is Nothing Then
                    _runOptions.AffinityCores = New List(Of Integer)()
                End If
            Else
                _runOptions = New RunOptions()
            End If

            If profile.PlotTimeWindowSeconds > 0 Then
                _plotTimeWindowSeconds = profile.PlotTimeWindowSeconds
                If _tempPlotForm IsNot Nothing AndAlso Not _tempPlotForm.IsDisposed Then
                    _tempPlotForm.TimeWindowSeconds = _plotTimeWindowSeconds
                End If
            End If

            ApplyUiSnappyMode()
            ApplyRunOptionsToRunningState()
        Finally
            _loadingProfile = False
        End Try
    End Sub

    Private Sub ApplyRunOptionsToRunningState()
        ApplyUiSnappyMode()
        SetTitleBarText(If(btnStart.Text = "Stop", "[Running]", "[Idle]"))

        If btnStart.Text = "Stop" Then
            If _runOptions.TelemetryEnabled AndAlso _telemetryTimer Is Nothing Then
                StartTelemetry()
            ElseIf Not _runOptions.TelemetryEnabled AndAlso _telemetryTimer IsNot Nothing Then
                StopTelemetry()
            End If

            If _runOptions.TimedRunMinutes > 0 AndAlso _timedRunTimer Is Nothing Then
                StartTimedRun()
            ElseIf _runOptions.TimedRunMinutes = 0 AndAlso _timedRunTimer IsNot Nothing Then
                StopTimedRun()
            End If

            If _runOptions.ValidationEnabled AndAlso _validationThread Is Nothing Then
                StartValidation()
            ElseIf Not _runOptions.ValidationEnabled AndAlso _validationThread IsNot Nothing Then
                StopValidation()
            End If

            If _runOptions.UseAffinity Then
                LogMessage("Core affinity changes apply on the next run.")
            End If
        End If
    End Sub

    Private Function CreateProfile(threads As Integer, stressType As StressTestType, priority As String, saveLog As Boolean, options As RunOptions, plotWindowSeconds As Single) As ProfileData
        Dim profile As New ProfileData() With {
            .Threads = Math.Max(1, threads),
            .StressType = stressType.ToString(),
            .ThreadPriority = priority,
            .SaveLogOnExit = saveLog,
            .RunOptions = options,
            .PlotTimeWindowSeconds = plotWindowSeconds
        }
        If profile.RunOptions Is Nothing Then
            profile.RunOptions = New RunOptions()
        End If
        If profile.RunOptions.AffinityCores Is Nothing Then
            profile.RunOptions.AffinityCores = New List(Of Integer)()
        End If
        Return profile
    End Function

    Private Function BuildBuiltInProfiles() As Dictionary(Of String, ProfileData)
        Dim profiles As New Dictionary(Of String, ProfileData)(StringComparer.OrdinalIgnoreCase)
        Dim threads As Integer = Environment.ProcessorCount

        profiles(DefaultProfileName) = CreateProfile(threads, StressTestType.FloatingPoint, "Normal", False, New RunOptions(), 120)

        profiles("OC Quick Thermal") = CreateProfile(threads, StressTestType.FloatingPoint, "Above Normal", False, New RunOptions() With {
            .TimedRunMinutes = 10,
            .AutoStopTempC = 90,
            .UiSnappyMode = True,
            .AutoShowTempPlot = True
        }, 120)

        profiles("OC Sustained Heat") = CreateProfile(threads, StressTestType.Mixed, "Normal", False, New RunOptions() With {
            .TimedRunMinutes = 60,
            .AutoStopTempC = 95,
            .UiSnappyMode = True,
            .TelemetryEnabled = True,
            .TelemetryIntervalMs = 1000,
            .ValidationEnabled = True,
            .AutoShowTempPlot = True
        }, 300)

        profiles("OC AVX Torture") = CreateProfile(threads, StressTestType.AVX, "Highest", False, New RunOptions() With {
            .TimedRunMinutes = 30,
            .AutoStopTempC = 90,
            .UiSnappyMode = True,
            .TelemetryEnabled = True,
            .TelemetryIntervalMs = 500,
            .AutoShowTempPlot = True
        }, 180)

        profiles("OC Mixed Stress") = CreateProfile(threads, StressTestType.Mixed, "Above Normal", False, New RunOptions() With {
            .TimedRunMinutes = 20,
            .AutoStopTempC = 92,
            .UiSnappyMode = True,
            .TelemetryEnabled = True,
            .TelemetryIntervalMs = 750,
            .AutoShowTempPlot = True
        }, 180)

        profiles("OC Integer Stability") = CreateProfile(threads, StressTestType.IntegerPrimes, "Above Normal", False, New RunOptions() With {
            .TimedRunMinutes = 120,
            .AutoStopTempC = 85,
            .UiSnappyMode = True,
            .TelemetryEnabled = True,
            .TelemetryIntervalMs = 1500,
            .AutoShowTempPlot = True
        }, 300)

        Return profiles
    End Function

    Private Function GetProfileStorePath() As String
        Return Path.Combine(AppContext.BaseDirectory, "profiles.json")
    End Function

    Private Function GetLegacyProfileStorePath() As String
        Dim root As String = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
        Dim dir As String = Path.Combine(root, "ClawHammer")
        Return Path.Combine(dir, "profiles.json")
    End Function

    Private Function LoadProfileStore(path As String) As ProfileStore
        Dim store As ProfileStore = LoadProfileStoreFromPath(path)
        If store IsNot Nothing Then
            Return store
        End If

        Dim legacyPath As String = GetLegacyProfileStorePath()
        Dim legacyStore As ProfileStore = LoadProfileStoreFromPath(legacyPath)
        If legacyStore IsNot Nothing Then
            SaveProfileStoreToPath(path, legacyStore)
            Return legacyStore
        End If

        Return New ProfileStore()
    End Function

    Private Function LoadProfileStoreFromPath(path As String) As ProfileStore
        If String.IsNullOrWhiteSpace(path) OrElse Not File.Exists(path) Then
            Return Nothing
        End If

        Try
            Dim json As String = File.ReadAllText(path)
            Dim options As New JsonSerializerOptions With {
                .PropertyNameCaseInsensitive = True
            }
            Dim loaded As ProfileStore = JsonSerializer.Deserialize(Of ProfileStore)(json, options)
            If loaded Is Nothing Then
                Return Nothing
            End If
            Dim store As New ProfileStore() With {
                .LastProfileName = loaded.LastProfileName
            }
            store.Profiles = New Dictionary(Of String, ProfileData)(StringComparer.OrdinalIgnoreCase)
            If loaded.Profiles IsNot Nothing Then
                For Each kvp In loaded.Profiles
                    store.Profiles(kvp.Key) = kvp.Value
                Next
            End If
            Return store
        Catch
            Return Nothing
        End Try
    End Function

    Private Sub SaveProfileStore()
        If String.IsNullOrWhiteSpace(_profileStorePath) Then
            _profileStorePath = GetProfileStorePath()
        End If

        Dim store As New ProfileStore() With {
            .LastProfileName = _currentProfileName,
            .Profiles = New Dictionary(Of String, ProfileData)(_profiles, StringComparer.OrdinalIgnoreCase)
        }
        SaveProfileStoreToPath(_profileStorePath, store)
    End Sub

    Private Sub SaveProfileStoreToPath(path As String, store As ProfileStore)
        If String.IsNullOrWhiteSpace(path) OrElse store Is Nothing Then
            Return
        End If

        Try
            Dim options As New JsonSerializerOptions With {
                .WriteIndented = True
            }
            Dim json As String = JsonSerializer.Serialize(store, options)
            File.WriteAllText(path, json)
        Catch
        End Try
    End Sub

    Private Sub PopulateProfileList()
        If cmbProfiles Is Nothing Then
            Return
        End If

        _loadingProfile = True
        Try
            cmbProfiles.Items.Clear()
            Dim added As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)

            For Each name As String In BuiltInProfileOrder
                If _profiles.ContainsKey(name) Then
                    cmbProfiles.Items.Add(name)
                    added.Add(name)
                End If
            Next

            Dim remaining As List(Of String) = _profiles.Keys.Where(Function(n) Not added.Contains(n)).OrderBy(Function(n) n).ToList()
            For Each name As String In remaining
                cmbProfiles.Items.Add(name)
            Next

            If cmbProfiles.Items.Count > 0 Then
                cmbProfiles.SelectedItem = _currentProfileName
            End If
        Finally
            _loadingProfile = False
        End Try
    End Sub

    Private Sub InitializeProfiles()
        _profileStorePath = GetProfileStorePath()
        Dim store As ProfileStore = LoadProfileStore(_profileStorePath)

        _profiles = New Dictionary(Of String, ProfileData)(StringComparer.OrdinalIgnoreCase)
        Dim builtIns As Dictionary(Of String, ProfileData) = BuildBuiltInProfiles()
        For Each kvp In builtIns
            _profiles(kvp.Key) = kvp.Value
        Next

        If store IsNot Nothing AndAlso store.Profiles IsNot Nothing Then
            For Each kvp In store.Profiles
                _profiles(kvp.Key) = kvp.Value
            Next
        End If

        If Not _profiles.ContainsKey(DefaultProfileName) Then
            _profiles(DefaultProfileName) = BuildProfileFromUi()
        End If

        Dim lastName As String = If(store Is Nothing, DefaultProfileName, store.LastProfileName)
        If String.IsNullOrWhiteSpace(lastName) OrElse Not _profiles.ContainsKey(lastName) Then
            lastName = DefaultProfileName
        End If

        _currentProfileName = lastName
        PopulateProfileList()
        ApplyProfile(_profiles(_currentProfileName))
        _profilesInitialized = True
        SaveProfileStore()
    End Sub

    Private Sub SaveCurrentProfile(Optional profileName As String = Nothing)
        If Not _profilesInitialized Then
            Return
        End If

        If _loadingProfile Then
            Return
        End If

        Dim name As String = If(profileName, _currentProfileName)
        If String.IsNullOrWhiteSpace(name) Then
            name = DefaultProfileName
        End If

        _profiles(name) = BuildProfileFromUi()
        _currentProfileName = name
        SaveProfileStore()
    End Sub

    Private Function ShowRunOptionsDialog() As RunOptions
        Dim working As RunOptions = CloneRunOptions(_runOptions)

        Using dlg As New Form()
            dlg.Text = "Run Options"
            dlg.FormBorderStyle = FormBorderStyle.FixedDialog
            dlg.StartPosition = FormStartPosition.CenterParent
            dlg.MaximizeBox = False
            dlg.MinimizeBox = False
            dlg.ShowInTaskbar = False
            dlg.ClientSize = New Size(420, 336)

            Dim leftLabel As Integer = 12
            Dim leftControl As Integer = 230
            Dim row As Integer = 12
            Dim rowHeight As Integer = 26

            Dim chkTimed As New CheckBox() With {.Text = "Timed run (minutes)", .AutoSize = True, .Location = New Point(leftLabel, row)}
            Dim numTimed As New NumericUpDown() With {.Location = New Point(leftControl, row - 2), .Minimum = 1, .Maximum = 1440, .Width = 120}
            chkTimed.Checked = working.TimedRunMinutes > 0
            Dim timedValue As Decimal = If(working.TimedRunMinutes > 0, CDec(working.TimedRunMinutes), 10D)
            If timedValue < numTimed.Minimum Then timedValue = numTimed.Minimum
            If timedValue > numTimed.Maximum Then timedValue = numTimed.Maximum
            numTimed.Value = timedValue
            numTimed.Enabled = chkTimed.Checked
            AddHandler chkTimed.CheckedChanged, Sub() numTimed.Enabled = chkTimed.Checked
            row += rowHeight

            Dim chkTemp As New CheckBox() With {.Text = "Auto-stop at temp (C)", .AutoSize = True, .Location = New Point(leftLabel, row)}
            Dim numTemp As New NumericUpDown() With {.Location = New Point(leftControl, row - 2), .Minimum = 40, .Maximum = 125, .Width = 120, .DecimalPlaces = 0}
            chkTemp.Checked = working.AutoStopTempC > 0
            Dim tempValue As Decimal = If(working.AutoStopTempC > 0, CDec(working.AutoStopTempC), 90D)
            If tempValue < numTemp.Minimum Then tempValue = numTemp.Minimum
            If tempValue > numTemp.Maximum Then tempValue = numTemp.Maximum
            numTemp.Value = tempValue
            numTemp.Enabled = chkTemp.Checked
            AddHandler chkTemp.CheckedChanged, Sub() numTemp.Enabled = chkTemp.Checked
            row += rowHeight

            Dim chkThrottle As New CheckBox() With {.Text = "Auto-stop on CPU throttling", .AutoSize = True, .Location = New Point(leftLabel, row)}
            chkThrottle.Checked = working.AutoStopOnThrottle
            row += rowHeight

            Dim chkUiSnappy As New CheckBox() With {.Text = "UI snappy mode (reserve 1 core)", .AutoSize = True, .Location = New Point(leftLabel, row)}
            chkUiSnappy.Checked = working.UiSnappyMode
            row += rowHeight

            Dim chkTelemetry As New CheckBox() With {.Text = "CSV telemetry log (ms)", .AutoSize = True, .Location = New Point(leftLabel, row)}
            Dim numTelemetry As New NumericUpDown() With {.Location = New Point(leftControl, row - 2), .Minimum = 250, .Maximum = 10000, .Increment = 250, .Width = 120}
            chkTelemetry.Checked = working.TelemetryEnabled
            Dim telemetryValue As Decimal = CDec(working.TelemetryIntervalMs)
            If telemetryValue < numTelemetry.Minimum Then telemetryValue = numTelemetry.Minimum
            If telemetryValue > numTelemetry.Maximum Then telemetryValue = numTelemetry.Maximum
            numTelemetry.Value = telemetryValue
            numTelemetry.Enabled = chkTelemetry.Checked
            AddHandler chkTelemetry.CheckedChanged, Sub() numTelemetry.Enabled = chkTelemetry.Checked
            row += rowHeight

            Dim chkValidation As New CheckBox() With {.Text = "Enable validation loop", .AutoSize = True, .Location = New Point(leftLabel, row)}
            chkValidation.Checked = working.ValidationEnabled
            row += rowHeight

            Dim chkAutoPlot As New CheckBox() With {.Text = "Auto-show temp plot on start", .AutoSize = True, .Location = New Point(leftLabel, row)}
            chkAutoPlot.Checked = working.AutoShowTempPlot
            row += rowHeight

            Dim chkAffinity As New CheckBox() With {.Text = "Use core affinity", .AutoSize = True, .Location = New Point(leftLabel, row)}
            Dim btnAffinity As New Button() With {.Text = "Select...", .Location = New Point(leftControl, row - 4), .Width = 120}
            Dim selectedCores As New List(Of Integer)(working.AffinityCores)
            chkAffinity.Checked = working.UseAffinity
            btnAffinity.Enabled = chkAffinity.Checked
            AddHandler chkAffinity.CheckedChanged, Sub() btnAffinity.Enabled = chkAffinity.Checked
            row += rowHeight

            Dim lblAffinity As New Label() With {.AutoSize = True, .Location = New Point(leftLabel, row)}
            Dim updateAffinityLabel As Action = Sub()
                                                    If selectedCores.Count = 0 Then
                                                        lblAffinity.Text = "Selected cores: All"
                                                    Else
                                                        lblAffinity.Text = $"Selected cores: {selectedCores.Count}"
                                                    End If
                                                End Sub
            updateAffinityLabel()
            AddHandler btnAffinity.Click, Sub()
                                              selectedCores = ShowCoreSelectionDialog(selectedCores)
                                              updateAffinityLabel()
                                          End Sub

            Dim btnOk As New Button() With {.Text = "OK", .DialogResult = DialogResult.OK, .Location = New Point(240, 286), .Width = 70}
            Dim btnCancel As New Button() With {.Text = "Cancel", .DialogResult = DialogResult.Cancel, .Location = New Point(320, 286), .Width = 70}
            dlg.AcceptButton = btnOk
            dlg.CancelButton = btnCancel

            dlg.Controls.AddRange(New Control() {chkTimed, numTimed, chkTemp, numTemp, chkThrottle, chkUiSnappy, chkTelemetry, numTelemetry, chkValidation, chkAutoPlot, chkAffinity, btnAffinity, lblAffinity, btnOk, btnCancel})

            If dlg.ShowDialog(Me) <> DialogResult.OK Then
                Return Nothing
            End If

            working.TimedRunMinutes = If(chkTimed.Checked, CInt(numTimed.Value), 0)
            working.AutoStopTempC = If(chkTemp.Checked, CSng(numTemp.Value), 0)
            working.AutoStopOnThrottle = chkThrottle.Checked
            working.UiSnappyMode = chkUiSnappy.Checked
            working.TelemetryEnabled = chkTelemetry.Checked
            working.TelemetryIntervalMs = CInt(numTelemetry.Value)
            working.ValidationEnabled = chkValidation.Checked
            working.AutoShowTempPlot = chkAutoPlot.Checked
            working.UseAffinity = chkAffinity.Checked AndAlso selectedCores.Count > 0
            working.AffinityCores = If(working.UseAffinity, selectedCores, New List(Of Integer)())
        End Using

        Return working
    End Function

    Private Function ShowCoreSelectionDialog(current As List(Of Integer)) As List(Of Integer)
        Dim selected As New List(Of Integer)(current)
        Dim maxSelectable As Integer = Math.Min(Environment.ProcessorCount, If(IntPtr.Size = 8, 64, 32))

        Using dlg As New Form()
            dlg.Text = "Core Affinity"
            dlg.FormBorderStyle = FormBorderStyle.FixedDialog
            dlg.StartPosition = FormStartPosition.CenterParent
            dlg.MaximizeBox = False
            dlg.MinimizeBox = False
            dlg.ShowInTaskbar = False
            dlg.ClientSize = New Size(260, 360)

            Dim list As New CheckedListBox() With {.Location = New Point(12, 12), .Size = New Size(236, 280)}
            For i As Integer = 0 To maxSelectable - 1
                Dim index As Integer = list.Items.Add($"CPU {i}")
                If selected.Contains(i) Then
                    list.SetItemChecked(index, True)
                End If
            Next

            Dim note As New Label() With {.AutoSize = True, .Location = New Point(12, 298), .Text = $"Showing 0-{maxSelectable - 1} cores"}

            Dim btnOk As New Button() With {.Text = "OK", .DialogResult = DialogResult.OK, .Location = New Point(90, 320), .Width = 70}
            Dim btnCancel As New Button() With {.Text = "Cancel", .DialogResult = DialogResult.Cancel, .Location = New Point(170, 320), .Width = 70}
            dlg.AcceptButton = btnOk
            dlg.CancelButton = btnCancel

            dlg.Controls.AddRange(New Control() {list, note, btnOk, btnCancel})

            If dlg.ShowDialog(Me) <> DialogResult.OK Then
                Return selected
            End If

            selected.Clear()
            For i As Integer = 0 To list.Items.Count - 1
                If list.GetItemChecked(i) Then
                    selected.Add(i)
                End If
            Next
        End Using

        Return selected
    End Function

    Private Sub RunOptionsToolStripMenuItem_Click(sender As Object, e As EventArgs) Handles RunOptionsToolStripMenuItem.Click
        Dim updated As RunOptions = ShowRunOptionsDialog()
        If updated Is Nothing Then
            Return
        End If

        _runOptions = updated
        ApplyRunOptionsToRunningState()
        SaveCurrentProfile()

        LogMessage("Run options updated.")
    End Sub

    Private Sub ShowTemperaturePlotWindow()
        If _tempPlotForm Is Nothing OrElse _tempPlotForm.IsDisposed Then
            _tempPlotForm = New TempPlotForm()
            If _uiLayout Is Nothing Then
                _uiLayout = UiLayoutManager.LoadLayout()
            End If
            _tempPlotForm.ApplyLayout(_uiLayout.TempPlotWindow)
            _tempPlotForm.TimeWindowSeconds = _plotTimeWindowSeconds
            AddHandler _tempPlotForm.TimeWindowChanged, AddressOf TempPlotForm_TimeWindowChanged
            AddHandler _tempPlotForm.FormClosing, Sub() SaveTempPlotLayout()
            AddHandler _tempPlotForm.FormClosed, Sub()
                                                     RemoveHandler _tempPlotForm.TimeWindowChanged, AddressOf TempPlotForm_TimeWindowChanged
                                                     _tempPlotForm = Nothing
                                                 End Sub
            _tempPlotForm.Show(Me)
        Else
            _tempPlotForm.BringToFront()
            _tempPlotForm.Focus()
        End If
    End Sub

    Private Sub TempPlotForm_TimeWindowChanged(seconds As Single)
        _plotTimeWindowSeconds = seconds
        SaveCurrentProfile()
    End Sub

    Private Sub TemperaturePlotToolStripMenuItem_Click(sender As Object, e As EventArgs) Handles TemperaturePlotToolStripMenuItem.Click
        ShowTemperaturePlotWindow()
    End Sub

    Private Sub CoreAffinityToolStripMenuItem_Click(sender As Object, e As EventArgs) Handles CoreAffinityToolStripMenuItem.Click
        Dim selected As List(Of Integer) = ShowCoreSelectionDialog(_runOptions.AffinityCores)
        _runOptions.AffinityCores = selected
        _runOptions.UseAffinity = selected.Count > 0
        LogMessage($"Core affinity {(If(_runOptions.UseAffinity, "enabled", "disabled"))}.")
        If btnStart.Text = "Stop" Then
            LogMessage("Core affinity changes apply on the next run.")
        End If
        ApplyRunOptionsToRunningState()
        SaveCurrentProfile()
    End Sub

    Private Sub SaveProfileToolStripMenuItem_Click(sender As Object, e As EventArgs) Handles SaveProfileToolStripMenuItem.Click
        Using dlg As New SaveFileDialog()
            dlg.Filter = "ClawHammer Profile (*.json)|*.json|All Files (*.*)|*.*"
            dlg.DefaultExt = "json"
            dlg.FileName = "clawhammer_profile.json"
            dlg.InitialDirectory = Application.StartupPath
            If dlg.ShowDialog(Me) <> DialogResult.OK Then
                Return
            End If

            Dim profile As ProfileData = BuildProfileFromUi()

            Try
                ProfileManager.SaveProfile(dlg.FileName, profile)
                LogMessage($"Profile saved: {dlg.FileName}")
            Catch ex As Exception
                MessageBox.Show(Me, "Failed to save profile: " & ex.Message, "Profile Save", MessageBoxButtons.OK, MessageBoxIcon.Error)
            End Try
        End Using
    End Sub

    Private Sub LoadProfileToolStripMenuItem_Click(sender As Object, e As EventArgs) Handles LoadProfileToolStripMenuItem.Click
        Using dlg As New OpenFileDialog()
            dlg.Filter = "ClawHammer Profile (*.json)|*.json|All Files (*.*)|*.*"
            dlg.InitialDirectory = Application.StartupPath
            If dlg.ShowDialog(Me) <> DialogResult.OK Then
                Return
            End If

            Dim profile As ProfileData = Nothing
            Try
                profile = ProfileManager.LoadProfile(dlg.FileName)
            Catch ex As Exception
                MessageBox.Show(Me, "Failed to load profile: " & ex.Message, "Profile Load", MessageBoxButtons.OK, MessageBoxIcon.Error)
                Return
            End Try
            If profile Is Nothing Then
                LogMessage("Failed to load profile.")
                Return
            End If

            Dim profileName As String = Path.GetFileNameWithoutExtension(dlg.FileName)
            If String.IsNullOrWhiteSpace(profileName) Then
                profileName = "Imported Profile"
            End If

            _profiles(profileName) = profile
            _currentProfileName = profileName
            PopulateProfileList()
            ApplyProfile(profile)
            SaveProfileStore()

            LogMessage($"Profile loaded: {dlg.FileName}")
        End Using
    End Sub

    Private Sub SystemInfoToolStripMenuItem_Click(sender As Object, e As EventArgs) Handles SystemInfoToolStripMenuItem.Click
        LogSystemInfo()
    End Sub

    Private Sub ExportReportToolStripMenuItem_Click(sender As Object, e As EventArgs) Handles ExportReportToolStripMenuItem.Click
        Using dlg As New SaveFileDialog()
            dlg.Filter = "HTML Report (*.html)|*.html|All Files (*.*)|*.*"
            dlg.DefaultExt = "html"
            dlg.FileName = "ClawHammer_Report.html"
            dlg.InitialDirectory = Application.StartupPath
            If dlg.ShowDialog(Me) <> DialogResult.OK Then
                Return
            End If

            Try
                ExportReport(dlg.FileName)
                LogMessage($"Report exported: {dlg.FileName}")
            Catch ex As Exception
                MessageBox.Show(Me, "Failed to export report: " & ex.Message, "Export Report", MessageBoxButtons.OK, MessageBoxIcon.Error)
            End Try
        End Using
    End Sub

    Private Async Sub CheckLhmUpdatesToolStripMenuItem_Click(sender As Object, e As EventArgs) Handles CheckLhmUpdatesToolStripMenuItem.Click
        CheckLhmUpdatesToolStripMenuItem.Enabled = False
        Try
            Dim installedVersion As String = GetInstalledLhmVersion()
            Dim latestVersion As String = Await GetLatestLhmVersionAsync()

            If String.IsNullOrWhiteSpace(latestVersion) Then
                MessageBox.Show(Me, "Unable to check for LibreHardwareMonitor updates right now.", "Update Check", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If

            Dim comparison As Integer = CompareNuGetVersions(installedVersion, latestVersion)
            Dim message As String
            If comparison >= 0 Then
                message = $"LibreHardwareMonitor is up to date.{Environment.NewLine}Installed: {installedVersion}{Environment.NewLine}Latest: {latestVersion}"
            Else
                message = $"An update is available.{Environment.NewLine}Installed: {installedVersion}{Environment.NewLine}Latest: {latestVersion}"
            End If
            MessageBox.Show(Me, message, "LibreHardwareMonitor Updates", MessageBoxButtons.OK, MessageBoxIcon.Information)
        Catch ex As Exception
            MessageBox.Show(Me, "Update check failed: " & ex.Message, "LibreHardwareMonitor Updates", MessageBoxButtons.OK, MessageBoxIcon.Warning)
        Finally
            CheckLhmUpdatesToolStripMenuItem.Enabled = True
        End Try
    End Sub

    Private Sub LogSystemInfo()
        LogMessage("System info snapshot:")
        For Each line In GetSystemInfoLines()
            LogMessage(line)
        Next
    End Sub

    Private Function GetSystemInfoLines() As List(Of String)
        Dim lines As New List(Of String) From {
            $"Machine: {Environment.MachineName}",
            $"OS: {Environment.OSVersion}",
            $"Process: {(If(Environment.Is64BitProcess, "64-bit", "32-bit"))}",
            $"Logical CPUs: {Environment.ProcessorCount}"
        }

        Try
            For Each item In New System.Management.ManagementObjectSearcher("Select Name,NumberOfCores,NumberOfLogicalProcessors,MaxClockSpeed from Win32_Processor").[Get]()
                Dim name As String = item("Name")?.ToString()
                Dim cores As String = item("NumberOfCores")?.ToString()
                Dim logical As String = item("NumberOfLogicalProcessors")?.ToString()
                Dim maxClock As String = item("MaxClockSpeed")?.ToString()
                If Not String.IsNullOrWhiteSpace(name) Then
                    lines.Add($"CPU: {name}")
                End If
                If Not String.IsNullOrWhiteSpace(cores) Then
                    lines.Add($"Physical Cores: {cores}")
                End If
                If Not String.IsNullOrWhiteSpace(logical) Then
                    lines.Add($"Logical Processors: {logical}")
                End If
                If Not String.IsNullOrWhiteSpace(maxClock) Then
                    lines.Add($"Max Clock (MHz): {maxClock}")
                End If
            Next
        Catch ex As Exception
            lines.Add("System info unavailable: " & ex.Message)
        End Try

        Try
            For Each item In New System.Management.ManagementObjectSearcher("Select TotalPhysicalMemory from Win32_ComputerSystem").[Get]()
                Dim totalMemoryText As String = item("TotalPhysicalMemory")?.ToString()
                Dim totalMemoryBytes As ULong
                If ULong.TryParse(totalMemoryText, totalMemoryBytes) Then
                    lines.Add($"Installed RAM: {FormatBytes(totalMemoryBytes)}")
                End If
            Next
        Catch ex As Exception
            lines.Add("RAM info unavailable: " & ex.Message)
        End Try

        Try
            Dim moduleIndex As Integer = 1
            For Each item In New System.Management.ManagementObjectSearcher("Select Capacity,Speed,Manufacturer,PartNumber from Win32_PhysicalMemory").[Get]()
                Dim capacityText As String = item("Capacity")?.ToString()
                Dim capacityBytes As ULong
                Dim capacityValue As String = If(ULong.TryParse(capacityText, capacityBytes), FormatBytes(capacityBytes), "Unknown")
                Dim speedText As String = item("Speed")?.ToString()
                Dim manufacturer As String = item("Manufacturer")?.ToString()
                Dim partNumber As String = item("PartNumber")?.ToString()

                Dim speedInfo As String = If(String.IsNullOrWhiteSpace(speedText), "Unknown", speedText & " MHz")
                Dim vendorInfo As String = If(String.IsNullOrWhiteSpace(manufacturer), String.Empty, manufacturer.Trim())
                Dim partInfo As String = If(String.IsNullOrWhiteSpace(partNumber), String.Empty, partNumber.Trim())
                Dim extraInfo As String = String.Join(" ", New String() {vendorInfo, partInfo}.Where(Function(v) Not String.IsNullOrWhiteSpace(v))).Trim()

                Dim moduleLine As String = $"RAM Module {moduleIndex}: {capacityValue} @ {speedInfo}"
                If Not String.IsNullOrWhiteSpace(extraInfo) Then
                    moduleLine &= $" ({extraInfo})"
                End If
                lines.Add(moduleLine)
                moduleIndex += 1
            Next
        Catch ex As Exception
            lines.Add("RAM module info unavailable: " & ex.Message)
        End Try

        Try
            For Each item In New System.Management.ManagementObjectSearcher("Select Name,AdapterRAM from Win32_VideoController").[Get]()
                Dim name As String = item("Name")?.ToString()
                Dim ramText As String = item("AdapterRAM")?.ToString()
                Dim ramBytes As ULong
                Dim ramInfo As String = If(ULong.TryParse(ramText, ramBytes), FormatBytes(ramBytes), "Unknown")
                If Not String.IsNullOrWhiteSpace(name) Then
                    lines.Add($"GPU: {name} ({ramInfo})")
                End If
            Next
        Catch ex As Exception
            lines.Add("GPU info unavailable: " & ex.Message)
        End Try

        Try
            For Each item In New System.Management.ManagementObjectSearcher("Select Manufacturer,Product from Win32_BaseBoard").[Get]()
                Dim manufacturer As String = item("Manufacturer")?.ToString()
                Dim product As String = item("Product")?.ToString()
                Dim board As String = String.Join(" ", New String() {manufacturer, product}.Where(Function(v) Not String.IsNullOrWhiteSpace(v))).Trim()
                If Not String.IsNullOrWhiteSpace(board) Then
                    lines.Add($"Motherboard: {board}")
                End If
            Next
        Catch ex As Exception
            lines.Add("Motherboard info unavailable: " & ex.Message)
        End Try

        Try
            For Each item In New System.Management.ManagementObjectSearcher("Select Manufacturer,SMBIOSBIOSVersion,Version from Win32_BIOS").[Get]()
                Dim manufacturer As String = item("Manufacturer")?.ToString()
                Dim version As String = item("SMBIOSBIOSVersion")?.ToString()
                If String.IsNullOrWhiteSpace(version) Then
                    version = item("Version")?.ToString()
                End If
                Dim bios As String = String.Join(" ", New String() {manufacturer, version}.Where(Function(v) Not String.IsNullOrWhiteSpace(v))).Trim()
                If Not String.IsNullOrWhiteSpace(bios) Then
                    lines.Add($"BIOS: {bios}")
                End If
            Next
        Catch ex As Exception
            lines.Add("BIOS info unavailable: " & ex.Message)
        End Try

        Try
            For Each item In New System.Management.ManagementObjectSearcher("Select Model,Size from Win32_DiskDrive").[Get]()
                Dim model As String = item("Model")?.ToString()
                Dim sizeText As String = item("Size")?.ToString()
                Dim sizeBytes As ULong
                Dim sizeInfo As String = If(ULong.TryParse(sizeText, sizeBytes), FormatBytes(sizeBytes), "Unknown")
                If Not String.IsNullOrWhiteSpace(model) Then
                    lines.Add($"Disk: {model} ({sizeInfo})")
                End If
            Next
        Catch ex As Exception
            lines.Add("Disk info unavailable: " & ex.Message)
        End Try

        Try
            If _computer IsNot Nothing Then
                For Each hw As IHardware In _computer.Hardware
                    lines.Add($"LHM: {hw.HardwareType} - {hw.Name}")
                Next
            End If
        Catch ex As Exception
            lines.Add("LibreHardwareMonitor info unavailable: " & ex.Message)
        End Try

        Return lines
    End Function

    Private Function FormatBytes(bytes As ULong) As String
        Dim size As Double = bytes
        Dim units As String() = {"B", "KB", "MB", "GB", "TB"}
        Dim unitIndex As Integer = 0

        While size >= 1024 AndAlso unitIndex < units.Length - 1
            size /= 1024
            unitIndex += 1
        End While

        Return $"{size:F1} {units(unitIndex)}"
    End Function

    Private Sub ExportReport(path As String)
        Dim sb As New System.Text.StringBuilder()
        sb.AppendLine("<html><head><meta charset=""utf-8""><title>ClawHammer Report</title></head><body>")
        sb.AppendLine("<h1>ClawHammer Report</h1>")
        sb.AppendLine("<p>Generated: " & Date.Now.ToString("yyyy-MM-dd HH:mm:ss") & "</p>")
        sb.AppendLine("<h2>Run Settings</h2><ul>")
        sb.AppendLine("<li>Threads: " & NumThreads.Value.ToString() & "</li>")
        sb.AppendLine("<li>Workload: " & If(cmbStressType.SelectedItem IsNot Nothing, cmbStressType.SelectedItem.ToString(), "Unknown") & "</li>")
        sb.AppendLine("<li>Priority: " & CmbThreadPriority.Text & "</li>")
        sb.AppendLine("<li>Timed Run (min): " & _runOptions.TimedRunMinutes.ToString() & "</li>")
        sb.AppendLine("<li>Auto-stop Temp (C): " & _runOptions.AutoStopTempC.ToString(CultureInfo.InvariantCulture) & "</li>")
        sb.AppendLine("<li>Auto-stop on Throttling: " & _runOptions.AutoStopOnThrottle.ToString() & "</li>")
        sb.AppendLine("<li>UI Snappy Mode: " & _runOptions.UiSnappyMode.ToString() & "</li>")
        sb.AppendLine("<li>Telemetry Enabled: " & _runOptions.TelemetryEnabled.ToString() & "</li>")
        sb.AppendLine("<li>Telemetry Interval (ms): " & _runOptions.TelemetryIntervalMs.ToString() & "</li>")
        sb.AppendLine("<li>Validation Enabled: " & _runOptions.ValidationEnabled.ToString() & "</li>")
        sb.AppendLine("<li>Core Affinity: " & If(_runOptions.UseAffinity AndAlso _runOptions.AffinityCores.Count > 0, String.Join(",", _runOptions.AffinityCores), "All") & "</li>")
        If Not String.IsNullOrWhiteSpace(_telemetryFilePath) Then
            sb.AppendLine("<li>Telemetry File: " & System.Net.WebUtility.HtmlEncode(_telemetryFilePath) & "</li>")
        End If
        sb.AppendLine("</ul>")

        sb.AppendLine("<h2>System Info</h2><pre>")
        For Each line In GetSystemInfoLines()
            sb.AppendLine(System.Net.WebUtility.HtmlEncode(line))
        Next
        sb.AppendLine("</pre>")

        sb.AppendLine("<h2>Log</h2><pre>")
        sb.AppendLine(System.Net.WebUtility.HtmlEncode(rhtxtlog.Text))
        sb.AppendLine("</pre></body></html>")

        File.WriteAllText(path, sb.ToString())
    End Sub

    Private Function GetInstalledLhmVersion() As String
        Dim asm As Assembly = GetType(Computer).Assembly
        Dim info As AssemblyInformationalVersionAttribute = asm.GetCustomAttribute(Of AssemblyInformationalVersionAttribute)()
        If info IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(info.InformationalVersion) Then
            Return info.InformationalVersion
        End If
        Return asm.GetName().Version.ToString()
    End Function

    Private Async Function GetLatestLhmVersionAsync() As Task(Of String)
        Using client As New HttpClient()
            client.Timeout = TimeSpan.FromSeconds(10)
            Dim json As String = Await client.GetStringAsync("https://api.nuget.org/v3-flatcontainer/librehardwaremonitorlib/index.json")
            Using doc As JsonDocument = JsonDocument.Parse(json)
                Dim versionsElement As JsonElement = doc.RootElement.GetProperty("versions")
                Dim latest As String = Nothing

                For Each entry As JsonElement In versionsElement.EnumerateArray()
                    Dim version As String = entry.GetString()
                    If String.IsNullOrWhiteSpace(version) Then
                        Continue For
                    End If
                    If String.IsNullOrWhiteSpace(latest) OrElse CompareNuGetVersions(version, latest) > 0 Then
                        latest = version
                    End If
                Next

                Return latest
            End Using
        End Using
    End Function

    Private Function CompareNuGetVersions(a As String, b As String) As Integer
        If String.Equals(a, b, StringComparison.OrdinalIgnoreCase) Then
            Return 0
        End If

        Dim aParts As String() = a.Split("-"c)
        Dim bParts As String() = b.Split("-"c)

        Dim aBase As Version = ParseVersionSafe(aParts(0))
        Dim bBase As Version = ParseVersionSafe(bParts(0))
        Dim baseCompare As Integer = aBase.CompareTo(bBase)
        If baseCompare <> 0 Then
            Return baseCompare
        End If

        Dim aPre As String = If(aParts.Length > 1, aParts(1), String.Empty)
        Dim bPre As String = If(bParts.Length > 1, bParts(1), String.Empty)

        If String.IsNullOrEmpty(aPre) AndAlso String.IsNullOrEmpty(bPre) Then
            Return 0
        End If
        If String.IsNullOrEmpty(aPre) Then
            Return 1
        End If
        If String.IsNullOrEmpty(bPre) Then
            Return -1
        End If

        Dim aNum As Integer = ParsePreReleaseNumber(aPre)
        Dim bNum As Integer = ParsePreReleaseNumber(bPre)
        If aNum >= 0 AndAlso bNum >= 0 Then
            Return aNum.CompareTo(bNum)
        End If

        Return StringComparer.OrdinalIgnoreCase.Compare(aPre, bPre)
    End Function

    Private Function ParseVersionSafe(text As String) As Version
        Dim clean As String = text
        Dim plusIndex As Integer = clean.IndexOf("+"c)
        If plusIndex >= 0 Then
            clean = clean.Substring(0, plusIndex)
        End If

        Dim parts As String() = clean.Split("."c)
        Dim major As Integer = If(parts.Length > 0, ToIntSafe(parts(0)), 0)
        Dim minor As Integer = If(parts.Length > 1, ToIntSafe(parts(1)), 0)
        Dim build As Integer = If(parts.Length > 2, ToIntSafe(parts(2)), 0)
        Dim revision As Integer = If(parts.Length > 3, ToIntSafe(parts(3)), 0)
        Return New Version(major, minor, build, revision)
    End Function

    Private Function ParsePreReleaseNumber(tag As String) As Integer
        If tag.StartsWith("pre", StringComparison.OrdinalIgnoreCase) Then
            Dim numText As String = tag.Substring(3)
            Dim value As Integer
            If Integer.TryParse(numText, value) Then
                Return value
            End If
        End If
        Return -1
    End Function

    Private Function ToIntSafe(text As String) As Integer
        Dim value As Integer
        If Integer.TryParse(text, value) Then
            Return value
        End If
        Return 0
    End Function





    Private Sub AboutToolStripMenuItem_Click(sender As System.Object, e As System.EventArgs) Handles AboutToolStripMenuItem.Click
        frmabout.ShowDialog(Me)
    End Sub

    Private Sub ExitToolStripMenuItem_Click(sender As System.Object, e As System.EventArgs) Handles ExitToolStripMenuItem.Click
        Me.Close()
    End Sub

    Private Sub cmbProfiles_SelectedIndexChanged(sender As Object, e As EventArgs) Handles cmbProfiles.SelectedIndexChanged
        If Not _profilesInitialized Then
            Return
        End If

        If _loadingProfile Then
            Return
        End If

        Dim name As String = TryCast(cmbProfiles.SelectedItem, String)
        If String.IsNullOrWhiteSpace(name) Then
            Return
        End If

        If _profiles.ContainsKey(name) Then
            _currentProfileName = name
            ApplyProfile(_profiles(name))
            SaveProfileStore()
            LogMessage($"Profile loaded: {name}")
        End If
    End Sub

    Private Sub NumThreads_ValueChanged(sender As Object, e As EventArgs) Handles NumThreads.ValueChanged
        SaveCurrentProfile()
    End Sub

    Private Sub cmbStressType_SelectedIndexChanged(sender As Object, e As EventArgs) Handles cmbStressType.SelectedIndexChanged
        SaveCurrentProfile()
    End Sub

    Private Sub CmbThreadPriority_SelectedIndexChanged(sender As Object, e As EventArgs) Handles CmbThreadPriority.SelectedIndexChanged
        SaveCurrentProfile()
    End Sub

    Private Sub chkSaveLog_CheckedChanged(sender As System.Object, e As System.EventArgs) Handles chkSaveLog.CheckedChanged
        SaveCurrentProfile()
    End Sub

End Class




