Imports ClawHammer.PluginContracts
Imports System.Numerics


Public MustInherit Class StressPluginBase
    Implements IStressPlugin

    Private ReadOnly _stressTester As New StressTester()

    Public MustOverride ReadOnly Property Id As String Implements IStressPlugin.Id
    Public MustOverride ReadOnly Property DisplayName As String Implements IStressPlugin.DisplayName

    Public Overridable ReadOnly Property Description As String Implements IStressPlugin.Description
        Get
            Return String.Empty
        End Get
    End Property

    Public Overridable ReadOnly Property Category As String Implements IStressPlugin.Category
        Get
            Return "CPU"
        End Get
    End Property

    Public Overridable ReadOnly Property SortOrder As Integer Implements IStressPlugin.SortOrder
        Get
            Return 0
        End Get
    End Property

    Public Overridable ReadOnly Property IsAdvanced As Boolean Implements IStressPlugin.IsAdvanced
        Get
            Return False
        End Get
    End Property

    Public Overridable ReadOnly Property SupportsValidation As Boolean Implements IStressPlugin.SupportsValidation
        Get
            Return True
        End Get
    End Property

    Public MustOverride Function CreateWorker(workerId As Integer, seed As ULong, context As StressPluginContext) As IStressWorker Implements IStressPlugin.CreateWorker

    Friend Function CreateWorkerForType(testType As StressTestType, workerId As Integer, seed As ULong, context As StressPluginContext) As IStressWorker
        If context Is Nothing Then
            Return _stressTester.CreateWorker(testType, workerId, seed)
        End If

        _stressTester.PrimeRangeMin = context.PrimeRangeMin
        _stressTester.PrimeRangeMax = context.PrimeRangeMax
        _stressTester.MemoryBufferBytes = context.MemoryBufferBytes
        Return _stressTester.CreateWorker(testType, workerId, seed)
    End Function
End Class

Public Class FloatingPointPlugin
    Inherits StressPluginBase

    Public Overrides ReadOnly Property Id As String
        Get
            Return DefaultPluginIds.FloatingPoint
        End Get
    End Property

    Public Overrides ReadOnly Property DisplayName As String
        Get
            Return "Floating Point"
        End Get
    End Property

    Public Overrides ReadOnly Property SortOrder As Integer
        Get
            Return 10
        End Get
    End Property

    Public Overrides ReadOnly Property Description As String
        Get
            Return "Focuses on scalar and vector floating-point math."
        End Get
    End Property

    Public Overrides Function CreateWorker(workerId As Integer, seed As ULong, context As StressPluginContext) As IStressWorker
        Return CreateWorkerForType(StressTestType.FloatingPoint, workerId, seed, context)
    End Function
End Class

Public Class IntegerPrimesPlugin
    Inherits StressPluginBase

    Public Overrides ReadOnly Property Id As String
        Get
            Return DefaultPluginIds.IntegerPrimes
        End Get
    End Property

    Public Overrides ReadOnly Property DisplayName As String
        Get
            Return "Integer (Primes)"
        End Get
    End Property

    Public Overrides ReadOnly Property SortOrder As Integer
        Get
            Return 20
        End Get
    End Property

    Public Overrides ReadOnly Property Description As String
        Get
            Return "Validates integer math by scanning for primes."
        End Get
    End Property

    Public Overrides Function CreateWorker(workerId As Integer, seed As ULong, context As StressPluginContext) As IStressWorker
        Return CreateWorkerForType(StressTestType.IntegerPrimes, workerId, seed, context)
    End Function
End Class

Public Class AvxPlugin
    Inherits StressPluginBase

    Public Overrides ReadOnly Property Id As String
        Get
            Return DefaultPluginIds.Avx
        End Get
    End Property

    Public Overrides ReadOnly Property DisplayName As String
        Get
            Return "AVX (Vector)"
        End Get
    End Property

    Public Overrides ReadOnly Property SortOrder As Integer
        Get
            Return 30
        End Get
    End Property

    Public Overrides ReadOnly Property Description As String
        Get
            Return "Uses SIMD vector math when supported by the CPU."
        End Get
    End Property

    Public Overrides Function CreateWorker(workerId As Integer, seed As ULong, context As StressPluginContext) As IStressWorker
        Dim avxSupported As Boolean = If(context IsNot Nothing, context.AvxSupported, Vector.IsHardwareAccelerated)
        If avxSupported Then
            Return CreateWorkerForType(StressTestType.AVX, workerId, seed, context)
        End If
        Return CreateWorkerForType(StressTestType.FloatingPoint, workerId, seed, context)
    End Function
End Class

Public Class MixedPlugin
    Inherits StressPluginBase

    Public Overrides ReadOnly Property Id As String
        Get
            Return DefaultPluginIds.Mixed
        End Get
    End Property

    Public Overrides ReadOnly Property DisplayName As String
        Get
            Return "Mixed"
        End Get
    End Property

    Public Overrides ReadOnly Property SortOrder As Integer
        Get
            Return 40
        End Get
    End Property

    Public Overrides ReadOnly Property Description As String
        Get
            Return "Alternates between floating point, primes, and AVX workloads."
        End Get
    End Property

    Public Overrides Function CreateWorker(workerId As Integer, seed As ULong, context As StressPluginContext) As IStressWorker
        Dim avxSupported As Boolean = If(context IsNot Nothing, context.AvxSupported, Vector.IsHardwareAccelerated)
        Dim chosen As StressTestType = SelectMixedWorkload(workerId, avxSupported)
        Return CreateWorkerForType(chosen, workerId, seed, context)
    End Function

    Private Shared Function SelectMixedWorkload(workerId As Integer, avxAvailable As Boolean) As StressTestType
        If avxAvailable Then
            Select Case workerId Mod 3
                Case 0
                    Return StressTestType.FloatingPoint
                Case 1
                    Return StressTestType.IntegerPrimes
                Case Else
                    Return StressTestType.AVX
            End Select
        End If

        If workerId Mod 2 = 0 Then
            Return StressTestType.FloatingPoint
        End If
        Return StressTestType.IntegerPrimes
    End Function
End Class

Public Class IntegerHeavyPlugin
    Inherits StressPluginBase

    Public Overrides ReadOnly Property Id As String
        Get
            Return DefaultPluginIds.IntegerHeavy
        End Get
    End Property

    Public Overrides ReadOnly Property DisplayName As String
        Get
            Return "Integer Heavy"
        End Get
    End Property

    Public Overrides ReadOnly Property SortOrder As Integer
        Get
            Return 60
        End Get
    End Property

    Public Overrides ReadOnly Property Description As String
        Get
            Return "Exercises integer pipelines with dependency chains."
        End Get
    End Property

    Public Overrides ReadOnly Property IsAdvanced As Boolean
        Get
            Return True
        End Get
    End Property

    Public Overrides Function CreateWorker(workerId As Integer, seed As ULong, context As StressPluginContext) As IStressWorker
        Return CreateWorkerForType(StressTestType.IntegerHeavy, workerId, seed, context)
    End Function
End Class

Public Class MemoryBandwidthPlugin
    Inherits StressPluginBase

    Public Overrides ReadOnly Property Id As String
        Get
            Return DefaultPluginIds.MemoryBandwidth
        End Get
    End Property

    Public Overrides ReadOnly Property DisplayName As String
        Get
            Return "Memory Bandwidth"
        End Get
    End Property

    Public Overrides ReadOnly Property SortOrder As Integer
        Get
            Return 70
        End Get
    End Property

    Public Overrides ReadOnly Property Description As String
        Get
            Return "Streams large buffers to stress the memory subsystem."
        End Get
    End Property

    Public Overrides ReadOnly Property IsAdvanced As Boolean
        Get
            Return True
        End Get
    End Property

    Public Overrides Function CreateWorker(workerId As Integer, seed As ULong, context As StressPluginContext) As IStressWorker
        Return CreateWorkerForType(StressTestType.MemoryBandwidth, workerId, seed, context)
    End Function
End Class

Public Class BlendPlugin
    Inherits StressPluginBase

    Public Overrides ReadOnly Property Id As String
        Get
            Return DefaultPluginIds.Blend
        End Get
    End Property

    Public Overrides ReadOnly Property DisplayName As String
        Get
            Return "Blend"
        End Get
    End Property

    Public Overrides ReadOnly Property SortOrder As Integer
        Get
            Return 50
        End Get
    End Property

    Public Overrides ReadOnly Property Description As String
        Get
            Return "Combines integer, floating point, and memory workloads."
        End Get
    End Property

    Public Overrides ReadOnly Property IsAdvanced As Boolean
        Get
            Return True
        End Get
    End Property

    Public Overrides Function CreateWorker(workerId As Integer, seed As ULong, context As StressPluginContext) As IStressWorker
        Dim avxSupported As Boolean = If(context IsNot Nothing, context.AvxSupported, Vector.IsHardwareAccelerated)
        Dim chosen As StressTestType = SelectBlendWorkload(workerId, avxSupported)
        Return CreateWorkerForType(chosen, workerId, seed, context)
    End Function

    Private Shared Function SelectBlendWorkload(workerId As Integer, avxAvailable As Boolean) As StressTestType
        If avxAvailable Then
            Select Case workerId Mod 5
                Case 0
                    Return StressTestType.FloatingPoint
                Case 1
                    Return StressTestType.IntegerPrimes
                Case 2
                    Return StressTestType.IntegerHeavy
                Case 3
                    Return StressTestType.MemoryBandwidth
                Case Else
                    Return StressTestType.AVX
            End Select
        End If

        Select Case workerId Mod 4
            Case 0
                Return StressTestType.FloatingPoint
            Case 1
                Return StressTestType.IntegerPrimes
            Case 2
                Return StressTestType.IntegerHeavy
            Case Else
                Return StressTestType.MemoryBandwidth
        End Select
    End Function
End Class










