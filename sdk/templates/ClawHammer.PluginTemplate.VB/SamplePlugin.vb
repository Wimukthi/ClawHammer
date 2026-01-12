Imports System
Imports System.Threading
Imports ClawHammer.PluginContracts

Public Class SamplePlugin
    Implements IStressPlugin

    Public ReadOnly Property Id As String Implements IStressPlugin.Id
        Get
            Return "com.example.sample"
        End Get
    End Property

    Public ReadOnly Property DisplayName As String Implements IStressPlugin.DisplayName
        Get
            Return "Sample Plugin"
        End Get
    End Property

    Public ReadOnly Property Description As String Implements IStressPlugin.Description
        Get
            Return "Demonstrates a minimal ClawHammer stress plugin."
        End Get
    End Property

    Public ReadOnly Property Category As String Implements IStressPlugin.Category
        Get
            Return "Sample"
        End Get
    End Property

    Public ReadOnly Property SortOrder As Integer Implements IStressPlugin.SortOrder
        Get
            Return 1000
        End Get
    End Property

    Public ReadOnly Property IsAdvanced As Boolean Implements IStressPlugin.IsAdvanced
        Get
            Return False
        End Get
    End Property

    Public ReadOnly Property SupportsValidation As Boolean Implements IStressPlugin.SupportsValidation
        Get
            Return True
        End Get
    End Property

    Public Function CreateWorker(workerId As Integer, seed As ULong, context As StressPluginContext) As IStressWorker Implements IStressPlugin.CreateWorker
        Return New SampleWorker(workerId, seed, context)
    End Function
End Class

Friend Class SampleWorker
    Implements IStressWorker

    Private ReadOnly _workerId As Integer
    Private ReadOnly _context As StressPluginContext
    Private _state As Double

    Public Sub New(workerId As Integer, seed As ULong, context As StressPluginContext)
        _workerId = workerId
        _context = context
        _state = (seed Mod 1000UL) / 3.0 + 1.0
    End Sub

    Public ReadOnly Property KernelName As String Implements IStressWorker.KernelName
        Get
            Return "Sample Loop"
        End Get
    End Property

    Public Sub Run(token As CancellationToken,
                   reportProgress As Action(Of Integer),
                   validation As ValidationSettings,
                   reportError As Action(Of String),
                   reportStatus As Action(Of String)) Implements IStressWorker.Run
        Dim ops As Integer = 0
        Dim lastValidationTick As Long = Environment.TickCount64

        Dim totalWorkers As Integer = If(_context IsNot Nothing, _context.TotalWorkers, 1)
        reportStatus?.Invoke($"STATUS|{_workerId}|{KernelName}|Init (workers={totalWorkers})")

        Do While Not token.IsCancellationRequested
            _state = Math.Sin(_state) + Math.Sqrt(_state + 1.0)
            ops += 1

            If ops >= 10000 Then
                reportProgress?.Invoke(ops)
                ops = 0
            End If

            If validation IsNot Nothing AndAlso validation.Mode <> ValidationMode.Off Then
                Dim nowTick As Long = Environment.TickCount64
                If nowTick - lastValidationTick >= validation.IntervalMs Then
                    lastValidationTick = nowTick

                    If Double.IsNaN(_state) OrElse Double.IsInfinity(_state) Then
                        validation.RecordError("Validation failed: non-finite value.")
                        reportError?.Invoke("Validation failed: non-finite value.")
                        Return
                    End If

                    Dim detail As String = $"Tick OK (state={_state:F2})"
                    reportStatus?.Invoke($"STATUS|{_workerId}|{KernelName}|{detail}")
                End If
            End If
        Loop
    End Sub
End Class
