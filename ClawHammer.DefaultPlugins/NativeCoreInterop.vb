Imports System.Runtime.InteropServices
Imports System.Threading
Imports ClawHammer.PluginContracts

Friend Module NativeCoreInterop
    Private Const NativeLibraryName As String = "ClawHammer.NativeCore.dll"
    Private _availability As Integer = -1

    Public Enum NativeStressKernel
        FloatingPoint
        Avx
        IntegerPrimes
        IntegerHeavy
        MemoryBandwidth
    End Enum

    <UnmanagedFunctionPointer(CallingConvention.StdCall)>
    Private Delegate Function NativeShouldCancelCallback(userData As IntPtr) As Integer

    <UnmanagedFunctionPointer(CallingConvention.StdCall)>
    Private Delegate Sub NativeReportProgressCallback(userData As IntPtr, operations As Integer)

    <UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet:=CharSet.Unicode)>
    Private Delegate Sub NativeReportMessageCallback(userData As IntPtr, <MarshalAs(UnmanagedType.LPWStr)> message As String)

    Private Delegate Function NativeRunWorkerCallback(ByRef config As NativeWorkerConfig, ByRef callbacks As NativeWorkerCallbacks) As Integer

    <StructLayout(LayoutKind.Sequential)>
    Private Structure NativeWorkerConfig
        Public WorkerId As Integer
        Public Seed As ULong
        Public ValidationMode As Integer
        Public ValidationIntervalMs As Integer
        Public BatchSize As Integer
        Public PrimeRangeMin As Long
        Public PrimeRangeMax As Long
        Public MemoryBufferBytes As Integer
    End Structure

    <StructLayout(LayoutKind.Sequential)>
    Private Structure NativeWorkerCallbacks
        Public UserData As IntPtr
        <MarshalAs(UnmanagedType.FunctionPtr)>
        Public ShouldCancel As NativeShouldCancelCallback
        <MarshalAs(UnmanagedType.FunctionPtr)>
        Public ReportProgress As NativeReportProgressCallback
        <MarshalAs(UnmanagedType.FunctionPtr)>
        Public ReportError As NativeReportMessageCallback
        <MarshalAs(UnmanagedType.FunctionPtr)>
        Public ReportStatus As NativeReportMessageCallback
    End Structure

    <DllImport(NativeLibraryName, CallingConvention:=CallingConvention.StdCall, EntryPoint:="CH_IsAvailable")>
    Private Function NativeIsAvailable() As Integer
    End Function

    <DllImport(NativeLibraryName, CallingConvention:=CallingConvention.StdCall, EntryPoint:="CH_RunFloatingPoint")>
    Private Function NativeRunFloatingPoint(ByRef config As NativeWorkerConfig, ByRef callbacks As NativeWorkerCallbacks) As Integer
    End Function

    <DllImport(NativeLibraryName, CallingConvention:=CallingConvention.StdCall, EntryPoint:="CH_RunAvx")>
    Private Function NativeRunAvx(ByRef config As NativeWorkerConfig, ByRef callbacks As NativeWorkerCallbacks) As Integer
    End Function

    <DllImport(NativeLibraryName, CallingConvention:=CallingConvention.StdCall, EntryPoint:="CH_RunIntegerPrimes")>
    Private Function NativeRunIntegerPrimes(ByRef config As NativeWorkerConfig, ByRef callbacks As NativeWorkerCallbacks) As Integer
    End Function

    <DllImport(NativeLibraryName, CallingConvention:=CallingConvention.StdCall, EntryPoint:="CH_RunIntegerHeavy")>
    Private Function NativeRunIntegerHeavy(ByRef config As NativeWorkerConfig, ByRef callbacks As NativeWorkerCallbacks) As Integer
    End Function

    <DllImport(NativeLibraryName, CallingConvention:=CallingConvention.StdCall, EntryPoint:="CH_RunMemoryBandwidth")>
    Private Function NativeRunMemoryBandwidth(ByRef config As NativeWorkerConfig, ByRef callbacks As NativeWorkerCallbacks) As Integer
    End Function

    Public Function IsAvailable() As Boolean
        Dim cached As Integer = Threading.Volatile.Read(_availability)
        If cached >= 0 Then
            Return cached = 1
        End If

        Dim available As Boolean = False
        Try
            available = NativeIsAvailable() = 1
        Catch ex As DllNotFoundException
            available = False
        Catch ex As BadImageFormatException
            available = False
        Catch ex As EntryPointNotFoundException
            available = False
        End Try

        Threading.Volatile.Write(_availability, If(available, 1, 0))
        Return available
    End Function

    Public Function RunWorker(kernel As NativeStressKernel,
                              workerId As Integer,
                              seed As ULong,
                              token As CancellationToken,
                              validation As ValidationSettings,
                              reportProgress As Action(Of Integer),
                              reportError As Action(Of String),
                              reportStatus As Action(Of String),
                              primeRangeMin As Long,
                              primeRangeMax As Long,
                              memoryBufferBytes As Integer) As Integer
        Dim config As New NativeWorkerConfig() With {
            .WorkerId = workerId,
            .Seed = seed,
            .ValidationMode = If(validation IsNot Nothing, CInt(validation.Mode), CInt(ValidationMode.Off)),
            .ValidationIntervalMs = If(validation IsNot Nothing, validation.IntervalMs, 30000),
            .BatchSize = If(validation IsNot Nothing, validation.BatchSize, 4096),
            .PrimeRangeMin = primeRangeMin,
            .PrimeRangeMax = primeRangeMax,
            .MemoryBufferBytes = memoryBufferBytes
        }

        Dim shouldCancel As NativeShouldCancelCallback =
            Function(userData)
                Return If(token.IsCancellationRequested, 1, 0)
            End Function
        Dim progressCallback As NativeReportProgressCallback =
            Sub(userData, operations)
                reportProgress?.Invoke(operations)
            End Sub
        Dim errorCallback As NativeReportMessageCallback =
            Sub(userData, message)
                If validation IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(message) Then
                    validation.RecordError(message)
                End If
                reportError?.Invoke(If(message, String.Empty))
            End Sub
        Dim statusCallback As NativeReportMessageCallback =
            Sub(userData, message)
                reportStatus?.Invoke(If(message, String.Empty))
            End Sub

        Dim callbacks As New NativeWorkerCallbacks() With {
            .UserData = IntPtr.Zero,
            .ShouldCancel = shouldCancel,
            .ReportProgress = progressCallback,
            .ReportError = errorCallback,
            .ReportStatus = statusCallback
        }

        Dim runner As NativeRunWorkerCallback = ResolveRunner(kernel)
        Return runner(config, callbacks)
    End Function

    Private Function ResolveRunner(kernel As NativeStressKernel) As NativeRunWorkerCallback
        Select Case kernel
            Case NativeStressKernel.Avx
                Return AddressOf NativeRunAvx
            Case NativeStressKernel.IntegerPrimes
                Return AddressOf NativeRunIntegerPrimes
            Case NativeStressKernel.IntegerHeavy
                Return AddressOf NativeRunIntegerHeavy
            Case NativeStressKernel.MemoryBandwidth
                Return AddressOf NativeRunMemoryBandwidth
            Case Else
                Return AddressOf NativeRunFloatingPoint
        End Select
    End Function
End Module
