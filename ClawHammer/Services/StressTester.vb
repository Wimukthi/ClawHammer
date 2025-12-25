Imports System.Threading
Imports System.Numerics

Public Enum StressTestType
    IntegerPrimes
    FloatingPoint
    AVX
    Mixed
End Enum

Public Class StressTester
    ' Primality check with cooperative cancellation.
    Public Function IsPrime(ByVal Value As Long, ByVal token As CancellationToken) As Boolean
        If Value <= 1 Then Return False
        If Value = 2 Then Return True
        If Value Mod 2 = 0 Then Return False

        If token.IsCancellationRequested Then Return False

        Dim MaxCheck As Long = CLng(Math.Sqrt(Value))

        For i As Long = 3 To MaxCheck Step 2
            If token.IsCancellationRequested Then Return False

            If Value Mod i = 0 Then
                Return False
            End If
        Next

        Return True
    End Function

    ' Integer workload: prime checks across a range.
    Public Sub FindPrimesInRange(ByVal minvalue As Long, ByVal maxvalue As Long, ByVal token As CancellationToken, ByVal reportProgress As Action(Of Integer))
        If minvalue > maxvalue Then
            Return
        End If

        Const ProgressBatchSize As Integer = 1024
        Dim progressBatch As Integer = 0

        Do While Not token.IsCancellationRequested
            For Teller As Long = minvalue To maxvalue
                If token.IsCancellationRequested Then
                    Exit For
                End If

                IsPrime(Teller, token)

                progressBatch += 1
                If progressBatch >= ProgressBatchSize Then
                    reportProgress?.Invoke(progressBatch)
                    progressBatch = 0
                End If
            Next

            If progressBatch > 0 Then
                reportProgress?.Invoke(progressBatch)
                progressBatch = 0
            End If
        Loop
    End Sub

    ' Floating-point workload.
    Public Sub PerformFpWorkload(ByVal token As CancellationToken, ByVal reportProgress As Action(Of Integer))
        Dim result As Double = 0.0
        Dim rng As New Random()
        Const ProgressBatchSize As Integer = 1024
        Dim progressBatch As Integer = 0

        Try
            Do While Not token.IsCancellationRequested
                Dim x As Double = rng.NextDouble() * 100.0
                Dim y As Double = rng.NextDouble() * 50.0

                result += Math.Sin(x) * Math.Cos(y)
                result -= Math.Sqrt(Math.Abs(result))
                result *= Math.Pow(x / (y + 0.001), 1.5)
                result /= (Math.Log(x + 1.0) + 1.0)

                If Double.IsInfinity(result) OrElse Double.IsNaN(result) Then
                    result = rng.NextDouble()
                End If

                progressBatch += 1

                If progressBatch >= ProgressBatchSize Then
                    reportProgress?.Invoke(progressBatch)
                    progressBatch = 0
                End If
            Loop
        Catch ex As Exception
            Console.WriteLine($"Error in FP Workload thread {Threading.Thread.CurrentThread.ManagedThreadId}: {ex.Message}")
        Finally
            If progressBatch > 0 Then
                reportProgress?.Invoke(progressBatch)
            End If
        End Try
    End Sub

    ' AVX/SIMD workload.
    Public Sub PerformAvxWorkload(ByVal token As CancellationToken, ByVal reportProgress As Action(Of Integer))
        If Not Vector.IsHardwareAccelerated Then
            Console.WriteLine($"AVX/SIMD hardware acceleration not supported on thread {Threading.Thread.CurrentThread.ManagedThreadId}. Exiting workload.")
            Return
        End If

        Dim vectorSize As Integer = Vector(Of Double).Count ' Number of doubles per vector
        Dim arr1(vectorSize - 1) As Double
        Dim arr2(vectorSize - 1) As Double
        Dim resultArr(vectorSize - 1) As Double

        Dim rng As New Random()
        Const ProgressBatchSize As Integer = 1024
        Dim progressBatch As Integer = 0

        Try
            Do While Not token.IsCancellationRequested
                For i As Integer = 0 To vectorSize - 1
                    arr1(i) = rng.NextDouble() * 100.0
                    arr2(i) = rng.NextDouble() * 50.0 + 0.001
                Next

                Dim vec1 As New Vector(Of Double)(arr1)
                Dim vec2 As New Vector(Of Double)(arr2)
                Dim resultVec As Vector(Of Double)

                resultVec = Vector.Add(vec1, vec2)
                resultVec = Vector.Multiply(resultVec, vec1)
                resultVec = Vector.Subtract(resultVec, Vector.SquareRoot(Vector.Abs(vec2)))
                resultVec = Vector.Divide(resultVec, vec2)

                resultVec.CopyTo(resultArr)

                progressBatch += 1

                If progressBatch >= ProgressBatchSize Then
                    reportProgress?.Invoke(progressBatch)
                    progressBatch = 0
                End If
            Loop
        Catch ex As Exception
            Console.WriteLine($"Error in AVX Workload thread {Threading.Thread.CurrentThread.ManagedThreadId}: {ex.Message}")
        Finally
            If progressBatch > 0 Then
                reportProgress?.Invoke(progressBatch)
            End If
        End Try
    End Sub

    Public Sub RunValidationLoop(ByVal token As CancellationToken, ByVal reportError As Action(Of String))
        Const IntervalMs As Integer = 30000

        Do While Not token.IsCancellationRequested
            If Not ValidatePrimeCount(token) Then
                reportError?.Invoke("Validation failed: prime count mismatch.")
            End If

            Dim remaining As Integer = IntervalMs
            While remaining > 0 AndAlso Not token.IsCancellationRequested
                Thread.Sleep(Math.Min(250, remaining))
                remaining -= 250
            End While
        Loop
    End Sub

    Private Function ValidatePrimeCount(ByVal token As CancellationToken) As Boolean
        Const MaxPrimeCheck As Integer = 10000
        Const ExpectedPrimeCount As Integer = 1229
        Dim count As Integer = 0

        For i As Integer = 2 To MaxPrimeCheck
            If token.IsCancellationRequested Then
                Return True
            End If
            If IsPrime(i, token) Then
                count += 1
            End If
        Next

        Return count = ExpectedPrimeCount
    End Function

End Class
