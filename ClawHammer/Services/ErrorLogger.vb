Imports System.IO
Imports System.Windows.Forms

<CLSCompliant(True)>
Public Class ErrorLogger
    ' Appends a structured error entry to the local Errors folder.
    Public Sub WriteToErrorLog(ByVal msg As String, ByVal stkTrace As String, ByVal title As String)
        Try
            Dim errorDir As String = Path.Combine(Application.StartupPath, "Errors")
            Directory.CreateDirectory(errorDir)

            Dim logPath As String = Path.Combine(errorDir, "Error_Log.txt")
            Dim entry As String = "Title: " & title & vbCrLf &
                "Message: " & msg & vbCrLf &
                "StackTrace: " & stkTrace & vbCrLf &
                "Date/Time: " & DateTime.Now.ToString() & vbCrLf &
                New String("="c, 91) & vbCrLf
            File.AppendAllText(logPath, entry)
        Catch ex As Exception
        End Try
    End Sub
End Class
