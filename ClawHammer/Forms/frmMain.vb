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
Imports System.Text.Json
Imports System.Threading.Tasks
Imports System.Linq
Imports ClawHammer.PluginContracts


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
    Private _pluginRegistry As New StressPluginRegistry()
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
    Private _validationSettings As ValidationSettings
    Private _validationStatusText As String = "Validation: Off"
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
    Private Const PawnIoSetupFileName As String = "PawnIO_setup.exe"
    Private Shared ReadOnly PawnIoMinVersion As Version = New Version(2, 0, 0, 0)
    Private Const IconCpu As String = "cpu"
    Private Const IconGpu As String = "gpu"
    Private Const IconMotherboard As String = "motherboard"
    Private Const IconMemory As String = "memory"
    Private Const IconStorage As String = "storage"
    Private Const IconController As String = "controller"
    Private Const IconDefault As String = "temp"
    Private Const IconTemperature As String = "temperature"
    Private Const IconVoltage As String = "voltage"
    Private Const IconClock As String = "clock"
    Private Const IconThrottle As String = "throttle"
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
    Private _throttlePeakClockMHz As Single = Single.NaN
    Private _throttleLoadedSamples As Integer = 0
    Private _throttleLowClockSamples As Integer = 0
    Private _throttleLastActive As Boolean = False
    Private _throttleLastDetail As String = String.Empty
    Private Const ThrottleMinCpuUsagePercent As Integer = 85
    Private Const ThrottleDropRatio As Single = 0.85F
    Private Const ThrottleSevereDropRatio As Single = 0.75F
    Private Const ThrottleMinLoadedSamples As Integer = 5
    Private Const ThrottleMinLowSamplesHot As Integer = 3
    Private Const ThrottleMinLowSamplesSevere As Integer = 5
    Private _treeListExpanded As Dictionary(Of String, Boolean) = New Dictionary(Of String, Boolean)(StringComparer.OrdinalIgnoreCase)
    Private _lastSensorReadings As List(Of SensorReading) = New List(Of SensorReading)()
    Private _lastAvgText As String = "CPU Temp: N/A"
    Private _sensorMinMax As Dictionary(Of String, SensorMinMax) = New Dictionary(Of String, SensorMinMax)(StringComparer.OrdinalIgnoreCase)
    Private _uiLayoutMissing As Boolean = False
    Private ReadOnly _validationStatusByWorker As New Dictionary(Of Integer, ValidationStatusSnapshot)()
    Private ReadOnly _validationStatusLock As New Object()
    Private _validationSummarySuffix As String = String.Empty
    Private _validationMonitor As frmValidationMonitor
    Private ReadOnly _workerAffinityMap As New Dictionary(Of Integer, WorkerAffinityInfo)()
    Private ReadOnly _workerAffinityLock As New Object()
    Private _cpuTopology As CpuTopologySnapshot
    Private _toolTipMain As ToolTip
    Private _sensorAccessWarningLogged As Integer = 0


    Public Shared Sub SetDoubleBuffered(ByVal control As Control)
        GetType(Control).InvokeMember("DoubleBuffered", BindingFlags.SetProperty Or BindingFlags.Instance Or BindingFlags.NonPublic, Nothing, control, New Object() {True})
    End Sub

    Private Shared Function FormatCelsius(value As Single) As String
        Return value.ToString("F1") & CelsiusSuffix
    End Function

    Private Shared Function FormatCelsiusOrNa(value As Single) As String
        If Single.IsNaN(value) Then
            Return "N/A"
        End If
        Return FormatCelsius(value)
    End Function

    Private Shared Function FormatClockMHzOrNa(value As Single) As String
        If Single.IsNaN(value) Then
            Return "N/A"
        End If
        Return value.ToString("F0", CultureInfo.InvariantCulture) & " MHz"
    End Function

    Private Shared Function FormatVoltageOrNa(value As Single) As String
        If Single.IsNaN(value) Then
            Return "N/A"
        End If
        Return value.ToString("F3", CultureInfo.InvariantCulture) & " V"
    End Function

    Private Enum SensorValueFormat
        None
        Celsius
        Mhz
        Volt
        Percent
        Watt
        Number
    End Enum

    Private Structure SensorReading
        Public ReadOnly Label As String
        Public ReadOnly ValueText As String
        Public ReadOnly IconKey As String
        Public ReadOnly ValueRaw As Nullable(Of Single)
        Public ReadOnly ValueFormat As SensorValueFormat

        Public Sub New(label As String, valueText As String, iconKey As String, Optional valueRaw As Nullable(Of Single) = Nothing, Optional valueFormat As SensorValueFormat = SensorValueFormat.None)
            Me.Label = label
            Me.ValueText = valueText
            Me.IconKey = iconKey
            Me.ValueRaw = valueRaw
            Me.ValueFormat = valueFormat
        End Sub
    End Structure

    Private Class WorkerAffinityInfo
        Public Property WorkerId As Integer
        Public Property LogicalId As Integer?
        Public Property CoreId As Integer?
        Public Property CoreLabel As String
        Public Property AffinityLabel As String
    End Class


    Private Structure SensorMinMax
        Public Min As Single
        Public Max As Single
        Public Format As SensorValueFormat

        Public Sub New(value As Single, format As SensorValueFormat)
            Min = value
            Max = value
            Me.Format = format
        End Sub
    End Structure

    Private Shared Function FormatSensorValue(value As Single, format As SensorValueFormat) As String
        Select Case format
            Case SensorValueFormat.Celsius
                Return FormatCelsius(value)
            Case SensorValueFormat.Mhz
                Return value.ToString("F0", CultureInfo.InvariantCulture) & " MHz"
            Case SensorValueFormat.Volt
                Return value.ToString("F3", CultureInfo.InvariantCulture) & " V"
            Case SensorValueFormat.Percent
                Return value.ToString("F0", CultureInfo.InvariantCulture) & "%"
            Case SensorValueFormat.Watt
                Return value.ToString("F1", CultureInfo.InvariantCulture) & " W"
            Case SensorValueFormat.Number
                Return value.ToString("F2", CultureInfo.InvariantCulture)
            Case Else
                Return value.ToString("F2", CultureInfo.InvariantCulture)
        End Select
    End Function

    Private Shared Function CreateSensorReading(label As String, iconKey As String, valueRaw As Nullable(Of Single), format As SensorValueFormat) As SensorReading
        Dim valueText As String = If(valueRaw.HasValue, FormatSensorValue(valueRaw.Value, format), "N/A")
        Return New SensorReading(label, valueText, iconKey, valueRaw, format)
    End Function

    Private Sub UpdateSensorMinMax(reading As SensorReading)
        If String.IsNullOrWhiteSpace(reading.Label) Then
            Return
        End If
        If Not reading.ValueRaw.HasValue Then
            Return
        End If
        If reading.ValueFormat = SensorValueFormat.None Then
            Return
        End If

        Dim value As Single = reading.ValueRaw.Value
        If Single.IsNaN(value) OrElse Single.IsInfinity(value) Then
            Return
        End If
        Dim existing As SensorMinMax
        If _sensorMinMax.TryGetValue(reading.Label, existing) Then
            existing.Min = Math.Min(existing.Min, value)
            existing.Max = Math.Max(existing.Max, value)
            If existing.Format = SensorValueFormat.None Then
                existing.Format = reading.ValueFormat
            End If
            _sensorMinMax(reading.Label) = existing
        Else
            _sensorMinMax(reading.Label) = New SensorMinMax(value, reading.ValueFormat)
        End If
    End Sub

    Private Sub GetSensorMinMaxText(reading As SensorReading, ByRef minText As String, ByRef maxText As String)
        minText = String.Empty
        maxText = String.Empty
        If reading.ValueFormat = SensorValueFormat.None Then
            Return
        End If

        Dim existing As SensorMinMax
        If Not _sensorMinMax.TryGetValue(reading.Label, existing) Then
            Return
        End If

        Dim format As SensorValueFormat = If(existing.Format <> SensorValueFormat.None, existing.Format, reading.ValueFormat)
        minText = FormatSensorValue(existing.Min, format)
        maxText = FormatSensorValue(existing.Max, format)
    End Sub

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

    Private Enum ThrottleMatchKind
        None
        Trigger
        Info
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

        Dim dpiScale As Single = Math.Max(1.0F, CSng(DeviceDpi) / 96.0F)
        Dim iconSize As Integer = Math.Max(16, CInt(Math.Round(16 * dpiScale)))
        Dim imageList As New ImageList() With {
            .ColorDepth = ColorDepth.Depth32Bit,
            .ImageSize = New Size(iconSize, iconSize)
        }

        Dim iconFiles As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {
            {IconCpu, "cpu.png"},
            {IconGpu, "gpu.png"},
            {IconMotherboard, "motherboard.png"},
            {IconMemory, "memory.png"},
            {IconStorage, "storage.png"},
            {IconController, "controller.png"},
            {IconDefault, "temperature.png"},
            {IconTemperature, "temperature.png"},
            {IconVoltage, "voltage.png"},
            {IconClock, "clocks.png"},
            {IconThrottle, "thorttle.png"}
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
        _uiLayoutMissing = Not File.Exists(UiLayoutManager.GetLayoutPath())

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

        Dim scale As Single = UiLayoutManager.GetLayoutScaleFactor(Me, layout)
        If lstvCoreTemps IsNot Nothing AndAlso layout.ColumnWidths IsNot Nothing AndAlso layout.ColumnWidths.Count > 0 Then
            Dim count As Integer = Math.Min(layout.ColumnWidths.Count, lstvCoreTemps.Columns.Count)
            For i As Integer = 0 To count - 1
                Dim width As Integer = UiLayoutManager.ScaleLayoutValue(layout.ColumnWidths(i), scale)
                If width > 30 Then
                    lstvCoreTemps.Columns(i).Width = width
                End If
            Next
        End If

        _pendingMainSplitterDistance = UiLayoutManager.ScaleLayoutValue(layout.SplitterDistance, scale)
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
            Dim selectionSet As Boolean = _tempPlotForm.SelectionSet
            _uiLayout.TempPlotSelectionSet = selectionSet
            _uiLayout.TempPlotSelectedSensors = If(selectionSet, _tempPlotForm.GetSelectedSensors(), New List(Of String)())
            _uiLayout.TempPlotRefreshIntervalMs = _tempPlotForm.RefreshIntervalMs
        End If

        If _validationMonitor IsNot Nothing AndAlso Not _validationMonitor.IsDisposed Then
            UiLayoutManager.CaptureWindowLayout(_validationMonitor, _uiLayout.ValidationMonitorWindow)
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
        Dim selectionSet As Boolean = _tempPlotForm.SelectionSet
        _uiLayout.TempPlotSelectionSet = selectionSet
        _uiLayout.TempPlotSelectedSensors = If(selectionSet, _tempPlotForm.GetSelectedSensors(), New List(Of String)())
        _uiLayout.TempPlotRefreshIntervalMs = _tempPlotForm.RefreshIntervalMs
        UiLayoutManager.SaveLayout(_uiLayout)
    End Sub

    Private Sub SaveValidationMonitorLayout()
        If _validationMonitor Is Nothing OrElse _validationMonitor.IsDisposed Then
            Return
        End If

        If _uiLayout Is Nothing Then
            _uiLayout = UiLayoutManager.LoadLayout()
        End If

        UiLayoutManager.CaptureWindowLayout(_validationMonitor, _uiLayout.ValidationMonitorWindow)
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

        Dim topIndex As Integer = -1
        Dim topLabel As String = String.Empty
        If lstvCoreTemps.TopItem IsNot Nothing Then
            topIndex = lstvCoreTemps.TopItem.Index
            topLabel = lstvCoreTemps.TopItem.Text
        End If

        _lastSensorReadings = If(readings IsNot Nothing, New List(Of SensorReading)(readings), New List(Of SensorReading)())
        _lastAvgText = avgText
        For Each reading As SensorReading In _lastSensorReadings
            UpdateSensorMinMax(reading)
        Next
        Dim items As List(Of ListViewItem) = BuildTreeListItems(_lastSensorReadings)

        lstvCoreTemps.BeginUpdate()
        Try
            If lstvCoreTemps.Items.Count = items.Count AndAlso items.Count > 0 Then
                UpdateTreeListItemsInPlace(items)
            Else
                lstvCoreTemps.Items.Clear()
                If items.Count > 0 Then
                    lstvCoreTemps.Items.AddRange(items.ToArray())
                End If
                RestoreTreeListScroll(topIndex, topLabel)
            End If
        Finally
            lstvCoreTemps.EndUpdate()
        End Try

        cputemp.Text = avgText
    End Sub

    Private Sub UpdateTreeListItemsInPlace(items As List(Of ListViewItem))
        If items Is Nothing OrElse items.Count = 0 Then
            Return
        End If

        For i As Integer = 0 To items.Count - 1
            Dim source As ListViewItem = items(i)
            Dim target As ListViewItem = lstvCoreTemps.Items(i)
            target.Text = source.Text
            EnsureListViewSubItems(target, 4)
            EnsureListViewSubItems(source, 4)
            target.SubItems(1).Text = source.SubItems(1).Text
            target.SubItems(2).Text = source.SubItems(2).Text
            target.SubItems(3).Text = source.SubItems(3).Text
            Dim sourceKey As String = source.ImageKey
            If Not String.IsNullOrWhiteSpace(sourceKey) Then
                target.ImageKey = sourceKey
            ElseIf source.ImageIndex >= 0 Then
                target.ImageIndex = source.ImageIndex
            Else
                target.ImageIndex = -1
                target.ImageKey = String.Empty
            End If
            target.Tag = source.Tag
        Next
    End Sub

    Private Shared Sub EnsureListViewSubItems(item As ListViewItem, count As Integer)
        If item Is Nothing Then
            Return
        End If

        While item.SubItems.Count < count
            item.SubItems.Add(String.Empty)
        End While
    End Sub

    Private Sub InitializeTreeListView()
        If lstvCoreTemps Is Nothing Then
            Return
        End If

        lstvCoreTemps.FullRowSelect = True
        AddHandler lstvCoreTemps.MouseDown, AddressOf TreeList_MouseDown
    End Sub

    Private Sub TreeList_MouseDown(sender As Object, e As MouseEventArgs)
        If e.Button <> MouseButtons.Left Then
            Return
        End If

        Dim listView As ListView = TryCast(sender, ListView)
        If listView Is Nothing Then
            Return
        End If

        Dim hit As ListViewHitTestInfo = listView.HitTest(e.Location)
        If hit Is Nothing OrElse hit.Item Is Nothing OrElse hit.SubItem Is Nothing Then
            Return
        End If

        Dim info As UiThemeManager.TreeListItemInfo = TryCast(hit.Item.Tag, UiThemeManager.TreeListItemInfo)
        If info Is Nothing OrElse Not info.IsGroup Then
            Return
        End If

        If hit.Item.SubItems.IndexOf(hit.SubItem) <> 0 Then
            Return
        End If

        ToggleTreeListGroup(info.Key)
    End Sub

    Private Sub ToggleTreeListGroup(key As String)
        If String.IsNullOrWhiteSpace(key) Then
            Return
        End If

        Dim expanded As Boolean = True
        If _treeListExpanded.TryGetValue(key, expanded) Then
            _treeListExpanded(key) = Not expanded
        Else
            _treeListExpanded(key) = False
        End If

        If _lastSensorReadings IsNot Nothing Then
            UpdateCoreTempUi(_lastSensorReadings, _lastAvgText)
        End If
    End Sub

    Private Sub RestoreTreeListScroll(topIndex As Integer, topLabel As String)
        If lstvCoreTemps Is Nothing OrElse lstvCoreTemps.Items.Count = 0 Then
            Return
        End If

        If topIndex >= 0 AndAlso topIndex < lstvCoreTemps.Items.Count Then
            Try
                lstvCoreTemps.TopItem = lstvCoreTemps.Items(topIndex)
                Return
            Catch
            End Try
        End If

        If String.IsNullOrWhiteSpace(topLabel) Then
            Return
        End If

        For Each item As ListViewItem In lstvCoreTemps.Items
            If String.Equals(item.Text, topLabel, StringComparison.OrdinalIgnoreCase) Then
                Try
                    lstvCoreTemps.TopItem = item
                Catch
                End Try
                Exit For
            End If
        Next
    End Sub

    Private Function BuildTreeListItems(readings As List(Of SensorReading)) As List(Of ListViewItem)
        Dim items As New List(Of ListViewItem)()
        If readings Is Nothing OrElse readings.Count = 0 Then
            Return items
        End If

        Dim cpuTemps As New List(Of SensorReading)()
        Dim cpuClocks As New List(Of SensorReading)()
        Dim cpuVoltages As New List(Of SensorReading)()
        Dim cpuThrottle As New List(Of SensorReading)()
        Dim otherGroups As New Dictionary(Of String, List(Of SensorReading))(StringComparer.OrdinalIgnoreCase)

        For Each reading As SensorReading In readings
            Dim label As String = If(reading.Label, String.Empty)
            Dim labelLower As String = label.ToLowerInvariant()

            If labelLower.StartsWith("cpu throttle") Then
                cpuThrottle.Add(reading)
            ElseIf labelLower.StartsWith("cpu clock") Then
                cpuClocks.Add(reading)
            ElseIf labelLower.StartsWith("cpu voltage") Then
                cpuVoltages.Add(reading)
            Else
                Dim prefix As String = ExtractHardwarePrefix(label)
                If prefix.Equals("Cpu", StringComparison.OrdinalIgnoreCase) Then
                    cpuTemps.Add(reading)
                Else
                    Dim groupName As String = NormalizeHardwareGroupName(prefix)
                    If String.IsNullOrWhiteSpace(groupName) Then
                        groupName = "Other"
                    End If
                    Dim list As List(Of SensorReading) = Nothing
                    If Not otherGroups.TryGetValue(groupName, list) Then
                        list = New List(Of SensorReading)()
                        otherGroups(groupName) = list
                    End If
                    list.Add(reading)
                End If
            End If
        Next

        Dim compare As Comparison(Of SensorReading) = Function(a, b) StringComparer.OrdinalIgnoreCase.Compare(a.Label, b.Label)
        cpuTemps.Sort(compare)
        cpuClocks.Sort(compare)
        cpuVoltages.Sort(compare)
        cpuThrottle.Sort(compare)

        Dim cpuHasData As Boolean = (cpuTemps.Count + cpuClocks.Count + cpuVoltages.Count + cpuThrottle.Count) > 0
        If cpuHasData Then
            Dim cpuKey As String = "CPU"
            Dim cpuExpanded As Boolean = GetTreeListExpanded(cpuKey, True)
            items.Add(CreateTreeListItem("CPU", String.Empty, String.Empty, String.Empty, IconCpu, cpuKey, 0, True, cpuExpanded))

            If cpuExpanded Then
                AddTreeListSubgroup(items, "Temperatures", IconTemperature, cpuKey & "/Temperatures", cpuTemps, 1)
                AddTreeListSubgroup(items, "Clocks", IconClock, cpuKey & "/Clocks", cpuClocks, 1)
                AddTreeListSubgroup(items, "Voltages", IconVoltage, cpuKey & "/Voltages", cpuVoltages, 1)
                AddTreeListSubgroup(items, "Throttle", IconThrottle, cpuKey & "/Throttle", cpuThrottle, 1)
            End If
        End If

        Dim orderedGroups As IEnumerable(Of String) = otherGroups.Keys.OrderBy(Function(key) key, StringComparer.OrdinalIgnoreCase)
        For Each groupName As String In orderedGroups
            Dim groupKey As String = groupName
            Dim expanded As Boolean = GetTreeListExpanded(groupKey, True)
            items.Add(CreateTreeListItem(groupName, String.Empty, String.Empty, String.Empty, GetGroupIconKey(groupName), groupKey, 0, True, expanded))
            If expanded Then
                Dim list As List(Of SensorReading) = otherGroups(groupName)
                list.Sort(compare)
                For Each reading As SensorReading In list
                    Dim minText As String = String.Empty
                    Dim maxText As String = String.Empty
                    GetSensorMinMaxText(reading, minText, maxText)
                    items.Add(CreateTreeListItem(reading.Label, reading.ValueText, minText, maxText, reading.IconKey, String.Empty, 1, False, False))
                Next
            End If
        Next

        If items.Count = 0 Then
            items.Add(CreateTreeListItem("Sensors", "N/A", String.Empty, String.Empty, IconDefault, String.Empty, 0, False, False))
        End If

        Return items
    End Function

    Private Sub AddTreeListSubgroup(items As List(Of ListViewItem), label As String, iconKey As String, key As String, readings As List(Of SensorReading), level As Integer)
        If readings Is Nothing OrElse readings.Count = 0 Then
            Return
        End If

        Dim expanded As Boolean = GetTreeListExpanded(key, True)
        items.Add(CreateTreeListItem(label, String.Empty, String.Empty, String.Empty, iconKey, key, level, True, expanded))

        If Not expanded Then
            Return
        End If

        For Each reading As SensorReading In readings
            Dim minText As String = String.Empty
            Dim maxText As String = String.Empty
            GetSensorMinMaxText(reading, minText, maxText)
            items.Add(CreateTreeListItem(reading.Label, reading.ValueText, minText, maxText, reading.IconKey, String.Empty, level + 1, False, False))
        Next
    End Sub

    Private Function CreateTreeListItem(label As String, valueText As String, minText As String, maxText As String, iconKey As String, key As String, level As Integer, isGroup As Boolean, isExpanded As Boolean) As ListViewItem
        Dim item As New ListViewItem(label)
        item.SubItems.Add(valueText)
        item.SubItems.Add(minText)
        item.SubItems.Add(maxText)
        ApplyIconToItem(item, iconKey)
        item.Tag = New UiThemeManager.TreeListItemInfo(key, level, isGroup, isExpanded)
        Return item
    End Function

    Private Function GetTreeListExpanded(key As String, defaultExpanded As Boolean) As Boolean
        If String.IsNullOrWhiteSpace(key) Then
            Return defaultExpanded
        End If

        Dim expanded As Boolean
        If _treeListExpanded.TryGetValue(key, expanded) Then
            Return expanded
        End If

        _treeListExpanded(key) = defaultExpanded
        Return defaultExpanded
    End Function

    Private Function ExtractHardwarePrefix(label As String) As String
        If String.IsNullOrWhiteSpace(label) Then
            Return String.Empty
        End If

        Dim index As Integer = label.IndexOf(":"c)
        If index <= 0 Then
            Return String.Empty
        End If

        Return label.Substring(0, index).Trim()
    End Function

    Private Function NormalizeHardwareGroupName(prefix As String) As String
        If String.IsNullOrWhiteSpace(prefix) Then
            Return String.Empty
        End If

        Dim lower As String = prefix.Trim().ToLowerInvariant()
        If lower.StartsWith("gpu") Then
            Return "GPU"
        End If
        If lower.StartsWith("cpu") Then
            Return "CPU"
        End If
        If lower.StartsWith("motherboard") Then
            Return "Motherboard"
        End If
        If lower.StartsWith("memory") Then
            Return "Memory"
        End If
        If lower.StartsWith("storage") Then
            Return "Storage"
        End If
        If lower.StartsWith("embeddedcontroller") OrElse lower.StartsWith("superio") OrElse lower.StartsWith("controller") Then
            Return "Controller"
        End If

        Return Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(lower)
    End Function

    Private Function GetGroupIconKey(groupName As String) As String
        If String.IsNullOrWhiteSpace(groupName) Then
            Return IconDefault
        End If

        Select Case groupName.Trim().ToLowerInvariant()
            Case "cpu"
                Return IconCpu
            Case "gpu"
                Return IconGpu
            Case "motherboard"
                Return IconMotherboard
            Case "memory"
                Return IconMemory
            Case "storage"
                Return IconStorage
            Case "controller"
                Return IconController
            Case Else
                Return IconDefault
        End Select
    End Function

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

    ' CPU polling callback: collects sensor data, updates UI, and checks auto-stop conditions.
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
            Dim cpuCoreClocks As New List(Of Single)()
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
                CollectTemperatureReadings(readings, samples, cpuTemps, throttleIndicators, cpuCoreClocks)
                LogSensorAccessWarningIfNeeded(readings, cpuCoreClocks)

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

            Dim avgCoreClock As Single = Single.NaN
            Dim peakCoreClock As Single = Single.NaN
            Dim dropPercent As Single = Single.NaN
            Dim throttleDetail As String = String.Empty
            Dim throttleActive As Boolean = False

            If cpuCoreClocks.Count > 0 Then
                avgCoreClock = ComputeAverage(cpuCoreClocks)
                throttleActive = EvaluateHeuristicThrottle(avgCoreClock, _lastMaxTemp, _lastCpuUsage, throttleDetail)
                peakCoreClock = _throttlePeakClockMHz
                dropPercent = ComputeClockDropPercent(avgCoreClock, peakCoreClock)
            End If

            AppendThrottleHeuristicReadings(readings, avgCoreClock, peakCoreClock, dropPercent, _lastCpuUsage, _lastMaxTemp, throttleActive)
            If shouldCheckThrottle AndAlso throttleIndicators IsNot Nothing AndAlso throttleActive AndAlso Not String.IsNullOrWhiteSpace(throttleDetail) Then
                throttleIndicators.Add(throttleDetail)
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
        If _uiLayoutMissing Then
            SaveUiLayout()
            _uiLayoutMissing = False
        End If
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

            initForm.SetStatus("Checking privileges...")
            Dim isElevated As Boolean = IsAdmin()
            If Not isElevated Then
                If IsUserAdmin() Then
                    initForm.AddDetail("Admin account detected. Restarting elevated.")
                    If TryRestartElevated() Then
                        Application.Exit()
                        Return
                    End If
                    initForm.AddDetail("Elevation canceled. Continuing without admin privileges.")
                    LogMessage("Elevation canceled; running with limited sensor access.")
                Else
                    initForm.AddDetail("Admin privileges not available. Continuing with limited sensor access.")
                    LogMessage("Admin privileges not available; running with limited sensor access.")
                End If
            Else
                initForm.AddDetail("Admin privileges detected.")
            End If
            initForm.SetStatus("Checking hardware driver...")
            initForm.AddDetail("Validating PawnIO installation.")
            EnsurePawnIoInstalled(initForm)
            If lstvCoreTemps Is Nothing Then
                System.Diagnostics.Debug.WriteLine("!!! ERROR: lstvCoreTemps is Nothing immediately before SetDoubleBuffered call in frmMain_Load !!!")
            End If

            initForm.SetStatus("Preparing UI...")
            initForm.AddDetail("Enabling double buffering.")
            SetDoubleBuffered(lstvCoreTemps)
            initForm.AddDetail("Loading sensor icons.")
            InitializeSensorIcons()
            InitializeTreeListView()
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

            Me.Text = "ClawHammer " & GetAppVersionDisplay() & " - [Idle]"
            NumThreads.Maximum = Environment.ProcessorCount
            NumThreads.Value = Environment.ProcessorCount
            CmbThreadPriority.Text = "Normal"
            lblProcessorCount.Text = Environment.ProcessorCount.ToString + " Hardware Threads"
            lblcores.Text = coreCount & " Physical Cores"

            initForm.SetStatus("Loading stress plugins...")
            initForm.AddDetail("Applying pending plugin installs.")
            PluginInstallManager.ApplyPending(AddressOf LogMessage)

            initForm.AddDetail("Loading stress workloads.")
            Dim pluginSettings As PluginSettings = PluginSettingsStore.LoadSettings()
            Dim disabledIds As New HashSet(Of String)(pluginSettings.DisabledPluginIds, StringComparer.OrdinalIgnoreCase)
            _pluginRegistry.LoadPlugins(AddressOf LogMessage, disabledIds)
            If _pluginRegistry.GetPlugins().Count = 0 Then
                initForm.AddDetail("No stress plugins detected.")
            End If
            cmbStressType.Items.Clear()
            UpdateStressModeList(DefaultPluginIds.FloatingPoint)

            initForm.SetStatus("Loading profiles...")
            initForm.AddDetail("Loading saved profiles.")
            InitializeProfiles()
            initForm.AddDetail($"Active profile: {_currentProfileName}.")
            ClearValidationStatus()
            ClearWorkerAffinityMap()

            InitializeToolTips()

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

    Private Sub HandleValidationStatusMessage(rawMessage As String)
        If String.IsNullOrWhiteSpace(rawMessage) Then
            Return
        End If

        Dim parts As String() = rawMessage.Split("|"c, 4)
        If parts.Length = 4 AndAlso String.Equals(parts(0), "STATUS", StringComparison.OrdinalIgnoreCase) Then
            Dim workerId As Integer
            If Integer.TryParse(parts(1), workerId) Then
                Dim kernel As String = parts(2)
                Dim detail As String = parts(3)
                Dim affinityLabel As String = GetWorkerAffinityLabel(workerId)
                Dim snapshot As List(Of ValidationStatusSnapshot) = Nothing

                SyncLock _validationStatusLock
                    _validationStatusByWorker(workerId) = New ValidationStatusSnapshot With {
                        .WorkerId = workerId,
                        .Kernel = kernel,
                        .AffinityLabel = affinityLabel,
                        .Detail = detail,
                        .UpdatedUtc = DateTime.UtcNow
                    }

                    snapshot = New List(Of ValidationStatusSnapshot)(_validationStatusByWorker.Count)
                    For Each entry As ValidationStatusSnapshot In _validationStatusByWorker.Values
                        snapshot.Add(New ValidationStatusSnapshot With {
                            .WorkerId = entry.WorkerId,
                            .Kernel = entry.Kernel,
                            .AffinityLabel = entry.AffinityLabel,
                            .Detail = entry.Detail,
                            .UpdatedUtc = entry.UpdatedUtc
                        })
                    Next
                End SyncLock

                snapshot.Sort(Function(a, b) a.WorkerId.CompareTo(b.WorkerId))
                UpdateValidationDisplay(snapshot)
                Return
            End If
        End If

        LogMessage(rawMessage)
    End Sub

    Private Sub ClearValidationStatus()
        SyncLock _validationStatusLock
            _validationStatusByWorker.Clear()
        End SyncLock

        _validationSummarySuffix = String.Empty
        UpdateValidationDisplay(New List(Of ValidationStatusSnapshot)())
    End Sub

    Private Function GetValidationStatusSnapshot() As List(Of ValidationStatusSnapshot)
        Dim snapshot As New List(Of ValidationStatusSnapshot)()
        SyncLock _validationStatusLock
            For Each entry As ValidationStatusSnapshot In _validationStatusByWorker.Values
                snapshot.Add(New ValidationStatusSnapshot With {
                    .WorkerId = entry.WorkerId,
                    .Kernel = entry.Kernel,
                    .AffinityLabel = entry.AffinityLabel,
                    .Detail = entry.Detail,
                    .UpdatedUtc = entry.UpdatedUtc
                })
            Next
        End SyncLock
        snapshot.Sort(Function(a, b) a.WorkerId.CompareTo(b.WorkerId))
        Return snapshot
    End Function

    Private Sub UpdateValidationDisplay(Optional snapshot As List(Of ValidationStatusSnapshot) = Nothing)
        Dim entries As List(Of ValidationStatusSnapshot) = If(snapshot, GetValidationStatusSnapshot())
        Dim workerCount As Integer = If(entries IsNot Nothing, entries.Count, 0)

        Dim mode As ValidationMode = ValidationMode.Off
        Dim isRunning As Boolean = btnStart IsNot Nothing AndAlso btnStart.Text = "Stop"
        If isRunning AndAlso _validationSettings IsNot Nothing Then
            mode = _validationSettings.Mode
        Else
            mode = _runOptions.ValidationMode
        End If

        If mode = ValidationMode.Off OrElse workerCount = 0 Then
            _validationSummarySuffix = String.Empty
        Else
            _validationSummarySuffix = $"Workers: {workerCount}"
        End If

        Dim tooltip As String = BuildValidationTooltip(entries, mode)

        Dim updateAction As Action = Sub()
                                         UpdateValidationStatusText()
                                         If lblValidation IsNot Nothing Then
                                             lblValidation.ToolTipText = tooltip
                                         End If
                                         If _validationMonitor IsNot Nothing AndAlso Not _validationMonitor.IsDisposed Then
                                             _validationMonitor.UpdateStatuses(entries)
                                         End If
                                     End Sub

        If Me.InvokeRequired Then
            Me.BeginInvoke(updateAction)
        Else
            updateAction()
        End If
    End Sub

    Private Function BuildValidationTooltip(entries As List(Of ValidationStatusSnapshot), mode As ValidationMode) As String
        If mode = ValidationMode.Off Then
            Return "Validation is disabled."
        End If
        If entries Is Nothing OrElse entries.Count = 0 Then
            Return "Validation is running; awaiting status updates."
        End If

        Dim lines As New List(Of String)()
        Dim maxLines As Integer = 8
        Dim count As Integer = Math.Min(maxLines, entries.Count)
        For i As Integer = 0 To count - 1
            Dim entry As ValidationStatusSnapshot = entries(i)
            Dim timeText As String = entry.UpdatedUtc.ToLocalTime().ToString("HH:mm:ss")
            Dim affinityText As String = If(String.IsNullOrWhiteSpace(entry.AffinityLabel), String.Empty, $" {entry.AffinityLabel}")
            lines.Add($"W{entry.WorkerId}{affinityText} {entry.Kernel}: {entry.Detail} ({timeText})")
        Next
        If entries.Count > maxLines Then
            lines.Add($"+{entries.Count - maxLines} more...")
        End If

        Return String.Join(Environment.NewLine, lines)
    End Function

    Private Sub InitializeToolTips()
        If _toolTipMain Is Nothing Then
            _toolTipMain = New ToolTip() With {
                .AutoPopDelay = 12000,
                .InitialDelay = 500,
                .ReshowDelay = 150,
                .ShowAlways = True
            }
        End If

        Dim tip As ToolTip = _toolTipMain
        tip.SetToolTip(grpClawHammer, "Configure workload, threads, and run settings.")
        tip.SetToolTip(NumThreads, "Number of worker threads for the stress test." & vbCrLf & "Higher values use more CPU.")
        tip.SetToolTip(CmbThreadPriority, "Windows priority for worker threads." & vbCrLf & "Higher can affect system responsiveness.")
        tip.SetToolTip(cmbStressType, "Select the workload type to run." & vbCrLf & "Includes all installed plugins.")
        tip.SetToolTip(cmbProfiles, "Saved profiles for quick setup." & vbCrLf & "Use File to save or load profiles.")
        tip.SetToolTip(chkSaveLog, "Save the session log on exit." & vbCrLf & "Stored alongside the executable.")
        tip.SetToolTip(btnStart, "Start or stop the stress test.")
        tip.SetToolTip(lblThroughput, "Estimated operations per second from worker threads.")
        tip.SetToolTip(rhtxtlog, "Live event log." & vbCrLf & "Validation details are shown in the Validation Monitor.")
        tip.SetToolTip(lstvCoreTemps, "Sensor tree with current, min, and max readings.")

        clawMenu.ShowItemToolTips = True
        FileToolStripMenuItem.ToolTipText = "File actions."
        ExitToolStripMenuItem.ToolTipText = "Exit the application."
        ViewToolStripMenuItem.ToolTipText = "Monitoring views and snapshots."
        SettingsToolStripMenuItem.ToolTipText = "Run options and CPU affinity."
        ToolsToolStripMenuItem.ToolTipText = "Plugin management."
        RunOptionsToolStripMenuItem.ToolTipText = "Configure run behavior, validation, telemetry, and advanced modes."
        PluginManagerToolStripMenuItem.ToolTipText = "Manage installed and online stress test plugins."
        TemperaturePlotToolStripMenuItem.ToolTipText = "Open the live temperature plot window."
        CoreAffinityToolStripMenuItem.ToolTipText = "Choose CPU cores for worker threads."
        SaveProfileToolStripMenuItem.ToolTipText = "Save current settings to a profile."
        LoadProfileToolStripMenuItem.ToolTipText = "Load a saved profile."
        SystemInfoToolStripMenuItem.ToolTipText = "Log system info to the output window."
        ValidationMonitorToolStripMenuItem.ToolTipText = "View live validation status per worker."
        ExportReportToolStripMenuItem.ToolTipText = "Export run settings and log to an HTML report."
        HelpToolStripMenuItem.ToolTipText = "Help, updates, and about."
        CheckForUpdatesToolStripMenuItem.ToolTipText = "Check GitHub for new ClawHammer releases."
        AboutToolStripMenuItem.ToolTipText = "View version and license information."

        StStatus.ShowItemToolTips = True
        lblcores.ToolTipText = "Detected physical CPU cores."
        lblProcessorCount.ToolTipText = "Detected logical hardware threads."
        LblActiveThreads.ToolTipText = "Active stress worker threads."
        cputemp.ToolTipText = "Average CPU temperature summary."
        lblusage.ToolTipText = "Current CPU usage."
        lblValidation.ToolTipText = "Validation summary. Open the Validation Monitor for details."
        progCPUUsage.ToolTipText = "CPU usage meter."
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
        If _runOptions.ValidationMode <> ValidationMode.Off Then extras.Add($"Validation:{_runOptions.ValidationMode}")

        Dim extraText As String = If(extras.Count > 0, " [" & String.Join(", ", extras) & "]", String.Empty)
        Dim title As String = $"ClawHammer {GetAppVersionDisplay()} - {effectiveStatus}{extraText}"
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

    Private Sub UpdateValidationStatusText()
        Dim mode As ValidationMode = ValidationMode.Off
        Dim errors As Integer = 0
        Dim isRunning As Boolean = btnStart IsNot Nothing AndAlso btnStart.Text = "Stop"
        If isRunning AndAlso _validationSettings IsNot Nothing Then
            mode = _validationSettings.Mode
            errors = _validationSettings.ErrorCount
        Else
            mode = _runOptions.ValidationMode
        End If
        Dim baseText As String
        If mode = ValidationMode.Off Then
            _validationSummarySuffix = String.Empty
            baseText = "Validation: Off"
        ElseIf errors > 0 Then
            baseText = $"Validation: {mode} (Errors: {errors})"
        Else
            baseText = $"Validation: {mode}"
        End If

        If Not String.IsNullOrWhiteSpace(_validationSummarySuffix) Then
            baseText &= " | " & _validationSummarySuffix
        End If
        _validationStatusText = baseText

        If lblValidation Is Nothing Then
            Return
        End If

        Dim parent As Control = lblValidation.GetCurrentParent()
        If parent IsNot Nothing AndAlso parent.InvokeRequired Then
            parent.BeginInvoke(Sub() lblValidation.Text = _validationStatusText)
        Else
            lblValidation.Text = _validationStatusText
        End If
    End Sub

    ' Walk the hardware tree and gather temperature + supporting sensor data.
    Private Sub CollectTemperatureReadings(readings As List(Of SensorReading), samples As List(Of TempSensorSample), cpuTemps As List(Of CpuTempReading), throttleIndicators As List(Of String), cpuCoreClocks As List(Of Single))
        For Each hw As IHardware In _computer.Hardware
            AddHardwareTemperatureReadings(hw, readings, samples, cpuTemps, throttleIndicators, cpuCoreClocks, hw.HardwareType = HardwareType.Cpu, Nothing)
        Next
    End Sub


    Private Sub LogSensorAccessWarningIfNeeded(readings As List(Of SensorReading), cpuCoreClocks As List(Of Single))
        If readings Is Nothing OrElse readings.Count = 0 Then
            Return
        End If

        If Interlocked.CompareExchange(_sensorAccessWarningLogged, 0, 0) <> 0 Then
            Return
        End If

        Dim hasClockSensors As Boolean = readings.Any(Function(reading) reading.Label IsNot Nothing AndAlso reading.Label.StartsWith("CPU Clock", StringComparison.OrdinalIgnoreCase))
        Dim hasVoltageSensors As Boolean = readings.Any(Function(reading) reading.Label IsNot Nothing AndAlso reading.Label.StartsWith("CPU Voltage", StringComparison.OrdinalIgnoreCase))
        Dim hasClockValues As Boolean = cpuCoreClocks IsNot Nothing AndAlso cpuCoreClocks.Count > 0
        Dim hasVoltageValues As Boolean = readings.Any(Function(reading) reading.Label IsNot Nothing AndAlso reading.Label.StartsWith("CPU Voltage", StringComparison.OrdinalIgnoreCase) AndAlso reading.ValueRaw.HasValue)

        If (hasClockSensors AndAlso Not hasClockValues) OrElse (hasVoltageSensors AndAlso Not hasVoltageValues) Then
            If Interlocked.Exchange(_sensorAccessWarningLogged, 1) = 1 Then
                Return
            End If

            LogMessage("Hardware access warning: some CPU sensors are unavailable (N/A).")
            For Each line As String In GetHardwareMonitorStatusLines()
                LogMessage(line)
            Next
        End If
    End Sub

    Private Function GetHardwareMonitorStatusLines() As List(Of String)
        Dim lines As New List(Of String)()
        lines.Add($"LibreHardwareMonitor: {GetLibreHardwareMonitorVersionText()}")
        lines.Add($"Process elevated: {If(IsAdmin(), "Yes", "No")}")

        Try
            Dim installed As Boolean = LibreHardwareMonitor.PawnIo.PawnIo.IsInstalled
            Dim version As Version = LibreHardwareMonitor.PawnIo.PawnIo.Version

            lines.Add($"PawnIO installed: {installed}")
            If installed AndAlso version IsNot Nothing Then
                lines.Add($"PawnIO version: {version}")
            ElseIf Not installed Then
                lines.Add("Note: PawnIO not installed; some sensors may read N/A.")
            End If
        Catch ex As Exception
            lines.Add("PawnIO status unavailable: " & ex.Message)
        End Try

        Return lines
    End Function

    Private Function GetLibreHardwareMonitorVersionText() As String
        Try
            Dim version As Version = GetType(Computer).Assembly.GetName().Version
            If version IsNot Nothing Then
                Return version.ToString()
            End If
        Catch
        End Try

        Return "Unknown"
    End Function

    Private Sub EnsurePawnIoInstalled(initForm As frmInit)
        Dim installed As Boolean = False
        Dim version As Version = Nothing

        Try
            installed = LibreHardwareMonitor.PawnIo.PawnIo.IsInstalled
            version = LibreHardwareMonitor.PawnIo.PawnIo.Version
        Catch ex As Exception
            LogMessage("PawnIO check failed: " & ex.Message)
            Return
        End Try

        If installed AndAlso version IsNot Nothing AndAlso version < PawnIoMinVersion Then
            initForm?.AddDetail($"PawnIO {version} detected; update required.")
            If Not PromptPawnIoInstall($"PawnIO is outdated (found {version}). Install update now?") Then
                LogMessage("PawnIO update skipped; some sensors may be unavailable.")
                Return
            End If
            RunPawnIoInstaller(initForm)
        ElseIf Not installed Then
            initForm?.AddDetail("PawnIO not installed.")
            If Not PromptPawnIoInstall("PawnIO is not installed. Install it now?") Then
                LogMessage("PawnIO not installed; some sensors may be unavailable.")
                Return
            End If
            RunPawnIoInstaller(initForm)
        End If
    End Sub

    Private Function PromptPawnIoInstall(message As String) As Boolean
        Dim detail As String = message & vbCrLf & vbCrLf &
            "ClawHammer uses the open-source PawnIO driver to access hardware sensors (CPU clocks, voltages, and temps)." & vbCrLf &
            "It runs in kernel mode like other sensor drivers."
        Dim result As DialogResult = MessageBox.Show(Me, detail, "ClawHammer", MessageBoxButtons.OKCancel, MessageBoxIcon.Information)
        Return result = DialogResult.OK
    End Function

    Private Sub RunPawnIoInstaller(initForm As frmInit)
        If Not IsAdmin() Then
            LogMessage("PawnIO install requires administrator privileges.")
            initForm?.AddDetail("PawnIO install requires administrator privileges.")
            MessageBox.Show(Me, "Installing PawnIO requires administrator privileges. Please restart ClawHammer as administrator.", "ClawHammer", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        Dim installerPath As String = ExtractPawnIoSetup()
        If String.IsNullOrWhiteSpace(installerPath) Then
            LogMessage("PawnIO installer not found.")
            initForm?.AddDetail("PawnIO installer not found.")
            Return
        End If

        Try
            initForm?.AddDetail("Running PawnIO installer.")
            Dim psi As New ProcessStartInfo(installerPath, "-install") With {
                .UseShellExecute = True,
                .Verb = "runas"
            }
            Dim proc As Process = Process.Start(psi)
            If proc IsNot Nothing Then
                proc.WaitForExit()
                proc.Dispose()
            End If
            initForm?.AddDetail("PawnIO installer finished.")
        Catch ex As Exception
            LogMessage("PawnIO install failed: " & ex.Message)
        Finally
            Try
                File.Delete(installerPath)
            Catch
            End Try
        End Try
    End Sub

    Private Function ExtractPawnIoSetup() As String
        Dim tempPath As String = Path.Combine(Path.GetTempPath(), PawnIoSetupFileName)
        Try
            If File.Exists(tempPath) Then
                File.Delete(tempPath)
            End If
        Catch
        End Try

        Dim localPath As String = Path.Combine(AppContext.BaseDirectory, PawnIoSetupFileName)
        If File.Exists(localPath) Then
            Try
                File.Copy(localPath, tempPath, True)
                Return tempPath
            Catch
            End Try
        End If

        Dim asm As Assembly = GetType(frmMain).Assembly
        Dim resourceName As String = FindPawnIoResourceName(asm)
        If String.IsNullOrWhiteSpace(resourceName) Then
            Return Nothing
        End If

        Try
            Using stream As Stream = asm.GetManifestResourceStream(resourceName)
                If stream Is Nothing Then
                    Return Nothing
                End If

                Using fileStream As New FileStream(tempPath, FileMode.Create, FileAccess.Write)
                    stream.CopyTo(fileStream)
                End Using
            End Using

            Return tempPath
        Catch
            Return Nothing
        End Try
    End Function

    Private Function FindPawnIoResourceName(asm As Assembly) As String
        If asm Is Nothing Then
            Return Nothing
        End If

        For Each name As String In asm.GetManifestResourceNames()
            If name.EndsWith(PawnIoSetupFileName, StringComparison.OrdinalIgnoreCase) Then
                Return name
            End If
        Next

        Return Nothing
    End Function

    ' Flatten hardware sensors into display rows while tracking CPU branches for extra metrics.
    Private Sub AddHardwareTemperatureReadings(hw As IHardware, readings As List(Of SensorReading), samples As List(Of TempSensorSample), cpuTemps As List(Of CpuTempReading), throttleIndicators As List(Of String), cpuCoreClocks As List(Of Single), isCpuBranch As Boolean, parentLabel As String)
        hw.Update()
        Dim baseLabel As String = BuildHardwareLabel(hw, parentLabel)
        Dim iconKey As String = GetSensorIconKey(hw.HardwareType, isCpuBranch)

        For Each sensor As ISensor In hw.Sensors
            If isCpuBranch Then
                ProcessThrottleSensor(sensor, readings, throttleIndicators, iconKey)
                ProcessCpuClockVoltageSensor(sensor, readings, cpuCoreClocks, iconKey)
            End If

            If sensor.SensorType <> SensorType.Temperature Then
                Continue For
            End If

            Dim label As String = $"{baseLabel} - {sensor.Name}"
            Dim hasValue As Boolean = sensor.Value.HasValue
            Dim sampleValue As Single = 0

            If hasValue Then
                sampleValue = sensor.Value.Value
                If isCpuBranch AndAlso cpuTemps IsNot Nothing Then
                    Dim kind As CpuTempKind = ClassifyCpuTempSensor(sensor.Name)
                    If kind <> CpuTempKind.Excluded Then
                        cpuTemps.Add(New CpuTempReading(sensor.Name, sampleValue, kind))
                    End If
                End If
            End If

            readings.Add(CreateSensorReading(label, IconTemperature, If(hasValue, CType(sampleValue, Nullable(Of Single)), Nothing), SensorValueFormat.Celsius))
            If samples IsNot Nothing Then
                samples.Add(New TempSensorSample(label, sampleValue, hasValue))
            End If
        Next

        For Each subHardware As IHardware In hw.SubHardware
            AddHardwareTemperatureReadings(subHardware, readings, samples, cpuTemps, throttleIndicators, cpuCoreClocks, isCpuBranch OrElse hw.HardwareType = HardwareType.Cpu, baseLabel)
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

    ' Extract throttle-related sensor values and emit diagnostics.
    Private Sub ProcessThrottleSensor(sensor As ISensor, readings As List(Of SensorReading), throttleIndicators As List(Of String), iconKey As String)
        If sensor Is Nothing Then
            Return
        End If

        Dim name As String = sensor.Name
        If String.IsNullOrWhiteSpace(name) Then
            Return
        End If

        Dim nameLower As String = name.Trim().ToLowerInvariant()
        Dim matchKind As ThrottleMatchKind = GetThrottleMatchKind(nameLower, sensor.SensorType)
        If matchKind = ThrottleMatchKind.None Then
            Return
        End If

        If readings IsNot Nothing Then
            Dim valueRaw As Nullable(Of Single) = Nothing
            Dim valueFormat As SensorValueFormat = SensorValueFormat.None
            Dim valueText As String = GetThrottleValueInfo(sensor, valueRaw, valueFormat)
            readings.Add(New SensorReading($"CPU Throttle - {name}", valueText, IconThrottle, valueRaw, valueFormat))
        End If

        If matchKind = ThrottleMatchKind.Trigger AndAlso throttleIndicators IsNot Nothing AndAlso sensor.Value.HasValue Then
            Dim value As Single = sensor.Value.Value
            If IsThrottleActive(sensor.SensorType, value, nameLower) Then
                throttleIndicators.Add(name)
            End If
        End If
    End Sub

    ' Capture per-core clock and voltage readings for display and heuristics.
    Private Sub ProcessCpuClockVoltageSensor(sensor As ISensor, readings As List(Of SensorReading), cpuCoreClocks As List(Of Single), iconKey As String)
        If sensor Is Nothing Then
            Return
        End If

        Dim name As String = sensor.Name
        If String.IsNullOrWhiteSpace(name) Then
            Return
        End If

        Dim nameLower As String = name.Trim().ToLowerInvariant()

        Select Case sensor.SensorType
            Case SensorType.Clock
                If Not IsCpuCoreClockSensor(nameLower) Then
                    Return
                End If

                Dim valueRaw As Nullable(Of Single) = Nothing
                Dim valueText As String = "N/A"
                If sensor.Value.HasValue Then
                    Dim value As Single = sensor.Value.Value
                    valueText = FormatSensorValue(value, SensorValueFormat.Mhz)
                    If value > 0 Then
                        cpuCoreClocks?.Add(value)
                        valueRaw = value
                    End If
                End If

                readings?.Add(New SensorReading($"CPU Clock - {name}", valueText, IconClock, valueRaw, SensorValueFormat.Mhz))

            Case SensorType.Voltage
                If Not IsCpuCoreVoltageSensor(nameLower) Then
                    Return
                End If

                Dim valueRaw As Nullable(Of Single) = Nothing
                Dim valueText As String = "N/A"
                If sensor.Value.HasValue Then
                    Dim value As Single = sensor.Value.Value
                    valueText = FormatSensorValue(value, SensorValueFormat.Volt)
                    valueRaw = value
                End If

                readings?.Add(New SensorReading($"CPU Voltage - {name}", valueText, IconVoltage, valueRaw, SensorValueFormat.Volt))
        End Select
    End Sub

    Private Function FormatThrottleValue(sensor As ISensor) As String
        Dim raw As Nullable(Of Single) = Nothing
        Dim format As SensorValueFormat = SensorValueFormat.None
        Return GetThrottleValueInfo(sensor, raw, format)
    End Function

    Private Function GetThrottleValueInfo(sensor As ISensor, ByRef valueRaw As Nullable(Of Single), ByRef valueFormat As SensorValueFormat) As String
        valueRaw = Nothing
        valueFormat = SensorValueFormat.None
        If sensor Is Nothing OrElse Not sensor.Value.HasValue Then
            Return "N/A"
        End If

        Dim value As Single = sensor.Value.Value
        Select Case sensor.SensorType
            Case SensorType.Control
                Return If(value > 0.5F, "Active", "Inactive")
            Case SensorType.Level, SensorType.Load, SensorType.Factor
                Dim percent As Single = If(value <= 1.0F, value * 100.0F, value)
                valueRaw = percent
                valueFormat = SensorValueFormat.Percent
                Return percent.ToString("F0", CultureInfo.InvariantCulture) & "%"
            Case SensorType.Power
                valueRaw = value
                valueFormat = SensorValueFormat.Watt
                Return value.ToString("F1", CultureInfo.InvariantCulture) & " W"
            Case SensorType.Clock
                valueRaw = value
                valueFormat = SensorValueFormat.Mhz
                Return value.ToString("F0", CultureInfo.InvariantCulture) & " MHz"
            Case SensorType.Voltage
                valueRaw = value
                valueFormat = SensorValueFormat.Volt
                Return value.ToString("F3", CultureInfo.InvariantCulture) & " V"
            Case SensorType.Temperature
                valueRaw = value
                valueFormat = SensorValueFormat.Celsius
                Return FormatCelsius(value)
            Case Else
                valueRaw = value
                valueFormat = SensorValueFormat.Number
                Return value.ToString("F2", CultureInfo.InvariantCulture)
        End Select
    End Function

    Private Function GetThrottleMatchKind(nameLower As String, sensorType As SensorType) As ThrottleMatchKind
        If String.IsNullOrWhiteSpace(nameLower) Then
            Return ThrottleMatchKind.None
        End If

        If nameLower.Contains("throttl") OrElse nameLower.Contains("prochot") OrElse nameLower.Contains("limit exceeded") OrElse nameLower.Contains("exceeded") Then
            Return ThrottleMatchKind.Trigger
        End If

        If nameLower.Contains("thermal limit") Then
            Return If(IsThrottleBooleanType(sensorType), ThrottleMatchKind.Trigger, ThrottleMatchKind.Info)
        End If

        If nameLower.Contains("power limit") OrElse nameLower.Contains("current limit") OrElse nameLower.Contains("edp") OrElse nameLower.Contains("ppt") OrElse nameLower.Contains("tdc") OrElse nameLower.Contains("edc") OrElse nameLower.Contains("pl1") OrElse nameLower.Contains("pl2") Then
            Return ThrottleMatchKind.Info
        End If

        Return ThrottleMatchKind.None
    End Function

    Private Function IsThrottleActive(sensorType As SensorType, value As Single, nameLower As String) As Boolean
        Select Case sensorType
            Case SensorType.Control, SensorType.Level, SensorType.Load, SensorType.Factor
                Return value > 0.5F
            Case Else
                If IsThrottleBooleanName(nameLower) Then
                    Return value > 0.5F
                End If
        End Select
        Return False
    End Function

    Private Function IsThrottleBooleanType(sensorType As SensorType) As Boolean
        Return sensorType = SensorType.Control OrElse sensorType = SensorType.Level OrElse sensorType = SensorType.Load OrElse sensorType = SensorType.Factor
    End Function

    Private Function IsThrottleBooleanName(nameLower As String) As Boolean
        Return nameLower.Contains("throttl") OrElse nameLower.Contains("exceed") OrElse nameLower.Contains("prochot")
    End Function

    Private Function IsCpuCoreClockSensor(nameLower As String) As Boolean
        If String.IsNullOrWhiteSpace(nameLower) Then
            Return False
        End If
        If nameLower.Contains("bus") OrElse nameLower.Contains("bclk") OrElse nameLower.Contains("uncore") OrElse nameLower.Contains("ring") OrElse nameLower.Contains("cache") Then
            Return False
        End If
        Return IsCoreReadingName(nameLower)
    End Function

    Private Function IsCpuCoreVoltageSensor(nameLower As String) As Boolean
        If String.IsNullOrWhiteSpace(nameLower) Then
            Return False
        End If
        If nameLower.Contains("core") OrElse nameLower.Contains("vid") Then
            Return True
        End If
        Return False
    End Function

    Private Sub ResetThrottleHeuristic()
        _throttlePeakClockMHz = Single.NaN
        _throttleLoadedSamples = 0
        _throttleLowClockSamples = 0
        _throttleLastActive = False
        _throttleLastDetail = String.Empty
    End Sub

    Private Function GetHeuristicThrottleTempThreshold() As Single
        Select Case GetCpuVendor()
            Case CpuVendor.Amd
                Return 90.0F
            Case CpuVendor.Intel
                Return 95.0F
            Case Else
                Return 92.0F
        End Select
    End Function

    Private Function EvaluateHeuristicThrottle(avgClockMHz As Single, maxTempC As Single, cpuUsage As Integer, ByRef detail As String) As Boolean
        detail = String.Empty

        If Single.IsNaN(avgClockMHz) OrElse avgClockMHz <= 0 Then
            _throttleLowClockSamples = 0
            Return False
        End If

        Dim usageLoaded As Boolean = cpuUsage >= ThrottleMinCpuUsagePercent
        If Not usageLoaded Then
            _throttleLoadedSamples = 0
            _throttleLowClockSamples = 0
            Return False
        End If

        _throttleLoadedSamples += 1
        If Single.IsNaN(_throttlePeakClockMHz) OrElse avgClockMHz > _throttlePeakClockMHz Then
            _throttlePeakClockMHz = avgClockMHz
        End If

        Dim peakClock As Single = _throttlePeakClockMHz
        If Single.IsNaN(peakClock) OrElse peakClock <= 0 Then
            Return False
        End If

        Dim ratio As Single = avgClockMHz / peakClock
        Dim tempThreshold As Single = GetHeuristicThrottleTempThreshold()
        Dim tempHigh As Boolean = Not Single.IsNaN(maxTempC) AndAlso maxTempC >= tempThreshold
        Dim clockDropped As Boolean = ratio <= ThrottleDropRatio
        Dim severeDrop As Boolean = ratio <= ThrottleSevereDropRatio
        Dim lowSampleTarget As Integer = If(tempHigh, ThrottleMinLowSamplesHot, ThrottleMinLowSamplesSevere)

        If (clockDropped AndAlso tempHigh) OrElse severeDrop Then
            _throttleLowClockSamples += 1
        Else
            _throttleLowClockSamples = 0
        End If

        Dim active As Boolean = _throttleLoadedSamples >= ThrottleMinLoadedSamples AndAlso _throttleLowClockSamples >= lowSampleTarget
        Dim dropPercent As Single = ComputeClockDropPercent(avgClockMHz, peakClock)
        detail = $"Heuristic clock drop {dropPercent:F0}% (avg {avgClockMHz:F0} MHz, peak {peakClock:F0} MHz, load {cpuUsage}%, max {FormatCelsiusOrNa(maxTempC)})"
        _throttleLastActive = active
        _throttleLastDetail = detail
        Return active
    End Function

    Private Function ComputeClockDropPercent(avgClockMHz As Single, peakClockMHz As Single) As Single
        If Single.IsNaN(avgClockMHz) OrElse Single.IsNaN(peakClockMHz) OrElse peakClockMHz <= 0 Then
            Return Single.NaN
        End If
        Dim ratio As Single = avgClockMHz / peakClockMHz
        If ratio > 1.0F Then
            ratio = 1.0F
        ElseIf ratio < 0.0F Then
            ratio = 0.0F
        End If
        Return (1.0F - ratio) * 100.0F
    End Function

    Private Sub AppendThrottleHeuristicReadings(readings As List(Of SensorReading), avgClockMHz As Single, peakClockMHz As Single, dropPercent As Single, cpuUsage As Integer, maxTempC As Single, isActive As Boolean)
        If readings Is Nothing Then
            Return
        End If

        readings.Add(New SensorReading("CPU Throttle (Heuristic)", If(isActive, "Active", "Inactive"), IconThrottle))
        readings.Add(CreateSensorReading("CPU Throttle (Heuristic) - Avg Core Clock", IconClock, If(Single.IsNaN(avgClockMHz), CType(Nothing, Nullable(Of Single)), avgClockMHz), SensorValueFormat.Mhz))
        readings.Add(CreateSensorReading("CPU Throttle (Heuristic) - Peak Core Clock", IconClock, If(Single.IsNaN(peakClockMHz), CType(Nothing, Nullable(Of Single)), peakClockMHz), SensorValueFormat.Mhz))

        Dim dropValue As Nullable(Of Single) = If(Single.IsNaN(dropPercent), CType(Nothing, Nullable(Of Single)), dropPercent)
        readings.Add(CreateSensorReading("CPU Throttle (Heuristic) - Clock Drop", IconThrottle, dropValue, SensorValueFormat.Percent))

        Dim clampedUsage As Single = CSng(Math.Max(0, Math.Min(100, cpuUsage)))
        readings.Add(CreateSensorReading("CPU Throttle (Heuristic) - Load", IconThrottle, clampedUsage, SensorValueFormat.Percent))
        readings.Add(CreateSensorReading("CPU Throttle (Heuristic) - Max Temp", IconTemperature, If(Single.IsNaN(maxTempC), CType(Nothing, Nullable(Of Single)), maxTempC), SensorValueFormat.Celsius))
    End Sub

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



    Private Sub StartStressTest()
        btnStart.Text = "Stop"
        btnStart.Image = My.Resources._stop
        ResetThrottleHeuristic()

        SetTitleBarText("[Running]")

        NormalizeRunOptions(_runOptions)

        ' Initialize throughput tracking.
        _operationsCompleted = 0
        lblThroughput.Text = "Throughput: N/A"
        _telemetryFilePath = Nothing
        _stopwatch = System.Diagnostics.Stopwatch.StartNew()
        _throughputTimer = New System.Windows.Forms.Timer()
        _throughputTimer.Interval = 1000
        AddHandler _throughputTimer.Tick, AddressOf _throughputTimer_Tick
        _throughputTimer.Start()

        Dim selectedPluginId As String = GetSelectedStressPluginId()
        Dim selectedPlugin As IStressPlugin = Nothing
        If Not _pluginRegistry.TryGetPlugin(selectedPluginId, selectedPlugin) Then
            LogMessage("Selected workload plugin unavailable. Falling back to Floating Point.")
            selectedPluginId = DefaultPluginIds.FloatingPoint
            SetSelectedStressPlugin(selectedPluginId)
            _pluginRegistry.TryGetPlugin(selectedPluginId, selectedPlugin)
        End If

        LogMessage($"Starting {GetStressModeLabel(selectedPluginId)} Stress Test...")

        cts = New Threading.CancellationTokenSource()
        Dim token As Threading.CancellationToken = cts.Token
        ThreadsArray.Clear()

        _pluginRegistry.PrimeRangeMin = MinValuePrime
        _pluginRegistry.PrimeRangeMax = MaxValuePrime
        StartValidation(True)
        Dim validationSettings As ValidationSettings = _validationSettings
        Dim reportStatus As Action(Of String) = Sub(message)
                                                    HandleValidationStatusMessage(message)
                                                End Sub

        ClearValidationStatus()
        ClearWorkerAffinityMap()
        Dim topology As CpuTopologySnapshot = GetCpuTopology()

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

        Dim threadCount As Integer = GetEffectiveThreadCount()
        If _runOptions.UiSnappyMode AndAlso threadCount < NumThreads.Value Then
            LogMessage($"UI snappy mode enabled. Threads reduced to {threadCount}.")
        End If

        If useAffinity AndAlso affinityCores.Count > 0 AndAlso threadCount > affinityCores.Count Then
            Dim requestedThreads As Integer = threadCount
            threadCount = affinityCores.Count
            LogMessage($"Core affinity selected {affinityCores.Count} logical thread(s); limiting worker threads from {requestedThreads} to {threadCount}.")
        End If

        Dim context As StressPluginContext = _pluginRegistry.CreateContext(threadCount)
        If String.Equals(selectedPluginId, DefaultPluginIds.Avx, StringComparison.OrdinalIgnoreCase) AndAlso Not context.AvxSupported Then
            LogMessage("AVX selected but SIMD acceleration is not available. Falling back to Floating Point.")
            selectedPluginId = DefaultPluginIds.FloatingPoint
            SetSelectedStressPlugin(selectedPluginId)
            _pluginRegistry.TryGetPlugin(selectedPluginId, selectedPlugin)
        End If

        If selectedPlugin Is Nothing Then
            StopStressTest("No stress plugin available to start the run.")
            Return
        End If

        Dim baseSeed As ULong = BitConverter.ToUInt64(BitConverter.GetBytes(Environment.TickCount64), 0) Xor &H9E3779B97F4A7C15UL

        For i = 0 To threadCount - 1
            Dim reportProgressAction As Action(Of Integer) = Sub(ops) Interlocked.Add(_operationsCompleted, ops)

            Dim workerId As Integer = i
            Dim coreIndex As Integer? = Nothing
            If useAffinity Then
                coreIndex = affinityCores(i Mod affinityCores.Count)
            End If

            SetWorkerAffinity(workerId, coreIndex, topology)

            Dim indexSeed As ULong = CULng(i)
            Dim workerSeed As ULong = baseSeed Xor (indexSeed << 1) Xor (indexSeed << 33) Xor (indexSeed << 47)
            Dim worker As IStressWorker = selectedPlugin.CreateWorker(i, workerSeed, context)
            Dim kernelName As String = If(worker IsNot Nothing, worker.KernelName, selectedPlugin.DisplayName)
            Dim workerReportError As Action(Of String) = Sub(message)
                                                             Dim decorated As String = DecorateWorkerMessage(workerId, message)
                                                             LogMessage(decorated)
                                                             UpdateValidationDisplay()
                                                             TriggerAutoStop(decorated)
                                                         End Sub

            Dim threadStart As Threading.ThreadStart = Sub()
                                                           If coreIndex.HasValue Then
                                                               If Not ThreadAffinity.TrySetCurrentThreadAffinity(coreIndex.Value) Then
                                                                   LogMessage($"Affinity set failed for core {coreIndex.Value}.")
                                                               End If
                                                           End If
                                                           If worker IsNot Nothing Then
                                                               Dim logicalId As Integer = ThreadAffinity.GetCurrentLogicalProcessor()
                                                               Dim runningLabel As String = BuildAffinityLabel(logicalId, topology)
                                                               LogMessage($"Thread Started ({kernelName}) {runningLabel} [Thread ID] : {Threading.Thread.CurrentThread.ManagedThreadId}")


                                                               Try
                                                                   worker.Run(token, reportProgressAction, validationSettings, workerReportError, reportStatus)
                                                               Catch ex As OperationCanceledException
                                                               Catch ex As Exception
                                                                   Dim detail As String = ex.GetType().Name
                                                                   If Not String.IsNullOrWhiteSpace(ex.Message) Then
                                                                       detail &= $": {ex.Message}"
                                                                   End If
                                                                   Dim crashMessage As String = $"Worker crashed (W{workerId}) {runningLabel} ({kernelName}) - {detail}"
                                                                   LogMessage(crashMessage)
                                                                   HandleValidationStatusMessage($"STATUS|{workerId}|{kernelName}|Crashed: {detail}")
                                                                   TriggerAutoStop("Stopping test: worker crash detected.")
                                                               End Try

                                                           End If
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
        Next

        UpdateActiveThreadCount()
        StartTelemetry()
        StartTimedRun()
        If _runOptions.AutoShowTempPlot Then
            ShowTemperaturePlotWindow()
        End If
    End Sub

    Private Sub StopStressTest(Optional reason As String = Nothing)
        If Interlocked.Exchange(_stopInProgress, 1) = 1 Then
            Return
        End If

        Try
            ResetThrottleHeuristic()
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
            ClearValidationStatus()
            ClearWorkerAffinityMap()

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

    Private Sub StartValidation(Optional reset As Boolean = True)
        NormalizeRunOptions(_runOptions)

        If _validationSettings Is Nothing Then
            _validationSettings = New ValidationSettings()
        End If

        _validationSettings.Mode = _runOptions.ValidationMode
        _validationSettings.IntervalMs = _runOptions.ValidationIntervalMs
        _validationSettings.BatchSize = _runOptions.ValidationBatchSize
        If reset Then
            _validationSettings.Reset()
        End If
        ClearValidationStatus()

        If _runOptions.ValidationMode <> ValidationMode.Off Then
            If Me.InvokeRequired Then
                Me.BeginInvoke(Sub() ShowValidationMonitorWindow())
            Else
                ShowValidationMonitorWindow()
            End If
        End If
    End Sub
    Private Sub StopValidation()
        If _validationSettings IsNot Nothing Then
            _validationSettings.Mode = ValidationMode.Off
        End If
        ClearValidationStatus()
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
            .ValidationMode = source.ValidationMode,
            .ValidationIntervalMs = source.ValidationIntervalMs,
            .ValidationBatchSize = source.ValidationBatchSize,
            .AutoShowTempPlot = source.AutoShowTempPlot,
            .UseAffinity = source.UseAffinity,
            .AffinityCores = If(source.AffinityCores IsNot Nothing, New List(Of Integer)(source.AffinityCores), New List(Of Integer)())
        }
        NormalizeRunOptions(copy)
        Return copy
    End Function

    Private Sub NormalizeRunOptions(options As RunOptions)
        If options Is Nothing Then
            Return
        End If

        If options.ValidationMode = ValidationMode.Off AndAlso options.ValidationEnabled Then
            options.ValidationMode = ValidationMode.Light
        End If

        If options.ValidationIntervalMs <= 0 Then
            options.ValidationIntervalMs = 30000
        End If

        If options.ValidationBatchSize <= 0 Then
            options.ValidationBatchSize = 4096
        End If
    End Sub

    Private Class StressModeOption
        Public ReadOnly DisplayName As String
        Public ReadOnly Id As String
        Public ReadOnly Plugin As IStressPlugin

        Public Sub New(displayName As String, id As String, plugin As IStressPlugin)
            Me.DisplayName = displayName
            Me.Id = id
            Me.Plugin = plugin
        End Sub

        Public Overrides Function ToString() As String
            Return DisplayName
        End Function
    End Class

    Private Function TryResolveStressPluginId(value As String, ByRef resolvedId As String) As Boolean
        resolvedId = Nothing
        If String.IsNullOrWhiteSpace(value) Then
            resolvedId = DefaultPluginIds.FloatingPoint
            Return True
        End If

        Dim trimmed As String = value.Trim()
        Dim plugin As IStressPlugin = Nothing
        If _pluginRegistry IsNot Nothing AndAlso _pluginRegistry.TryGetPlugin(trimmed, plugin) Then
            resolvedId = plugin.Id
            Return True
        End If

        Select Case trimmed.ToLowerInvariant()
            Case "floatingpoint", "floating point"
                resolvedId = DefaultPluginIds.FloatingPoint
                Return True
            Case "integerprimes", "integer primes", "integer (primes)"
                resolvedId = DefaultPluginIds.IntegerPrimes
                Return True
            Case "avx", "avx (vector)", "vector"
                resolvedId = DefaultPluginIds.Avx
                Return True
            Case "mixed"
                resolvedId = DefaultPluginIds.Mixed
                Return True
            Case "blend"
                resolvedId = DefaultPluginIds.Blend
                Return True
            Case "integerheavy", "integer heavy"
                resolvedId = DefaultPluginIds.IntegerHeavy
                Return True
            Case "memorybandwidth", "memory bandwidth"
                resolvedId = DefaultPluginIds.MemoryBandwidth
                Return True
        End Select

        If _pluginRegistry IsNot Nothing Then
            For Each item As IStressPlugin In _pluginRegistry.GetPlugins()
                If String.Equals(item.DisplayName, trimmed, StringComparison.OrdinalIgnoreCase) Then
                    resolvedId = item.Id
                    Return True
                End If
            Next
        End If

        Return False
    End Function

    Private Function ResolveStressPluginId(value As String) As String
        Dim resolved As String = Nothing
        If TryResolveStressPluginId(value, resolved) Then
            Return resolved
        End If
        Return DefaultPluginIds.FloatingPoint
    End Function

    Private Function GetStressModeLabel(value As String) As String
        Dim plugin As IStressPlugin = Nothing
        Dim resolved As String = ResolveStressPluginId(value)
        If _pluginRegistry IsNot Nothing AndAlso _pluginRegistry.TryGetPlugin(resolved, plugin) Then
            Return plugin.DisplayName
        End If
        If Not String.IsNullOrWhiteSpace(value) Then
            Return value
        End If
        Return "Unknown"
    End Function

    Private Function GetSelectedStressPluginId() As String
        Dim selectedOption As StressModeOption = TryCast(cmbStressType.SelectedItem, StressModeOption)
        If selectedOption IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(selectedOption.Id) Then
            Return selectedOption.Id
        End If

        Dim raw As String = If(cmbStressType.SelectedItem?.ToString(), String.Empty)
        Return ResolveStressPluginId(raw)
    End Function

    Private Sub SetSelectedStressPlugin(value As String)
        UpdateStressModeList(value)
    End Sub

    Private Sub UpdateStressModeList(Optional selectedId As String = Nothing)
        If cmbStressType Is Nothing Then
            Return
        End If

        Dim currentId As String = ResolveStressPluginId(If(selectedId, GetSelectedStressPluginId()))

        Dim options As New List(Of StressModeOption)()
        If _pluginRegistry IsNot Nothing Then
            Dim ordered = _pluginRegistry.GetPlugins().OrderBy(Function(p) p.SortOrder).ThenBy(Function(p) p.DisplayName)
            For Each plugin As IStressPlugin In ordered
                options.Add(New StressModeOption(plugin.DisplayName, plugin.Id, plugin))
            Next
        End If

        cmbStressType.DataSource = Nothing
        cmbStressType.DataSource = options

        Dim selection As StressModeOption = options.FirstOrDefault(Function(o) String.Equals(o.Id, currentId, StringComparison.OrdinalIgnoreCase))
        If selection IsNot Nothing Then
            cmbStressType.SelectedItem = selection
        ElseIf options.Count > 0 Then
            cmbStressType.SelectedIndex = 0
        End If
    End Sub

    Private Function BuildProfileFromUi() As ProfileData
        Dim stressName As String = GetSelectedStressPluginId()

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

            Dim resolvedId As String = Nothing
            If TryResolveStressPluginId(profile.StressType, resolvedId) Then
                SetSelectedStressPlugin(resolvedId)
            ElseIf Not String.IsNullOrWhiteSpace(profile.StressType) Then
                LogMessage($"Profile workload '{profile.StressType}' is unavailable. Using Floating Point.")
                SetSelectedStressPlugin(DefaultPluginIds.FloatingPoint)
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
                NormalizeRunOptions(_runOptions)
            Else
                _runOptions = New RunOptions()
                NormalizeRunOptions(_runOptions)
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

            If _runOptions.ValidationMode <> ValidationMode.Off Then
                StartValidation(False)
            Else
                StopValidation()
            End If

            If _runOptions.UseAffinity Then
                LogMessage("Core affinity changes apply on the next run.")
            End If
        End If

        UpdateStressModeList(GetSelectedStressPluginId())
        UpdateValidationDisplay()
    End Sub

    Private Function CreateProfile(threads As Integer, stressTypeId As String, priority As String, saveLog As Boolean, options As RunOptions, plotWindowSeconds As Single) As ProfileData
        Dim profile As New ProfileData() With {
            .Threads = Math.Max(1, threads),
            .StressType = stressTypeId,
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
        NormalizeRunOptions(profile.RunOptions)
        Return profile
    End Function

    Private Function BuildBuiltInProfiles() As Dictionary(Of String, ProfileData)
        Dim profiles As New Dictionary(Of String, ProfileData)(StringComparer.OrdinalIgnoreCase)
        Dim threads As Integer = Environment.ProcessorCount

        profiles(DefaultProfileName) = CreateProfile(threads, DefaultPluginIds.FloatingPoint, "Normal", False, New RunOptions(), 120)

        profiles("OC Quick Thermal") = CreateProfile(threads, DefaultPluginIds.FloatingPoint, "Above Normal", False, New RunOptions() With {
            .TimedRunMinutes = 10,
            .AutoStopTempC = 90,
            .UiSnappyMode = True,
            .AutoShowTempPlot = True
        }, 120)

        profiles("OC Sustained Heat") = CreateProfile(threads, DefaultPluginIds.Mixed, "Normal", False, New RunOptions() With {
            .TimedRunMinutes = 60,
            .AutoStopTempC = 95,
            .UiSnappyMode = True,
            .TelemetryEnabled = True,
            .TelemetryIntervalMs = 1000,
            .ValidationMode = ValidationMode.Light,
            .AutoShowTempPlot = True
        }, 300)

        profiles("OC AVX Torture") = CreateProfile(threads, DefaultPluginIds.Avx, "Highest", False, New RunOptions() With {
            .TimedRunMinutes = 30,
            .AutoStopTempC = 90,
            .UiSnappyMode = True,
            .TelemetryEnabled = True,
            .TelemetryIntervalMs = 500,
            .AutoShowTempPlot = True
        }, 180)

        profiles("OC Mixed Stress") = CreateProfile(threads, DefaultPluginIds.Mixed, "Above Normal", False, New RunOptions() With {
            .TimedRunMinutes = 20,
            .AutoStopTempC = 92,
            .UiSnappyMode = True,
            .TelemetryEnabled = True,
            .TelemetryIntervalMs = 750,
            .AutoShowTempPlot = True
        }, 180)

        profiles("OC Integer Stability") = CreateProfile(threads, DefaultPluginIds.IntegerPrimes, "Above Normal", False, New RunOptions() With {
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
            dlg.ClientSize = New Size(420, 392)

            Dim tip As New ToolTip() With {
                .AutoPopDelay = 12000,
                .InitialDelay = 500,
                .ReshowDelay = 150,
                .ShowAlways = True
            }

            Dim leftLabel As Integer = 12
            Dim leftControl As Integer = 230
            Dim row As Integer = 12
            Dim rowHeight As Integer = 26

            Dim chkTimed As New CheckBox() With {.Text = "Timed run (minutes)", .AutoSize = True, .Location = New Point(leftLabel, row)}
            Dim numTimed As New NumericUpDown() With {.Location = New Point(leftControl, row - 2), .Minimum = 1, .Maximum = 1440, .Width = 120}
            tip.SetToolTip(chkTimed, "Stop automatically after a fixed duration.")
            tip.SetToolTip(numTimed, "Duration in minutes for the timed run.")
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
            tip.SetToolTip(chkTemp, "Stop when CPU temperature exceeds the limit.")
            tip.SetToolTip(numTemp, "Temperature threshold in Celsius.")
            chkTemp.Checked = working.AutoStopTempC > 0
            Dim tempValue As Decimal = If(working.AutoStopTempC > 0, CDec(working.AutoStopTempC), 90D)
            If tempValue < numTemp.Minimum Then tempValue = numTemp.Minimum
            If tempValue > numTemp.Maximum Then tempValue = numTemp.Maximum
            numTemp.Value = tempValue
            numTemp.Enabled = chkTemp.Checked
            AddHandler chkTemp.CheckedChanged, Sub() numTemp.Enabled = chkTemp.Checked
            row += rowHeight

            Dim chkThrottle As New CheckBox() With {.Text = "Auto-stop on CPU throttling", .AutoSize = True, .Location = New Point(leftLabel, row)}
            tip.SetToolTip(chkThrottle, "Stop if throttling is detected while running.")
            chkThrottle.Checked = working.AutoStopOnThrottle
            row += rowHeight

            Dim chkUiSnappy As New CheckBox() With {.Text = "UI snappy mode (reserve 1 core)", .AutoSize = True, .Location = New Point(leftLabel, row)}
            tip.SetToolTip(chkUiSnappy, "Reserves one core to keep the UI responsive.")
            chkUiSnappy.Checked = working.UiSnappyMode
            row += rowHeight

            Dim chkTelemetry As New CheckBox() With {.Text = "CSV telemetry log (ms)", .AutoSize = True, .Location = New Point(leftLabel, row)}
            Dim numTelemetry As New NumericUpDown() With {.Location = New Point(leftControl, row - 2), .Minimum = 250, .Maximum = 10000, .Increment = 250, .Width = 120}
            tip.SetToolTip(chkTelemetry, "Write CSV telemetry while running.")
            tip.SetToolTip(numTelemetry, "Sampling interval in milliseconds.")
            chkTelemetry.Checked = working.TelemetryEnabled
            Dim telemetryValue As Decimal = CDec(working.TelemetryIntervalMs)
            If telemetryValue < numTelemetry.Minimum Then telemetryValue = numTelemetry.Minimum
            If telemetryValue > numTelemetry.Maximum Then telemetryValue = numTelemetry.Maximum
            numTelemetry.Value = telemetryValue
            numTelemetry.Enabled = chkTelemetry.Checked
            AddHandler chkTelemetry.CheckedChanged, Sub() numTelemetry.Enabled = chkTelemetry.Checked
            row += rowHeight

            Dim lblValidation As New Label() With {.Text = "Validation mode", .AutoSize = True, .Location = New Point(leftLabel, row)}
            Dim cmbValidation As New ComboBox() With {.Location = New Point(leftControl, row - 3), .Width = 120, .DropDownStyle = ComboBoxStyle.DropDownList}
            tip.SetToolTip(lblValidation, "Select how much validation to perform.")
            tip.SetToolTip(cmbValidation, "Off disables validation. Light/Full add periodic checks.")
            cmbValidation.Items.AddRange(New Object() {ValidationMode.Off.ToString(), ValidationMode.Light.ToString(), ValidationMode.Full.ToString()})
            cmbValidation.SelectedItem = working.ValidationMode.ToString()
            row += rowHeight

            Dim lblValidationInterval As New Label() With {.Text = "Validation interval (ms)", .AutoSize = True, .Location = New Point(leftLabel, row)}
            Dim numValidationInterval As New NumericUpDown() With {.Location = New Point(leftControl, row - 2), .Minimum = 250, .Maximum = 600000, .Increment = 250, .Width = 120}
            tip.SetToolTip(lblValidationInterval, "How often each worker validates results.")
            tip.SetToolTip(numValidationInterval, "Validation interval in milliseconds.")
            Dim intervalValue As Decimal = CDec(working.ValidationIntervalMs)
            If intervalValue < numValidationInterval.Minimum Then intervalValue = numValidationInterval.Minimum
            If intervalValue > numValidationInterval.Maximum Then intervalValue = numValidationInterval.Maximum
            numValidationInterval.Value = intervalValue
            numValidationInterval.Enabled = Not String.Equals(cmbValidation.SelectedItem?.ToString(), ValidationMode.Off.ToString(), StringComparison.OrdinalIgnoreCase)
            AddHandler cmbValidation.SelectedIndexChanged, Sub()
                                                               numValidationInterval.Enabled = Not String.Equals(cmbValidation.SelectedItem?.ToString(), ValidationMode.Off.ToString(), StringComparison.OrdinalIgnoreCase)
                                                           End Sub
            row += rowHeight

            Dim chkAutoPlot As New CheckBox() With {.Text = "Auto-show temp plot on start", .AutoSize = True, .Location = New Point(leftLabel, row)}
            tip.SetToolTip(chkAutoPlot, "Open the temperature plot automatically when starting.")
            chkAutoPlot.Checked = working.AutoShowTempPlot
            row += rowHeight

            Dim chkAffinity As New CheckBox() With {.Text = "Use core affinity", .AutoSize = True, .Location = New Point(leftLabel, row)}
            Dim btnAffinity As New Button() With {.Text = "Select...", .Location = New Point(leftControl, row - 4), .Width = 120}
            tip.SetToolTip(chkAffinity, "Pin worker threads to selected CPU cores.")
            tip.SetToolTip(btnAffinity, "Choose which CPU cores to use.")
            Dim selectedCores As New List(Of Integer)(working.AffinityCores)
            chkAffinity.Checked = working.UseAffinity
            btnAffinity.Enabled = chkAffinity.Checked
            AddHandler chkAffinity.CheckedChanged, Sub() btnAffinity.Enabled = chkAffinity.Checked
            row += rowHeight

            Dim lblAffinity As New Label() With {.AutoSize = True, .Location = New Point(leftLabel, row)}
            tip.SetToolTip(lblAffinity, "Summary of selected cores.")
            Dim updateAffinityLabel As Action = Sub()
                                                    If Not chkAffinity.Checked OrElse selectedCores.Count = 0 Then
                                                        lblAffinity.Text = "Selected cores: All"
                                                    Else
                                                        lblAffinity.Text = GetAffinitySummary(selectedCores)
                                                    End If
                                                End Sub
            updateAffinityLabel()
            AddHandler btnAffinity.Click, Sub()
                                              selectedCores = ShowCoreSelectionDialog(selectedCores)
                                              updateAffinityLabel()
                                          End Sub

            Dim btnOk As New Button() With {.Text = "OK", .DialogResult = DialogResult.OK, .Location = New Point(240, 342), .Width = 70}
            Dim btnCancel As New Button() With {.Text = "Cancel", .DialogResult = DialogResult.Cancel, .Location = New Point(320, 342), .Width = 70}
            tip.SetToolTip(btnOk, "Apply changes and close.")
            tip.SetToolTip(btnCancel, "Discard changes and close.")
            dlg.AcceptButton = btnOk
            dlg.CancelButton = btnCancel

            dlg.Controls.AddRange(New Control() {chkTimed, numTimed, chkTemp, numTemp, chkThrottle, chkUiSnappy, chkTelemetry, numTelemetry, lblValidation, cmbValidation, lblValidationInterval, numValidationInterval, chkAutoPlot, chkAffinity, btnAffinity, lblAffinity, btnOk, btnCancel})

            If dlg.ShowDialog(Me) <> DialogResult.OK Then
                Return Nothing
            End If

            working.TimedRunMinutes = If(chkTimed.Checked, CInt(numTimed.Value), 0)
            working.AutoStopTempC = If(chkTemp.Checked, CSng(numTemp.Value), 0)
            working.AutoStopOnThrottle = chkThrottle.Checked
            working.UiSnappyMode = chkUiSnappy.Checked
            working.TelemetryEnabled = chkTelemetry.Checked
            working.TelemetryIntervalMs = CInt(numTelemetry.Value)
            Dim validationSelection As String = If(cmbValidation.SelectedItem?.ToString(), ValidationMode.Off.ToString())
            Dim parsedMode As ValidationMode = ValidationMode.Off
            [Enum].TryParse(validationSelection, parsedMode)
            working.ValidationMode = parsedMode
            working.ValidationIntervalMs = CInt(numValidationInterval.Value)
            working.AutoShowTempPlot = chkAutoPlot.Checked
            working.UseAffinity = chkAffinity.Checked AndAlso selectedCores.Count > 0
            working.AffinityCores = If(working.UseAffinity, selectedCores, New List(Of Integer)())
        End Using

        Return working
    End Function

    Private Function ShowCoreSelectionDialog(current As List(Of Integer)) As List(Of Integer)
        Dim selected As New List(Of Integer)(current)
        Dim maxSelectable As Integer = Math.Min(Environment.ProcessorCount, If(IntPtr.Size = 8, 64, 32))
        Dim topology As CpuTopologySnapshot = GetCpuTopology()

        If topology Is Nothing OrElse Not topology.IsValid Then
            Return ShowCoreSelectionDialogLegacy(selected, maxSelectable)
        End If

        Using dlg As New Form()
            dlg.Text = "Core Affinity"
            dlg.FormBorderStyle = FormBorderStyle.FixedDialog
            dlg.StartPosition = FormStartPosition.CenterParent
            dlg.MaximizeBox = False
            dlg.MinimizeBox = False
            dlg.ShowInTaskbar = False
            dlg.ClientSize = New Size(420, 420)
            dlg.MinimumSize = New Size(420, 420)

            Dim tip As New ToolTip() With {
                .AutoPopDelay = 12000,
                .InitialDelay = 500,
                .ReshowDelay = 150,
                .ShowAlways = True
            }

            Dim root As New TableLayoutPanel() With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 1,
                .RowCount = 5,
                .Padding = New Padding(12)
            }
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            root.RowStyles.Add(New RowStyle(SizeType.AutoSize))

            Dim lblInfo As New Label() With {.AutoSize = True, .Text = "Logical threads grouped by physical core."}
            root.Controls.Add(lblInfo, 0, 0)

            Dim tree As New TreeView() With {
                .CheckBoxes = True,
                .Dock = DockStyle.Fill,
                .HideSelection = False,
                .FullRowSelect = True
            }
            tip.SetToolTip(tree, "Select logical threads or entire cores to pin worker threads.")

            Dim updating As Boolean = False
            Dim updateSummary As Action(Of Label) = Sub(summary)
                                                        Dim logicalSelected As Integer = 0
                                                        Dim coreSelected As Integer = 0
                                                        For Each coreNode As TreeNode In tree.Nodes
                                                            Dim coreHasSelection As Boolean = False
                                                            For Each child As TreeNode In coreNode.Nodes
                                                                If child.Checked Then
                                                                    logicalSelected += 1
                                                                    coreHasSelection = True
                                                                End If
                                                            Next
                                                            If coreHasSelection Then
                                                                coreSelected += 1
                                                            End If
                                                        Next
                                                        summary.Text = $"Selected cores: {coreSelected}, logical threads: {logicalSelected}"
                                                    End Sub

            For Each core As PhysicalCoreInfo In topology.PhysicalCores
                Dim coreLabel As String = GetCoreDisplayLabel(core, topology)
                Dim coreNode As New TreeNode(coreLabel) With {.Tag = core}
                For Each logicalId As Integer In core.LogicalProcessors
                    If logicalId >= maxSelectable Then
                        Continue For
                    End If
                    Dim child As New TreeNode($"LP {logicalId}") With {.Tag = logicalId}
                    If selected.Contains(logicalId) Then
                        child.Checked = True
                    End If
                    coreNode.Nodes.Add(child)
                Next

                Dim allChecked As Boolean = True
                For Each child As TreeNode In coreNode.Nodes
                    If Not child.Checked Then
                        allChecked = False
                        Exit For
                    End If
                Next
                coreNode.Checked = allChecked AndAlso coreNode.Nodes.Count > 0
                coreNode.Expand()
                tree.Nodes.Add(coreNode)
            Next

            AddHandler tree.AfterCheck, Sub(sender, e)
                                            If updating Then
                                                Return
                                            End If
                                            updating = True
                                            Try
                                                If e.Node.Level = 0 Then
                                                    For Each child As TreeNode In e.Node.Nodes
                                                        child.Checked = e.Node.Checked
                                                    Next
                                                Else
                                                    Dim parent As TreeNode = e.Node.Parent
                                                    If parent IsNot Nothing Then
                                                        Dim allChecked As Boolean = True
                                                        For Each child As TreeNode In parent.Nodes
                                                            If Not child.Checked Then
                                                                allChecked = False
                                                                Exit For
                                                            End If
                                                        Next
                                                        parent.Checked = allChecked
                                                    End If
                                                End If
                                            Finally
                                                updating = False
                                            End Try
                                        End Sub

            root.Controls.Add(tree, 0, 1)

            Dim lblSummary As New Label() With {.AutoSize = True}
            updateSummary(lblSummary)
            AddHandler tree.AfterCheck, Sub() updateSummary(lblSummary)
            root.Controls.Add(lblSummary, 0, 2)

            Dim noteText As String = $"Showing logical processors 0-{maxSelectable - 1}."
            If Not String.IsNullOrWhiteSpace(topology.Warning) Then
                noteText &= " " & topology.Warning
            End If
            Dim lblNote As New Label() With {.AutoSize = True, .Text = noteText}
            root.Controls.Add(lblNote, 0, 3)

            Dim buttonPanel As New FlowLayoutPanel() With {
                .Dock = DockStyle.Bottom,
                .FlowDirection = FlowDirection.RightToLeft,
                .Padding = New Padding(0, 8, 0, 0),
                .AutoSize = True
            }
            Dim btnOk As New Button() With {.Text = "OK", .DialogResult = DialogResult.OK, .Width = 80}
            Dim btnCancel As New Button() With {.Text = "Cancel", .DialogResult = DialogResult.Cancel, .Width = 80}
            tip.SetToolTip(btnOk, "Apply core selection.")
            tip.SetToolTip(btnCancel, "Keep current selection.")
            buttonPanel.Controls.Add(btnCancel)
            buttonPanel.Controls.Add(btnOk)
            root.Controls.Add(buttonPanel, 0, 4)

            dlg.Controls.Add(root)
            dlg.AcceptButton = btnOk
            dlg.CancelButton = btnCancel

            ApplyCoreSelectionTheme(dlg, tree)

            If dlg.ShowDialog(Me) <> DialogResult.OK Then
                Return selected
            End If

            selected.Clear()
            For Each coreNode As TreeNode In tree.Nodes
                For Each child As TreeNode In coreNode.Nodes
                    If child.Checked AndAlso TypeOf child.Tag Is Integer Then
                        selected.Add(CInt(child.Tag))
                    End If
                Next
            Next
        End Using

        Return selected
    End Function

    Private Function ShowCoreSelectionDialogLegacy(selected As List(Of Integer), maxSelectable As Integer) As List(Of Integer)
        Using dlg As New Form()
            dlg.Text = "Core Affinity"
            dlg.FormBorderStyle = FormBorderStyle.FixedDialog
            dlg.StartPosition = FormStartPosition.CenterParent
            dlg.MaximizeBox = False
            dlg.MinimizeBox = False
            dlg.ShowInTaskbar = False
            dlg.ClientSize = New Size(260, 360)

            Dim tip As New ToolTip() With {
                .AutoPopDelay = 12000,
                .InitialDelay = 500,
                .ReshowDelay = 150,
                .ShowAlways = True
            }

            Dim list As New CheckedListBox() With {.Location = New Point(12, 12), .Size = New Size(236, 280)}
            tip.SetToolTip(list, "Select CPU cores to pin worker threads.")
            For i As Integer = 0 To maxSelectable - 1
                Dim index As Integer = list.Items.Add($"CPU {i}")
                If selected.Contains(i) Then
                    list.SetItemChecked(index, True)
                End If
            Next

            Dim note As New Label() With {.AutoSize = True, .Location = New Point(12, 298), .Text = $"Showing 0-{maxSelectable - 1} cores"}
            tip.SetToolTip(note, "Core list limited by OS affinity mask.")

            Dim btnOk As New Button() With {.Text = "OK", .DialogResult = DialogResult.OK, .Location = New Point(90, 320), .Width = 70}
            Dim btnCancel As New Button() With {.Text = "Cancel", .DialogResult = DialogResult.Cancel, .Location = New Point(170, 320), .Width = 70}
            tip.SetToolTip(btnOk, "Apply core selection.")
            tip.SetToolTip(btnCancel, "Keep current selection.")
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

    Private Sub ApplyCoreSelectionTheme(dialog As Form, tree As TreeView)
        If dialog Is Nothing Then
            Return
        End If

        UiThemeManager.ApplyTheme(dialog)

        If tree Is Nothing Then
            Return
        End If

        Dim palette As UiThemePalette = UiThemeManager.Palette
        tree.BackColor = palette.Surface
        tree.ForeColor = palette.Text
    End Sub

    Private Function GetAffinitySummary(selected As List(Of Integer)) As String
        If selected Is Nothing OrElse selected.Count = 0 Then
            Return "Selected cores: All"
        End If

        Dim topology As CpuTopologySnapshot = GetCpuTopology()

        If topology Is Nothing OrElse Not topology.IsValid Then
            Return $"Selected cores: {selected.Count}"
        End If

        Dim physicalSet As New HashSet(Of Integer)()
        For Each logicalId As Integer In selected
            Dim core As PhysicalCoreInfo = Nothing
            If topology.TryGetCoreForLogical(logicalId, core) AndAlso core IsNot Nothing Then
                physicalSet.Add(core.CoreId)
            End If
        Next

        Dim physicalCount As Integer = physicalSet.Count
        Return $"Selected cores: {physicalCount} ({selected.Count} threads)"
    End Function

    Private Function GetCpuTopology() As CpuTopologySnapshot
        If _cpuTopology Is Nothing Then
            _cpuTopology = CpuTopologyService.GetTopology()
        End If
        Return _cpuTopology
    End Function

    Private Function GetCoreDisplayLabel(core As PhysicalCoreInfo, topology As CpuTopologySnapshot) As String
        If core Is Nothing Then
            Return "Core"
        End If

        If topology IsNot Nothing AndAlso topology.HasEfficiencyClasses Then
            Return $"Core {core.CoreId} (EC {core.EfficiencyClass})"
        End If

        Return $"Core {core.CoreId}"
    End Function

    Private Function BuildAffinityLabel(logicalId As Integer?, topology As CpuTopologySnapshot) As String
        If Not logicalId.HasValue Then
            Return "Unpinned"
        End If

        Dim core As PhysicalCoreInfo = Nothing
        If topology IsNot Nothing AndAlso topology.TryGetCoreForLogical(logicalId.Value, core) AndAlso core IsNot Nothing Then
            Dim coreLabel As String = GetCoreDisplayLabel(core, topology)
            Return $"{coreLabel} / LP {logicalId.Value}"
        End If

        Return $"LP {logicalId.Value}"
    End Function

    Private Sub SetWorkerAffinity(workerId As Integer, logicalId As Integer?, topology As CpuTopologySnapshot)
        Dim info As New WorkerAffinityInfo() With {
            .WorkerId = workerId,
            .LogicalId = logicalId,
            .AffinityLabel = BuildAffinityLabel(logicalId, topology)
        }

        Dim core As PhysicalCoreInfo = Nothing
        If logicalId.HasValue AndAlso topology IsNot Nothing AndAlso topology.TryGetCoreForLogical(logicalId.Value, core) AndAlso core IsNot Nothing Then
            info.CoreId = core.CoreId
            info.CoreLabel = GetCoreDisplayLabel(core, topology)
        Else
            info.CoreLabel = info.AffinityLabel
        End If

        SyncLock _workerAffinityLock
            _workerAffinityMap(workerId) = info
        End SyncLock
    End Sub

    Private Function GetWorkerAffinityInfo(workerId As Integer) As WorkerAffinityInfo
        SyncLock _workerAffinityLock
            Dim info As WorkerAffinityInfo = Nothing
            If _workerAffinityMap.TryGetValue(workerId, info) Then
                Return info
            End If
        End SyncLock
        Return Nothing
    End Function

    Private Function GetWorkerAffinityLabel(workerId As Integer) As String
        Dim info As WorkerAffinityInfo = GetWorkerAffinityInfo(workerId)
        If info Is Nothing Then
            Return String.Empty
        End If
        Return info.AffinityLabel
    End Function

    Private Function DecorateWorkerMessage(workerId As Integer, message As String) As String
        If String.IsNullOrWhiteSpace(message) Then
            Return message
        End If

        Dim info As WorkerAffinityInfo = GetWorkerAffinityInfo(workerId)
        If info Is Nothing OrElse String.IsNullOrWhiteSpace(info.AffinityLabel) Then
            Return message
        End If

        If message.IndexOf($"W{workerId}", StringComparison.OrdinalIgnoreCase) >= 0 Then
            Return message
        End If

        Return $"{message} (W{workerId} {info.AffinityLabel})"
    End Function

    Private Sub ClearWorkerAffinityMap()
        SyncLock _workerAffinityLock
            _workerAffinityMap.Clear()
        End SyncLock
    End Sub
    Private Sub RunOptionsToolStripMenuItem_Click(sender As Object, e As EventArgs) Handles RunOptionsToolStripMenuItem.Click
        Dim updated As RunOptions = ShowRunOptionsDialog()
        If updated Is Nothing Then
            Return
        End If

        _runOptions = updated
        NormalizeRunOptions(_runOptions)
        ApplyRunOptionsToRunningState()
        SaveCurrentProfile()

        LogMessage("Run options updated.")
    End Sub
    Private Sub PluginManagerToolStripMenuItem_Click(sender As Object, e As EventArgs) Handles PluginManagerToolStripMenuItem.Click
        Using dlg As New frmPluginManager()
            dlg.ShowDialog(Me)

            If dlg.HasChanges Then
                Dim pluginSettings As PluginSettings = PluginSettingsStore.LoadSettings()
                Dim disabledIds As New HashSet(Of String)(pluginSettings.DisabledPluginIds, StringComparer.OrdinalIgnoreCase)
                _pluginRegistry.LoadPlugins(AddressOf LogMessage, disabledIds)
                UpdateStressModeList(GetSelectedStressPluginId())
            End If

            If dlg.RequiresRestart Then
                MessageBox.Show(Me, "Plugins were staged. Restart ClawHammer to apply changes.", "Plugin Manager", MessageBoxButtons.OK, MessageBoxIcon.Information)
            End If
        End Using
    End Sub

    Private Sub ShowValidationMonitorWindow()
        If _validationMonitor Is Nothing OrElse _validationMonitor.IsDisposed Then
            _validationMonitor = New frmValidationMonitor()
            If _uiLayout Is Nothing Then
                _uiLayout = UiLayoutManager.LoadLayout()
            End If
            UiLayoutManager.ApplyWindowLayout(_validationMonitor, _uiLayout.ValidationMonitorWindow)
            AddHandler _validationMonitor.FormClosing, Sub() SaveValidationMonitorLayout()
            AddHandler _validationMonitor.FormClosed, Sub()
                                                          _validationMonitor = Nothing
                                                      End Sub
            _validationMonitor.Show(Me)
            UpdateValidationDisplay()
        Else
            _validationMonitor.BringToFront()
            _validationMonitor.Focus()
        End If
    End Sub
    Private Sub ShowTemperaturePlotWindow()
        If _tempPlotForm Is Nothing OrElse _tempPlotForm.IsDisposed Then
            _tempPlotForm = New TempPlotForm()
            If _uiLayout Is Nothing Then
                _uiLayout = UiLayoutManager.LoadLayout()
            End If
            _tempPlotForm.ApplyLayout(_uiLayout.TempPlotWindow)
            _tempPlotForm.TimeWindowSeconds = _plotTimeWindowSeconds
            _tempPlotForm.ApplySensorSelection(_uiLayout.TempPlotSelectedSensors, _uiLayout.TempPlotSelectionSet)
            _tempPlotForm.RefreshIntervalMs = _uiLayout.TempPlotRefreshIntervalMs
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

    Private Sub ValidationMonitorToolStripMenuItem_Click(sender As Object, e As EventArgs) Handles ValidationMonitorToolStripMenuItem.Click
        ShowValidationMonitorWindow()
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

    Private Async Sub CheckForUpdatesToolStripMenuItem_Click(sender As Object, e As EventArgs) Handles CheckForUpdatesToolStripMenuItem.Click
        CheckForUpdatesToolStripMenuItem.Enabled = False
        Try
            Dim release As UpdateReleaseInfo = Await UpdateService.GetLatestReleaseAsync()
            If release Is Nothing OrElse String.IsNullOrWhiteSpace(release.TagName) Then
                MessageBox.Show(Me, "Unable to check for updates right now.", "Check for Updates", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If

            Dim currentVersion As Version = My.Application.Info.Version
            If Not UpdateService.IsUpdateAvailable(currentVersion, release.Version) Then
                Dim message As String = $"Installed: {GetAppVersionDisplay()}{Environment.NewLine}Latest: {FormatVersionDisplay(release.Version)}"
                MessageBox.Show(Me, $"You're up to date.{Environment.NewLine}{message}", "Check for Updates", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If

            Using dlg As New frmUpdate(release, currentVersion)
                dlg.ShowDialog(Me)
            End Using
        Catch ex As Exception
            MessageBox.Show(Me, "Update check failed: " & ex.Message, "Check for Updates", MessageBoxButtons.OK, MessageBoxIcon.Warning)
        Finally
            CheckForUpdatesToolStripMenuItem.Enabled = True
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
        sb.AppendLine("<li>Validation Mode: " & _runOptions.ValidationMode.ToString() & "</li>")
        sb.AppendLine("<li>Validation Interval (ms): " & _runOptions.ValidationIntervalMs.ToString() & "</li>")
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























































