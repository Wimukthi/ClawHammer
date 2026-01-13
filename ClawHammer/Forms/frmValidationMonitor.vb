Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Windows.Forms

Friend Class ValidationStatusSnapshot
    Public Property WorkerId As Integer
    Public Property Kernel As String
    Public Property AffinityLabel As String
    Public Property Detail As String
    Public Property UpdatedUtc As DateTime
End Class

Friend Class frmValidationMonitor
    Inherits Form

    Private ReadOnly _list As ListView
    Private ReadOnly _toolTip As ToolTip

    Public Sub New()
        Text = "Validation Monitor"
        StartPosition = FormStartPosition.CenterParent
        Size = New Size(720, 420)
        MinimumSize = New Size(540, 320)

        Try
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath)
        Catch
        End Try

        _list = New ListView() With {
            .Dock = DockStyle.Fill,
            .View = View.Details,
            .FullRowSelect = True,
            .HideSelection = False,
            .BorderStyle = BorderStyle.FixedSingle
        }
        _list.Columns.Add("Worker", 70)
        _list.Columns.Add("Core/LP", 120)
        _list.Columns.Add("Kernel", 160)
        _list.Columns.Add("Status", 300)
        _list.Columns.Add("Updated", 90)

        _toolTip = New ToolTip() With {
            .AutoPopDelay = 12000,
            .InitialDelay = 500,
            .ReshowDelay = 150,
            .ShowAlways = True
        }
        _toolTip.SetToolTip(_list, "Latest validation status per worker thread.")

        Controls.Add(_list)
        UiThemeManager.ApplyTheme(Me)
    End Sub

    Public Sub UpdateStatuses(statuses As IReadOnlyList(Of ValidationStatusSnapshot))
        If IsDisposed Then
            Return
        End If

        If InvokeRequired Then
            BeginInvoke(Sub() UpdateStatuses(statuses))
            Return
        End If

        _list.BeginUpdate()
        _list.Items.Clear()

        If statuses IsNot Nothing Then
            For Each entry As ValidationStatusSnapshot In statuses
                Dim item As New ListViewItem($"W{entry.WorkerId}")
                item.SubItems.Add(If(entry.AffinityLabel, String.Empty))
                item.SubItems.Add(If(entry.Kernel, String.Empty))
                item.SubItems.Add(If(entry.Detail, String.Empty))

                Dim timeText As String = String.Empty
                If entry.UpdatedUtc <> DateTime.MinValue Then
                    timeText = entry.UpdatedUtc.ToLocalTime().ToString("HH:mm:ss")
                End If
                item.SubItems.Add(timeText)

                _list.Items.Add(item)
            Next
        End If

        _list.EndUpdate()
    End Sub
End Class
