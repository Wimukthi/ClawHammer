<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Class frmMain
    Inherits System.Windows.Forms.Form

    'Form overrides dispose to clean up the component list.
    <System.Diagnostics.DebuggerNonUserCode()>
    Protected Overrides Sub Dispose(ByVal disposing As Boolean)
        Try
            If disposing AndAlso components IsNot Nothing Then
                components.Dispose()
            End If
        Finally
            MyBase.Dispose(disposing)
        End Try
    End Sub

    'Required by the Windows Form Designer
    Private components As System.ComponentModel.IContainer
    'NOTE: The following procedure is required by the Windows Form Designer
    'It can be modified using the Windows Form Designer.  
    'Do not modify it using the code editor.
    <System.Diagnostics.DebuggerStepThrough()>
    Private Sub InitializeComponent()
        components = New ComponentModel.Container()
        Dim resources As System.ComponentModel.ComponentResourceManager = New System.ComponentModel.ComponentResourceManager(GetType(frmMain))
        grpClawHammer = New GroupBox()
        Label1 = New Label()
        lblProfile = New Label()
        cmbProfiles = New ComboBox()
        cmbStressType = New ComboBox()
        chkSaveLog = New CheckBox()
        btnStart = New Button()
        lblThroughput = New Label()
        lblpriority = New Label()
        CmbThreadPriority = New ComboBox()
        lblNumberOf = New Label()
        NumThreads = New NumericUpDown()
        rhtxtlog = New RichTextBox()
        StStatus = New StatusStrip()
        lblcores = New ToolStripStatusLabel()
        lblProcessorCount = New ToolStripStatusLabel()
        LblActiveThreads = New ToolStripStatusLabel()
        cputemp = New ToolStripStatusLabel()
        lblusage = New ToolStripStatusLabel()
        progCPUUsage = New ToolStripProgressBar()
        clawMenu = New MenuStrip()
        FileToolStripMenuItem = New ToolStripMenuItem()
        ExitToolStripMenuItem = New ToolStripMenuItem()
        ToolsToolStripMenuItem = New ToolStripMenuItem()
        RunOptionsToolStripMenuItem = New ToolStripMenuItem()
        TemperaturePlotToolStripMenuItem = New ToolStripMenuItem()
        CoreAffinityToolStripMenuItem = New ToolStripMenuItem()
        SaveProfileToolStripMenuItem = New ToolStripMenuItem()
        LoadProfileToolStripMenuItem = New ToolStripMenuItem()
        SystemInfoToolStripMenuItem = New ToolStripMenuItem()
        ExportReportToolStripMenuItem = New ToolStripMenuItem()
        CheckLhmUpdatesToolStripMenuItem = New ToolStripMenuItem()
        HelpToolStripMenuItem = New ToolStripMenuItem()
        AboutToolStripMenuItem = New ToolStripMenuItem()
        SplitContainer1 = New SplitContainer()
        lstvCoreTemps = New ListView()
        clmcore = New ColumnHeader()
        clmCoreTemp = New ColumnHeader()
        imgcpu = New ImageList(components)
        grpClawHammer.SuspendLayout()
        CType(NumThreads, ComponentModel.ISupportInitialize).BeginInit()
        StStatus.SuspendLayout()
        clawMenu.SuspendLayout()
        CType(SplitContainer1, ComponentModel.ISupportInitialize).BeginInit()
        SplitContainer1.Panel1.SuspendLayout()
        SplitContainer1.Panel2.SuspendLayout()
        SplitContainer1.SuspendLayout()
        SuspendLayout()
        ' 
        ' grpClawHammer
        ' 
        grpClawHammer.Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right
        grpClawHammer.Controls.Add(Label1)
        grpClawHammer.Controls.Add(lblProfile)
        grpClawHammer.Controls.Add(cmbProfiles)
        grpClawHammer.Controls.Add(cmbStressType)
        grpClawHammer.Controls.Add(chkSaveLog)
        grpClawHammer.Controls.Add(btnStart)
        grpClawHammer.Controls.Add(lblThroughput)
        grpClawHammer.Controls.Add(lblpriority)
        grpClawHammer.Controls.Add(CmbThreadPriority)
        grpClawHammer.Controls.Add(lblNumberOf)
        grpClawHammer.Controls.Add(NumThreads)
        grpClawHammer.FlatStyle = FlatStyle.Flat
        grpClawHammer.Location = New Point(5, 27)
        grpClawHammer.Name = "grpClawHammer"
        grpClawHammer.Size = New Size(1281, 79)
        grpClawHammer.TabIndex = 0
        grpClawHammer.TabStop = False
        ' 
        ' Label1
        ' 
        Label1.AutoSize = True
        Label1.Location = New Point(380, 21)
        Label1.Name = "Label1"
        Label1.Size = New Size(55, 13)
        Label1.TabIndex = 7
        Label1.Text = "Workload"
        ' 
        ' lblProfile
        ' 
        lblProfile.AutoSize = True
        lblProfile.Location = New Point(702, 22)
        lblProfile.Name = "lblProfile"
        lblProfile.Size = New Size(49, 13)
        lblProfile.TabIndex = 9
        lblProfile.Text = "Profile"
        ' 
        ' cmbProfiles
        ' 
        cmbProfiles.DropDownStyle = ComboBoxStyle.DropDownList
        cmbProfiles.FormattingEnabled = True
        cmbProfiles.Location = New Point(516, 19)
        cmbProfiles.Name = "cmbProfiles"
        cmbProfiles.Size = New Size(180, 21)
        cmbProfiles.TabIndex = 10
        ' 
        ' cmbStressType
        ' 
        cmbStressType.FormattingEnabled = True
        cmbStressType.Location = New Point(253, 18)
        cmbStressType.Name = "cmbStressType"
        cmbStressType.Size = New Size(121, 21)
        cmbStressType.TabIndex = 6
        ' 
        ' chkSaveLog
        ' 
        chkSaveLog.Location = New Point(267, 49)
        chkSaveLog.Name = "chkSaveLog"
        chkSaveLog.Size = New Size(140, 17)
        chkSaveLog.TabIndex = 5
        chkSaveLog.Text = "Save Log on Exit"
        chkSaveLog.UseVisualStyleBackColor = True
        ' 
        ' btnStart
        ' 
        btnStart.Anchor = AnchorStyles.Top Or AnchorStyles.Right
        btnStart.Font = New Font("Consolas", 12F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        btnStart.Image = CType(resources.GetObject("btnStart.Image"), Image)
        btnStart.ImageAlign = ContentAlignment.TopCenter
        btnStart.Location = New Point(1042, 16)
        btnStart.Name = "btnStart"
        btnStart.Size = New Size(221, 52)
        btnStart.TabIndex = 4
        btnStart.Text = "Start"
        btnStart.TextAlign = ContentAlignment.BottomCenter
        btnStart.UseVisualStyleBackColor = True
        ' 
        ' lblThroughput
        ' 
        lblThroughput.AutoSize = True
        lblThroughput.Font = New Font("Consolas", 9.75F, FontStyle.Bold, GraphicsUnit.Point, CByte(0))
        lblThroughput.Location = New Point(516, 53)
        lblThroughput.Name = "lblThroughput"
        lblThroughput.Size = New Size(112, 15)
        lblThroughput.TabIndex = 8
        lblThroughput.Text = "Throughput: N/A"
        ' 
        ' lblpriority
        ' 
        lblpriority.AutoSize = True
        lblpriority.Location = New Point(138, 50)
        lblpriority.Name = "lblpriority"
        lblpriority.Size = New Size(97, 13)
        lblpriority.TabIndex = 3
        lblpriority.Text = "Thread Priority"
        ' 
        ' CmbThreadPriority
        ' 
        CmbThreadPriority.DropDownStyle = ComboBoxStyle.DropDownList
        CmbThreadPriority.FormattingEnabled = True
        CmbThreadPriority.Items.AddRange(New Object() {"Highest", "Above Normal", "Below Normal", "Normal", "Lowest"})
        CmbThreadPriority.Location = New Point(7, 46)
        CmbThreadPriority.Name = "CmbThreadPriority"
        CmbThreadPriority.Size = New Size(125, 21)
        CmbThreadPriority.TabIndex = 2
        ' 
        ' lblNumberOf
        ' 
        lblNumberOf.AutoSize = True
        lblNumberOf.Location = New Point(138, 21)
        lblNumberOf.Name = "lblNumberOf"
        lblNumberOf.Size = New Size(109, 13)
        lblNumberOf.TabIndex = 1
        lblNumberOf.Text = "Number of Threads"
        ' 
        ' NumThreads
        ' 
        NumThreads.Location = New Point(6, 19)
        NumThreads.Maximum = New Decimal(New Integer() {128, 0, 0, 0})
        NumThreads.Minimum = New Decimal(New Integer() {1, 0, 0, 0})
        NumThreads.Name = "NumThreads"
        NumThreads.Size = New Size(126, 20)
        NumThreads.TabIndex = 0
        NumThreads.Value = New Decimal(New Integer() {1, 0, 0, 0})
        ' 
        ' rhtxtlog
        ' 
        rhtxtlog.BackColor = Color.WhiteSmoke
        rhtxtlog.BorderStyle = BorderStyle.None
        rhtxtlog.Dock = DockStyle.Fill
        rhtxtlog.Font = New Font("Consolas", 9.75F, FontStyle.Regular, GraphicsUnit.Point, CByte(0))
        rhtxtlog.ForeColor = Color.Black
        rhtxtlog.Location = New Point(0, 0)
        rhtxtlog.Name = "rhtxtlog"
        rhtxtlog.ReadOnly = True
        rhtxtlog.Size = New Size(959, 751)
        rhtxtlog.TabIndex = 1
        rhtxtlog.Text = ""
        ' 
        ' StStatus
        ' 
        StStatus.Items.AddRange(New ToolStripItem() {lblcores, lblProcessorCount, LblActiveThreads, cputemp, lblusage, progCPUUsage})
        StStatus.Location = New Point(0, 870)
        StStatus.Name = "StStatus"
        StStatus.Size = New Size(1292, 24)
        StStatus.TabIndex = 2
        ' 
        ' lblcores
        ' 
        lblcores.BorderSides = ToolStripStatusLabelBorderSides.Left Or ToolStripStatusLabelBorderSides.Top Or ToolStripStatusLabelBorderSides.Right Or ToolStripStatusLabelBorderSides.Bottom
        lblcores.BorderStyle = Border3DStyle.SunkenInner
        lblcores.Name = "lblcores"
        lblcores.Size = New Size(4, 19)
        ' 
        ' lblProcessorCount
        ' 
        lblProcessorCount.BorderSides = ToolStripStatusLabelBorderSides.Left Or ToolStripStatusLabelBorderSides.Top Or ToolStripStatusLabelBorderSides.Right Or ToolStripStatusLabelBorderSides.Bottom
        lblProcessorCount.BorderStyle = Border3DStyle.SunkenOuter
        lblProcessorCount.Name = "lblProcessorCount"
        lblProcessorCount.Size = New Size(4, 19)
        ' 
        ' LblActiveThreads
        ' 
        LblActiveThreads.BorderSides = ToolStripStatusLabelBorderSides.Left Or ToolStripStatusLabelBorderSides.Top Or ToolStripStatusLabelBorderSides.Right Or ToolStripStatusLabelBorderSides.Bottom
        LblActiveThreads.BorderStyle = Border3DStyle.SunkenOuter
        LblActiveThreads.Name = "LblActiveThreads"
        LblActiveThreads.Size = New Size(98, 19)
        LblActiveThreads.Text = "0 Threads Active"
        ' 
        ' cputemp
        ' 
        cputemp.BorderSides = ToolStripStatusLabelBorderSides.Left Or ToolStripStatusLabelBorderSides.Top Or ToolStripStatusLabelBorderSides.Right Or ToolStripStatusLabelBorderSides.Bottom
        cputemp.BorderStyle = Border3DStyle.SunkenOuter
        cputemp.Name = "cputemp"
        cputemp.Size = New Size(4, 19)
        ' 
        ' lblusage
        ' 
        lblusage.AutoSize = False
        lblusage.BorderSides = ToolStripStatusLabelBorderSides.Left Or ToolStripStatusLabelBorderSides.Top Or ToolStripStatusLabelBorderSides.Right Or ToolStripStatusLabelBorderSides.Bottom
        lblusage.BorderStyle = Border3DStyle.SunkenOuter
        lblusage.Name = "lblusage"
        lblusage.Size = New Size(100, 19)
        ' 
        ' progCPUUsage
        ' 
        progCPUUsage.Name = "progCPUUsage"
        progCPUUsage.Size = New Size(220, 18)
        progCPUUsage.Step = 1
        ' 
        ' clawMenu
        ' 
        clawMenu.Items.AddRange(New ToolStripItem() {FileToolStripMenuItem, ToolsToolStripMenuItem, HelpToolStripMenuItem})
        clawMenu.Location = New Point(0, 0)
        clawMenu.Name = "clawMenu"
        clawMenu.RenderMode = ToolStripRenderMode.System
        clawMenu.Size = New Size(1292, 24)
        clawMenu.TabIndex = 3
        clawMenu.Text = "MenuStrip1"
        ' 
        ' FileToolStripMenuItem
        ' 
        FileToolStripMenuItem.DropDownItems.AddRange(New ToolStripItem() {ExitToolStripMenuItem})
        FileToolStripMenuItem.Name = "FileToolStripMenuItem"
        FileToolStripMenuItem.Size = New Size(37, 20)
        FileToolStripMenuItem.Text = "&File"
        ' 
        ' ExitToolStripMenuItem
        ' 
        ExitToolStripMenuItem.Name = "ExitToolStripMenuItem"
        ExitToolStripMenuItem.ShortcutKeys = Keys.Control Or Keys.Q
        ExitToolStripMenuItem.Size = New Size(135, 22)
        ExitToolStripMenuItem.Text = "&Exit"
        ' 
        ' ToolsToolStripMenuItem
        ' 
        ToolsToolStripMenuItem.DropDownItems.AddRange(New ToolStripItem() {RunOptionsToolStripMenuItem, TemperaturePlotToolStripMenuItem, CoreAffinityToolStripMenuItem, SaveProfileToolStripMenuItem, LoadProfileToolStripMenuItem, SystemInfoToolStripMenuItem, ExportReportToolStripMenuItem, CheckLhmUpdatesToolStripMenuItem})
        ToolsToolStripMenuItem.Name = "ToolsToolStripMenuItem"
        ToolsToolStripMenuItem.Size = New Size(47, 20)
        ToolsToolStripMenuItem.Text = "&Tools"
        ' 
        ' RunOptionsToolStripMenuItem
        ' 
        RunOptionsToolStripMenuItem.Name = "RunOptionsToolStripMenuItem"
        RunOptionsToolStripMenuItem.ShortcutKeys = Keys.Control Or Keys.Shift Or Keys.O
        RunOptionsToolStripMenuItem.Size = New Size(257, 22)
        RunOptionsToolStripMenuItem.Text = "Run Options"
        ' 
        ' TemperaturePlotToolStripMenuItem
        ' 
        TemperaturePlotToolStripMenuItem.Name = "TemperaturePlotToolStripMenuItem"
        TemperaturePlotToolStripMenuItem.ShortcutKeys = Keys.Control Or Keys.Shift Or Keys.P
        TemperaturePlotToolStripMenuItem.Size = New Size(257, 22)
        TemperaturePlotToolStripMenuItem.Text = "Temperature Plot"
        ' 
        ' CoreAffinityToolStripMenuItem
        ' 
        CoreAffinityToolStripMenuItem.Name = "CoreAffinityToolStripMenuItem"
        CoreAffinityToolStripMenuItem.ShortcutKeys = Keys.Control Or Keys.Shift Or Keys.A
        CoreAffinityToolStripMenuItem.Size = New Size(257, 22)
        CoreAffinityToolStripMenuItem.Text = "Core Affinity"
        ' 
        ' SaveProfileToolStripMenuItem
        ' 
        SaveProfileToolStripMenuItem.Name = "SaveProfileToolStripMenuItem"
        SaveProfileToolStripMenuItem.ShortcutKeys = Keys.Control Or Keys.Shift Or Keys.S
        SaveProfileToolStripMenuItem.Size = New Size(257, 22)
        SaveProfileToolStripMenuItem.Text = "Save Profile"
        ' 
        ' LoadProfileToolStripMenuItem
        ' 
        LoadProfileToolStripMenuItem.Name = "LoadProfileToolStripMenuItem"
        LoadProfileToolStripMenuItem.ShortcutKeys = Keys.Control Or Keys.Shift Or Keys.L
        LoadProfileToolStripMenuItem.Size = New Size(257, 22)
        LoadProfileToolStripMenuItem.Text = "Load Profile"
        ' 
        ' SystemInfoToolStripMenuItem
        ' 
        SystemInfoToolStripMenuItem.Name = "SystemInfoToolStripMenuItem"
        SystemInfoToolStripMenuItem.ShortcutKeys = Keys.Control Or Keys.Shift Or Keys.I
        SystemInfoToolStripMenuItem.Size = New Size(257, 22)
        SystemInfoToolStripMenuItem.Text = "System Info Snapshot"
        ' 
        ' ExportReportToolStripMenuItem
        ' 
        ExportReportToolStripMenuItem.Name = "ExportReportToolStripMenuItem"
        ExportReportToolStripMenuItem.ShortcutKeys = Keys.Control Or Keys.Shift Or Keys.E
        ExportReportToolStripMenuItem.Size = New Size(257, 22)
        ExportReportToolStripMenuItem.Text = "Export Report"
        ' 
        ' CheckLhmUpdatesToolStripMenuItem
        ' 
        CheckLhmUpdatesToolStripMenuItem.Name = "CheckLhmUpdatesToolStripMenuItem"
        CheckLhmUpdatesToolStripMenuItem.ShortcutKeys = Keys.Control Or Keys.Shift Or Keys.U
        CheckLhmUpdatesToolStripMenuItem.Size = New Size(257, 22)
        CheckLhmUpdatesToolStripMenuItem.Text = "Check LHM Updates"
        ' 
        ' HelpToolStripMenuItem
        ' 
        HelpToolStripMenuItem.DropDownItems.AddRange(New ToolStripItem() {AboutToolStripMenuItem})
        HelpToolStripMenuItem.Name = "HelpToolStripMenuItem"
        HelpToolStripMenuItem.Size = New Size(44, 20)
        HelpToolStripMenuItem.Text = "&Help"
        ' 
        ' AboutToolStripMenuItem
        ' 
        AboutToolStripMenuItem.Name = "AboutToolStripMenuItem"
        AboutToolStripMenuItem.ShortcutKeys = Keys.F1
        AboutToolStripMenuItem.Size = New Size(126, 22)
        AboutToolStripMenuItem.Text = "&About"
        ' 
        ' SplitContainer1
        ' 
        SplitContainer1.Anchor = AnchorStyles.Top Or AnchorStyles.Bottom Or AnchorStyles.Left Or AnchorStyles.Right
        SplitContainer1.BorderStyle = BorderStyle.Fixed3D
        SplitContainer1.Location = New Point(5, 112)
        SplitContainer1.Name = "SplitContainer1"
        ' 
        ' SplitContainer1.Panel1
        ' 
        SplitContainer1.Panel1.Controls.Add(rhtxtlog)
        ' 
        ' SplitContainer1.Panel2
        ' 
        SplitContainer1.Panel2.Controls.Add(lstvCoreTemps)
        SplitContainer1.Size = New Size(1283, 755)
        SplitContainer1.SplitterDistance = 963
        SplitContainer1.TabIndex = 5
        ' 
        ' lstvCoreTemps
        ' 
        lstvCoreTemps.BackColor = Color.WhiteSmoke
        lstvCoreTemps.BorderStyle = BorderStyle.None
        lstvCoreTemps.Columns.AddRange(New ColumnHeader() {clmcore, clmCoreTemp})
        lstvCoreTemps.Dock = DockStyle.Fill
        lstvCoreTemps.GridLines = True
        lstvCoreTemps.Location = New Point(0, 0)
        lstvCoreTemps.Name = "lstvCoreTemps"
        lstvCoreTemps.Size = New Size(312, 751)
        lstvCoreTemps.TabIndex = 4
        lstvCoreTemps.UseCompatibleStateImageBehavior = False
        lstvCoreTemps.View = View.Details
        ' 
        ' clmcore
        ' 
        clmcore.Text = "Sensor"
        clmcore.Width = 200
        ' 
        ' clmCoreTemp
        ' 
        clmCoreTemp.Text = "Temperature"
        clmCoreTemp.Width = 100
        ' 
        ' imgcpu
        ' 
        imgcpu.ColorDepth = ColorDepth.Depth8Bit
        imgcpu.ImageStream = CType(resources.GetObject("imgcpu.ImageStream"), ImageListStreamer)
        imgcpu.TransparentColor = Color.Transparent
        imgcpu.Images.SetKeyName(0, "processor.png")
        ' 
        ' frmMain
        ' 
        AutoScaleDimensions = New SizeF(6F, 13F)
        AutoScaleMode = AutoScaleMode.Font
        ClientSize = New Size(1292, 894)
        Controls.Add(SplitContainer1)
        Controls.Add(StStatus)
        Controls.Add(clawMenu)
        Controls.Add(grpClawHammer)
        DoubleBuffered = True
        Font = New Font("Consolas", 8.25F, FontStyle.Regular, GraphicsUnit.Point, CByte(0))
        Icon = CType(resources.GetObject("$this.Icon"), Icon)
        MainMenuStrip = clawMenu
        MinimumSize = New Size(1024, 768)
        Name = "frmMain"
        SizeGripStyle = SizeGripStyle.Show
        StartPosition = FormStartPosition.CenterScreen
        grpClawHammer.ResumeLayout(False)
        grpClawHammer.PerformLayout()
        CType(NumThreads, ComponentModel.ISupportInitialize).EndInit()
        StStatus.ResumeLayout(False)
        StStatus.PerformLayout()
        clawMenu.ResumeLayout(False)
        clawMenu.PerformLayout()
        SplitContainer1.Panel1.ResumeLayout(False)
        SplitContainer1.Panel2.ResumeLayout(False)
        CType(SplitContainer1, ComponentModel.ISupportInitialize).EndInit()
        SplitContainer1.ResumeLayout(False)
        ResumeLayout(False)
        PerformLayout()

    End Sub
    Friend WithEvents grpClawHammer As System.Windows.Forms.GroupBox
    Friend WithEvents btnStart As System.Windows.Forms.Button
    Friend WithEvents lblpriority As System.Windows.Forms.Label
    Friend WithEvents CmbThreadPriority As System.Windows.Forms.ComboBox
    Friend WithEvents lblNumberOf As System.Windows.Forms.Label
    Friend WithEvents NumThreads As System.Windows.Forms.NumericUpDown
    Friend WithEvents rhtxtlog As System.Windows.Forms.RichTextBox
    Friend WithEvents StStatus As System.Windows.Forms.StatusStrip
    Friend WithEvents LblActiveThreads As System.Windows.Forms.ToolStripStatusLabel
    Friend WithEvents clawMenu As System.Windows.Forms.MenuStrip
    Friend WithEvents FileToolStripMenuItem As System.Windows.Forms.ToolStripMenuItem
    Friend WithEvents ExitToolStripMenuItem As System.Windows.Forms.ToolStripMenuItem
    Friend WithEvents ToolsToolStripMenuItem As ToolStripMenuItem
    Friend WithEvents RunOptionsToolStripMenuItem As ToolStripMenuItem
    Friend WithEvents TemperaturePlotToolStripMenuItem As ToolStripMenuItem
    Friend WithEvents CoreAffinityToolStripMenuItem As ToolStripMenuItem
    Friend WithEvents SaveProfileToolStripMenuItem As ToolStripMenuItem
    Friend WithEvents LoadProfileToolStripMenuItem As ToolStripMenuItem
    Friend WithEvents SystemInfoToolStripMenuItem As ToolStripMenuItem
    Friend WithEvents ExportReportToolStripMenuItem As ToolStripMenuItem
    Friend WithEvents CheckLhmUpdatesToolStripMenuItem As ToolStripMenuItem
    Friend WithEvents HelpToolStripMenuItem As System.Windows.Forms.ToolStripMenuItem
    Friend WithEvents AboutToolStripMenuItem As System.Windows.Forms.ToolStripMenuItem
    Friend WithEvents lblProcessorCount As System.Windows.Forms.ToolStripStatusLabel
    Friend WithEvents lblusage As System.Windows.Forms.ToolStripStatusLabel
    Friend WithEvents progCPUUsage As System.Windows.Forms.ToolStripProgressBar
    Friend WithEvents chkSaveLog As System.Windows.Forms.CheckBox
    Friend WithEvents cputemp As ToolStripStatusLabel
    Friend WithEvents lblcores As ToolStripStatusLabel
    Friend WithEvents SplitContainer1 As SplitContainer
    Friend WithEvents lstvCoreTemps As ListView
    Friend WithEvents clmcore As ColumnHeader
    Friend WithEvents clmCoreTemp As ColumnHeader
    Friend WithEvents imgcpu As ImageList
    Friend WithEvents cmbStressType As ComboBox
    Friend WithEvents Label1 As Label
    Friend WithEvents lblProfile As Label
    Friend WithEvents cmbProfiles As ComboBox
    Friend WithEvents lblThroughput As Label ' Declaration for the new label
End Class
