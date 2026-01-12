Imports System.Security.Principal
Imports System.Runtime.InteropServices

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

    Private Const TOKEN_QUERY As Integer = &H8
    Private Const TokenElevation As Integer = 20
    Private Const TokenElevationType As Integer = 18

    <StructLayout(LayoutKind.Sequential)>
    Private Structure TOKEN_ELEVATION
        Public TokenIsElevated As Integer
    End Structure

    Private Enum TokenElevationTypeValue As Integer
        DefaultType = 1
        Full = 2
        Limited = 3
    End Enum

    Private Declare Auto Function OpenProcessToken Lib "advapi32.dll" (ByVal ProcessHandle As IntPtr, ByVal DesiredAccess As Integer, ByRef TokenHandle As IntPtr) As Boolean
    Private Declare Auto Function GetTokenInformation Lib "advapi32.dll" (ByVal TokenHandle As IntPtr, ByVal TokenInformationClass As Integer, ByRef TokenInformation As TOKEN_ELEVATION, ByVal TokenInformationLength As Integer, ByRef ReturnLength As Integer) As Boolean
    Private Declare Auto Function GetTokenInformation Lib "advapi32.dll" (ByVal TokenHandle As IntPtr, ByVal TokenInformationClass As Integer, ByRef TokenInformation As Integer, ByVal TokenInformationLength As Integer, ByRef ReturnLength As Integer) As Boolean
    Private Declare Auto Function CloseHandle Lib "kernel32.dll" (ByVal handle As IntPtr) As Boolean

    ' Checks if the process is elevated.
    Public Function IsAdmin() As Boolean
        Return IsProcessElevated()
    End Function

    ' Checks if the current user belongs to the Administrators group.
    Public Function IsUserAdmin() As Boolean
        Dim elevationType As Nullable(Of TokenElevationTypeValue) = GetElevationType()
        If elevationType.HasValue Then
            Return elevationType.Value <> TokenElevationTypeValue.DefaultType
        End If

        Dim id As WindowsIdentity = WindowsIdentity.GetCurrent()
        Dim p As WindowsPrincipal = New WindowsPrincipal(id)
        Return p.IsInRole(WindowsBuiltInRole.Administrator)
    End Function

    Private Function IsProcessElevated() As Boolean
        Dim token As IntPtr = IntPtr.Zero
        If Not OpenProcessToken(Process.GetCurrentProcess().Handle, TOKEN_QUERY, token) Then
            Return False
        End If

        Try
            Dim elevation As TOKEN_ELEVATION
            Dim size As Integer = Marshal.SizeOf(elevation)
            Dim returned As Integer = 0
            If GetTokenInformation(token, TokenElevation, elevation, size, returned) Then
                Return elevation.TokenIsElevated <> 0
            End If
        Finally
            If token <> IntPtr.Zero Then
                CloseHandle(token)
            End If
        End Try

        Return False
    End Function

    Private Function GetElevationType() As Nullable(Of TokenElevationTypeValue)
        Dim token As IntPtr = IntPtr.Zero
        If Not OpenProcessToken(Process.GetCurrentProcess().Handle, TOKEN_QUERY, token) Then
            Return Nothing
        End If

        Try
            Dim elevationType As Integer = 0
            Dim size As Integer = Marshal.SizeOf(GetType(Integer))
            Dim returned As Integer = 0
            If GetTokenInformation(token, TokenElevationType, elevationType, size, returned) Then
                If elevationType >= 1 AndAlso elevationType <= 3 Then
                    Return CType(elevationType, TokenElevationTypeValue)
                End If
            End If
        Finally
            If token <> IntPtr.Zero Then
                CloseHandle(token)
            End If
        End Try

        Return Nothing
    End Function

    ' Adds the UAC shield icon to a button.
    Public Sub AddShieldToButton(ByRef b As Button)
        b.FlatStyle = FlatStyle.System
        SendMessage(b.Handle, BCM_SETSHIELD, 0, &HFFFFFFFF)
    End Sub

    ' Attempts to restart the current process with elevation.
    Public Function TryRestartElevated() As Boolean
        Dim startInfo As ProcessStartInfo = New ProcessStartInfo() With {
            .UseShellExecute = True,
            .WorkingDirectory = Environment.CurrentDirectory,
            .FileName = Application.ExecutablePath,
            .Verb = "runas"
        }

        Try
            Dim p As Process = Process.Start(startInfo)
            Return p IsNot Nothing
        Catch
            Return False
        End Try
    End Function

    ' Restarts the current process with elevation.
    Public Sub RestartElevated()
        If TryRestartElevated() Then
            Application.Exit()
        End If
    End Sub

End Module
