Imports System.Collections.Generic
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Windows.Forms

Public Class TemperaturePlotControl
    Inherits Control

    Private Const MaxPoints As Integer = 3000
    Private _plotPalette As PlotPalette
    Private _gridPen As Pen
    Private _axisPen As Pen
    Private _hoverLinePen As Pen
    Private _hoverBorderPen As Pen
    Private _hoverBackBrush As Brush
    Private _legendBackBrush As Brush
    Private _legendBorderPen As Pen
    Private _legendSwatchBorderPen As Pen
    Private Const HoverThresholdPx As Single = 6.0F
    Private ReadOnly _series As Dictionary(Of String, SeriesData)
    Private ReadOnly _timestamps As Single()
    Private _index As Integer
    Private _count As Integer
    Private _startUtc As DateTime
    Private _hoverPoint As Point
    Private _hoverActive As Boolean
    Private _timeWindowSeconds As Single = 120.0F

    Private Class SeriesData
        Public ReadOnly Label As String
        Public ReadOnly Pen As Pen
        Public ReadOnly Brush As Brush
        Public ReadOnly Values As Single()
        Public Enabled As Boolean

        Public Sub New(label As String, color As Color, capacity As Integer)
            Me.Label = label
            Pen = New Pen(color, 1.5F)
            Brush = New SolidBrush(color)
            Values = New Single(capacity - 1) {}
            For i As Integer = 0 To capacity - 1
                Values(i) = Single.NaN
            Next
        End Sub
    End Class

    Public Sub New()
        SetStyle(ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.UserPaint Or ControlStyles.ResizeRedraw, True)
        UpdateStyles()
        _series = New Dictionary(Of String, SeriesData)(StringComparer.OrdinalIgnoreCase)
        _timestamps = New Single(MaxPoints - 1) {}
        ApplyPalette(UiThemeManager.PlotPalette)
    End Sub

    Public Sub ApplyPalette(palette As PlotPalette)
        _plotPalette = palette

        If _gridPen IsNot Nothing Then _gridPen.Dispose()
        If _axisPen IsNot Nothing Then _axisPen.Dispose()
        If _hoverLinePen IsNot Nothing Then _hoverLinePen.Dispose()
        If _hoverBorderPen IsNot Nothing Then _hoverBorderPen.Dispose()
        If _hoverBackBrush IsNot Nothing Then _hoverBackBrush.Dispose()
        If _legendBackBrush IsNot Nothing Then _legendBackBrush.Dispose()
        If _legendBorderPen IsNot Nothing Then _legendBorderPen.Dispose()
        If _legendSwatchBorderPen IsNot Nothing Then _legendSwatchBorderPen.Dispose()

        _gridPen = New Pen(palette.GridColor)
        _axisPen = New Pen(palette.AxisColor)
        _hoverLinePen = New Pen(palette.HoverLineColor)
        _hoverBorderPen = New Pen(palette.HoverBorderColor)
        _hoverBackBrush = New SolidBrush(palette.HoverBackColor)
        _legendBackBrush = New SolidBrush(palette.LegendBackColor)
        _legendBorderPen = New Pen(palette.LegendBorderColor)
        _legendSwatchBorderPen = New Pen(palette.LegendSwatchBorderColor)

        BackColor = palette.BackColor
        ForeColor = palette.ForeColor
        Invalidate()
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then
            For Each series As SeriesData In _series.Values
                series.Pen.Dispose()
                series.Brush.Dispose()
            Next
            If _gridPen IsNot Nothing Then _gridPen.Dispose()
            If _axisPen IsNot Nothing Then _axisPen.Dispose()
            If _hoverLinePen IsNot Nothing Then _hoverLinePen.Dispose()
            If _hoverBorderPen IsNot Nothing Then _hoverBorderPen.Dispose()
            If _hoverBackBrush IsNot Nothing Then _hoverBackBrush.Dispose()
            If _legendBackBrush IsNot Nothing Then _legendBackBrush.Dispose()
            If _legendBorderPen IsNot Nothing Then _legendBorderPen.Dispose()
            If _legendSwatchBorderPen IsNot Nothing Then _legendSwatchBorderPen.Dispose()
        End If
        MyBase.Dispose(disposing)
    End Sub

    Public Sub EnsureSeries(label As String, color As Color)
        If _series.ContainsKey(label) Then
            Return
        End If
        _series(label) = New SeriesData(label, color, MaxPoints)
    End Sub

    Public Sub SetSeriesEnabled(label As String, enabled As Boolean)
        Dim data As SeriesData = Nothing
        If _series.TryGetValue(label, data) Then
            data.Enabled = enabled
        End If
    End Sub

    <System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden),
     System.ComponentModel.Browsable(False)>
    Public Property TimeWindowSeconds As Single
        Get
            Return _timeWindowSeconds
        End Get
        Set(value As Single)
            If Single.IsNaN(value) OrElse Single.IsInfinity(value) Then
                _timeWindowSeconds = 0
            Else
                _timeWindowSeconds = Math.Max(0.0F, value)
            End If
            Invalidate()
        End Set
    End Property

    Public Sub AppendSnapshot(values As Dictionary(Of String, Single), timestampUtc As DateTime)
        If values Is Nothing Then
            Return
        End If

        If _count = 0 Then
            _startUtc = timestampUtc
        End If

        Dim seconds As Single = CSng((timestampUtc - _startUtc).TotalSeconds)
        _timestamps(_index) = seconds

        For Each kvp As KeyValuePair(Of String, SeriesData) In _series
            Dim value As Single
            If values.TryGetValue(kvp.Key, value) Then
                kvp.Value.Values(_index) = value
            Else
                kvp.Value.Values(_index) = Single.NaN
            End If
        Next

        _index = (_index + 1) Mod MaxPoints
        If _count < MaxPoints Then
            _count += 1
        End If
    End Sub

    Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
        MyBase.OnMouseMove(e)
        _hoverPoint = e.Location
        _hoverActive = True
        Invalidate()
    End Sub

    Protected Overrides Sub OnMouseLeave(e As EventArgs)
        MyBase.OnMouseLeave(e)
        _hoverActive = False
        Invalidate()
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        MyBase.OnPaint(e)

        Dim g As Graphics = e.Graphics
        g.Clear(BackColor)
        g.SmoothingMode = SmoothingMode.HighSpeed
        g.CompositingQuality = CompositingQuality.HighSpeed
        g.InterpolationMode = InterpolationMode.NearestNeighbor
        g.PixelOffsetMode = PixelOffsetMode.Half

        If _count = 0 Then
            TextRenderer.DrawText(g, "No data yet.", Font, ClientRectangle, ForeColor, TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter)
            Return
        End If

        Dim leftPad As Integer = 50
        Dim rightPad As Integer = 12
        Dim topPad As Integer = 10
        Dim bottomPad As Integer = 24
        Dim plotRect As New Rectangle(leftPad, topPad, Math.Max(1, Width - leftPad - rightPad), Math.Max(1, Height - topPad - bottomPad))

        Dim startIndex As Integer = If(_count = MaxPoints, _index, 0)
        Dim endIndex As Integer = (_index - 1 + MaxPoints) Mod MaxPoints
        Dim minTimeBase As Single = _timestamps(startIndex)
        Dim maxTime As Single = _timestamps(endIndex)
        Dim windowSeconds As Single = _timeWindowSeconds
        Dim useWindow As Boolean = windowSeconds > 0.0F
        Dim minTimeWindow As Single = If(useWindow, maxTime - windowSeconds, minTimeBase)
        Dim minTime As Single = If(useWindow, Math.Max(0.0F, minTimeWindow), minTimeBase)
        If maxTime <= minTime Then
            maxTime = minTime + 1.0F
        End If

        Dim minTemp As Single = Single.MaxValue
        Dim maxTemp As Single = Single.MinValue
        Dim enabledSeries As New List(Of SeriesData)()

        For Each series As SeriesData In _series.Values
            If Not series.Enabled Then
                Continue For
            End If
            enabledSeries.Add(series)
            For i As Integer = 0 To _count - 1
                Dim idx As Integer = (startIndex + i) Mod MaxPoints
                Dim ts As Single = _timestamps(idx)
                If useWindow AndAlso ts < minTimeWindow Then
                    Continue For
                End If

                Dim v As Single = series.Values(idx)
                If Single.IsNaN(v) Then
                    Continue For
                End If
                If v < minTemp Then minTemp = v
                If v > maxTemp Then maxTemp = v
            Next
        Next

        If enabledSeries.Count = 0 OrElse minTemp = Single.MaxValue OrElse maxTemp = Single.MinValue Then
            TextRenderer.DrawText(g, "Select sensors to plot.", Font, ClientRectangle, ForeColor, TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter)
            Return
        End If

        If maxTemp <= minTemp Then
            maxTemp = minTemp + 1
        End If

        Dim tempPad As Single = Math.Max(1.0F, (maxTemp - minTemp) * 0.08F)
        minTemp -= tempPad
        maxTemp += tempPad

        For i As Integer = 0 To 4
            Dim y As Single = plotRect.Top + (plotRect.Height * i / 4.0F)
            g.DrawLine(_gridPen, plotRect.Left, y, plotRect.Right, y)
        Next

        g.DrawRectangle(_axisPen, plotRect)

        Dim minLabel As String = FormatTempLabel(minTemp)
        Dim maxLabel As String = FormatTempLabel(maxTemp)
        TextRenderer.DrawText(g, maxLabel, Font, New Point(2, plotRect.Top - 2), ForeColor)
        TextRenderer.DrawText(g, minLabel, Font, New Point(2, plotRect.Bottom - Font.Height), ForeColor)

        Dim minTimeLabel As String = $"{minTime:F0}s"
        Dim maxTimeLabel As String = $"{maxTime:F0}s"
        TextRenderer.DrawText(g, minTimeLabel, Font, New Point(plotRect.Left, plotRect.Bottom + 2), ForeColor)
        Dim maxTimeWidth As Integer = TextRenderer.MeasureText(maxTimeLabel, Font).Width
        TextRenderer.DrawText(g, maxTimeLabel, Font, New Point(Math.Max(0, plotRect.Right - maxTimeWidth), plotRect.Bottom + 2), ForeColor)

        Dim xScale As Single = plotRect.Width / (maxTime - minTime)
        Dim yScale As Single = plotRect.Height / (maxTemp - minTemp)

        For Each series As SeriesData In enabledSeries

            Dim hasPrev As Boolean = False
            Dim prevPoint As PointF = PointF.Empty

            For i As Integer = 0 To _count - 1
                Dim idx As Integer = (startIndex + i) Mod MaxPoints
                Dim ts As Single = _timestamps(idx)
                If useWindow AndAlso ts < minTimeWindow Then
                    hasPrev = False
                    Continue For
                End If

                Dim v As Single = series.Values(idx)
                If Single.IsNaN(v) Then
                    hasPrev = False
                    Continue For
                End If

                Dim x As Single = plotRect.Left + (ts - minTime) * xScale
                Dim y As Single = plotRect.Bottom - ((v - minTemp) * yScale)
                Dim current As New PointF(x, y)

                If hasPrev Then
                    g.DrawLine(series.Pen, prevPoint, current)
                End If

                prevPoint = current
                hasPrev = True
            Next
        Next

        DrawLegend(g, plotRect, enabledSeries)
        DrawHover(g, plotRect, startIndex, minTime, maxTime, minTemp, xScale, yScale, enabledSeries, useWindow, minTimeWindow)
    End Sub

    Private Shared Function FormatTempLabel(value As Single) As String
        Return value.ToString("F0") & ChrW(&HB0) & "C"
    End Function

    Private Sub DrawHover(g As Graphics, plotRect As Rectangle, startIndex As Integer, minTime As Single, maxTime As Single, minTemp As Single, xScale As Single, yScale As Single, enabledSeries As List(Of SeriesData), useWindow As Boolean, minTimeWindow As Single)
        If Not _hoverActive OrElse enabledSeries.Count = 0 Then
            Return
        End If

        If Not plotRect.Contains(_hoverPoint) Then
            Return
        End If

        Dim hoverX As Single = _hoverPoint.X
        Dim hoverTime As Single
        Dim totalTime As Single = Math.Max(1.0F, maxTime - minTime)
        Dim normalizedX As Single = (hoverX - plotRect.Left) / Math.Max(1.0F, plotRect.Width)
        hoverTime = minTime + (totalTime * Math.Max(0.0F, Math.Min(1.0F, normalizedX)))

        Dim bestIndex As Integer = -1
        Dim bestDiff As Single = Single.MaxValue
        For i As Integer = 0 To _count - 1
            Dim idx As Integer = (startIndex + i) Mod MaxPoints
            Dim ts As Single = _timestamps(idx)
            If useWindow AndAlso ts < minTimeWindow Then
                Continue For
            End If
            Dim diff As Single = Math.Abs(ts - hoverTime)
            If diff < bestDiff Then
                bestDiff = diff
                bestIndex = idx
            End If
        Next

        If bestIndex < 0 Then
            Return
        End If

        Dim bestSeries As SeriesData = Nothing
        Dim bestValue As Single = Single.NaN
        Dim bestY As Single = 0.0F
        Dim bestDist As Single = HoverThresholdPx + 1

        For Each series As SeriesData In enabledSeries
            Dim v As Single = series.Values(bestIndex)
            If Single.IsNaN(v) Then
                Continue For
            End If
            Dim y As Single = plotRect.Bottom - ((v - minTemp) * yScale)
            Dim dist As Single = Math.Abs(y - _hoverPoint.Y)
            If dist < bestDist Then
                bestDist = dist
                bestSeries = series
                bestValue = v
                bestY = y
            End If
        Next

        If bestSeries Is Nothing OrElse bestDist > HoverThresholdPx Then
            Return
        End If

        Dim sampleTime As Single = _timestamps(bestIndex)
        Dim markerX As Single = plotRect.Left + ((sampleTime - minTime) * xScale)
        g.DrawLine(_hoverLinePen, markerX, plotRect.Top, markerX, plotRect.Bottom)

        Dim dotSize As Single = 6.0F
        Dim dotRect As New RectangleF(markerX - dotSize / 2.0F, bestY - dotSize / 2.0F, dotSize, dotSize)
        g.FillEllipse(bestSeries.Brush, dotRect)
        g.DrawEllipse(_legendSwatchBorderPen, dotRect)

        Dim hoverText As String = $"{bestSeries.Label}: {FormatTempLabel(bestValue)}  t={sampleTime:F1}s"
        Dim textFlags As TextFormatFlags = TextFormatFlags.NoPadding
        Dim textSize As Size = TextRenderer.MeasureText(hoverText, Font, New Size(Integer.MaxValue, Integer.MaxValue), textFlags)
        Dim padding As Integer = 6
        Dim boxWidth As Integer = textSize.Width + padding * 2
        Dim boxHeight As Integer = textSize.Height + padding * 2
        Dim boxX As Integer = CInt(markerX + 10)
        Dim boxY As Integer = CInt(bestY - boxHeight - 8)

        If boxX + boxWidth > plotRect.Right Then
            boxX = plotRect.Right - boxWidth - 4
        End If
        If boxY < plotRect.Top Then
            boxY = plotRect.Top + 4
        End If
        If boxY + boxHeight > plotRect.Bottom Then
            boxY = plotRect.Bottom - boxHeight - 4
        End If

        Dim boxRect As New Rectangle(boxX, boxY, boxWidth, boxHeight)
        g.FillRectangle(_hoverBackBrush, boxRect)
        g.DrawRectangle(_hoverBorderPen, boxRect)
        TextRenderer.DrawText(g, hoverText, Font, New Rectangle(boxX + padding, boxY + padding, textSize.Width, textSize.Height), ForeColor, textFlags)
    End Sub

    Private Sub DrawLegend(g As Graphics, plotRect As Rectangle, enabledSeries As List(Of SeriesData))
        If enabledSeries.Count = 0 Then
            Return
        End If

        Dim padding As Integer = 6
        Dim swatchSize As Integer = 10
        Dim lineHeight As Integer = Math.Max(Font.Height + 4, swatchSize + 2)
        Dim maxLines As Integer = Math.Max(1, (plotRect.Height - padding * 2) \ lineHeight)
        Dim visibleCount As Integer = Math.Min(enabledSeries.Count, maxLines)
        Dim showMore As Boolean = enabledSeries.Count > visibleCount
        If showMore AndAlso visibleCount > 1 Then
            visibleCount -= 1
        End If

        Dim legendWidth As Integer = Math.Min(plotRect.Width - 8, 320)
        Dim totalLines As Integer = visibleCount + If(showMore, 1, 0)
        Dim legendHeight As Integer = padding * 2 + totalLines * lineHeight
        Dim legendX As Integer = plotRect.Right - legendWidth - 6
        Dim legendY As Integer = plotRect.Top + 6
        If legendX < plotRect.Left + 2 Then
            legendX = plotRect.Left + 2
        End If

        Dim legendRect As New Rectangle(legendX, legendY, legendWidth, legendHeight)
        g.FillRectangle(_legendBackBrush, legendRect)
        g.DrawRectangle(_legendBorderPen, legendRect)

        For i As Integer = 0 To visibleCount - 1
            Dim series As SeriesData = enabledSeries(i)
            Dim rowY As Integer = legendY + padding + i * lineHeight
            Dim swatchRect As New Rectangle(legendX + padding, rowY + (lineHeight - swatchSize) \ 2, swatchSize, swatchSize)
            g.FillRectangle(series.Brush, swatchRect)
            g.DrawRectangle(_legendSwatchBorderPen, swatchRect)

            Dim textRect As New Rectangle(swatchRect.Right + 6, rowY, legendWidth - (swatchRect.Width + padding * 2 + 6), lineHeight)
            TextRenderer.DrawText(g, series.Label, Font, textRect, ForeColor, TextFormatFlags.VerticalCenter Or TextFormatFlags.EndEllipsis)
        Next

        If showMore Then
            Dim moreCount As Integer = enabledSeries.Count - visibleCount
            Dim rowY As Integer = legendY + padding + visibleCount * lineHeight
            Dim textRect As New Rectangle(legendX + padding, rowY, legendWidth - padding * 2, lineHeight)
            TextRenderer.DrawText(g, $"+{moreCount} more", Font, textRect, ForeColor, TextFormatFlags.VerticalCenter)
        End If
    End Sub
End Class
