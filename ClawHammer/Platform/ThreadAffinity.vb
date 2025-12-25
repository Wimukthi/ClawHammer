Imports System.Runtime.InteropServices

Public Module ThreadAffinity
    <DllImport("kernel32.dll")>
    Private Function GetCurrentThread() As IntPtr
    End Function

    <DllImport("kernel32.dll")>
    Private Function SetThreadAffinityMask(hThread As IntPtr, dwThreadAffinityMask As UIntPtr) As UIntPtr
    End Function

    Public Function TrySetCurrentThreadAffinity(coreIndex As Integer) As Boolean
        If coreIndex < 0 Then
            Return False
        End If

        Dim maxBits As Integer = If(IntPtr.Size = 8, 64, 32)
        If coreIndex >= maxBits Then
            Return False
        End If

        Dim mask As ULong = 1UL << coreIndex
        Dim result As UIntPtr = SetThreadAffinityMask(GetCurrentThread(), New UIntPtr(mask))
        Return result <> UIntPtr.Zero
    End Function
End Module
