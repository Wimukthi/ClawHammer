Imports OpenHardwareMonitor
Imports OpenHardwareMonitor.Hardware
Imports System.Timers
Imports System.Numerics
Imports System.Reflection

' ----------------------------------------------------------------------------------------
' Author:                    Wimukthi Bandara
' Company:                   Grey Element Software
' Assembly version:          1.2.0.160
' ----------------------------------------------------------------------------------------
' This program is free software: you can redistribute it and/or modify
' it under the terms of the GNU General Public License as published by
' the Free Software Foundation, either version 3 of the License, or
' any later version.
' This program is distributed in the hope that it will be useful,
' but WITHOUT ANY WARRANTY; without even the implied warranty of
' MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
' GNU General Public License for more details.
' You should have received a copy of the GNU General Public License
' along with this program.  If not, see http://www.gnu.org/licenses/.
' ----------------------------------------------------------------------------------------



Public Class frmMain
    Public minvalue As Int64 = 2 ' Start position
    Public maxvalue As Int64 = 9223372036854775807 ' Prime Search space
    Public pilist As String ' Stores the list of prime numbers found
    Public ThreadsArray As List(Of Threading.Thread) = New List(Of Threading.Thread) 'Thread array to store the started threads
    Private perfCPU As New _
        System.Diagnostics.PerformanceCounter(
            "Processor", "% Processor Time", "_Total") ' Performance counter is used to get CPU usage
    Public logs As New ClawLog
    Public coreCount As Integer = 0


    Public Shared Sub SetDoubleBuffered(ByVal control As Control)
        GetType(Control).InvokeMember("DoubleBuffered", BindingFlags.SetProperty Or BindingFlags.Instance Or BindingFlags.NonPublic, Nothing, control, New Object() {True})
    End Sub

    Sub SubCPUDatTimer(ByVal sender As Object, ByVal e As ElapsedEventArgs) 'CPU Data retrieval subroutine
        ' Write the SignalTime.
        Try



            Dim computer As New Computer()
            computer.Open()
            computer.CPUEnabled = True

            Dim cpu = computer.Hardware.Where(Function(h) h.HardwareType = HardwareType.CPU).FirstOrDefault()

            If cpu IsNot Nothing Then
                cpu.Update()

                Dim tempSensors = cpu.Sensors.Where(Function(s) s.SensorType = SensorType.Temperature)
                Dim CPUTEMPVAR As Integer = 0
                lstvCoreTemps.Items.Clear()
                lstvCoreTemps.BeginUpdate()

                For i = 0 To coreCount - 1
                    CPUTEMPVAR += tempSensors.ToList.Item(i).Value
                    lstvCoreTemps.Items.Add("core " & i.ToString)
                    lstvCoreTemps.Items(i).SubItems.Add(tempSensors.ToList.Item(i).Value & "°C")
                Next

                lstvCoreTemps.EndUpdate()
                cputemp.Text = "CPU Temp: " & Conversion.Int(CPUTEMPVAR / coreCount) & "°C"
            End If
        Catch ex As Exception
            Dim el As New ErrorLogger
            el.WriteToErrorLog(ex.Message, ex.StackTrace, My.Application.Info.AssemblyName.ToString & " Encountered an Error")
        End Try
    End Sub

    Sub SubCPUUsage(ByVal sender As Object, ByVal e As ElapsedEventArgs)
        progCPUUsage.Value = CInt(Fix(perfCPU.NextValue()))
        lblusage.Text = "CPU Usage: " & progCPUUsage.Value.ToString() & "%"
    End Sub

    Private Sub frmMain_Closing(sender As Object, e As System.ComponentModel.CancelEventArgs) Handles MyBase.Closing
        ' if chksavelog is checked then save log on exit
        If chkSaveLog.Checked = True Then
            logs.WriteLog(rhtxtlog.Text)
        End If
    End Sub

    Private Sub frmMain_Load(sender As System.Object, e As System.EventArgs) Handles MyBase.Load

        Try
            SetDoubleBuffered(lstvCoreTemps) 'Enable double buffering for the Listview control

            Dim CpuDatTimer As Timer = New Timer(200) 'CPU Data retrieval timer
            AddHandler CpuDatTimer.Elapsed, New ElapsedEventHandler(AddressOf SubCPUDatTimer)
            CpuDatTimer.Start()


            Dim CpuUsageTimer As Timer = New Timer(200) 'CPU usage Data retrieval timer
            AddHandler CpuUsageTimer.Elapsed, New ElapsedEventHandler(AddressOf SubCPUUsage)
            CpuUsageTimer.Start()


            For Each item In New System.Management.ManagementObjectSearcher("Select * from Win32_Processor").[Get]()
                coreCount += Integer.Parse(item("NumberOfCores").ToString())
            Next



            Me.Text = "ClawHammer v" + My.Application.Info.Version.ToString + " - [Idle]" ' Set the default title bar text
            NumThreads.Maximum = Environment.ProcessorCount 'get the Logical processor count
            NumThreads.Value = Environment.ProcessorCount 'set the default thread count to current processor count
            CmbThreadPriority.Text = "Normal" ' Set the default Thread priority
            lblProcessorCount.Text = Environment.ProcessorCount.ToString + " Hardware Threads" ' Set the status bar processor count
            lblcores.Text = coreCount & " Physical Cores"
            Control.CheckForIllegalCrossThreadCalls = False 'Dont check for illegal cross thread calls


            rhtxtlog.Text &= Date.Now.ToString + " - ClawHammer Startup Successful" + vbCrLf ' Startup is ok
        Catch ex As Exception
            Dim el As New ErrorLogger
            rhtxtlog.Text &= Date.Now.ToString + " - ClawHammer Encountered Errors while starting up!" + vbCrLf 'Startup is not Ok
            el.WriteToErrorLog(ex.Message, ex.StackTrace, My.Application.Info.AssemblyName.ToString & " Encountered an Error")
        End Try

    End Sub

    Private Sub btnStart_Click(sender As System.Object, e As System.EventArgs) Handles btnStart.Click

        Try

            If btnStart.Text = "Start" Then 'if button text is start then
                btnStart.Text = "Stop" ' set button text to stop
                btnStart.Image = My.Resources._stop

                Me.Text = "ClawHammer v" + My.Application.Info.Version.ToString + " - [Running]" ' set the title bar text to running

                rhtxtlog.Text &= Date.Now.ToString + " - Calculating Primes" + vbCrLf 'Display log info

                For i = 1 To NumThreads.Value 'Start the Thread Execution loop
                    Dim t As Threading.Thread = New Threading.Thread(AddressOf GetPrimeRange) 'Declare new thread

                    ThreadsArray.Add(t) 'add threads to the thread array, this will be used to terminate the started threads later


                    Select Case CmbThreadPriority.Text 'Set the Process priority as selected
                        Case "Normal"
                            t.Priority = System.Threading.ThreadPriority.Normal
                        Case "Above Normal"
                            t.Priority = System.Threading.ThreadPriority.AboveNormal
                        Case "Below Normal"
                            t.Priority = System.Threading.ThreadPriority.BelowNormal
                        Case "Lowest"
                            t.Priority = System.Threading.ThreadPriority.Lowest
                        Case "Highest"
                            t.Priority = System.Threading.ThreadPriority.Highest
                    End Select

                    t.IsBackground = True ' This will cause all threads to terminate when the main thread is closed

                    t.Start() ' Start the Thread

                    rhtxtlog.Text &= "[" + Date.Now.ToString + "] " + "Thread Created [Thread ID] : " + t.ManagedThreadId.ToString + vbCrLf ' Display what thread was started

                Next

                LblActiveThreads.Text = ThreadsArray.Count.ToString + " Threads Active" ' Display the number of threads active on status bar

                'TmrThread.Enabled = True
            ElseIf btnStart.Text = "Stop" Then 'if Pressed while threads are running

                'TmrThread.Enabled = False

                rhtxtlog.Text &= "[" + Date.Now.ToString + "] " + "Sending Abort Signal to All Threads..." + vbCrLf 'Show the confirmation of Abort signal

                For i = 0 To ThreadsArray.Count - 1 'Starting the loop to Abort all the threads in the ThreadsArray

                    If ThreadsArray.Item(i).IsAlive = True Then
                        ThreadsArray.Item(i).Abort()
                        rhtxtlog.Text &= "[" + Date.Now.ToString + "] " + "Thread Aborted [Thread ID] : " + ThreadsArray.Item(i).ManagedThreadId.ToString + vbCrLf 'Show which thread was aborted

                    End If

                Next

                Me.Text = "ClawHammer v" + My.Application.Info.Version.ToString + " - [Idle]" 'Set the title bar to idle
                ThreadsArray.Clear() 'Clear the threads array
                LblActiveThreads.Text = ThreadsArray.Count.ToString + " Threads Active" ' Set the active thread count on status bar

                btnStart.Text = "Start" 'Set the button text to Start
                btnStart.Image = My.Resources.arrow_right_3

            End If

        Catch ex As Exception
            Dim el As New ErrorLogger
            el.WriteToErrorLog(ex.Message, ex.StackTrace, My.Application.Info.AssemblyName.ToString & " Encountered an Error")
        End Try

    End Sub

    '================================================ Prime Search Algorithm Start ===============================================================================================
    Dim Primes As New List(Of Int64) 'Declare array to store prime numbers
    Dim Max As Int64 = 1 ' Max
    Private Function IsPrimeBase(ByVal Value As Int64) As Boolean
        Try

            For Teller As Int64 = 0 To Primes.Count - 1



                Dim Prime As Int64 = Primes(Teller)
                If Value Mod Prime = 0 Then
                    Return False
                End If
            Next
            Return True
        Catch ex As Exception
            Dim el As New ErrorLogger
            el.WriteToErrorLog(ex.Message, ex.StackTrace, My.Application.Info.AssemblyName.ToString & " Encountered an Error")
            Return False
        End Try
    End Function
    Public Function IsPrime(ByVal Value As Int64) As Boolean

        Try

            '' MAX

            Dim MaxCheck As Int64 = Math.Sqrt(Value)
            '' 1
            If Value = 1 Then
                Return True
            End If
            '' ALREADY TRIED
            For Teller As Int64 = 0 To Primes.Count - 1


                Dim Prime As Int64 = Primes(Teller)
                If Prime > MaxCheck Then
                    Exit For
                End If
                If Value Mod Prime = 0 Then
                    Return False
                End If
            Next
            '' UNTRIED VALUES
            For Current As Int64 = Max + 1 To MaxCheck


                '' CURRENT PRIME
                If Not IsPrimeBase(Current) Then
                    Continue For
                End If
                Max = Current
                Primes.Add(Current)
                '' EXIT
                If Value Mod Current = 0 Then
                    Return False
                End If
            Next
            '' NO PRESEDING PRIMES
            Return True
        Catch ex As Exception
            Dim el As New ErrorLogger
            el.WriteToErrorLog(ex.Message, ex.StackTrace, My.Application.Info.AssemblyName.ToString & " Encountered an Error")
            Return False
        End Try
    End Function
    Public Sub GetPrimeRange()
        Try

            Dim ToReturn As New List(Of Int64)
            For Teller As Int64 = minvalue To maxvalue


                If IsPrime(Teller) Then
                    ToReturn.Add(Teller)
                End If

            Next
        Catch ex As Exception
            Dim el As New ErrorLogger
            el.WriteToErrorLog(ex.Message, ex.StackTrace, My.Application.Info.AssemblyName.ToString & " Encountered an Error")
        End Try
    End Sub





    Private Sub grpClawHammer_Enter(sender As System.Object, e As System.EventArgs) Handles grpClawHammer.Enter

    End Sub



    Private Sub AboutToolStripMenuItem_Click(sender As System.Object, e As System.EventArgs) Handles AboutToolStripMenuItem.Click
        frmabout.ShowDialog(Me)
    End Sub

    Private Sub ExitToolStripMenuItem_Click(sender As System.Object, e As System.EventArgs) Handles ExitToolStripMenuItem.Click
        Me.Close()
    End Sub

    Private Sub tmrCPU_Tick(sender As System.Object, e As System.EventArgs)

    End Sub

    Private Sub chkSaveLog_CheckedChanged(sender As System.Object, e As System.EventArgs) Handles chkSaveLog.CheckedChanged
        My.Settings.Save()
    End Sub

    Private Sub rhtxtlog_TextChanged(sender As System.Object, e As System.EventArgs) Handles rhtxtlog.TextChanged

    End Sub


    Private Sub bgwMonitor_DoWork(sender As Object, e As System.ComponentModel.DoWorkEventArgs)

    End Sub

    Private Sub Button1_Click(sender As Object, e As EventArgs)
        If ThreadsArray.Item(1).IsAlive = True Then
            ThreadsArray.Item(1).Abort()
        End If
    End Sub
End Class
