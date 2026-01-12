Friend Module AppVersionHelper
    Friend Function GetAppVersionDisplay() As String
        Return FormatVersionDisplay(My.Application.Info.Version)
    End Function

    Friend Function FormatVersionDisplay(version As Version) As String
        Dim baseText As String
        If version.Build >= 0 Then
            baseText = $"{version.Major}.{version.Minor}.{version.Build}"
        Else
            baseText = $"{version.Major}.{version.Minor}"
        End If

        If version.Revision >= 0 Then
            Return $"v{baseText} (Build {version.Revision})"
        End If

        Return $"v{baseText}"
    End Function
End Module
