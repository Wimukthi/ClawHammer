Imports System.IO
Imports System.Text.Json

Public Enum ValidationMode
    Off
    Light
    Full
End Enum

Public Class RunOptions
    Public Property TimedRunMinutes As Integer = 0
    Public Property AutoStopTempC As Single = 0
    Public Property AutoStopOnThrottle As Boolean = False
    Public Property UiSnappyMode As Boolean = False
    Public Property TelemetryEnabled As Boolean = False
    Public Property TelemetryIntervalMs As Integer = 1000
    ' Toggle advanced workload visibility in the workload selector.
    Public Property ShowAdvancedWorkloads As Boolean = False
    Private _validationMode As ValidationMode = ValidationMode.Off
    ' Validation mode controls self-test and periodic checks in workers.
    Public Property ValidationMode As ValidationMode
        Get
            Return _validationMode
        End Get
        Set(value As ValidationMode)
            _validationMode = value
        End Set
    End Property
    ' How often workers should validate their results (ms).
    Public Property ValidationIntervalMs As Integer = 30000
    ' Per-worker batch size; larger values reduce overhead.
    Public Property ValidationBatchSize As Integer = 4096
    ' Backward-compatible flag; true when ValidationMode is not Off.
    Public Property ValidationEnabled As Boolean
        Get
            Return _validationMode <> ValidationMode.Off
        End Get
        Set(value As Boolean)
            If value Then
                If _validationMode = ValidationMode.Off Then
                    _validationMode = ValidationMode.Light
                End If
            Else
                _validationMode = ValidationMode.Off
            End If
        End Set
    End Property
    Public Property AutoShowTempPlot As Boolean = False
    Public Property UseAffinity As Boolean = False
    Public Property AffinityCores As List(Of Integer) = New List(Of Integer)()
End Class

Public Class ProfileData
    Public Property Threads As Integer
    Public Property StressType As String
    Public Property ThreadPriority As String
    Public Property SaveLogOnExit As Boolean
    Public Property RunOptions As RunOptions
    Public Property PlotTimeWindowSeconds As Single = 120
End Class

Public Class ProfileStore
    Public Property LastProfileName As String = "Default"
    Public Property Profiles As Dictionary(Of String, ProfileData) = New Dictionary(Of String, ProfileData)(StringComparer.OrdinalIgnoreCase)
End Class

Public Module ProfileManager
    Public Sub SaveProfile(path As String, profile As ProfileData)
        Dim options As New JsonSerializerOptions With {
            .WriteIndented = True
        }
        Dim json As String = JsonSerializer.Serialize(profile, options)
        File.WriteAllText(path, json)
    End Sub

    Public Function LoadProfile(path As String) As ProfileData
        Dim json As String = File.ReadAllText(path)
        Dim options As New JsonSerializerOptions With {
            .PropertyNameCaseInsensitive = True
        }
        Return JsonSerializer.Deserialize(Of ProfileData)(json, options)
    End Function
End Module
