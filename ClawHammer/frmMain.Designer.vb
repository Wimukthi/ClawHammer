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
        Me.components = New System.ComponentModel.Container()
        Dim resources As System.ComponentModel.ComponentResourceManager = New System.ComponentModel.ComponentResourceManager(GetType(frmMain))
        Me.grpClawHammer = New System.Windows.Forms.GroupBox()
        Me.chkSaveLog = New System.Windows.Forms.CheckBox()
        Me.btnStart = New System.Windows.Forms.Button()
        Me.lblpriority = New System.Windows.Forms.Label()
        Me.CmbThreadPriority = New System.Windows.Forms.ComboBox()
        Me.lblNumberOf = New System.Windows.Forms.Label()
        Me.NumThreads = New System.Windows.Forms.NumericUpDown()
        Me.rhtxtlog = New System.Windows.Forms.RichTextBox()
        Me.StStatus = New System.Windows.Forms.StatusStrip()
        Me.lblcores = New System.Windows.Forms.ToolStripStatusLabel()
        Me.lblProcessorCount = New System.Windows.Forms.ToolStripStatusLabel()
        Me.LblActiveThreads = New System.Windows.Forms.ToolStripStatusLabel()
        Me.cputemp = New System.Windows.Forms.ToolStripStatusLabel()
        Me.lblusage = New System.Windows.Forms.ToolStripStatusLabel()
        Me.progCPUUsage = New System.Windows.Forms.ToolStripProgressBar()
        Me.clawMenu = New System.Windows.Forms.MenuStrip()
        Me.FileToolStripMenuItem = New System.Windows.Forms.ToolStripMenuItem()
        Me.ExitToolStripMenuItem = New System.Windows.Forms.ToolStripMenuItem()
        Me.HelpToolStripMenuItem = New System.Windows.Forms.ToolStripMenuItem()
        Me.AboutToolStripMenuItem = New System.Windows.Forms.ToolStripMenuItem()
        Me.SplitContainer1 = New System.Windows.Forms.SplitContainer()
        Me.lstvCoreTemps = New System.Windows.Forms.ListView()
        Me.clmcore = CType(New System.Windows.Forms.ColumnHeader(), System.Windows.Forms.ColumnHeader)
        Me.clmCoreTemp = CType(New System.Windows.Forms.ColumnHeader(), System.Windows.Forms.ColumnHeader)
        Me.imgcpu = New System.Windows.Forms.ImageList(Me.components)
        Me.grpClawHammer.SuspendLayout()
        CType(Me.NumThreads, System.ComponentModel.ISupportInitialize).BeginInit()
        Me.StStatus.SuspendLayout()
        Me.clawMenu.SuspendLayout()
        CType(Me.SplitContainer1, System.ComponentModel.ISupportInitialize).BeginInit()
        Me.SplitContainer1.Panel1.SuspendLayout()
        Me.SplitContainer1.Panel2.SuspendLayout()
        Me.SplitContainer1.SuspendLayout()
        Me.SuspendLayout()
        '
        'grpClawHammer
        '
        Me.grpClawHammer.Controls.Add(Me.chkSaveLog)
        Me.grpClawHammer.Controls.Add(Me.btnStart)
        Me.grpClawHammer.Controls.Add(Me.lblpriority)
        Me.grpClawHammer.Controls.Add(Me.CmbThreadPriority)
        Me.grpClawHammer.Controls.Add(Me.lblNumberOf)
        Me.grpClawHammer.Controls.Add(Me.NumThreads)
        Me.grpClawHammer.Location = New System.Drawing.Point(5, 27)
        Me.grpClawHammer.Name = "grpClawHammer"
        Me.grpClawHammer.Size = New System.Drawing.Size(769, 79)
        Me.grpClawHammer.TabIndex = 0
        Me.grpClawHammer.TabStop = False
        '
        'chkSaveLog
        '
        Me.chkSaveLog.Checked = Global.ClawHammer.My.MySettings.Default.logtoDisk
        Me.chkSaveLog.DataBindings.Add(New System.Windows.Forms.Binding("Checked", Global.ClawHammer.My.MySettings.Default, "logtoDisk", True, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged))
        Me.chkSaveLog.Location = New System.Drawing.Point(267, 49)
        Me.chkSaveLog.Name = "chkSaveLog"
        Me.chkSaveLog.Size = New System.Drawing.Size(140, 17)
        Me.chkSaveLog.TabIndex = 5
        Me.chkSaveLog.Text = "Save Log on Exit"
        Me.chkSaveLog.UseVisualStyleBackColor = True
        '
        'btnStart
        '
        Me.btnStart.Anchor = CType((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.btnStart.Font = New System.Drawing.Font("Consolas", 12.0!, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.btnStart.Image = Global.ClawHammer.My.Resources.Resources.arrow_right_3
        Me.btnStart.ImageAlign = System.Drawing.ContentAlignment.TopCenter
        Me.btnStart.Location = New System.Drawing.Point(541, 15)
        Me.btnStart.Name = "btnStart"
        Me.btnStart.Size = New System.Drawing.Size(221, 52)
        Me.btnStart.TabIndex = 4
        Me.btnStart.Text = "Start"
        Me.btnStart.TextAlign = System.Drawing.ContentAlignment.BottomCenter
        Me.btnStart.UseVisualStyleBackColor = True
        '
        'lblpriority
        '
        Me.lblpriority.AutoSize = True
        Me.lblpriority.Location = New System.Drawing.Point(138, 50)
        Me.lblpriority.Name = "lblpriority"
        Me.lblpriority.Size = New System.Drawing.Size(97, 13)
        Me.lblpriority.TabIndex = 3
        Me.lblpriority.Text = "Thread Priority"
        '
        'CmbThreadPriority
        '
        Me.CmbThreadPriority.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList
        Me.CmbThreadPriority.FormattingEnabled = True
        Me.CmbThreadPriority.Items.AddRange(New Object() {"Highest", "Above Normal", "Below Normal", "Normal", "Lowest"})
        Me.CmbThreadPriority.Location = New System.Drawing.Point(7, 46)
        Me.CmbThreadPriority.Name = "CmbThreadPriority"
        Me.CmbThreadPriority.Size = New System.Drawing.Size(125, 21)
        Me.CmbThreadPriority.TabIndex = 2
        '
        'lblNumberOf
        '
        Me.lblNumberOf.AutoSize = True
        Me.lblNumberOf.Location = New System.Drawing.Point(138, 21)
        Me.lblNumberOf.Name = "lblNumberOf"
        Me.lblNumberOf.Size = New System.Drawing.Size(109, 13)
        Me.lblNumberOf.TabIndex = 1
        Me.lblNumberOf.Text = "Number of Threads"
        '
        'NumThreads
        '
        Me.NumThreads.Location = New System.Drawing.Point(6, 19)
        Me.NumThreads.Maximum = New Decimal(New Integer() {128, 0, 0, 0})
        Me.NumThreads.Minimum = New Decimal(New Integer() {1, 0, 0, 0})
        Me.NumThreads.Name = "NumThreads"
        Me.NumThreads.Size = New System.Drawing.Size(126, 20)
        Me.NumThreads.TabIndex = 0
        Me.NumThreads.Value = New Decimal(New Integer() {1, 0, 0, 0})
        '
        'rhtxtlog
        '
        Me.rhtxtlog.BackColor = System.Drawing.Color.White
        Me.rhtxtlog.BorderStyle = System.Windows.Forms.BorderStyle.None
        Me.rhtxtlog.Dock = System.Windows.Forms.DockStyle.Fill
        Me.rhtxtlog.Font = New System.Drawing.Font("Consolas", 9.75!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.rhtxtlog.ForeColor = System.Drawing.Color.Black
        Me.rhtxtlog.Location = New System.Drawing.Point(0, 0)
        Me.rhtxtlog.Name = "rhtxtlog"
        Me.rhtxtlog.ReadOnly = True
        Me.rhtxtlog.Size = New System.Drawing.Size(555, 271)
        Me.rhtxtlog.TabIndex = 1
        Me.rhtxtlog.Text = ""
        '
        'StStatus
        '
        Me.StStatus.Items.AddRange(New System.Windows.Forms.ToolStripItem() {Me.lblcores, Me.lblProcessorCount, Me.LblActiveThreads, Me.cputemp, Me.lblusage, Me.progCPUUsage})
        Me.StStatus.Location = New System.Drawing.Point(0, 390)
        Me.StStatus.Name = "StStatus"
        Me.StStatus.Size = New System.Drawing.Size(778, 24)
        Me.StStatus.TabIndex = 2
        '
        'lblcores
        '
        Me.lblcores.BorderSides = CType((((System.Windows.Forms.ToolStripStatusLabelBorderSides.Left Or System.Windows.Forms.ToolStripStatusLabelBorderSides.Top) _
            Or System.Windows.Forms.ToolStripStatusLabelBorderSides.Right) _
            Or System.Windows.Forms.ToolStripStatusLabelBorderSides.Bottom), System.Windows.Forms.ToolStripStatusLabelBorderSides)
        Me.lblcores.BorderStyle = System.Windows.Forms.Border3DStyle.SunkenInner
        Me.lblcores.Name = "lblcores"
        Me.lblcores.Size = New System.Drawing.Size(4, 19)
        '
        'lblProcessorCount
        '
        Me.lblProcessorCount.BorderSides = CType((((System.Windows.Forms.ToolStripStatusLabelBorderSides.Left Or System.Windows.Forms.ToolStripStatusLabelBorderSides.Top) _
            Or System.Windows.Forms.ToolStripStatusLabelBorderSides.Right) _
            Or System.Windows.Forms.ToolStripStatusLabelBorderSides.Bottom), System.Windows.Forms.ToolStripStatusLabelBorderSides)
        Me.lblProcessorCount.BorderStyle = System.Windows.Forms.Border3DStyle.SunkenOuter
        Me.lblProcessorCount.Name = "lblProcessorCount"
        Me.lblProcessorCount.Size = New System.Drawing.Size(4, 19)
        '
        'LblActiveThreads
        '
        Me.LblActiveThreads.BorderSides = CType((((System.Windows.Forms.ToolStripStatusLabelBorderSides.Left Or System.Windows.Forms.ToolStripStatusLabelBorderSides.Top) _
            Or System.Windows.Forms.ToolStripStatusLabelBorderSides.Right) _
            Or System.Windows.Forms.ToolStripStatusLabelBorderSides.Bottom), System.Windows.Forms.ToolStripStatusLabelBorderSides)
        Me.LblActiveThreads.BorderStyle = System.Windows.Forms.Border3DStyle.SunkenOuter
        Me.LblActiveThreads.Name = "LblActiveThreads"
        Me.LblActiveThreads.Size = New System.Drawing.Size(97, 19)
        Me.LblActiveThreads.Text = "0 Threads Active"
        '
        'cputemp
        '
        Me.cputemp.BorderSides = CType((((System.Windows.Forms.ToolStripStatusLabelBorderSides.Left Or System.Windows.Forms.ToolStripStatusLabelBorderSides.Top) _
            Or System.Windows.Forms.ToolStripStatusLabelBorderSides.Right) _
            Or System.Windows.Forms.ToolStripStatusLabelBorderSides.Bottom), System.Windows.Forms.ToolStripStatusLabelBorderSides)
        Me.cputemp.BorderStyle = System.Windows.Forms.Border3DStyle.SunkenOuter
        Me.cputemp.Name = "cputemp"
        Me.cputemp.Size = New System.Drawing.Size(4, 19)
        '
        'lblusage
        '
        Me.lblusage.AutoSize = False
        Me.lblusage.BorderSides = CType((((System.Windows.Forms.ToolStripStatusLabelBorderSides.Left Or System.Windows.Forms.ToolStripStatusLabelBorderSides.Top) _
            Or System.Windows.Forms.ToolStripStatusLabelBorderSides.Right) _
            Or System.Windows.Forms.ToolStripStatusLabelBorderSides.Bottom), System.Windows.Forms.ToolStripStatusLabelBorderSides)
        Me.lblusage.BorderStyle = System.Windows.Forms.Border3DStyle.SunkenOuter
        Me.lblusage.Name = "lblusage"
        Me.lblusage.Size = New System.Drawing.Size(100, 19)
        '
        'progCPUUsage
        '
        Me.progCPUUsage.Name = "progCPUUsage"
        Me.progCPUUsage.Size = New System.Drawing.Size(220, 18)
        Me.progCPUUsage.Step = 1
        '
        'clawMenu
        '
        Me.clawMenu.Items.AddRange(New System.Windows.Forms.ToolStripItem() {Me.FileToolStripMenuItem, Me.HelpToolStripMenuItem})
        Me.clawMenu.Location = New System.Drawing.Point(0, 0)
        Me.clawMenu.Name = "clawMenu"
        Me.clawMenu.RenderMode = System.Windows.Forms.ToolStripRenderMode.System
        Me.clawMenu.Size = New System.Drawing.Size(778, 24)
        Me.clawMenu.TabIndex = 3
        Me.clawMenu.Text = "MenuStrip1"
        '
        'FileToolStripMenuItem
        '
        Me.FileToolStripMenuItem.DropDownItems.AddRange(New System.Windows.Forms.ToolStripItem() {Me.ExitToolStripMenuItem})
        Me.FileToolStripMenuItem.Name = "FileToolStripMenuItem"
        Me.FileToolStripMenuItem.Size = New System.Drawing.Size(37, 20)
        Me.FileToolStripMenuItem.Text = "&File"
        '
        'ExitToolStripMenuItem
        '
        Me.ExitToolStripMenuItem.Name = "ExitToolStripMenuItem"
        Me.ExitToolStripMenuItem.Size = New System.Drawing.Size(93, 22)
        Me.ExitToolStripMenuItem.Text = "&Exit"
        '
        'HelpToolStripMenuItem
        '
        Me.HelpToolStripMenuItem.DropDownItems.AddRange(New System.Windows.Forms.ToolStripItem() {Me.AboutToolStripMenuItem})
        Me.HelpToolStripMenuItem.Name = "HelpToolStripMenuItem"
        Me.HelpToolStripMenuItem.Size = New System.Drawing.Size(44, 20)
        Me.HelpToolStripMenuItem.Text = "&Help"
        '
        'AboutToolStripMenuItem
        '
        Me.AboutToolStripMenuItem.Name = "AboutToolStripMenuItem"
        Me.AboutToolStripMenuItem.Size = New System.Drawing.Size(107, 22)
        Me.AboutToolStripMenuItem.Text = "&About"
        '
        'SplitContainer1
        '
        Me.SplitContainer1.Anchor = CType((((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Bottom) _
            Or System.Windows.Forms.AnchorStyles.Left) _
            Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.SplitContainer1.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D
        Me.SplitContainer1.Location = New System.Drawing.Point(5, 112)
        Me.SplitContainer1.Name = "SplitContainer1"
        '
        'SplitContainer1.Panel1
        '
        Me.SplitContainer1.Panel1.Controls.Add(Me.rhtxtlog)
        '
        'SplitContainer1.Panel2
        '
        Me.SplitContainer1.Panel2.Controls.Add(Me.lstvCoreTemps)
        Me.SplitContainer1.Size = New System.Drawing.Size(769, 275)
        Me.SplitContainer1.SplitterDistance = 559
        Me.SplitContainer1.TabIndex = 5
        '
        'lstvCoreTemps
        '
        Me.lstvCoreTemps.BorderStyle = System.Windows.Forms.BorderStyle.None
        Me.lstvCoreTemps.Columns.AddRange(New System.Windows.Forms.ColumnHeader() {Me.clmcore, Me.clmCoreTemp})
        Me.lstvCoreTemps.Dock = System.Windows.Forms.DockStyle.Fill
        Me.lstvCoreTemps.GridLines = True
        Me.lstvCoreTemps.HideSelection = False
        Me.lstvCoreTemps.Location = New System.Drawing.Point(0, 0)
        Me.lstvCoreTemps.Name = "lstvCoreTemps"
        Me.lstvCoreTemps.Size = New System.Drawing.Size(202, 271)
        Me.lstvCoreTemps.TabIndex = 4
        Me.lstvCoreTemps.UseCompatibleStateImageBehavior = False
        Me.lstvCoreTemps.View = System.Windows.Forms.View.Details
        '
        'clmcore
        '
        Me.clmcore.Text = "Core"
        Me.clmcore.Width = 90
        '
        'clmCoreTemp
        '
        Me.clmCoreTemp.Text = "Core Temperature"
        Me.clmCoreTemp.Width = 150
        '
        'imgcpu
        '
        Me.imgcpu.ImageStream = CType(resources.GetObject("imgcpu.ImageStream"), System.Windows.Forms.ImageListStreamer)
        Me.imgcpu.TransparentColor = System.Drawing.Color.Transparent
        Me.imgcpu.Images.SetKeyName(0, "processor.png")
        '
        'frmMain
        '
        Me.AutoScaleDimensions = New System.Drawing.SizeF(6.0!, 13.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.ClientSize = New System.Drawing.Size(778, 414)
        Me.Controls.Add(Me.SplitContainer1)
        Me.Controls.Add(Me.StStatus)
        Me.Controls.Add(Me.clawMenu)
        Me.Controls.Add(Me.grpClawHammer)
        Me.DoubleBuffered = True
        Me.Font = New System.Drawing.Font("Consolas", 8.25!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.Icon = CType(resources.GetObject("$this.Icon"), System.Drawing.Icon)
        Me.MainMenuStrip = Me.clawMenu
        Me.MinimumSize = New System.Drawing.Size(794, 453)
        Me.Name = "frmMain"
        Me.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen
        Me.grpClawHammer.ResumeLayout(False)
        Me.grpClawHammer.PerformLayout()
        CType(Me.NumThreads, System.ComponentModel.ISupportInitialize).EndInit()
        Me.StStatus.ResumeLayout(False)
        Me.StStatus.PerformLayout()
        Me.clawMenu.ResumeLayout(False)
        Me.clawMenu.PerformLayout()
        Me.SplitContainer1.Panel1.ResumeLayout(False)
        Me.SplitContainer1.Panel2.ResumeLayout(False)
        CType(Me.SplitContainer1, System.ComponentModel.ISupportInitialize).EndInit()
        Me.SplitContainer1.ResumeLayout(False)
        Me.ResumeLayout(False)
        Me.PerformLayout()

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
End Class
