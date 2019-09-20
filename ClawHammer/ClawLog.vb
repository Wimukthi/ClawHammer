Imports System.IO
Imports System.Text
Imports System.Windows.Forms

Public Class ClawLog
    Public Sub New()

        'default constructor

    End Sub


    Public Sub WriteLog(ByVal msg As String)
        Try


            'check and make the directory if necessary; this is set to look in the application
            'folder, you may wish to place the error log in another location depending upon the
            'the user's role and write access to different areas of the file system
            If Not System.IO.Directory.Exists(Application.StartupPath & "\Logs\") Then
                System.IO.Directory.CreateDirectory(Application.StartupPath & "\Logs\")
            End If

            'check the file
            Dim fs As FileStream = New FileStream(Application.StartupPath & "\Logs\ClawHammer_log.txt", FileMode.OpenOrCreate, FileAccess.ReadWrite)
            Dim s As StreamWriter = New StreamWriter(fs)
            s.Close()
            fs.Close()

            'log it
            Dim fs1 As FileStream = New FileStream(Application.StartupPath & "\Logs\ClawHammer_log.txt", FileMode.Append, FileAccess.Write)
            Dim s1 As StreamWriter = New StreamWriter(fs1)


            s1.Write("Date/Time: " & DateTime.Now.ToString() & vbCrLf)
            s1.Write("Message: " & msg & vbCrLf)
            s1.Write("===========================================================================================" & vbCrLf)
            s1.Close()
            fs1.Close()
        Catch ex As Exception

        End Try
    End Sub
End Class
