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
