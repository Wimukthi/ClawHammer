Imports System.Threading
Imports System.Numerics
Imports System.Diagnostics
Imports ClawHammer.PluginContracts

' Worker kernels and validation helpers used by the stress engine.

Friend Enum StressTestType
    IntegerPrimes
    FloatingPoint
    AVX
    Mixed
    IntegerHeavy
    MemoryBandwidth
    Blend
End Enum



Friend Class ValidationScheduler
    Private _lastValidationMs As Long

    ' Gate validation work so each worker validates on a fixed cadence.
    Public Function ShouldValidate(settings As ValidationSettings) As Boolean
        If settings Is Nothing Then
            Return False
        End If
        If settings.Mode = ValidationMode.Off Then
            Return False
        End If
        Dim intervalMs As Integer = Math.Max(250, settings.IntervalMs)
        Dim nowMs As Long = Environment.TickCount64
        If nowMs - _lastValidationMs >= intervalMs Then
            _lastValidationMs = nowMs
            Return True
        End If
        Return False
    End Function
End Class

Friend Structure XorShift64Star
    Private _state As ULong

    Public Sub New(seed As ULong)
        If seed = 0UL Then
            seed = &H9E3779B97F4A7C15UL
        End If
        _state = seed
    End Sub

    Public Function NextULong() As ULong
        Dim x As ULong = _state
        x = x Xor (x << 13)
        x = x Xor (x >> 7)
        x = x Xor (x << 17)
        _state = x
        Return x
    End Function

    Public Function NextDouble() As Double
        Const Scale As Double = 1.0R / 9007199254740992.0R
        Return (NextULong() >> 11) * Scale
    End Function

    Public Function NextSingle() As Single
        Return CSng(NextDouble())
    End Function
End Structure

Friend Class StressTester
    Public Property PrimeRangeMin As Long = 2
    Public Property PrimeRangeMax As Long = 25000000
    Public Property MemoryBufferBytes As Integer = 4 * 1024 * 1024

    ' Build a per-thread worker for the selected workload type.
    Public Function CreateWorker(testType As StressTestType, workerId As Integer, seed As ULong) As IStressWorker
        Select Case testType
            Case StressTestType.IntegerPrimes
                Return New PrimeWorker(PrimeRangeMin, PrimeRangeMax, workerId)
            Case StressTestType.IntegerHeavy
                Return New IntegerHeavyWorker(workerId, seed)
            Case StressTestType.MemoryBandwidth
                Return New MemoryBandwidthWorker(workerId, seed, MemoryBufferBytes)
            Case StressTestType.AVX
                If Not Vector.IsHardwareAccelerated Then
                    Return New FloatingPointWorker(workerId, seed)
                End If
                Return New AvxWorker(workerId, seed)
            Case StressTestType.FloatingPoint
                Return New FloatingPointWorker(workerId, seed)
            Case Else
                Return New FloatingPointWorker(workerId, seed)
        End Select
    End Function

    ' Primality test with cooperative cancellation for the integer workload.
    Friend Shared Function IsPrime(ByVal Value As Long, ByVal token As CancellationToken) As Boolean
        If Value <= 1 Then Return False
        If Value = 2 Then Return True
        If Value Mod 2 = 0 Then Return False

        If token.IsCancellationRequested Then Return False

        Dim maxCheck As Long = CLng(Math.Sqrt(Value))

        For i As Long = 3 To maxCheck Step 2
            If token.IsCancellationRequested Then Return False

            If Value Mod i = 0 Then
                Return False
            End If
        Next

        Return True
    End Function

    Private Structure PrimeValidationRange
        Public ReadOnly MaxValue As Integer
        Public ReadOnly ExpectedCount As Integer

        Public Sub New(maxValue As Integer, expected As Integer)
            Me.MaxValue = maxValue
            Me.ExpectedCount = expected
        End Sub
    End Structure

    Private Class PrimeWorker
        Implements IStressWorker

        Private Const DefaultBatchSize As Integer = 2048
        Private Shared ReadOnly LightRanges As PrimeValidationRange() = {
            New PrimeValidationRange(1000, 168),
            New PrimeValidationRange(5000, 669)
        }
        Private Shared ReadOnly FullRanges As PrimeValidationRange() = {
            New PrimeValidationRange(1000, 168),
            New PrimeValidationRange(5000, 669),
            New PrimeValidationRange(10000, 1229),
            New PrimeValidationRange(20000, 2262)
        }

        Private ReadOnly _minValue As Long
        Private ReadOnly _maxValue As Long
        Private ReadOnly _workerId As Integer
        Private _current As Long
        Private _reportedError As Boolean
        Private _reportedValidationOk As Boolean

        Public Sub New(minValue As Long, maxValue As Long, workerId As Integer)
            _minValue = Math.Max(2, minValue)
            _maxValue = Math.Max(_minValue, maxValue)
            _workerId = workerId
            Dim start As Long = _minValue + workerId * 2
            If start Mod 2 = 0 Then start += 1
            _current = start
        End Sub

        Public ReadOnly Property KernelName As String Implements IStressWorker.KernelName
            Get
                Return "Integer Primes"
            End Get
        End Property

        ' Self-test once at start, then periodically validate on the timer cadence.
        Public Sub Run(token As CancellationToken, reportProgress As Action(Of Integer), validation As ValidationSettings, reportError As Action(Of String), reportStatus As Action(Of String)) Implements IStressWorker.Run
            Dim scheduler As New ValidationScheduler()
            If validation IsNot Nothing AndAlso validation.Mode <> ValidationMode.Off Then
                If Not ValidatePrimeRanges(validation.Mode, token) Then
                    ReportOnce("Validation failed: prime self-test mismatch.", reportError, validation)
                    Return
                End If
                ReportValidationOkOnce($"Self-test OK ({GetPrimeRangeDetail(validation.Mode)})", reportStatus)
            End If

            Do While Not token.IsCancellationRequested
                Dim batchSize As Integer = If(validation IsNot Nothing, validation.BatchSize, DefaultBatchSize)
                If batchSize <= 0 Then
                    batchSize = DefaultBatchSize
                End If

                Dim ops As Integer = 0
                For i As Integer = 0 To batchSize - 1
                    If token.IsCancellationRequested Then Exit For

                    IsPrime(_current, token)
                    ops += 1
                    _current += 2
                    If _current > _maxValue Then
                        _current = If(_minValue Mod 2 = 0, _minValue + 1, _minValue)
                    End If
                Next

                If ops > 0 Then
                    reportProgress?.Invoke(ops)
                End If

                If scheduler.ShouldValidate(validation) Then
                    If Not ValidatePrimeRanges(validation.Mode, token) Then
                        ReportOnce("Validation failed: prime count mismatch.", reportError, validation)
                        Return
                    End If
                    EmitStatus($"Tick OK ({GetPrimeRangeDetail(validation.Mode)})", reportStatus)
                End If
            Loop
        End Sub

        Private Sub ReportOnce(message As String, reportError As Action(Of String), validation As ValidationSettings)
            If _reportedError Then
                Return
            End If
            _reportedError = True
            validation?.RecordError(message)
            reportError?.Invoke(message)
        End Sub

        Private Sub ReportValidationOkOnce(message As String, reportStatus As Action(Of String))
            If _reportedValidationOk Then
                Return
            End If
            _reportedValidationOk = True
            EmitStatus(message, reportStatus)
        End Sub

        Private Sub EmitStatus(detail As String, reportStatus As Action(Of String))
            If reportStatus Is Nothing Then
                Return
            End If
            reportStatus.Invoke($"STATUS|{_workerId}|{KernelName}|{detail}")
        End Sub

        Private Shared Function GetPrimeRangeDetail(mode As ValidationMode) As String
            Dim ranges As PrimeValidationRange() = If(mode = ValidationMode.Full, FullRanges, LightRanges)
            Dim parts As New List(Of String)()
            For Each range As PrimeValidationRange In ranges
                parts.Add($"primes<={range.MaxValue}")
            Next
            Return String.Join(",", parts)
        End Function

        Private Shared Function CountPrimesUpTo(maxValue As Integer, token As CancellationToken) As Integer
            Dim count As Integer = 0
            For i As Integer = 2 To maxValue
                If token.IsCancellationRequested Then
                    Return count
                End If
                If IsPrime(i, token) Then
                    count += 1
                End If
            Next
            Return count
        End Function

        Private Shared Function ValidatePrimeRanges(mode As ValidationMode, token As CancellationToken) As Boolean
            Dim ranges As PrimeValidationRange() = If(mode = ValidationMode.Full, FullRanges, LightRanges)
            For Each range As PrimeValidationRange In ranges
                If token.IsCancellationRequested Then
                    Return True
                End If
                Dim count As Integer = CountPrimesUpTo(range.MaxValue, token)
                If count <> range.ExpectedCount Then
                    Return False
                End If
            Next
            Return True
        End Function
    End Class

    Private Class FloatingPointWorker
        Implements IStressWorker

        Private Const DefaultBatchSize As Integer = 4096
        Private ReadOnly _vecSize As Integer = Vector(Of Single).Count
        Private ReadOnly _workerId As Integer
        Private _v1 As Vector(Of Single)
        Private _v2 As Vector(Of Single)
        Private _v3 As Vector(Of Single)
        Private _v4 As Vector(Of Single)
        Private _reportedError As Boolean
        Private _reportedValidationOk As Boolean

        Public Sub New(workerId As Integer, seed As ULong)
            _workerId = workerId
            Dim rng As New XorShift64Star(seed Xor &H83A9B6C9D3F2A7C5UL)
            Dim data1(_vecSize - 1) As Single
            Dim data2(_vecSize - 1) As Single
            Dim data3(_vecSize - 1) As Single
            Dim data4(_vecSize - 1) As Single
            For i As Integer = 0 To _vecSize - 1
                data1(i) = 0.01F + rng.NextSingle() * 10.0F
                data2(i) = 0.01F + rng.NextSingle() * 5.0F
                data3(i) = 0.01F + rng.NextSingle() * 3.0F
                data4(i) = 0.01F + rng.NextSingle() * 7.0F
            Next
            _v1 = New Vector(Of Single)(data1)
            _v2 = New Vector(Of Single)(data2)
            _v3 = New Vector(Of Single)(data3)
            _v4 = New Vector(Of Single)(data4)
        End Sub

        Public ReadOnly Property KernelName As String Implements IStressWorker.KernelName
            Get
                Return "Vector FP"
            End Get
        End Property

        ' Vector FP kernel with periodic scalar/vector cross-checks.
        Public Sub Run(token As CancellationToken, reportProgress As Action(Of Integer), validation As ValidationSettings, reportError As Action(Of String), reportStatus As Action(Of String)) Implements IStressWorker.Run
            Dim scheduler As New ValidationScheduler()
            If validation IsNot Nothing AndAlso validation.Mode <> ValidationMode.Off Then
                If Not ValidateVectorPath(validation.Mode) Then
                    ReportOnce("Validation failed: vector FP self-test mismatch.", reportError, validation)
                    Return
                End If
                ReportValidationOkOnce($"Self-test OK (scalar/vector iters={GetVectorIterations(validation.Mode)})", reportStatus)
            End If

            Dim vEps As New Vector(Of Single)(0.001F)
            Dim vScale As New Vector(Of Single)(0.0001F)

            Do While Not token.IsCancellationRequested
                Dim batchSize As Integer = If(validation IsNot Nothing, validation.BatchSize, DefaultBatchSize)
                If batchSize <= 0 Then
                    batchSize = DefaultBatchSize
                End If

                For i As Integer = 0 To batchSize - 1
                    _v1 = Vector.Multiply(Vector.Add(Vector.Multiply(_v1, _v2), _v3), vScale)
                    _v2 = Vector.Multiply(Vector.Add(Vector.Multiply(_v2, _v4), _v1), vScale)
                    _v3 = Vector.Add(Vector.Multiply(_v3, _v1), _v2)
                    _v4 = Vector.SquareRoot(Vector.Abs(_v1) + Vector.Abs(_v2) + Vector.Abs(_v3) + vEps)
                Next

                reportProgress?.Invoke(batchSize)

                Dim checksum As Single = SumVector(_v1) + SumVector(_v2)
                If Single.IsNaN(checksum) OrElse Single.IsInfinity(checksum) Then
                    ReportOnce("Validation failed: FP produced NaN/Infinity.", reportError, validation)
                    Return
                End If

                If scheduler.ShouldValidate(validation) Then
                    If Not ValidateVectorPath(validation.Mode) Then
                        ReportOnce("Validation failed: vector FP validation mismatch.", reportError, validation)
                        Return
                    End If
                    EmitStatus($"Tick OK (scalar/vector iters={GetVectorIterations(validation.Mode)})", reportStatus)
                End If
            Loop
        End Sub

        Private Sub ReportOnce(message As String, reportError As Action(Of String), validation As ValidationSettings)
            If _reportedError Then
                Return
            End If
            _reportedError = True
            validation?.RecordError(message)
            reportError?.Invoke(message)
        End Sub

        Private Sub ReportValidationOkOnce(message As String, reportStatus As Action(Of String))
            If _reportedValidationOk Then
                Return
            End If
            _reportedValidationOk = True
            EmitStatus(message, reportStatus)
        End Sub

        Private Sub EmitStatus(detail As String, reportStatus As Action(Of String))
            If reportStatus Is Nothing Then
                Return
            End If
            reportStatus.Invoke($"STATUS|{_workerId}|{KernelName}|{detail}")
        End Sub

        Private Shared Function GetVectorIterations(mode As ValidationMode) As Integer
            Return If(mode = ValidationMode.Full, 128, 64)
        End Function

        Private Function ValidateVectorPath(mode As ValidationMode) As Boolean
            Dim iterations As Integer = If(mode = ValidationMode.Full, 128, 64)
            Dim rng As New XorShift64Star(&H12345678ABCDEF01UL Xor CULng(_workerId))
            Dim data1(_vecSize - 1) As Single
            Dim data2(_vecSize - 1) As Single
            Dim data3(_vecSize - 1) As Single
            Dim data4(_vecSize - 1) As Single
            For i As Integer = 0 To _vecSize - 1
                data1(i) = 0.1F + rng.NextSingle() * 4.0F
                data2(i) = 0.1F + rng.NextSingle() * 3.0F
                data3(i) = 0.1F + rng.NextSingle() * 2.0F
                data4(i) = 0.1F + rng.NextSingle() * 5.0F
            Next

            Dim scalarSum As Single = RunScalarValidation(data1, data2, data3, data4, iterations)
            Dim vectorSum As Single = RunVectorValidation(New Vector(Of Single)(data1), New Vector(Of Single)(data2), New Vector(Of Single)(data3), New Vector(Of Single)(data4), iterations)
            Dim diff As Single = Math.Abs(scalarSum - vectorSum)
            Return diff <= 0.01F
        End Function

        Private Shared Function RunScalarValidation(ByVal v1() As Single, ByVal v2() As Single, ByVal v3() As Single, ByVal v4() As Single, iterations As Integer) As Single
            Dim length As Integer = v1.Length
            Dim sum As Single = 0.0F
            For iter As Integer = 0 To iterations - 1
                For i As Integer = 0 To length - 1
                    v1(i) = (v1(i) * v2(i) + v3(i)) * 0.0001F
                    v2(i) = (v2(i) * v4(i) + v1(i)) * 0.0001F
                    v3(i) = v3(i) * v1(i) + v2(i)
                    Dim sqrtIn As Single = Math.Abs(v1(i)) + Math.Abs(v2(i)) + Math.Abs(v3(i)) + 0.001F
                    v4(i) = CSng(Math.Sqrt(sqrtIn))
                Next
            Next
            For i As Integer = 0 To length - 1
                sum += v1(i) + v2(i)
            Next
            Return sum
        End Function

        Private Shared Function RunVectorValidation(ByVal v1 As Vector(Of Single), ByVal v2 As Vector(Of Single), ByVal v3 As Vector(Of Single), ByVal v4 As Vector(Of Single), iterations As Integer) As Single
            Dim vEps As New Vector(Of Single)(0.001F)
            Dim vScale As New Vector(Of Single)(0.0001F)
            For iter As Integer = 0 To iterations - 1
                v1 = Vector.Multiply(Vector.Add(Vector.Multiply(v1, v2), v3), vScale)
                v2 = Vector.Multiply(Vector.Add(Vector.Multiply(v2, v4), v1), vScale)
                v3 = Vector.Add(Vector.Multiply(v3, v1), v2)
                v4 = Vector.SquareRoot(Vector.Abs(v1) + Vector.Abs(v2) + Vector.Abs(v3) + vEps)
            Next
            Return SumVector(v1) + SumVector(v2)
        End Function

        Private Shared Function SumVector(ByVal v As Vector(Of Single)) As Single
            Dim sum As Single = 0.0F
            For i As Integer = 0 To Vector(Of Single).Count - 1
                sum += v(i)
            Next
            Return sum
        End Function
    End Class

    Private Class AvxWorker
        Implements IStressWorker

        Private Const DefaultBatchSize As Integer = 4096
        Private ReadOnly _vecSize As Integer = Vector(Of Double).Count
        Private ReadOnly _workerId As Integer
        Private _v1 As Vector(Of Double)
        Private _v2 As Vector(Of Double)
        Private _v3 As Vector(Of Double)
        Private _v4 As Vector(Of Double)
        Private _reportedError As Boolean
        Private _reportedValidationOk As Boolean

        Public Sub New(workerId As Integer, seed As ULong)
            _workerId = workerId
            Dim rng As New XorShift64Star(seed Xor &H4C957F2D3A1B0E67UL)
            Dim data1(_vecSize - 1) As Double
            Dim data2(_vecSize - 1) As Double
            Dim data3(_vecSize - 1) As Double
            Dim data4(_vecSize - 1) As Double
            For i As Integer = 0 To _vecSize - 1
                data1(i) = 0.01R + rng.NextDouble() * 100.0R
                data2(i) = 0.01R + rng.NextDouble() * 50.0R
                data3(i) = 0.01R + rng.NextDouble() * 20.0R
                data4(i) = 0.01R + rng.NextDouble() * 80.0R
            Next
            _v1 = New Vector(Of Double)(data1)
            _v2 = New Vector(Of Double)(data2)
            _v3 = New Vector(Of Double)(data3)
            _v4 = New Vector(Of Double)(data4)
        End Sub

        Public ReadOnly Property KernelName As String Implements IStressWorker.KernelName
            Get
                Return "AVX"
            End Get
        End Property

        ' Double-precision vector kernel for AVX-heavy stress.
        Public Sub Run(token As CancellationToken, reportProgress As Action(Of Integer), validation As ValidationSettings, reportError As Action(Of String), reportStatus As Action(Of String)) Implements IStressWorker.Run
            Dim scheduler As New ValidationScheduler()
            If validation IsNot Nothing AndAlso validation.Mode <> ValidationMode.Off Then
                If Not ValidateVectorPath(validation.Mode) Then
                    ReportOnce("Validation failed: AVX self-test mismatch.", reportError, validation)
                    Return
                End If
                ReportValidationOkOnce($"Self-test OK (scalar/vector iters={GetVectorIterations(validation.Mode)})", reportStatus)
            End If

            Dim vEps As New Vector(Of Double)(0.000001R)
            Dim vScale As New Vector(Of Double)(0.000001R)

            Do While Not token.IsCancellationRequested
                Dim batchSize As Integer = If(validation IsNot Nothing, validation.BatchSize, DefaultBatchSize)
                If batchSize <= 0 Then
                    batchSize = DefaultBatchSize
                End If

                For i As Integer = 0 To batchSize - 1
                    _v1 = Vector.Multiply(Vector.Add(Vector.Multiply(_v1, _v2), _v3), vScale)
                    _v2 = Vector.Multiply(Vector.Add(Vector.Multiply(_v2, _v4), _v1), vScale)
                    _v3 = Vector.Add(Vector.Multiply(_v3, _v1), _v2)
                    _v4 = Vector.SquareRoot(Vector.Abs(_v1) + Vector.Abs(_v2) + Vector.Abs(_v3) + vEps)
                Next

                reportProgress?.Invoke(batchSize)

                Dim checksum As Double = SumVector(_v1) + SumVector(_v2)
                If Double.IsNaN(checksum) OrElse Double.IsInfinity(checksum) Then
                    ReportOnce("Validation failed: AVX produced NaN/Infinity.", reportError, validation)
                    Return
                End If

                If scheduler.ShouldValidate(validation) Then
                    If Not ValidateVectorPath(validation.Mode) Then
                        ReportOnce("Validation failed: AVX validation mismatch.", reportError, validation)
                        Return
                    End If
                    EmitStatus($"Tick OK (scalar/vector iters={GetVectorIterations(validation.Mode)})", reportStatus)
                End If
            Loop
        End Sub

        Private Sub ReportOnce(message As String, reportError As Action(Of String), validation As ValidationSettings)
            If _reportedError Then
                Return
            End If
            _reportedError = True
            validation?.RecordError(message)
            reportError?.Invoke(message)
        End Sub

        Private Sub ReportValidationOkOnce(message As String, reportStatus As Action(Of String))
            If _reportedValidationOk Then
                Return
            End If
            _reportedValidationOk = True
            EmitStatus(message, reportStatus)
        End Sub

        Private Sub EmitStatus(detail As String, reportStatus As Action(Of String))
            If reportStatus Is Nothing Then
                Return
            End If
            reportStatus.Invoke($"STATUS|{_workerId}|{KernelName}|{detail}")
        End Sub

        Private Shared Function GetVectorIterations(mode As ValidationMode) As Integer
            Return If(mode = ValidationMode.Full, 96, 48)
        End Function

        Private Function ValidateVectorPath(mode As ValidationMode) As Boolean
            Dim iterations As Integer = If(mode = ValidationMode.Full, 96, 48)
            Dim rng As New XorShift64Star(&H1B2A3C4D5E6F7788UL)
            Dim data1(_vecSize - 1) As Double
            Dim data2(_vecSize - 1) As Double
            Dim data3(_vecSize - 1) As Double
            Dim data4(_vecSize - 1) As Double
            For i As Integer = 0 To _vecSize - 1
                data1(i) = 0.01R + rng.NextDouble() * 30.0R
                data2(i) = 0.01R + rng.NextDouble() * 20.0R
                data3(i) = 0.01R + rng.NextDouble() * 15.0R
                data4(i) = 0.01R + rng.NextDouble() * 25.0R
            Next
            Dim scalarSum As Double = RunScalarValidation(data1, data2, data3, data4, iterations)
            Dim vectorSum As Double = RunVectorValidation(New Vector(Of Double)(data1), New Vector(Of Double)(data2), New Vector(Of Double)(data3), New Vector(Of Double)(data4), iterations)
            Dim diff As Double = Math.Abs(scalarSum - vectorSum)
            Return diff <= 0.01R
        End Function

        Private Shared Function RunScalarValidation(ByVal v1() As Double, ByVal v2() As Double, ByVal v3() As Double, ByVal v4() As Double, iterations As Integer) As Double
            Dim length As Integer = v1.Length
            For iter As Integer = 0 To iterations - 1
                For i As Integer = 0 To length - 1
                    v1(i) = (v1(i) * v2(i) + v3(i)) * 0.000001R
                    v2(i) = (v2(i) * v4(i) + v1(i)) * 0.000001R
                    v3(i) = v3(i) * v1(i) + v2(i)
                    Dim sqrtIn As Double = Math.Abs(v1(i)) + Math.Abs(v2(i)) + Math.Abs(v3(i)) + 0.000001R
                    v4(i) = Math.Sqrt(sqrtIn)
                Next
            Next
            Dim sum As Double = 0.0R
            For i As Integer = 0 To length - 1
                sum += v1(i) + v2(i)
            Next
            Return sum
        End Function

        Private Shared Function RunVectorValidation(ByVal v1 As Vector(Of Double), ByVal v2 As Vector(Of Double), ByVal v3 As Vector(Of Double), ByVal v4 As Vector(Of Double), iterations As Integer) As Double
            Dim vEps As New Vector(Of Double)(0.000001R)
            Dim vScale As New Vector(Of Double)(0.000001R)
            For iter As Integer = 0 To iterations - 1
                v1 = Vector.Multiply(Vector.Add(Vector.Multiply(v1, v2), v3), vScale)
                v2 = Vector.Multiply(Vector.Add(Vector.Multiply(v2, v4), v1), vScale)
                v3 = Vector.Add(Vector.Multiply(v3, v1), v2)
                v4 = Vector.SquareRoot(Vector.Abs(v1) + Vector.Abs(v2) + Vector.Abs(v3) + vEps)
            Next
            Return SumVector(v1) + SumVector(v2)
        End Function

        Private Shared Function SumVector(ByVal v As Vector(Of Double)) As Double
            Dim sum As Double = 0.0R
            For i As Integer = 0 To Vector(Of Double).Count - 1
                sum += v(i)
            Next
            Return sum
        End Function
    End Class

    Private Class IntegerHeavyWorker
        Implements IStressWorker

        Private Const DefaultBatchSize As Integer = 8192
        Private Const Modulus As UInteger = &HFFFFFFFBUI
        Private Const Multiplier As UInteger = 1664525UI
        Private Const Increment As UInteger = 1013904223UI

        Private _state As UInteger
        Private ReadOnly _workerId As Integer
        Private _reportedError As Boolean
        Private _reportedValidationOk As Boolean

        Public Sub New(workerId As Integer, seed As ULong)
            Dim workerMix As ULong = CULng(workerId)
            workerMix = workerMix Xor (workerMix << 11) Xor (workerMix << 21) Xor (workerMix << 32)
            Dim mixed As ULong = (seed And &HFFFFFFFFUL) Xor workerMix
            _state = CUInt(mixed And &HFFFFFFFFUL)
            _workerId = workerId
            If _state = 0UI Then
                _state = 1UI
            End If
        End Sub

        Public ReadOnly Property KernelName As String Implements IStressWorker.KernelName
            Get
                Return "Integer Heavy"
            End Get
        End Property

        ' Integer-heavy kernel with deterministic chain validation.
        Public Sub Run(token As CancellationToken, reportProgress As Action(Of Integer), validation As ValidationSettings, reportError As Action(Of String), reportStatus As Action(Of String)) Implements IStressWorker.Run
            Dim scheduler As New ValidationScheduler()
            If validation IsNot Nothing AndAlso validation.Mode <> ValidationMode.Off Then
                If Not ValidateIntegerChain(validation.Mode) Then
                    ReportOnce("Validation failed: integer self-test mismatch.", reportError, validation)
                    Return
                End If
                ReportValidationOkOnce($"Self-test OK (iters={GetChainIterations(validation.Mode)})", reportStatus)
            End If

            Do While Not token.IsCancellationRequested
                Dim batchSize As Integer = If(validation IsNot Nothing, validation.BatchSize, DefaultBatchSize)
                If batchSize <= 0 Then
                    batchSize = DefaultBatchSize
                End If

                Dim checksum As UInteger = 0UI
                For i As Integer = 0 To batchSize - 1
                    Dim product As ULong = CULng(_state) * Multiplier + Increment
                    _state = CUInt(product Mod Modulus)
                    _state = _state Xor (_state << 13)
                    _state = _state Xor (_state >> 7)
                    _state = _state Xor (_state << 17)
                    checksum = checksum Xor _state
                Next

                reportProgress?.Invoke(batchSize)

                If checksum = 0UI Then
                    ReportOnce("Validation failed: integer checksum zero.", reportError, validation)
                    Return
                End If

                If scheduler.ShouldValidate(validation) Then
                    If Not ValidateIntegerChain(validation.Mode) Then
                        ReportOnce("Validation failed: integer validation mismatch.", reportError, validation)
                        Return
                    End If
                    EmitStatus($"Tick OK (iters={GetChainIterations(validation.Mode)})", reportStatus)
                End If
            Loop
        End Sub

        Private Sub ReportOnce(message As String, reportError As Action(Of String), validation As ValidationSettings)
            If _reportedError Then
                Return
            End If
            _reportedError = True
            validation?.RecordError(message)
            reportError?.Invoke(message)
        End Sub

        Private Sub ReportValidationOkOnce(message As String, reportStatus As Action(Of String))
            If _reportedValidationOk Then
                Return
            End If
            _reportedValidationOk = True
            EmitStatus(message, reportStatus)
        End Sub

        Private Sub EmitStatus(detail As String, reportStatus As Action(Of String))
            If reportStatus Is Nothing Then
                Return
            End If
            reportStatus.Invoke($"STATUS|{_workerId}|{KernelName}|{detail}")
        End Sub

        Private Shared Function GetChainIterations(mode As ValidationMode) As Integer
            Return If(mode = ValidationMode.Full, 20000, 5000)
        End Function

        Private Function ValidateIntegerChain(mode As ValidationMode) As Boolean
            Dim iterations As Integer = If(mode = ValidationMode.Full, 20000, 5000)
            Dim testState As UInteger = 1UI
            Dim expected As UInteger = ComputeExpectedState(testState, iterations)
            Dim actual As UInteger = ComputeFastState(testState, iterations)
            Return expected = actual
        End Function

        Private Shared Function ComputeFastState(state As UInteger, iterations As Integer) As UInteger
            Dim current As UInteger = state
            For i As Integer = 0 To iterations - 1
                Dim product As ULong = CULng(current) * Multiplier + Increment
                current = CUInt(product Mod Modulus)
                current = current Xor (current << 13)
                current = current Xor (current >> 7)
                current = current Xor (current << 17)
            Next
            Return current
        End Function

        Private Shared Function ComputeExpectedState(state As UInteger, iterations As Integer) As UInteger
            Dim current As BigInteger = New BigInteger(state)
            Dim modValue As BigInteger = New BigInteger(Modulus)
            Dim multValue As BigInteger = New BigInteger(Multiplier)
            Dim incValue As BigInteger = New BigInteger(Increment)
            Dim mask As BigInteger = New BigInteger(&HFFFFFFFFUI)
            For i As Integer = 0 To iterations - 1
                current = (current * multValue + incValue) Mod modValue
                Dim tmp As UInteger = CUInt(current And mask)
                tmp = tmp Xor (tmp << 13)
                tmp = tmp Xor (tmp >> 7)
                tmp = tmp Xor (tmp << 17)
                current = New BigInteger(tmp)
            Next
            Return CUInt(current And mask)
        End Function
    End Class

    Private Class MemoryBandwidthWorker
        Implements IStressWorker

        Private Const LightSampleCount As Integer = 16
        Private Const FullSampleCount As Integer = 64
        Private Const PatternShift1 As Integer = 11
        Private Const PatternShift2 As Integer = 27
        Private Const PatternShift3 As Integer = 43
        Private ReadOnly _buffer As ULong()
        Private ReadOnly _length As Integer
        Private _batchCounter As ULong
        Private _seed As ULong
        Private ReadOnly _workerId As Integer
        Private _reportedError As Boolean
        Private _reportedValidationOk As Boolean

        Public Sub New(workerId As Integer, seed As ULong, bufferBytes As Integer)
            Dim clampedBytes As Integer = Math.Max(256 * 1024, bufferBytes)
            Dim length As Integer = Math.Max(1, clampedBytes \ 8)
            _buffer = New ULong(length - 1) {}
            _length = length
            _seed = seed Xor (CULng(workerId) * &H85EBCA6BUL)
            _workerId = workerId
        End Sub

        Public ReadOnly Property KernelName As String Implements IStressWorker.KernelName
            Get
                Return "Memory Bandwidth"
            End Get
        End Property

        ' Memory bandwidth kernel with pattern fill + spot-check validation.
        Public Sub Run(token As CancellationToken, reportProgress As Action(Of Integer), validation As ValidationSettings, reportError As Action(Of String), reportStatus As Action(Of String)) Implements IStressWorker.Run
            Dim scheduler As New ValidationScheduler()
            If validation IsNot Nothing AndAlso validation.Mode <> ValidationMode.Off Then
                Dim patternCounter As ULong = _batchCounter
                Dim sampleCount As Integer = GetSampleCount(validation.Mode)
                FillPattern(patternCounter)
                If Not ValidateMemoryPattern(patternCounter, sampleCount) Then
                    ReportOnce("Validation failed: memory self-test mismatch.", reportError, validation)
                    Return
                End If
                ReportValidationOkOnce($"Self-test OK (samples={sampleCount})", reportStatus)
            End If

            Do While Not token.IsCancellationRequested
                Dim patternCounter As ULong = _batchCounter
                FillPattern(patternCounter)

                Dim checksum As ULong = 0UL
                For i As Integer = 0 To _length - 1 Step 4
                    checksum = checksum Xor _buffer(i)
                    If i + 1 < _length Then checksum = checksum Xor _buffer(i + 1)
                    If i + 2 < _length Then checksum = checksum Xor _buffer(i + 2)
                    If i + 3 < _length Then checksum = checksum Xor _buffer(i + 3)
                Next

                reportProgress?.Invoke(_length)

                If scheduler.ShouldValidate(validation) Then
                    Dim sampleCount As Integer = GetSampleCount(validation.Mode)
                    If Not ValidateMemoryPattern(patternCounter, sampleCount) Then
                        ReportOnce("Validation failed: memory validation mismatch.", reportError, validation)
                        Return
                    End If
                    EmitStatus($"Tick OK (samples={sampleCount})", reportStatus)
                End If

                _batchCounter += 1
            Loop
        End Sub

        Private Sub ReportOnce(message As String, reportError As Action(Of String), validation As ValidationSettings)
            If _reportedError Then
                Return
            End If
            _reportedError = True
            validation?.RecordError(message)
            reportError?.Invoke(message)
        End Sub

        Private Sub ReportValidationOkOnce(message As String, reportStatus As Action(Of String))
            If _reportedValidationOk Then
                Return
            End If
            _reportedValidationOk = True
            EmitStatus(message, reportStatus)
        End Sub

        Private Sub EmitStatus(detail As String, reportStatus As Action(Of String))
            If reportStatus Is Nothing Then
                Return
            End If
            reportStatus.Invoke($"STATUS|{_workerId}|{KernelName}|{detail}")
        End Sub

        Private Shared Function GetSampleCount(mode As ValidationMode) As Integer
            Return If(mode = ValidationMode.Full, FullSampleCount, LightSampleCount)
        End Function

        Private Function ValidateMemoryPattern(patternCounter As ULong, sampleCount As Integer) As Boolean
            Dim rng As New XorShift64Star(_seed Xor patternCounter)
            Dim baseValue As ULong = _seed Xor patternCounter
            For i As Integer = 0 To sampleCount - 1
                Dim idx As Integer = CInt(rng.NextULong() Mod CULng(_length))
                Dim idxValue As ULong = CULng(idx)
                Dim expected As ULong = baseValue Xor (idxValue << PatternShift1) Xor (idxValue << PatternShift2) Xor (idxValue << PatternShift3)
                If _buffer(idx) <> expected Then
                    Return False
                End If
            Next
            Return True
        End Function

        Private Sub FillPattern(patternCounter As ULong)
            Dim baseValue As ULong = _seed Xor patternCounter
            For i As Integer = 0 To _length - 1
                Dim idxValue As ULong = CULng(i)
                _buffer(i) = baseValue Xor (idxValue << PatternShift1) Xor (idxValue << PatternShift2) Xor (idxValue << PatternShift3)
            Next
        End Sub
    End Class
End Class



