Imports System.Drawing
Imports System.IO
Imports System.Text.Json
Imports System.Windows.Forms

Public Class UiWindowLayout
    Public Property X As Integer
    Public Property Y As Integer
    Public Property Width As Integer
    Public Property Height As Integer
    Public Property WindowState As FormWindowState = FormWindowState.Normal
    Public Property SplitterDistance As Integer = -1
    Public Property ColumnWidths As List(Of Integer) = New List(Of Integer)()
End Class

Public Class UiLayoutStore
    Public Property MainWindow As UiWindowLayout = New UiWindowLayout()
    Public Property TempPlotWindow As UiWindowLayout = New UiWindowLayout()
End Class

Public Module UiLayoutManager
    Private Const LayoutFileName As String = "ui-layout.json"

    Public Function GetLayoutPath() As String
        Return Path.Combine(AppContext.BaseDirectory, LayoutFileName)
    End Function

    Public Function LoadLayout() As UiLayoutStore
        Dim path As String = GetLayoutPath()
        Dim layout As UiLayoutStore = LoadLayoutFromPath(path)
        If layout IsNot Nothing Then
            Return layout
        End If

        Dim legacyPath As String = GetLegacyLayoutPath()
        Dim legacyLayout As UiLayoutStore = LoadLayoutFromPath(legacyPath)
        If legacyLayout IsNot Nothing Then
            SaveLayout(legacyLayout)
            Return legacyLayout
        End If

        Return New UiLayoutStore()
    End Function

    Public Sub SaveLayout(layout As UiLayoutStore)
        If layout Is Nothing Then
            Return
        End If

        Dim path As String = GetLayoutPath()
        Try
            Dim options As New JsonSerializerOptions With {
                .WriteIndented = True
            }
            Dim json As String = JsonSerializer.Serialize(layout, options)
            File.WriteAllText(path, json)
        Catch
        End Try
    End Sub

    Private Function LoadLayoutFromPath(path As String) As UiLayoutStore
        If String.IsNullOrWhiteSpace(path) OrElse Not File.Exists(path) Then
            Return Nothing
        End If

        Try
            Dim json As String = File.ReadAllText(path)
            Dim options As New JsonSerializerOptions With {
                .PropertyNameCaseInsensitive = True
            }
            Dim layout As UiLayoutStore = JsonSerializer.Deserialize(Of UiLayoutStore)(json, options)
            If layout Is Nothing Then
                Return Nothing
            End If
            If layout.MainWindow Is Nothing Then
                layout.MainWindow = New UiWindowLayout()
            End If
            If layout.TempPlotWindow Is Nothing Then
                layout.TempPlotWindow = New UiWindowLayout()
            End If
            Return layout
        Catch
            Return Nothing
        End Try
    End Function

    Private Function GetLegacyLayoutPath() As String
        Dim dir As String = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ClawHammer")
        Return Path.Combine(dir, LayoutFileName)
    End Function

    Public Sub CaptureWindowLayout(target As Form, layout As UiWindowLayout)
        If target Is Nothing OrElse layout Is Nothing Then
            Return
        End If

        Dim state As FormWindowState = target.WindowState
        If state = FormWindowState.Minimized Then
            state = FormWindowState.Normal
        End If

        Dim bounds As Rectangle = If(target.WindowState = FormWindowState.Normal, target.Bounds, target.RestoreBounds)
        layout.X = bounds.X
        layout.Y = bounds.Y
        layout.Width = bounds.Width
        layout.Height = bounds.Height
        layout.WindowState = state
    End Sub

    Public Sub ApplyWindowLayout(target As Form, layout As UiWindowLayout)
        If target Is Nothing OrElse layout Is Nothing Then
            Return
        End If

        If layout.Width <= 0 OrElse layout.Height <= 0 Then
            Return
        End If

        Dim desired As New Rectangle(layout.X, layout.Y, layout.Width, layout.Height)
        Dim adjusted As Rectangle = EnsureVisibleBounds(desired)

        target.StartPosition = FormStartPosition.Manual
        target.Bounds = adjusted

        If layout.WindowState = FormWindowState.Maximized Then
            target.WindowState = FormWindowState.Maximized
        Else
            target.WindowState = FormWindowState.Normal
        End If
    End Sub

    Public Sub ApplySplitterDistanceSafe(split As SplitContainer, distance As Integer)
        If split Is Nothing Then
            Return
        End If
        If distance <= 0 Then
            Return
        End If

        Dim min1 As Integer = Math.Max(0, split.Panel1MinSize)
        Dim min2 As Integer = Math.Max(0, split.Panel2MinSize)
        Dim total As Integer = If(split.Orientation = Orientation.Vertical, split.Width, split.Height)
        Dim maxDistance As Integer = Math.Max(min1, total - min2)
        Dim desired As Integer = Math.Max(min1, Math.Min(distance, maxDistance))
        split.SplitterDistance = desired
    End Sub

    Private Function EnsureVisibleBounds(bounds As Rectangle) As Rectangle
        Dim target As Screen = Nothing
        For Each screen As Screen In Screen.AllScreens
            If screen.WorkingArea.IntersectsWith(bounds) Then
                target = screen
                Exit For
            End If
        Next

        If target Is Nothing Then
            target = Screen.PrimaryScreen
        End If

        Dim area As Rectangle = target.WorkingArea
        Dim width As Integer = Math.Min(bounds.Width, area.Width)
        Dim height As Integer = Math.Min(bounds.Height, area.Height)
        Dim x As Integer = Math.Max(area.Left, Math.Min(bounds.X, area.Right - width))
        Dim y As Integer = Math.Max(area.Top, Math.Min(bounds.Y, area.Bottom - height))
        Return New Rectangle(x, y, width, height)
    End Function
End Module
