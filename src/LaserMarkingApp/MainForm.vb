Imports System
Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.Drawing
Imports System.IO
Imports System.Text
Imports System.Text.RegularExpressions
Imports System.Windows.Forms

Public Class MainForm
    Inherits Form

    Private ReadOnly _database As DatabaseService
    Private _settings As AppSettingsRecord
    Private _activePart As PartRecord
    Private _currentUser As UserRecord = New UserRecord With {.Username = "operator", .Role = UserRole.OperatorUser}
    Private _lastActivity As DateTime = DateTime.Now
    Private _exitAuthorized As Boolean = False

    Private ReadOnly _activityTimer As Timer
    Private ReadOnly _contentPanel As Panel
    Private ReadOnly _partLabel As Label
    Private ReadOnly _vendorLabel As Label
    Private ReadOnly _qrPreviewLabel As Label
    Private ReadOnly _serialBox As TextBox
    Private ReadOnly _markButton As Button
    Private ReadOnly _statusLabel As Label
    Private ReadOnly _loggedInLabel As Label
    Private ReadOnly _accessButton As Button
    Private ReadOnly _setterPanel As Panel
    Private ReadOnly _partsCombo As ComboBox
    Private ReadOnly _partNumberBox As TextBox
    Private ReadOnly _vendorBox As TextBox
    Private ReadOnly _plantBox As TextBox
    Private ReadOnly _customerBox As TextBox
    Private ReadOnly _prefixBox As TextBox
    Private ReadOnly _qrFormatBox As TextBox
    Private ReadOnly _templateBox As TextBox
    Private ReadOnly _outputPathBox As TextBox
    Private ReadOnly _templateDirectoryBox As TextBox
    Private ReadOnly _serialRegexBox As TextBox
    Private ReadOnly _externalCommandBox As TextBox
    Private ReadOnly _setterStatusLabel As Label
    Private ReadOnly _setterPreviewLabel As Label
    Private ReadOnly _newPartButton As Button
    Private ReadOnly _deletePartButton As Button
    Private ReadOnly _browseButton As Button
    Private ReadOnly _saveButton As Button
    Private ReadOnly _setActiveButton As Button
    Private ReadOnly _usersButton As Button
    Private ReadOnly _logsButton As Button
    Private ReadOnly _baseBounds As New Dictionary(Of Control, Rectangle)()
    Private ReadOnly _baseFontSizes As New Dictionary(Of Control, Single)()

    Private Const DesignWidth As Integer = 980
    Private Const DesignHeight As Integer = 640

    Public Sub New(database As DatabaseService)
        _database = database
        _settings = _database.LoadSettings()
        _activePart = _database.GetActivePart()

        Text = "Laser Marking QR App"
        StartPosition = FormStartPosition.CenterScreen
        FormBorderStyle = FormBorderStyle.Sizable
        WindowState = FormWindowState.Normal
        TopMost = False
        MinimumSize = New Size(980, 640)
        ClientSize = New Size(DesignWidth, DesignHeight)
        Font = New Font("Segoe UI", 10.0F, FontStyle.Regular, GraphicsUnit.Point)

        _contentPanel = New Panel With {.Location = New Point(0, 0), .Size = New Size(DesignWidth, DesignHeight)}
        Dim operatorPanel = New Panel With {.Location = New Point(24, 24), .Size = New Size(420, 560), .BorderStyle = BorderStyle.FixedSingle}
        Dim header = New Label With {.Text = "OPERATOR", .Font = New Font(Font, FontStyle.Bold), .Location = New Point(20, 18), .AutoSize = True}
        Dim currentPartText = New Label With {.Text = "Current Part:", .Location = New Point(20, 68), .AutoSize = True}
        _partLabel = New Label With {.Location = New Point(150, 68), .Size = New Size(230, 24), .Font = New Font(Font, FontStyle.Bold)}
        Dim vendorText = New Label With {.Text = "Item Code:", .Location = New Point(20, 104), .AutoSize = True}
        _vendorLabel = New Label With {.Location = New Point(150, 104), .Size = New Size(230, 24), .Font = New Font(Font, FontStyle.Bold)}
        Dim previewText = New Label With {.Text = "Engraving Preview:", .Location = New Point(20, 140), .AutoSize = True}
        _qrPreviewLabel = New Label With {.Location = New Point(20, 168), .Size = New Size(360, 62), .BorderStyle = BorderStyle.FixedSingle}
        Dim serialLabel = New Label With {.Text = "Heat / Lot Number", .Location = New Point(20, 246), .AutoSize = True}
        _serialBox = New TextBox With {.Location = New Point(20, 276), .Width = 260, .Font = New Font("Segoe UI", 18.0F, FontStyle.Regular, GraphicsUnit.Point), .CharacterCasing = CharacterCasing.Upper}
        _markButton = New Button With {.Text = "MARK", .Location = New Point(292, 274), .Size = New Size(88, 44)}
        _statusLabel = New Label With {.Location = New Point(20, 350), .Size = New Size(360, 80), .ForeColor = Color.DarkGreen}
        _loggedInLabel = New Label With {.Location = New Point(20, 508), .Size = New Size(220, 24)}
        _accessButton = New Button With {.Text = "Setter Login", .Location = New Point(252, 502), .Size = New Size(128, 34)}

        AddHandler _markButton.Click, AddressOf MarkButton_Click
        AddHandler _accessButton.Click, AddressOf AccessButton_Click
        AddHandler _serialBox.KeyDown, AddressOf SerialBox_KeyDown
        AddHandler _serialBox.TextChanged, AddressOf OperatorInput_TextChanged

        operatorPanel.Controls.AddRange({
            header, currentPartText, _partLabel, vendorText, _vendorLabel, previewText, _qrPreviewLabel,
            serialLabel, _serialBox, _markButton, _statusLabel, _loggedInLabel, _accessButton
        })

        _setterPanel = New Panel With {
            .Location = New Point(468, 24),
            .Size = New Size(488, 560),
            .BorderStyle = BorderStyle.FixedSingle,
            .Enabled = False,
            .AutoScroll = True,
            .AutoScrollMinSize = New Size(0, 650)
        }
        Dim setterHeader = New Label With {.Text = "SETTER SETTINGS", .Font = New Font(Font, FontStyle.Bold), .Location = New Point(20, 18), .AutoSize = True}
        Dim logoutButton = New Button With {.Text = "Logout", .Location = New Point(382, 14), .Size = New Size(82, 32)}
        Dim partSelectLabel = New Label With {.Text = "Part", .Location = New Point(20, 62), .AutoSize = True}
        _partsCombo = New ComboBox With {.Location = New Point(120, 58), .Width = 220, .DropDownStyle = ComboBoxStyle.DropDownList}
        _newPartButton = New Button With {.Text = "New", .Location = New Point(350, 56), .Size = New Size(54, 30)}
        Dim loadPartButton = New Button With {.Text = "Load", .Location = New Point(410, 56), .Size = New Size(54, 30)}
        _setterPreviewLabel = New Label With {.Location = New Point(20, 92), .Size = New Size(444, 30), .BorderStyle = BorderStyle.FixedSingle, .Font = New Font("Segoe UI", 7.0F, FontStyle.Regular, GraphicsUnit.Point)}

        _partNumberBox = AddLabeledTextBox(_setterPanel, "Part Number", 130)
        _vendorBox = AddLabeledTextBox(_setterPanel, "Item Code", 168)
        _plantBox = AddLabeledTextBox(_setterPanel, "Material", 206)
        _customerBox = AddLabeledTextBox(_setterPanel, "Revision", 244)
        _prefixBox = AddLabeledTextBox(_setterPanel, "Product", 282)
        _qrFormatBox = AddLabeledTextBox(_setterPanel, "Supplier", 320)
        _templateBox = AddLabeledTextBox(_setterPanel, "Template", 358)
        _outputPathBox = AddLabeledTextBox(_setterPanel, "Engraving File", 396)
        _templateDirectoryBox = AddLabeledTextBox(_setterPanel, "Active Template Folder", 434)
        _serialRegexBox = New TextBox With {.Visible = False}
        _externalCommandBox = AddLabeledTextBox(_setterPanel, "Command", 472)
        _setterStatusLabel = New Label With {.Location = New Point(20, 584), .Size = New Size(444, 42), .ForeColor = Color.DarkGreen}

        _browseButton = New Button With {.Text = "...", .Location = New Point(430, 356), .Size = New Size(34, 28)}
        _usersButton = New Button With {.Text = "Users", .Location = New Point(20, 544), .Size = New Size(68, 30)}
        _deletePartButton = New Button With {.Text = "Delete", .Location = New Point(96, 544), .Size = New Size(68, 30)}
        _logsButton = New Button With {.Text = "Logs", .Location = New Point(172, 544), .Size = New Size(68, 30)}
        _saveButton = New Button With {.Text = "Save", .Location = New Point(282, 544), .Size = New Size(58, 30)}
        _setActiveButton = New Button With {.Text = "Set Active", .Location = New Point(348, 544), .Size = New Size(84, 30)}

        AddHandler logoutButton.Click, Sub() LogoutToOperator()
        AddHandler _newPartButton.Click, AddressOf NewPartButton_Click
        AddHandler loadPartButton.Click, AddressOf LoadPartButton_Click
        AddHandler _browseButton.Click, AddressOf BrowseButton_Click
        AddHandler _usersButton.Click, AddressOf UsersButton_Click
        AddHandler _logsButton.Click, AddressOf LogsButton_Click
        AddHandler _deletePartButton.Click, AddressOf DeletePartButton_Click
        AddHandler _saveButton.Click, AddressOf SaveButton_Click
        AddHandler _setActiveButton.Click, AddressOf SetActiveButton_Click
        AddHandler _qrFormatBox.TextChanged, AddressOf SetterField_TextChanged
        AddHandler _vendorBox.TextChanged, AddressOf SetterField_TextChanged
        AddHandler _plantBox.TextChanged, AddressOf SetterField_TextChanged
        AddHandler _customerBox.TextChanged, AddressOf SetterField_TextChanged
        AddHandler _prefixBox.TextChanged, AddressOf SetterField_TextChanged
        AddHandler _partNumberBox.TextChanged, AddressOf SetterField_TextChanged

        _setterPanel.Controls.AddRange({
            setterHeader, logoutButton, partSelectLabel, _partsCombo, _newPartButton, loadPartButton,
            _setterPreviewLabel, _browseButton, _usersButton, _deletePartButton, _logsButton, _saveButton, _setActiveButton, _setterStatusLabel
        })

        _contentPanel.Controls.AddRange({operatorPanel, _setterPanel})
        Controls.Add(_contentPanel)

        AddHandler MouseMove, AddressOf AnyActivity
        AddHandler KeyDown, AddressOf AnyKeyActivity
        AddHandler FormClosing, AddressOf MainForm_FormClosing
        AddHandler Resize, AddressOf MainForm_Resize
        WireActivityHandlers(Me)
        CaptureBaseLayout(_contentPanel)
        ApplyFullscreenLayout()

        _activityTimer = New Timer With {.Interval = 1000}
        AddHandler _activityTimer.Tick, AddressOf ActivityTimer_Tick
        _activityTimer.Start()

        RefreshAll()
        _serialBox.Focus()
    End Sub

    Private Sub CaptureBaseLayout(parent As Control)
        For Each child As Control In parent.Controls
            _baseBounds(child) = child.Bounds
            _baseFontSizes(child) = child.Font.Size
            If child.HasChildren Then
                CaptureBaseLayout(child)
            End If
        Next
    End Sub

    Private Sub MainForm_Resize(sender As Object, e As EventArgs)
        ApplyFullscreenLayout()
    End Sub

    Private Sub ApplyFullscreenLayout()
        If _contentPanel Is Nothing OrElse _baseBounds.Count = 0 OrElse ClientSize.Width <= 0 OrElse ClientSize.Height <= 0 Then
            Return
        End If

        Dim scale = Math.Min(ClientSize.Width / CSng(DesignWidth), ClientSize.Height / CSng(DesignHeight))
        scale = Math.Max(1.0F, scale)

        Dim scaledWidth = CInt(Math.Round(DesignWidth * scale))
        Dim scaledHeight = CInt(Math.Round(DesignHeight * scale))
        _contentPanel.Bounds = New Rectangle(
            Math.Max(0, (ClientSize.Width - scaledWidth) \ 2),
            Math.Max(0, (ClientSize.Height - scaledHeight) \ 2),
            scaledWidth,
            scaledHeight)

        ApplyScaledLayout(_contentPanel, scale)
    End Sub

    Private Sub ApplyScaledLayout(parent As Control, scale As Single)
        For Each child As Control In parent.Controls
            Dim originalBounds = _baseBounds(child)
            child.Bounds = New Rectangle(
                CInt(Math.Round(originalBounds.X * scale)),
                CInt(Math.Round(originalBounds.Y * scale)),
                CInt(Math.Round(originalBounds.Width * scale)),
                CInt(Math.Round(originalBounds.Height * scale)))

            Dim originalFontSize = _baseFontSizes(child)
            child.Font = New Font(child.Font.FontFamily, originalFontSize * scale, child.Font.Style, child.Font.Unit)

            If child.HasChildren Then
                ApplyScaledLayout(child, scale)
            End If
        Next
    End Sub

    Private Function AddLabeledTextBox(parent As Control, labelText As String, y As Integer) As TextBox
        Dim label = New Label With {.Text = labelText, .Location = New Point(20, y + 4), .Size = New Size(112, 24)}
        Dim box = New TextBox With {.Location = New Point(136, y), .Width = 288}
        parent.Controls.Add(label)
        parent.Controls.Add(box)
        Return box
    End Function

    Private Sub RefreshAll(Optional editorPartId As Integer = 0)
        _settings = _database.LoadSettings()
        _activePart = _database.GetActivePart()
        RefreshOperatorView()
        RefreshSetterParts(editorPartId)
        LoadSettingsIntoSetterFields()
    End Sub

    Private Sub RefreshOperatorView()
        If _activePart Is Nothing Then
            _partLabel.Text = "No active part"
            _vendorLabel.Text = "-"
            _qrPreviewLabel.Text = ""
            _serialBox.Enabled = False
            _markButton.Enabled = False
        Else
            _partLabel.Text = _activePart.PartNumber
            _vendorLabel.Text = _activePart.CustomerItemCode
            UpdateOperatorPreview()
            _serialBox.Enabled = True
            _markButton.Enabled = _currentUser.Role = UserRole.OperatorUser
        End If

        _loggedInLabel.Text = $"Logged in as {_currentUser.Username}"
        _accessButton.Text = If(_currentUser.Role = UserRole.OperatorUser, "Setter Login", "Logout")
    End Sub

    Private Function CurrentHeatLotPreview() As String
        Dim heatLot = _serialBox.Text.Trim().ToUpperInvariant()
        If String.IsNullOrWhiteSpace(heatLot) Then
            Return "26-4B-21"
        End If

        Return heatLot
    End Function

    Private Sub UpdateOperatorPreview()
        If _activePart Is Nothing Then
            _qrPreviewLabel.Text = ""
            Return
        End If

        Try
            _qrPreviewLabel.Text = EngravingFormatter.Build(_activePart, _database.PeekNextSerialNumber(), CurrentHeatLotPreview(), DateTime.Now)
        Catch ex As Exception
            _qrPreviewLabel.Text = ""
        End Try
    End Sub

    Private Sub RefreshSetterParts(Optional selectedPartId As Integer = 0)
        _partsCombo.Items.Clear()
        Dim fallbackPart As PartRecord = Nothing
        For Each part In _database.GetParts()
            _partsCombo.Items.Add(part)
            If selectedPartId > 0 AndAlso part.Id = selectedPartId Then
                _partsCombo.SelectedItem = part
            ElseIf part.IsActive Then
                fallbackPart = part
            End If
        Next

        If _partsCombo.SelectedItem Is Nothing AndAlso fallbackPart IsNot Nothing Then
            _partsCombo.SelectedItem = fallbackPart
        End If
    End Sub

    Private Sub LoadSettingsIntoSetterFields()
        _outputPathBox.Text = _settings.QrOutputPath
        _templateDirectoryBox.Text = _settings.ActiveTemplateDirectory
        _serialRegexBox.Text = _settings.SerialRegex
        _externalCommandBox.Text = _settings.ExternalCommand
        Dim selected = TryCast(_partsCombo.SelectedItem, PartRecord)
        If selected IsNot Nothing Then
            LoadPartIntoFields(selected)
        ElseIf _activePart IsNot Nothing Then
            LoadPartIntoFields(_activePart)
        Else
            ' Clear part-related fields when no part is available
            _partNumberBox.Tag = Nothing
            _partNumberBox.Text = ""
            _vendorBox.Text = ""
            _plantBox.Text = ""
            _customerBox.Text = ""
            _prefixBox.Text = ""
            _qrFormatBox.Text = ""
            _templateBox.Text = ""
            UpdateSetterPreview()
        End If
    End Sub

    Private Sub LoadPartIntoFields(part As PartRecord)
        _partNumberBox.Tag = part.Id
        _partNumberBox.Text = part.PartNumber
        _vendorBox.Text = part.CustomerItemCode
        _plantBox.Text = part.Material
        _customerBox.Text = part.Pattern
        _prefixBox.Text = part.ProductName
        _qrFormatBox.Text = part.SupplierName
        _templateBox.Text = part.TemplateFile
        UpdateSetterPreview()
    End Sub

    Private Function ReadPartFromFields() As PartRecord
        Dim partId = 0
        If TypeOf _partNumberBox.Tag Is Integer Then
            partId = CInt(_partNumberBox.Tag)
        End If

        Return New PartRecord With {
            .Id = partId,
            .PartNumber = _partNumberBox.Text.Trim(),
            .CustomerItemCode = _vendorBox.Text.Trim(),
            .VendorCode = _vendorBox.Text.Trim(),
            .CustomerCode = _vendorBox.Text.Trim(),
            .Material = _plantBox.Text.Trim(),
            .Pattern = _customerBox.Text.Trim(),
            .ProductName = _prefixBox.Text.Trim(),
            .SupplierName = _qrFormatBox.Text.Trim(),
            .QrFormat = "{CustomerItemCode}${PartNumber}${DatePrefixSerial}${MarkDate}${MonthLabel}${Material}${HeatLot}${Pattern}${ProductName}${SupplierName}$",
            .TemplateFile = _templateBox.Text.Trim()
        }
    End Function

    Private Function ReadSettingsFromFields() As AppSettingsRecord
        Dim timeout = _settings.AutoLogoutMinutes
        Return New AppSettingsRecord With {
            .QrOutputPath = _outputPathBox.Text.Trim(),
            .ActiveTemplateDirectory = _templateDirectoryBox.Text.Trim(),
            .AutoLogoutMinutes = timeout,
            .SerialRegex = _serialRegexBox.Text.Trim(),
            .ExternalCommand = _externalCommandBox.Text.Trim()
        }
    End Function

    Private Sub MarkButton_Click(sender As Object, e As EventArgs)
        If _currentUser.Role <> UserRole.OperatorUser Then
            ShowOperatorError("Logout before marking.")
            Return
        End If

        If _activePart Is Nothing Then
            ShowOperatorError("No active part is configured.")
            Return
        End If

        Dim heatLotNumber = _serialBox.Text.Trim().ToUpperInvariant()
        If String.IsNullOrWhiteSpace(heatLotNumber) Then
            ShowOperatorError("Heat / lot number is required.")
            _serialBox.SelectAll()
            _serialBox.Focus()
            Return
        End If

        Dim generatedSerial = _database.PeekNextSerialNumber()
        Dim qrData = EngravingFormatter.Build(_activePart, generatedSerial, heatLotNumber, DateTime.Now)

        Try
            WriteQrData(_settings.QrOutputPath, qrData)
            Dim result = "Prepared"
            If Not String.IsNullOrWhiteSpace(_settings.ExternalCommand) Then
                result = RunExternalCommand(_settings.ExternalCommand)
            End If

            _database.InsertMarkLog(_activePart.PartNumber, generatedSerial, heatLotNumber, qrData, _currentUser.Username, result)
            _statusLabel.ForeColor = Color.DarkGreen
            _statusLabel.Text = $"Ready for EZCAD: {qrData}"
            _serialBox.Clear()
            _serialBox.Focus()
        Catch ex As Exception
            ShowOperatorError(ex.Message)
        End Try
    End Sub

    Private Sub SerialBox_KeyDown(sender As Object, e As KeyEventArgs)
        If e.KeyCode = Keys.Enter Then
            MarkButton_Click(sender, EventArgs.Empty)
            e.SuppressKeyPress = True
        End If
    End Sub

    Private Sub OperatorInput_TextChanged(sender As Object, e As EventArgs)
        UpdateOperatorPreview()
    End Sub

    Private Sub AccessButton_Click(sender As Object, e As EventArgs)
        If _currentUser.Role <> UserRole.OperatorUser Then
            LogoutToOperator()
            Return
        End If

        Using login = New LoginForm(_database)
            If login.ShowDialog(Me) <> DialogResult.OK Then
                Return
            End If

            _currentUser = login.AuthenticatedUser
            _setterPanel.Enabled = True
            _lastActivity = DateTime.Now
            ApplySetterPermissions()
            RefreshOperatorView()
            _setterStatusLabel.Text = $"Setter access: {_currentUser.Username}"
        End Using
    End Sub

    Private Sub ApplySetterPermissions()
        Dim isAdmin = _currentUser.Role = UserRole.Admin
        For Each box In GetAdminOnlyTextBoxes()
            box.ReadOnly = Not isAdmin
        Next

        _newPartButton.Enabled = isAdmin
        _deletePartButton.Enabled = isAdmin
        _browseButton.Enabled = isAdmin
        _saveButton.Enabled = isAdmin
        _usersButton.Enabled = isAdmin
        _logsButton.Enabled = isAdmin
    End Sub

    Private Function GetAdminOnlyTextBoxes() As IEnumerable(Of TextBox)
        Return {
            _partNumberBox,
            _vendorBox,
            _plantBox,
            _customerBox,
            _prefixBox,
            _qrFormatBox,
            _templateBox,
            _outputPathBox,
            _templateDirectoryBox,
            _externalCommandBox
        }
    End Function

    Private Function RequireAdminAction() As Boolean
        If _currentUser.Role = UserRole.Admin Then
            Return True
        End If

        _setterStatusLabel.ForeColor = Color.DarkRed
        _setterStatusLabel.Text = "Admin role required."
        Return False
    End Function

    Private Sub NewPartButton_Click(sender As Object, e As EventArgs)
        If Not RequireAdminAction() Then
            Return
        End If

        _partNumberBox.Tag = 0
        _partNumberBox.Text = ""
        _vendorBox.Text = "7201097"
        _plantBox.Text = "FG260"
        _customerBox.Text = "#"
        _prefixBox.Text = "FLYWHEEL"
        _qrFormatBox.Text = "SREERAMENGG"
        _templateBox.Text = ""
        UpdateSetterPreview()
        _partNumberBox.Focus()
    End Sub

    Private Sub LoadPartButton_Click(sender As Object, e As EventArgs)
        Dim selected = TryCast(_partsCombo.SelectedItem, PartRecord)
        If selected IsNot Nothing Then
            LoadPartIntoFields(selected)
            _setterStatusLabel.Text = $"Loaded {selected.PartNumber}."
        End If
    End Sub

    Private Sub BrowseButton_Click(sender As Object, e As EventArgs)
        If Not RequireAdminAction() Then
            Return
        End If

        Using dialog = New OpenFileDialog()
            dialog.Title = "Select EZCAD template"
            dialog.Filter = "EZCAD templates (*.ezd)|*.ezd|All files (*.*)|*.*"
            If Directory.Exists("D:\QUALITY-3") Then
                dialog.InitialDirectory = "D:\QUALITY-3"
            End If
            If dialog.ShowDialog(Me) = DialogResult.OK Then
                _templateBox.Text = dialog.FileName
            End If
        End Using
    End Sub

    Private Sub UsersButton_Click(sender As Object, e As EventArgs)
        If Not RequireAdminAction() Then
            Return
        End If

        Using form = New UserManagementForm(_database)
            form.ShowDialog(Me)
        End Using
    End Sub

    Private Sub LogsButton_Click(sender As Object, e As EventArgs)
        If Not RequireAdminAction() Then
            Return
        End If

        Using form = New ProductionLogsForm(_database)
            form.ShowDialog(Me)
        End Using
    End Sub

    Private Sub SaveButton_Click(sender As Object, e As EventArgs)
        If Not RequireAdminAction() Then
            Return
        End If

        Try
            Dim part = ReadPartFromFields()
            ValidatePart(part)
            Dim savedId = _database.SavePart(part)
            _settings = ReadSettingsFromFields()
            _database.SaveSettings(_settings)
            part.Id = savedId
            RefreshAll(savedId)
            ApplySetterPermissions()
            _setterStatusLabel.ForeColor = Color.DarkGreen
            _setterStatusLabel.Text = "Saved."
        Catch ex As Exception
            _setterStatusLabel.ForeColor = Color.DarkRed
            _setterStatusLabel.Text = ex.Message
        End Try
    End Sub

    Private Sub DeletePartButton_Click(sender As Object, e As EventArgs)
        If Not RequireAdminAction() Then
            Return
        End If

        Dim part = ReadPartFromFields()
        If part.Id <= 0 Then
            _setterStatusLabel.ForeColor = Color.DarkRed
            _setterStatusLabel.Text = "Load a saved part before deleting."
            Return
        End If

        If MessageBox.Show(Me, $"Delete part {part.PartNumber}?", "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) <> DialogResult.Yes Then
            Return
        End If

        Try
            _database.DeletePart(part.Id)
            RefreshAll()
            ApplySetterPermissions()
            _setterStatusLabel.ForeColor = Color.DarkGreen
            _setterStatusLabel.Text = $"Deleted {part.PartNumber}."
        Catch ex As Exception
            _setterStatusLabel.ForeColor = Color.DarkRed
            _setterStatusLabel.Text = ex.Message
        End Try
    End Sub

    Private Sub SetActiveButton_Click(sender As Object, e As EventArgs)
        Try
            Dim part = If(_currentUser.Role = UserRole.Admin, ReadPartFromFields(), TryCast(_partsCombo.SelectedItem, PartRecord))
            If part Is Nothing OrElse part.Id <= 0 Then
                Throw New InvalidOperationException("Select a saved part before setting active.")
            End If

            Dim savedId = part.Id
            If _currentUser.Role = UserRole.Admin Then
                ValidatePart(part)
                savedId = _database.SavePart(part)
            End If

            ' Set active part first, then promote template (atomic ordering)
            _database.SetActivePart(savedId, _currentUser)
            CopyTemplateToActiveFolder(part.TemplateFile, _templateDirectoryBox.Text.Trim())
            If _currentUser.Role = UserRole.Admin Then
                _settings = ReadSettingsFromFields()
                _database.SaveSettings(_settings)
            End If

            RefreshAll()
            ApplySetterPermissions()
            _setterStatusLabel.ForeColor = Color.DarkGreen
            _setterStatusLabel.Text = $"Active part: {part.PartNumber}"
            _serialBox.Focus()
        Catch ex As Exception
            _setterStatusLabel.ForeColor = Color.DarkRed
            _setterStatusLabel.Text = ex.Message
        End Try
    End Sub

    Private Sub SetterField_TextChanged(sender As Object, e As EventArgs)
        UpdateSetterPreview()
    End Sub

    Private Sub UpdateSetterPreview()
        Try
            _setterPreviewLabel.Text = EngravingFormatter.Build(ReadPartFromFields(), _database.PeekNextSerialNumber(), "26-4B-21", DateTime.Now)
        Catch ex As Exception
            _setterPreviewLabel.Text = ""
        End Try
    End Sub

    Private Sub ActivityTimer_Tick(sender As Object, e As EventArgs)
        If _currentUser.Role = UserRole.Setter OrElse _currentUser.Role = UserRole.Admin Then
            Dim timeout = TimeSpan.FromMinutes(_settings.AutoLogoutMinutes)
            If DateTime.Now.Subtract(_lastActivity) > timeout Then
                LogoutToOperator()
                _statusLabel.ForeColor = Color.DarkGreen
                _statusLabel.Text = "Setter access timed out."
            End If
        End If
    End Sub

    Private Sub LogoutToOperator()
        _currentUser = New UserRecord With {.Username = "operator", .Role = UserRole.OperatorUser}
        _setterPanel.Enabled = False
        ApplySetterPermissions()
        RefreshOperatorView()
        _serialBox.Focus()
    End Sub

    Private Sub AnyActivity(sender As Object, e As MouseEventArgs)
        _lastActivity = DateTime.Now
    End Sub

    Private Sub AnyKeyActivity(sender As Object, e As KeyEventArgs)
        _lastActivity = DateTime.Now
    End Sub

    Private Sub WireActivityHandlers(parent As Control)
        For Each child As Control In parent.Controls
            AddHandler child.MouseMove, AddressOf AnyActivity
            AddHandler child.KeyDown, AddressOf AnyKeyActivity
            If child.HasChildren Then
                WireActivityHandlers(child)
            End If
        Next
    End Sub

    Private Sub MainForm_FormClosing(sender As Object, e As FormClosingEventArgs)
        If _exitAuthorized Then
            Return
        End If

        Dim defaultUsername = If(_currentUser.Role = UserRole.Setter OrElse _currentUser.Role = UserRole.Admin, _currentUser.Username, "setter")
        Using login = New LoginForm(_database, "Confirm Exit", defaultUsername, "Exit")
            If login.ShowDialog(Me) = DialogResult.OK Then
                _exitAuthorized = True
                Return
            End If
        End Using

        e.Cancel = True
        _statusLabel.ForeColor = Color.DarkRed
        _statusLabel.Text = "Exit cancelled. Setter or admin password is required."
        _serialBox.Focus()
    End Sub

    Private Sub ShowOperatorError(message As String)
        _statusLabel.ForeColor = Color.DarkRed
        _statusLabel.Text = message
    End Sub

    Private Shared Sub ValidatePart(part As PartRecord)
        If String.IsNullOrWhiteSpace(part.PartNumber) Then
            Throw New InvalidOperationException("Part number is required.")
        End If

        If String.IsNullOrWhiteSpace(part.CustomerItemCode) Then
            Throw New InvalidOperationException("Item code is required.")
        End If

        If String.IsNullOrWhiteSpace(part.Material) Then
            Throw New InvalidOperationException("Material is required.")
        End If

        If String.IsNullOrWhiteSpace(part.Pattern) Then
            Throw New InvalidOperationException("Revision is required.")
        End If

        If String.IsNullOrWhiteSpace(part.ProductName) Then
            Throw New InvalidOperationException("Product is required.")
        End If

        If String.IsNullOrWhiteSpace(part.SupplierName) Then
            Throw New InvalidOperationException("Supplier is required.")
        End If
    End Sub

    Private Shared Sub WriteQrData(outputPath As String, qrData As String)
        If String.IsNullOrWhiteSpace(outputPath) Then
            Throw New InvalidOperationException("QR output path is not configured.")
        End If

        Dim outputDirectory = System.IO.Path.GetDirectoryName(outputPath)
        If String.IsNullOrWhiteSpace(outputDirectory) Then
            Throw New InvalidOperationException("QR output path must include a folder.")
        End If

        System.IO.Directory.CreateDirectory(outputDirectory)
        Dim tempPath = System.IO.Path.Combine(outputDirectory, $"{System.IO.Path.GetFileName(outputPath)}.{Guid.NewGuid():N}.tmp")
        File.WriteAllText(tempPath, qrData, Encoding.UTF8)

        If File.Exists(outputPath) Then
            File.Replace(tempPath, outputPath, Nothing)
        Else
            File.Move(tempPath, outputPath)
        End If
    End Sub

    Private Shared Sub CopyTemplateToActiveFolder(templateFile As String, activeFolder As String)
        If String.IsNullOrWhiteSpace(templateFile) Then
            Return
        End If

        If Not File.Exists(templateFile) Then
            Throw New FileNotFoundException("Template file was not found.", templateFile)
        End If

        If String.IsNullOrWhiteSpace(activeFolder) Then
            Throw New InvalidOperationException("Active template folder is not configured.")
        End If

        System.IO.Directory.CreateDirectory(activeFolder)
        Dim destination = System.IO.Path.Combine(activeFolder, System.IO.Path.GetFileName(templateFile))
        File.Copy(templateFile, destination, True)
    End Sub

    Private Shared Function RunExternalCommand(commandLine As String) As String
        Dim startInfo = New ProcessStartInfo With {
            .FileName = "cmd.exe",
            .Arguments = $"/c {commandLine}",
            .UseShellExecute = False,
            .CreateNoWindow = True
        }

        Using runningProcess = System.Diagnostics.Process.Start(startInfo)
            If runningProcess Is Nothing Then
                Throw New InvalidOperationException("External command could not be started.")
            End If

            If Not runningProcess.WaitForExit(30000) Then
                Try
                    runningProcess.Kill()
                Catch ex As InvalidOperationException
                End Try
                Throw New TimeoutException("External command timed out after 30 seconds.")
            End If

            Return $"CommandExit{runningProcess.ExitCode}"
        End Using
    End Function
End Class
