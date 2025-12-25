
Public Class uacprompt

    Private Sub btnuac_Click(ByVal sender As System.Object, _
                             ByVal e As System.EventArgs) _
            Handles btnuac.Click
        RestartElevated()
    End Sub

    Private Sub uacprompt_Load(ByVal sender As System.Object, _
                               ByVal e As System.EventArgs) _
            Handles MyBase.Load
        UiThemeManager.ApplyTheme(Me)
        Beep()
        AddShieldToButton(btnuac)
    End Sub

End Class
