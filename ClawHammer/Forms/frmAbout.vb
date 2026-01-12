
Public Class frmabout
    Private _toolTip As ToolTip

    Private Sub frmabout_Load(ByVal sender As System.Object, _
                              ByVal e As System.EventArgs) _
            Handles MyBase.Load

        Try
            TextBox1.ReadOnly = True
            TextBox1.Enabled = True
            TextBox1.TabStop = False
            UiThemeManager.ApplyTheme(Me)
            _toolTip = New ToolTip() With {
                .AutoPopDelay = 12000,
                .InitialDelay = 500,
                .ReshowDelay = 150,
                .ShowAlways = True
            }
            _toolTip.SetToolTip(Button1, "Close the About window.")
            _toolTip.SetToolTip(RichTextBox1, "License and credits.")
            _toolTip.SetToolTip(TextBox1, "Version and attribution details.")
            _toolTip.SetToolTip(lblversion, "Current application version.")
            _toolTip.SetToolTip(PictureBox1, "ClawHammer logo.")
            lblversion.Text = GetAppVersionDisplay()
            TextBox1.Text = "Application Version : " & GetAppVersionDisplay() & vbCrLf & "Program Icon © David Lanham, from the Icon Pack Amora"
            TextBox1.DeselectAll()

        Catch ex As Exception
            Dim el As New ErrorLogger
            el.WriteToErrorLog(ex.Message, ex.StackTrace, My.Application.Info.AssemblyName.ToString & " Encountered an Error")
        End Try

    End Sub


    Private Sub Button1_Click(ByVal sender As System.Object, _
                              ByVal e As System.EventArgs) _
            Handles Button1.Click
        Me.Close()
    End Sub

    Private Sub GroupBox1_Enter(sender As Object, e As EventArgs)

    End Sub
End Class


