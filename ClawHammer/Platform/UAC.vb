
Imports System.Security.Principal

Module UAC

    ' Win32 message for the UAC shield icon on buttons.
    Private Declare Ansi Function SendMessage _
                         Lib "user32.dll" _
                         Alias "SendMessageA" (ByVal hwnd As Integer, _
                                               ByVal wMsg As Integer, _
                                               ByVal wParam As Integer, _
                                               ByVal lParam As String) As Integer
    Private Const BCM_FIRST As Int32 = &H1600
    Private Const BCM_SETSHIELD As Int32 = (BCM_FIRST + &HC)

    ' Checks if the process is elevated.
    Public Function IsAdmin() As Boolean

        Dim id As WindowsIdentity = WindowsIdentity.GetCurrent()
        Dim p As WindowsPrincipal = New WindowsPrincipal(id)
        Return p.IsInRole(WindowsBuiltInRole.Administrator)
    End Function

    ' Adds the UAC shield icon to a button.
    Public Sub AddShieldToButton(ByRef b As Button)
        b.FlatStyle = FlatStyle.System
        SendMessage(b.Handle, BCM_SETSHIELD, 0, &HFFFFFFFF)
    End Sub

    ' Restarts the current process with elevation.
    Public Sub RestartElevated()

        Dim startInfo As ProcessStartInfo = New ProcessStartInfo()
        startInfo.UseShellExecute = True
        startInfo.WorkingDirectory = Environment.CurrentDirectory
        startInfo.FileName = Application.ExecutablePath
        startInfo.Verb = "runas"

        Try
            Dim p As Process = Process.Start(startInfo)
        Catch ex As Exception
            Return
        End Try

        Application.Exit()
    End Sub

End Module
