Imports System.IO
Imports System.Windows.Forms

Public Class ClawLog
    ' Appends a log entry to the local Logs folder.
    Public Sub WriteLog(ByVal msg As String)
        Try
            Dim logDir As String = Path.Combine(Application.StartupPath, "Logs")
            Directory.CreateDirectory(logDir)

            Dim logPath As String = Path.Combine(logDir, "ClawHammer_log.txt")
            Dim entry As String = "Date/Time: " & DateTime.Now.ToString() & vbCrLf &
                "Message: " & msg & vbCrLf &
                New String("="c, 91) & vbCrLf
            File.AppendAllText(logPath, entry)
        Catch ex As Exception
        End Try
    End Sub
End Class
