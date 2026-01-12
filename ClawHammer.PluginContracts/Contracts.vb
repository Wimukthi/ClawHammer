Imports System.Threading


Public Enum ValidationMode
    Off
    Light
    Full
End Enum

Public Interface IStressWorker
    ReadOnly Property KernelName As String
    Sub Run(token As CancellationToken, reportProgress As Action(Of Integer), validation As ValidationSettings, reportError As Action(Of String), reportStatus As Action(Of String))
End Interface

Public Interface IStressPlugin
    ReadOnly Property Id As String
    ReadOnly Property DisplayName As String
    ReadOnly Property Description As String
    ReadOnly Property Category As String
    ReadOnly Property SortOrder As Integer
    ReadOnly Property IsAdvanced As Boolean
    ReadOnly Property SupportsValidation As Boolean
    Function CreateWorker(workerId As Integer, seed As ULong, context As StressPluginContext) As IStressWorker
End Interface

Public Class StressPluginContext
    Public ReadOnly Property TotalWorkers As Integer
    Public ReadOnly Property PrimeRangeMin As Long
    Public ReadOnly Property PrimeRangeMax As Long
    Public ReadOnly Property MemoryBufferBytes As Integer
    Public ReadOnly Property AvxSupported As Boolean

    Public Sub New(totalWorkers As Integer, primeRangeMin As Long, primeRangeMax As Long, memoryBufferBytes As Integer, avxSupported As Boolean)
        Me.TotalWorkers = Math.Max(1, totalWorkers)
        Me.PrimeRangeMin = primeRangeMin
        Me.PrimeRangeMax = primeRangeMax
        Me.MemoryBufferBytes = Math.Max(0, memoryBufferBytes)
        Me.AvxSupported = avxSupported
    End Sub
End Class
Public Module DefaultPluginIds
    Public Const FloatingPoint As String = "clawhammer.floating-point"
    Public Const IntegerPrimes As String = "clawhammer.integer-primes"
    Public Const Avx As String = "clawhammer.avx"
    Public Const Mixed As String = "clawhammer.mixed"
    Public Const Blend As String = "clawhammer.blend"
    Public Const IntegerHeavy As String = "clawhammer.integer-heavy"
    Public Const MemoryBandwidth As String = "clawhammer.memory-bandwidth"
End Module
Public Class ValidationSettings
    Private _mode As Integer
    Private _intervalMs As Integer
    Private _batchSize As Integer
    Private _errorCount As Integer
    Private _lastError As String
    Private ReadOnly _errorLock As New Object()

    Public Sub New(Optional mode As ValidationMode = ValidationMode.Off, Optional intervalMs As Integer = 30000, Optional batchSize As Integer = 4096)
        _mode = CInt(mode)
        _intervalMs = Math.Max(250, intervalMs)
        _batchSize = Math.Max(256, batchSize)
    End Sub

    Public Property Mode As ValidationMode
        Get
            Return CType(Threading.Volatile.Read(_mode), ValidationMode)
        End Get
        Set(value As ValidationMode)
            Threading.Volatile.Write(_mode, CInt(value))
        End Set
    End Property

    Public Property IntervalMs As Integer
        Get
            Return Threading.Volatile.Read(_intervalMs)
        End Get
        Set(value As Integer)
            Threading.Volatile.Write(_intervalMs, Math.Max(250, value))
        End Set
    End Property

    Public Property BatchSize As Integer
        Get
            Return Threading.Volatile.Read(_batchSize)
        End Get
        Set(value As Integer)
            Threading.Volatile.Write(_batchSize, Math.Max(256, value))
        End Set
    End Property

    Public ReadOnly Property ErrorCount As Integer
        Get
            Return Threading.Volatile.Read(_errorCount)
        End Get
    End Property

    Public ReadOnly Property LastError As String
        Get
            SyncLock _errorLock
                Return _lastError
            End SyncLock
        End Get
    End Property

    Public Sub Reset()
        Threading.Volatile.Write(_errorCount, 0)
        SyncLock _errorLock
            _lastError = String.Empty
        End SyncLock
    End Sub

    Public Sub RecordError(message As String)
        Interlocked.Increment(_errorCount)
        SyncLock _errorLock
            _lastError = message
        End SyncLock
    End Sub
End Class




