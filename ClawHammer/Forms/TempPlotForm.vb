Imports System.Collections.Generic
Imports System.Drawing
Imports System.Windows.Forms

Friend Class TempPlotForm
    Inherits Form

    Private ReadOnly _plot As TemperaturePlotControl
    Private ReadOnly _sensorList As CheckedListBox
    Private ReadOnly _sensorIndex As Dictionary(Of String, Integer)
    Private ReadOnly _snapshotValues As Dictionary(Of String, Single)
    Private ReadOnly _sensorColors As Dictionary(Of String, Color)
    Private ReadOnly _timeWindowBox As NumericUpDown
    Private ReadOnly _refreshTimer As Timer
    Private ReadOnly _split As SplitContainer
    Private _colorIndex As Integer
    Private _plotDirty As Integer = 0
    Private _suppressTimeWindowEvent As Boolean = False
    Private _pendingSplitterDistance As Integer = -1
    Private ReadOnly _palette As UiThemePalette
    Private ReadOnly _plotPalette As PlotPalette

    Private Shared ReadOnly Palette As Color() = {
        Color.FromArgb(231, 76, 60),
        Color.FromArgb(52, 152, 219),
        Color.FromArgb(46, 204, 113),
        Color.FromArgb(155, 89, 182),
        Color.FromArgb(241, 196, 15),
        Color.FromArgb(230, 126, 34),
        Color.FromArgb(26, 188, 156),
        Color.FromArgb(236, 240, 241),
        Color.FromArgb(116, 185, 255),
        Color.FromArgb(253, 121, 168)
    }

    Public Event TimeWindowChanged As Action(Of Single)

    Public Sub New()
        Text = "Temperature Plot"
        StartPosition = FormStartPosition.CenterParent
        Size = New Size(900, 600)
        MinimumSize = New Size(720, 420)
        _palette = UiThemeManager.Palette
        _plotPalette = UiThemeManager.PlotPalette
        BackColor = _palette.Background
        ForeColor = _palette.Text
        Try
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath)
        Catch
        End Try

        _sensorIndex = New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)
        _snapshotValues = New Dictionary(Of String, Single)(StringComparer.OrdinalIgnoreCase)
        _sensorColors = New Dictionary(Of String, Color)(StringComparer.OrdinalIgnoreCase)

        _split = New SplitContainer()
        _split.Dock = DockStyle.Fill
        _split.FixedPanel = FixedPanel.Panel1
        _split.BackColor = _palette.Border
        _split.Panel1.BackColor = _palette.Panel
        _split.Panel2.BackColor = _palette.Background

        _sensorList = New CheckedListBox()
        _sensorList.Dock = DockStyle.Fill
        _sensorList.CheckOnClick = True
        _sensorList.IntegralHeight = False
        _sensorList.BackColor = _palette.Panel
        _sensorList.ForeColor = _palette.Text
        _sensorList.BorderStyle = BorderStyle.FixedSingle
        _sensorList.DrawMode = DrawMode.OwnerDrawFixed
        _sensorList.ItemHeight = Math.Max(18, _sensorList.Font.Height + 4)
        _split.Panel1.Controls.Add(_sensorList)

        _plot = New TemperaturePlotControl()
        _plot.Dock = DockStyle.Fill
        _plot.ApplyPalette(_plotPalette)
        Dim plotToolbar As New FlowLayoutPanel()
        plotToolbar.Dock = DockStyle.Top
        plotToolbar.Height = 32
        plotToolbar.Padding = New Padding(8, 5, 8, 4)
        plotToolbar.BackColor = _palette.Panel
        plotToolbar.WrapContents = False
        plotToolbar.AutoSize = False

        Dim lblWindow As New Label() With {.Text = "Time window (sec)", .AutoSize = True, .ForeColor = _palette.Text, .Margin = New Padding(0, 4, 8, 0)}
        _timeWindowBox = New NumericUpDown() With {.Minimum = 10, .Maximum = 3600, .Increment = 10, .Value = 120, .Width = 80, .BackColor = _palette.Surface, .ForeColor = _palette.Text, .Margin = New Padding(0, 0, 8, 0)}
        AddHandler _timeWindowBox.ValueChanged, Sub()
                                                    Dim seconds As Single = CSng(_timeWindowBox.Value)
                                                    _plot.TimeWindowSeconds = seconds
                                                    If Not _suppressTimeWindowEvent Then
                                                        RaiseEvent TimeWindowChanged(seconds)
                                                    End If
                                                End Sub
        _plot.TimeWindowSeconds = CSng(_timeWindowBox.Value)
        plotToolbar.Controls.Add(lblWindow)
        plotToolbar.Controls.Add(_timeWindowBox)

        _split.Panel2.Controls.Add(_plot)
        _split.Panel2.Controls.Add(plotToolbar)

        Controls.Add(_split)

        AddHandler _sensorList.ItemCheck, AddressOf SensorList_ItemCheck
        AddHandler _sensorList.DrawItem, AddressOf SensorList_DrawItem

        _refreshTimer = New Timer()
        _refreshTimer.Interval = 200
        AddHandler _refreshTimer.Tick, Sub()
                                           If Threading.Interlocked.Exchange(_plotDirty, 0) = 1 Then
                                               _plot.Invalidate()
                                           End If
                                       End Sub
        _refreshTimer.Start()

        AddHandler Me.Shown, Sub()
                                 _split.Panel1MinSize = 200
                                 _split.Panel2MinSize = 320
                                 Dim desired As Integer = If(_pendingSplitterDistance > 0, _pendingSplitterDistance, 260)
                                 UiLayoutManager.ApplySplitterDistanceSafe(_split, desired)
                             End Sub
    End Sub

    Public Sub ApplyLayout(layout As UiWindowLayout)
        If layout Is Nothing Then
            Return
        End If

        UiLayoutManager.ApplyWindowLayout(Me, layout)
        _pendingSplitterDistance = layout.SplitterDistance
    End Sub

    Public Sub CaptureLayout(layout As UiWindowLayout)
        If layout Is Nothing Then
            Return
        End If

        UiLayoutManager.CaptureWindowLayout(Me, layout)
        If _split IsNot Nothing Then
            layout.SplitterDistance = _split.SplitterDistance
        End If
    End Sub

    <System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden),
     System.ComponentModel.Browsable(False)>
    Public Property TimeWindowSeconds As Single
        Get
            Return CSng(_timeWindowBox.Value)
        End Get
        Set(value As Single)
            Dim clamped As Decimal = Math.Max(_timeWindowBox.Minimum, Math.Min(_timeWindowBox.Maximum, CDec(value)))
            _suppressTimeWindowEvent = True
            _timeWindowBox.Value = clamped
            _plot.TimeWindowSeconds = CSng(_timeWindowBox.Value)
            _suppressTimeWindowEvent = False
        End Set
    End Property

    Protected Overrides Sub OnFormClosing(e As FormClosingEventArgs)
        MyBase.OnFormClosing(e)
        _refreshTimer.Stop()
    End Sub

    Private Sub SensorList_ItemCheck(sender As Object, e As ItemCheckEventArgs)
        Dim label As String = _sensorList.Items(e.Index).ToString()
        Dim enabled As Boolean = (e.NewValue = CheckState.Checked)
        _plot.SetSeriesEnabled(label, enabled)
    End Sub

    Private Sub SensorList_DrawItem(sender As Object, e As DrawItemEventArgs)
        If e.Index < 0 Then
            Return
        End If

        Dim label As String = _sensorList.Items(e.Index).ToString()
        Dim isChecked As Boolean = _sensorList.GetItemChecked(e.Index)
        Dim swatchColor As Color = _plotPalette.SwatchBackColor
        Dim labelColor As Color = If(isChecked, _sensorList.ForeColor, _palette.TextMuted)

        Dim mappedColor As Color = Nothing
        If isChecked AndAlso _sensorColors.TryGetValue(label, mappedColor) Then
            swatchColor = mappedColor
        End If

        e.DrawBackground()

        Dim swatchSize As Integer = Math.Min(12, e.Bounds.Height - 6)
        Dim swatchRect As New Rectangle(e.Bounds.Left + 6, e.Bounds.Top + (e.Bounds.Height - swatchSize) \ 2, swatchSize, swatchSize)
        Using brush As New SolidBrush(swatchColor)
            e.Graphics.FillRectangle(brush, swatchRect)
        End Using
        Using pen As New Pen(_plotPalette.SwatchBorderColor)
            e.Graphics.DrawRectangle(pen, swatchRect)
        End Using

        Dim textRect As New Rectangle(swatchRect.Right + 8, e.Bounds.Top + 1, e.Bounds.Width - (swatchRect.Width + 14), e.Bounds.Height - 2)
        TextRenderer.DrawText(e.Graphics, label, e.Font, textRect, labelColor, TextFormatFlags.VerticalCenter Or TextFormatFlags.EndEllipsis)
        e.DrawFocusRectangle()
    End Sub

    Private Function NextColor() As Color
        Dim color As Color = Palette(_colorIndex Mod Palette.Length)
        _colorIndex += 1
        Return color
    End Function

    Private Sub InitializeComponent()
        Dim resources As System.ComponentModel.ComponentResourceManager = New System.ComponentModel.ComponentResourceManager(GetType(TempPlotForm))
        SuspendLayout()
        ClientSize = New Size(284, 261)
        Icon = CType(resources.GetObject("$this.Icon"), Icon)
        Name = "TempPlotForm"
        ResumeLayout(False)

    End Sub

    Public Sub UpdateSnapshot(samples As IReadOnlyList(Of TempSensorSample), timestampUtc As DateTime)
        If samples Is Nothing OrElse samples.Count = 0 Then
            Return
        End If

        If Me.IsDisposed OrElse Not Me.IsHandleCreated Then
            Return
        End If

        If Me.InvokeRequired Then
            Me.BeginInvoke(Sub() UpdateSnapshot(samples, timestampUtc))
            Return
        End If

        _snapshotValues.Clear()

        For i As Integer = 0 To samples.Count - 1
            Dim sample As TempSensorSample = samples(i)
            If sample.HasValue Then
                _snapshotValues(sample.Label) = sample.ValueC
            End If

            If Not _sensorIndex.ContainsKey(sample.Label) Then
                Dim isCpu As Boolean = IsCpuLabel(sample.Label)
                Dim color As Color = NextColor()
                Dim idx As Integer = _sensorList.Items.Add(sample.Label)
                _sensorIndex(sample.Label) = idx
                _sensorColors(sample.Label) = color
                _plot.EnsureSeries(sample.Label, color)
                If isCpu Then
                    _sensorList.SetItemChecked(idx, True)
                    _plot.SetSeriesEnabled(sample.Label, True)
                End If
                _sensorList.Invalidate()
            End If
        Next

        _plot.AppendSnapshot(_snapshotValues, timestampUtc)
        Threading.Interlocked.Exchange(_plotDirty, 1)
    End Sub

    Private Shared Function IsCpuLabel(label As String) As Boolean
        If String.IsNullOrWhiteSpace(label) Then
            Return False
        End If
        If label.StartsWith("Cpu:", StringComparison.OrdinalIgnoreCase) Then
            Return True
        End If
        Return label.IndexOf(" CPU", StringComparison.OrdinalIgnoreCase) >= 0
    End Function
End Class
