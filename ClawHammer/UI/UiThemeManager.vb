Imports System.Drawing
Imports System.Runtime.CompilerServices
Imports System.Windows.Forms
Imports Microsoft.Win32

Public Structure UiThemePalette
    Public ReadOnly Background As Color
    Public ReadOnly Surface As Color
    Public ReadOnly Panel As Color
    Public ReadOnly Text As Color
    Public ReadOnly TextMuted As Color
    Public ReadOnly Border As Color
    Public ReadOnly GridLine As Color
    Public ReadOnly SelectionBack As Color
    Public ReadOnly SelectionBackInactive As Color
    Public ReadOnly SelectionText As Color
    Public ReadOnly MenuBack As Color
    Public ReadOnly MenuSelected As Color
    Public ReadOnly MenuBorder As Color

    Public Sub New(background As Color,
                   surface As Color,
                   panel As Color,
                   text As Color,
                   textMuted As Color,
                   border As Color,
                   gridLine As Color,
                   selectionBack As Color,
                   selectionBackInactive As Color,
                   selectionText As Color,
                   menuBack As Color,
                   menuSelected As Color,
                   menuBorder As Color)
        Me.Background = background
        Me.Surface = surface
        Me.Panel = panel
        Me.Text = text
        Me.TextMuted = textMuted
        Me.Border = border
        Me.GridLine = gridLine
        Me.SelectionBack = selectionBack
        Me.SelectionBackInactive = selectionBackInactive
        Me.SelectionText = selectionText
        Me.MenuBack = menuBack
        Me.MenuSelected = menuSelected
        Me.MenuBorder = menuBorder
    End Sub
End Structure

Public Structure PlotPalette
    Public ReadOnly BackColor As Color
    Public ReadOnly ForeColor As Color
    Public ReadOnly GridColor As Color
    Public ReadOnly AxisColor As Color
    Public ReadOnly HoverLineColor As Color
    Public ReadOnly HoverBorderColor As Color
    Public ReadOnly HoverBackColor As Color
    Public ReadOnly LegendBackColor As Color
    Public ReadOnly LegendBorderColor As Color
    Public ReadOnly LegendSwatchBorderColor As Color
    Public ReadOnly SwatchBackColor As Color
    Public ReadOnly SwatchBorderColor As Color

    Public Sub New(backColor As Color,
                   foreColor As Color,
                   gridColor As Color,
                   axisColor As Color,
                   hoverLineColor As Color,
                   hoverBorderColor As Color,
                   hoverBackColor As Color,
                   legendBackColor As Color,
                   legendBorderColor As Color,
                   legendSwatchBorderColor As Color,
                   swatchBackColor As Color,
                   swatchBorderColor As Color)
        Me.BackColor = backColor
        Me.ForeColor = foreColor
        Me.GridColor = gridColor
        Me.AxisColor = axisColor
        Me.HoverLineColor = hoverLineColor
        Me.HoverBorderColor = hoverBorderColor
        Me.HoverBackColor = hoverBackColor
        Me.LegendBackColor = legendBackColor
        Me.LegendBorderColor = legendBorderColor
        Me.LegendSwatchBorderColor = legendSwatchBorderColor
        Me.SwatchBackColor = swatchBackColor
        Me.SwatchBorderColor = swatchBorderColor
    End Sub
End Structure

Public Module UiThemeManager
    Private ReadOnly ListViewStates As New ConditionalWeakTable(Of ListView, ListViewThemeState)()
    Private ReadOnly GroupBoxStates As New ConditionalWeakTable(Of GroupBox, GroupBoxThemeState)()
    Private ReadOnly StatusStripStates As New ConditionalWeakTable(Of StatusStrip, StatusStripThemeState)()

    Private ReadOnly DarkPaletteValue As UiThemePalette = New UiThemePalette(
        Color.FromArgb(24, 24, 28),
        Color.FromArgb(32, 32, 38),
        Color.FromArgb(28, 28, 34),
        Color.FromArgb(230, 230, 235),
        Color.FromArgb(170, 170, 180),
        Color.FromArgb(50, 50, 60),
        Color.FromArgb(40, 40, 48),
        Color.FromArgb(58, 75, 100),
        Color.FromArgb(50, 50, 60),
        Color.FromArgb(245, 245, 248),
        Color.FromArgb(26, 26, 30),
        Color.FromArgb(55, 55, 65),
        Color.FromArgb(70, 70, 80)
    )

    Private ReadOnly LightPaletteValue As UiThemePalette = New UiThemePalette(
        SystemColors.Control,
        SystemColors.Window,
        SystemColors.Control,
        SystemColors.ControlText,
        SystemColors.GrayText,
        SystemColors.ControlDark,
        SystemColors.ControlLight,
        SystemColors.Highlight,
        SystemColors.ControlDark,
        SystemColors.HighlightText,
        SystemColors.Control,
        SystemColors.Highlight,
        SystemColors.ControlDark
    )

    Private ReadOnly DarkPlotPaletteValue As PlotPalette = New PlotPalette(
        Color.FromArgb(18, 18, 22),
        Color.FromArgb(230, 230, 235),
        Color.FromArgb(40, 40, 48),
        Color.FromArgb(90, 90, 102),
        Color.FromArgb(120, 200, 200, 210),
        Color.FromArgb(90, 90, 110),
        Color.FromArgb(230, 26, 26, 32),
        Color.FromArgb(200, 20, 20, 28),
        Color.FromArgb(70, 70, 90),
        Color.FromArgb(110, 110, 130),
        Color.FromArgb(70, 70, 80),
        Color.FromArgb(90, 90, 100)
    )

    Private ReadOnly LightPlotPaletteValue As PlotPalette = New PlotPalette(
        Color.White,
        Color.FromArgb(30, 30, 40),
        Color.FromArgb(225, 225, 232),
        Color.FromArgb(180, 180, 190),
        Color.FromArgb(120, 130, 140),
        Color.FromArgb(170, 170, 180),
        Color.FromArgb(230, 245, 246, 248),
        Color.FromArgb(220, 250, 250, 252),
        Color.FromArgb(190, 190, 200),
        Color.FromArgb(170, 170, 180),
        Color.FromArgb(210, 210, 220),
        Color.FromArgb(180, 180, 190)
    )

    Private _cachedIsDarkMode As Boolean?
    Private _toolStripRenderer As ToolStripProfessionalRenderer
    Private _toolStripRendererPalette As UiThemePalette
    Private _toolStripRendererPaletteSet As Boolean

    Public ReadOnly Property IsDarkMode As Boolean
        Get
            If _cachedIsDarkMode.HasValue Then
                Return _cachedIsDarkMode.Value
            End If

            Dim isDark As Boolean = False
            Try
                Using key As RegistryKey = Registry.CurrentUser.OpenSubKey("Software\Microsoft\Windows\CurrentVersion\Themes\Personalize")
                    Dim value As Object = Nothing
                    If key IsNot Nothing Then
                        value = key.GetValue("AppsUseLightTheme")
                    End If
                    If value IsNot Nothing Then
                        Dim intValue As Integer
                        If Integer.TryParse(value.ToString(), intValue) Then
                            isDark = (intValue = 0)
                        End If
                    End If
                End Using
            Catch
            End Try

            _cachedIsDarkMode = isDark
            Return isDark
        End Get
    End Property

    Public ReadOnly Property Palette As UiThemePalette
        Get
            Return If(IsDarkMode, DarkPaletteValue, LightPaletteValue)
        End Get
    End Property

    Public ReadOnly Property PlotPalette As PlotPalette
        Get
            Return If(IsDarkMode, DarkPlotPaletteValue, LightPlotPaletteValue)
        End Get
    End Property

    ' Apply the dark palette to a control tree when dark mode is enabled.
    Public Sub ApplyTheme(root As Control)
        If root Is Nothing Then
            Return
        End If
        If Not IsDarkMode Then
            Return
        End If
        ApplyControlTheme(root, DarkPaletteValue)
    End Sub

    ' Recursively theme supported controls and wire owner-draw handlers.
    Private Sub ApplyControlTheme(control As Control, palette As UiThemePalette)
        If control Is Nothing Then
            Return
        End If

        If TypeOf control Is Form Then
            Dim form As Form = DirectCast(control, Form)
            form.BackColor = palette.Background
            form.ForeColor = palette.Text
            If form.MainMenuStrip IsNot Nothing Then
                ApplyToolStripTheme(form.MainMenuStrip, palette)
            End If
        End If

        Select Case True
            Case TypeOf control Is SplitContainer
                Dim split As SplitContainer = DirectCast(control, SplitContainer)
                split.BorderStyle = BorderStyle.None
                split.BackColor = palette.Border
                split.Panel1.BackColor = palette.Background
                split.Panel2.BackColor = palette.Background
            Case TypeOf control Is GroupBox
                ConfigureGroupBox(DirectCast(control, GroupBox), palette)
            Case TypeOf control Is StatusStrip
                ConfigureStatusStrip(DirectCast(control, StatusStrip), palette)
            Case TypeOf control Is Label
                control.ForeColor = palette.Text
            Case TypeOf control Is ButtonBase
                control.ForeColor = palette.Text
            Case TypeOf control Is TextBoxBase
                control.BackColor = palette.Surface
                control.ForeColor = palette.Text
            Case TypeOf control Is ComboBox
                Dim combo As ComboBox = DirectCast(control, ComboBox)
                combo.BackColor = palette.Surface
                combo.ForeColor = palette.Text
            Case TypeOf control Is NumericUpDown
                Dim nud As NumericUpDown = DirectCast(control, NumericUpDown)
                nud.BackColor = palette.Surface
                nud.ForeColor = palette.Text
            Case TypeOf control Is ListBox
                control.BackColor = palette.Surface
                control.ForeColor = palette.Text
            Case TypeOf control Is ListView
                ConfigureListView(DirectCast(control, ListView), palette)
            Case TypeOf control Is ToolStrip
                ApplyToolStripTheme(DirectCast(control, ToolStrip), palette)
            Case TypeOf control Is TabControl
                control.BackColor = palette.Background
                control.ForeColor = palette.Text
            Case TypeOf control Is TabPage
                control.BackColor = palette.Background
                control.ForeColor = palette.Text
        End Select

        For Each child As Control In control.Controls
            ApplyControlTheme(child, palette)
        Next
    End Sub

    Private Sub ApplyToolStripTheme(strip As ToolStrip, palette As UiThemePalette)
        If strip Is Nothing Then
            Return
        End If

        Dim renderer As ToolStripProfessionalRenderer = GetToolStripRenderer(palette)

        strip.RenderMode = ToolStripRenderMode.Professional
        strip.Renderer = renderer
        strip.BackColor = palette.MenuBack
        strip.ForeColor = palette.Text

        For Each item As ToolStripItem In strip.Items
            ApplyToolStripItemTheme(item, palette, renderer)
        Next
    End Sub

    Private Sub ApplyToolStripItemTheme(item As ToolStripItem, palette As UiThemePalette, renderer As ToolStripProfessionalRenderer)
        If item Is Nothing Then
            Return
        End If

        item.BackColor = palette.MenuBack
        item.ForeColor = palette.Text

        Dim dropDownItem As ToolStripDropDownItem = TryCast(item, ToolStripDropDownItem)
        If dropDownItem IsNot Nothing AndAlso dropDownItem.DropDown IsNot Nothing Then
            dropDownItem.DropDown.Renderer = renderer
            dropDownItem.DropDown.BackColor = palette.MenuBack
            dropDownItem.DropDown.ForeColor = palette.Text
        End If

        Dim menuItem As ToolStripMenuItem = TryCast(item, ToolStripMenuItem)
        If menuItem Is Nothing Then
            Return
        End If

        For Each subItem As ToolStripItem In menuItem.DropDownItems
            ApplyToolStripItemTheme(subItem, palette, renderer)
        Next
    End Sub

    Private Function GetToolStripRenderer(palette As UiThemePalette) As ToolStripProfessionalRenderer
        If _toolStripRenderer Is Nothing OrElse Not _toolStripRendererPaletteSet OrElse Not palette.Equals(_toolStripRendererPalette) Then
            _toolStripRenderer = New ToolStripProfessionalRenderer(New DarkToolStripColorTable(palette))
            _toolStripRendererPalette = palette
            _toolStripRendererPaletteSet = True
        End If
        Return _toolStripRenderer
    End Function

    ' Owner-draw ListView so headers/rows match the dark palette.
    Private Sub ConfigureListView(listView As ListView, palette As UiThemePalette)
        If listView Is Nothing Then
            Return
        End If

        Dim state As ListViewThemeState = Nothing
        If Not ListViewStates.TryGetValue(listView, state) Then
            state = New ListViewThemeState(listView.GridLines)
            ListViewStates.Add(listView, state)
            AddHandler listView.DrawColumnHeader, AddressOf ListView_DrawColumnHeader
            AddHandler listView.DrawItem, AddressOf ListView_DrawItem
            AddHandler listView.DrawSubItem, AddressOf ListView_DrawSubItem
        End If
        listView.BackColor = palette.Surface
        listView.ForeColor = palette.Text
        listView.BorderStyle = BorderStyle.None
        listView.GridLines = False
        listView.OwnerDraw = True
        listView.HideSelection = False

    End Sub

    ' StatusStrip needs explicit theming and paint hooks for dark mode.
    Private Sub ConfigureStatusStrip(statusStrip As StatusStrip, palette As UiThemePalette)
        If statusStrip Is Nothing Then
            Return
        End If

        Dim state As StatusStripThemeState = Nothing
        If StatusStripStates.TryGetValue(statusStrip, state) Then
            Return
        End If

        StatusStripStates.Add(statusStrip, New StatusStripThemeState())
        statusStrip.BackColor = palette.MenuBack
        statusStrip.ForeColor = palette.Text
        statusStrip.SizingGrip = False
        statusStrip.GripStyle = ToolStripGripStyle.Hidden

        ApplyToolStripTheme(statusStrip, palette)
        AddHandler statusStrip.Paint, AddressOf StatusStrip_Paint

        For Each item As ToolStripItem In statusStrip.Items
            item.BackColor = palette.MenuBack
            item.ForeColor = palette.Text
            Dim label As ToolStripStatusLabel = TryCast(item, ToolStripStatusLabel)
            If label IsNot Nothing Then
                label.BorderSides = ToolStripStatusLabelBorderSides.None
                label.BorderStyle = Border3DStyle.Flat
            End If
        Next
    End Sub

    Private Sub ConfigureGroupBox(groupBox As GroupBox, palette As UiThemePalette)
        If groupBox Is Nothing Then
            Return
        End If

        Dim state As GroupBoxThemeState = Nothing
        If GroupBoxStates.TryGetValue(groupBox, state) Then
            Return
        End If

        GroupBoxStates.Add(groupBox, New GroupBoxThemeState())
        groupBox.BackColor = palette.Background
        groupBox.ForeColor = palette.Text
        AddHandler groupBox.Paint, AddressOf GroupBox_Paint
    End Sub

    Private Sub GroupBox_Paint(sender As Object, e As PaintEventArgs)
        Dim groupBox As GroupBox = TryCast(sender, GroupBox)
        If groupBox Is Nothing Then
            Return
        End If

        Dim palette As UiThemePalette = palette
        Dim g As Graphics = e.Graphics
        g.Clear(groupBox.BackColor)

        Dim text As String = groupBox.Text
        Dim hasText As Boolean = Not String.IsNullOrWhiteSpace(text)
        Dim textFlags As TextFormatFlags = TextFormatFlags.Left Or TextFormatFlags.VerticalCenter Or TextFormatFlags.EndEllipsis
        Dim textSize As Size = If(hasText, TextRenderer.MeasureText(g, text, groupBox.Font, New Size(Integer.MaxValue, Integer.MaxValue), textFlags), Size.Empty)
        Dim textHeight As Integer = If(hasText, Math.Max(0, textSize.Height), 0)
        Dim textRect As New Rectangle(8, 0, Math.Max(0, groupBox.Width - 16), textHeight)

        Dim borderTop As Integer = textHeight \ 2
        Dim borderRect As New Rectangle(0, borderTop, groupBox.Width - 1, groupBox.Height - borderTop - 1)

        Using borderPen As New Pen(palette.Border)
            g.DrawRectangle(borderPen, borderRect)
        End Using

        If hasText Then
            Using backBrush As New SolidBrush(groupBox.BackColor)
                g.FillRectangle(backBrush, textRect)
            End Using
            TextRenderer.DrawText(g, text, groupBox.Font, textRect, palette.Text, textFlags)
        End If
    End Sub

    Private Sub StatusStrip_Paint(sender As Object, e As PaintEventArgs)
        Dim statusStrip As StatusStrip = TryCast(sender, StatusStrip)
        If statusStrip Is Nothing Then
            Return
        End If

        Dim palette As UiThemePalette = palette
        Dim bounds As New Rectangle(0, 0, statusStrip.Width, statusStrip.Height)
        Using backBrush As New SolidBrush(palette.MenuBack)
            e.Graphics.FillRectangle(backBrush, bounds)
        End Using
        Using borderPen As New Pen(palette.Border)
            e.Graphics.DrawLine(borderPen, 0, 0, statusStrip.Width, 0)
        End Using
    End Sub

    Private NotInheritable Class GroupBoxThemeState
    End Class

    Private NotInheritable Class StatusStripThemeState
    End Class

    Private Sub ListView_DrawColumnHeader(sender As Object, e As DrawListViewColumnHeaderEventArgs)
        Dim palette As UiThemePalette = palette
        Dim listView As ListView = TryCast(sender, ListView)
        Dim fontToUse As Font = If(listView IsNot Nothing, listView.Font, Control.DefaultFont)
        Dim bounds As Rectangle = e.Bounds
        e.DrawDefault = False
        Using backBrush As New SolidBrush(palette.Panel)
            e.Graphics.FillRectangle(backBrush, bounds)
        End Using
        Using borderPen As New Pen(palette.Border)
            e.Graphics.DrawRectangle(borderPen, bounds)
        End Using

        Dim textRect As New Rectangle(bounds.X + 6, bounds.Y, Math.Max(0, bounds.Width - 12), bounds.Height)
        Dim headerAlign As HorizontalAlignment = If(e.Header IsNot Nothing, e.Header.TextAlign, HorizontalAlignment.Left)
        Dim flags As TextFormatFlags = TextFormatFlags.VerticalCenter Or TextFormatFlags.EndEllipsis
        Select Case headerAlign
            Case HorizontalAlignment.Center
                flags = flags Or TextFormatFlags.HorizontalCenter
            Case HorizontalAlignment.Right
                flags = flags Or TextFormatFlags.Right
            Case Else
                flags = flags Or TextFormatFlags.Left
        End Select
        Dim headerTextColor As Color = GetReadableTextColor(palette.Panel, DarkPaletteValue.Text, LightPaletteValue.Text)
        TextRenderer.DrawText(e.Graphics, e.Header.Text, fontToUse, textRect, headerTextColor, flags)
    End Sub

    Private Sub ListView_DrawItem(sender As Object, e As DrawListViewItemEventArgs)
        If e Is Nothing Then
            Return
        End If

        If e.Item Is Nothing Then
            Return
        End If

        If e.Item.ListView IsNot Nothing AndAlso e.Item.ListView.View <> View.Details Then
            e.DrawDefault = True
            Return
        End If

        e.DrawDefault = False
    End Sub

    Private Sub ListView_DrawSubItem(sender As Object, e As DrawListViewSubItemEventArgs)
        Dim listView As ListView = TryCast(sender, ListView)
        If listView Is Nothing OrElse e Is Nothing OrElse e.Item Is Nothing Then
            Return
        End If

        e.DrawDefault = False

        Dim palette As UiThemePalette = palette
        Dim fontToUse As Font = If(e.Item.Font, listView.Font)
        Dim isSelected As Boolean = e.Item.Selected
        Dim isFocused As Boolean = listView.Focused
        Dim backColor As Color = If(isSelected, If(isFocused, palette.SelectionBack, palette.SelectionBackInactive), palette.Surface)
        Dim foreColor As Color
        If isSelected Then
            foreColor = GetReadableTextColor(backColor, DarkPaletteValue.SelectionText, LightPaletteValue.SelectionText)
        Else
            foreColor = GetReadableTextColor(backColor, DarkPaletteValue.Text, LightPaletteValue.Text)
        End If

        Using backBrush As New SolidBrush(backColor)
            e.Graphics.FillRectangle(backBrush, e.Bounds)
        End Using

        Dim textRect As Rectangle = e.Bounds
        textRect.Inflate(-4, 0)

        Dim treeIndent As Integer = 0
        Dim treeGlyphText As String = Nothing
        If e.ColumnIndex = 0 Then
            Dim treeInfo As TreeListItemInfo = TryCast(e.Item.Tag, TreeListItemInfo)
            If treeInfo IsNot Nothing Then
                treeIndent = Math.Max(0, treeInfo.Level) * 14
                If treeInfo.IsGroup Then
                    treeGlyphText = If(treeInfo.IsExpanded, "[-]", "[+]")
                End If
            End If
        End If

        If e.ColumnIndex = 0 Then
            Dim leftX As Integer = textRect.X + treeIndent
            If treeGlyphText IsNot Nothing Then
                Dim glyphSize As Size = TextRenderer.MeasureText(e.Graphics, treeGlyphText, fontToUse, New Size(Integer.MaxValue, Integer.MaxValue), TextFormatFlags.NoPadding)
                Dim glyphRect As New Rectangle(leftX, e.Bounds.Top, glyphSize.Width, e.Bounds.Height)
                TextRenderer.DrawText(e.Graphics, treeGlyphText, fontToUse, glyphRect, foreColor, TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding)
                leftX += glyphSize.Width + 4
            End If

            Dim imageList As ImageList = listView.SmallImageList
            If imageList IsNot Nothing Then
                Dim imageIndex As Integer = e.Item.ImageIndex
                If imageIndex < 0 AndAlso Not String.IsNullOrWhiteSpace(e.Item.ImageKey) Then
                    imageIndex = imageList.Images.IndexOfKey(e.Item.ImageKey)
                End If
                If imageIndex >= 0 AndAlso imageIndex < imageList.Images.Count Then
                    Dim icon As Image = imageList.Images(imageIndex)
                    Dim iconY As Integer = e.Bounds.Top + (e.Bounds.Height - icon.Height) \ 2
                    e.Graphics.DrawImage(icon, leftX, iconY, icon.Width, icon.Height)
                    leftX += icon.Width + 6
                End If
            End If

            textRect.X = leftX
            textRect.Width = Math.Max(0, e.Bounds.Right - leftX - 4)
        End If

        Dim headerAlign As HorizontalAlignment = If(e.Header IsNot Nothing, e.Header.TextAlign, HorizontalAlignment.Left)
        Dim flags As TextFormatFlags = TextFormatFlags.VerticalCenter Or TextFormatFlags.EndEllipsis
        Select Case headerAlign
            Case HorizontalAlignment.Center
                flags = flags Or TextFormatFlags.HorizontalCenter
            Case HorizontalAlignment.Right
                flags = flags Or TextFormatFlags.Right
            Case Else
                flags = flags Or TextFormatFlags.Left
        End Select

        TextRenderer.DrawText(e.Graphics, e.SubItem.Text, fontToUse, textRect, foreColor, flags)

        Dim state As ListViewThemeState = Nothing
        Dim showGridLines As Boolean = listView.GridLines
        If ListViewStates.TryGetValue(listView, state) Then
            showGridLines = state.ShowGridLines
        End If

        If showGridLines Then
            Using gridPen As New Pen(palette.GridLine)
                e.Graphics.DrawLine(gridPen, e.Bounds.Right - 1, e.Bounds.Top, e.Bounds.Right - 1, e.Bounds.Bottom)
                e.Graphics.DrawLine(gridPen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1)
            End Using
        End If
    End Sub

    Public NotInheritable Class TreeListItemInfo
        Public ReadOnly Property Key As String
        Public ReadOnly Property Level As Integer
        Public ReadOnly Property IsGroup As Boolean
        Public ReadOnly Property IsExpanded As Boolean

        Public Sub New(key As String, level As Integer, isGroup As Boolean, isExpanded As Boolean)
            Me.Key = key
            Me.Level = level
            Me.IsGroup = isGroup
            Me.IsExpanded = isExpanded
        End Sub
    End Class

    Private NotInheritable Class ListViewThemeState
        Public ReadOnly ShowGridLines As Boolean

        Public Sub New(showGridLines As Boolean)
            Me.ShowGridLines = showGridLines
        End Sub
    End Class

    Private Function GetReadableTextColor(backColor As Color, lightText As Color, darkText As Color) As Color
        Dim luminance As Double = (0.2126 * backColor.R) + (0.7152 * backColor.G) + (0.0722 * backColor.B)
        If luminance < 128.0 Then
            Return lightText
        End If
        Return darkText
    End Function

    Private NotInheritable Class DarkToolStripColorTable
        Inherits ProfessionalColorTable

        Private ReadOnly _palette As UiThemePalette

        Public Sub New(palette As UiThemePalette)
            UseSystemColors = False
            _palette = palette
        End Sub

        Public Overrides ReadOnly Property MenuStripGradientBegin As Color
            Get
                Return _palette.MenuBack
            End Get
        End Property

        Public Overrides ReadOnly Property MenuStripGradientEnd As Color
            Get
                Return _palette.MenuBack
            End Get
        End Property

        Public Overrides ReadOnly Property ToolStripGradientBegin As Color
            Get
                Return _palette.MenuBack
            End Get
        End Property

        Public Overrides ReadOnly Property ToolStripGradientMiddle As Color
            Get
                Return _palette.MenuBack
            End Get
        End Property

        Public Overrides ReadOnly Property ToolStripGradientEnd As Color
            Get
                Return _palette.MenuBack
            End Get
        End Property

        Public Overrides ReadOnly Property ToolStripBorder As Color
            Get
                Return _palette.MenuBorder
            End Get
        End Property

        Public Overrides ReadOnly Property MenuItemSelected As Color
            Get
                Return _palette.MenuSelected
            End Get
        End Property

        Public Overrides ReadOnly Property MenuItemBorder As Color
            Get
                Return _palette.MenuBorder
            End Get
        End Property

        Public Overrides ReadOnly Property MenuItemPressedGradientBegin As Color
            Get
                Return _palette.MenuSelected
            End Get
        End Property

        Public Overrides ReadOnly Property MenuItemPressedGradientMiddle As Color
            Get
                Return _palette.MenuSelected
            End Get
        End Property

        Public Overrides ReadOnly Property MenuItemPressedGradientEnd As Color
            Get
                Return _palette.MenuSelected
            End Get
        End Property

        Public Overrides ReadOnly Property MenuItemSelectedGradientBegin As Color
            Get
                Return _palette.MenuSelected
            End Get
        End Property

        Public Overrides ReadOnly Property MenuItemSelectedGradientEnd As Color
            Get
                Return _palette.MenuSelected
            End Get
        End Property

        Public Overrides ReadOnly Property ImageMarginGradientBegin As Color
            Get
                Return _palette.MenuBack
            End Get
        End Property

        Public Overrides ReadOnly Property ImageMarginGradientMiddle As Color
            Get
                Return _palette.MenuBack
            End Get
        End Property

        Public Overrides ReadOnly Property ImageMarginGradientEnd As Color
            Get
                Return _palette.MenuBack
            End Get
        End Property

        Public Overrides ReadOnly Property SeparatorDark As Color
            Get
                Return _palette.MenuBorder
            End Get
        End Property

        Public Overrides ReadOnly Property SeparatorLight As Color
            Get
                Return _palette.MenuBorder
            End Get
        End Property

        Public Overrides ReadOnly Property StatusStripGradientBegin As Color
            Get
                Return _palette.MenuBack
            End Get
        End Property

        Public Overrides ReadOnly Property StatusStripGradientEnd As Color
            Get
                Return _palette.MenuBack
            End Get
        End Property
    End Class
End Module
