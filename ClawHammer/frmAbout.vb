
Public Class frmabout

    Private Sub frmabout_Load(ByVal sender As System.Object, _
                              ByVal e As System.EventArgs) _
            Handles MyBase.Load

        Try
            lblversion.Text = My.Application.Info.Version.ToString
            TextBox1.Text = "Application Version : " & My.Application.Info.Version.ToString & vbCrLf & "Program Icon © David Lanham, from the Icon Pack Amora"
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

End Class
