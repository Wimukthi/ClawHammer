Imports System.IO
Imports System.Text.Json

Public Class RunOptions
    Public Property TimedRunMinutes As Integer = 0
    Public Property AutoStopTempC As Single = 0
    Public Property AutoStopOnThrottle As Boolean = False
    Public Property UiSnappyMode As Boolean = False
    Public Property TelemetryEnabled As Boolean = False
    Public Property TelemetryIntervalMs As Integer = 1000
    Public Property ValidationEnabled As Boolean = False
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
