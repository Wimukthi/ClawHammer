
Public Class uacprompt
    Private _toolTip As ToolTip

    Private Sub btnuac_Click(ByVal sender As System.Object, _
                             ByVal e As System.EventArgs) _
            Handles btnuac.Click
        RestartElevated()
    End Sub

    Private Sub uacprompt_Load(ByVal sender As System.Object, _
                               ByVal e As System.EventArgs) _
            Handles MyBase.Load
        UiThemeManager.ApplyTheme(Me)
        _toolTip = New ToolTip() With {
            .AutoPopDelay = 12000,
            .InitialDelay = 500,
            .ReshowDelay = 150,
            .ShowAlways = True
        }
        _toolTip.SetToolTip(btnuac, "Restart ClawHammer with administrator privileges.")
        _toolTip.SetToolTip(Label1, "This action is required for privileged hardware access.")
        Beep()
        AddShieldToButton(btnuac)
    End Sub

End Class
