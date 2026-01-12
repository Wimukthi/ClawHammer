Imports System.Drawing
Imports System.Windows.Forms

Public Class frmInit
    Inherits Form

    Private ReadOnly _statusLabel As Label
    Private ReadOnly _details As ListBox
    Private ReadOnly _progress As ProgressBar
    Private ReadOnly _toolTip As ToolTip

    Public Sub New()
        Text = "Initializing ClawHammer"
        FormBorderStyle = FormBorderStyle.FixedDialog
        StartPosition = FormStartPosition.CenterScreen
        MaximizeBox = False
        MinimizeBox = False
        ShowInTaskbar = False
        Size = New Size(560, 360)

        Try
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath)
        Catch
        End Try

        _statusLabel = New Label() With {
            .AutoSize = False,
            .Dock = DockStyle.Top,
            .Height = 28,
            .TextAlign = ContentAlignment.MiddleLeft,
            .Padding = New Padding(12, 6, 12, 4),
            .Text = "Starting..."
        }

        _progress = New ProgressBar() With {
            .Dock = DockStyle.Top,
            .Height = 18,
            .Style = ProgressBarStyle.Marquee,
            .MarqueeAnimationSpeed = 30
        }

        _details = New ListBox() With {
            .Dock = DockStyle.Fill
        }

        Controls.Add(_details)
        Controls.Add(_progress)
        Controls.Add(_statusLabel)

        _toolTip = New ToolTip() With {
            .AutoPopDelay = 12000,
            .InitialDelay = 500,
            .ReshowDelay = 150,
            .ShowAlways = True
        }
        _toolTip.SetToolTip(_statusLabel, "Current initialization step.")
        _toolTip.SetToolTip(_progress, "Initialization progress indicator.")
        _toolTip.SetToolTip(_details, "Detailed startup messages.")

        UiThemeManager.ApplyTheme(Me)
    End Sub

    Public Sub SetStatus(message As String)
        If InvokeRequired Then
            BeginInvoke(Sub() SetStatus(message))
            Return
        End If
        _statusLabel.Text = message
    End Sub

    Public Sub AddDetail(message As String)
        If InvokeRequired Then
            BeginInvoke(Sub() AddDetail(message))
            Return
        End If
        _details.Items.Add($"{Date.Now:HH:mm:ss}  {message}")
        _details.TopIndex = Math.Max(0, _details.Items.Count - 1)
    End Sub
End Class
