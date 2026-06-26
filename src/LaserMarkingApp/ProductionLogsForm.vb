Imports System
Imports System.Collections.Generic
Imports System.Globalization
Imports System.IO
Imports System.Text
Imports System.Windows.Forms

Public Class ProductionLogsForm
    Inherits Form

    Private Const LogLimit As Integer = 500

    Private ReadOnly _database As DatabaseService
    Private ReadOnly _tabs As TabControl
    Private ReadOnly _partSelectionGrid As DataGridView
    Private ReadOnly _markLogGrid As DataGridView
    Private ReadOnly _exportButton As Button
    Private ReadOnly _closeButton As Button
    Private ReadOnly _partSelectionLogs As List(Of PartSelectionLogRecord)
    Private ReadOnly _markLogs As List(Of MarkLogRecord)

    Public Sub New(database As DatabaseService)
        _database = database
        _partSelectionLogs = _database.GetPartSelectionLogs(LogLimit)
        _markLogs = _database.GetMarkLogs(LogLimit)

        Text = "Production Logs"
        StartPosition = FormStartPosition.CenterParent
        FormBorderStyle = FormBorderStyle.FixedDialog
        MaximizeBox = False
        MinimizeBox = False
        ShowInTaskbar = False
        Size = New Drawing.Size(980, 560)

        _tabs = New TabControl With {
            .Dock = DockStyle.Top,
            .Height = 470
        }

        _partSelectionGrid = CreateGrid()
        _markLogGrid = CreateGrid()

        Dim partSelectionTab = New TabPage("Part Selection Logs")
        partSelectionTab.Controls.Add(_partSelectionGrid)

        Dim markLogTab = New TabPage("Mark Logs")
        markLogTab.Controls.Add(_markLogGrid)

        _tabs.TabPages.Add(partSelectionTab)
        _tabs.TabPages.Add(markLogTab)

        _exportButton = New Button With {
            .Text = "Export CSV",
            .Location = New Drawing.Point(756, 482),
            .Size = New Drawing.Size(96, 32)
        }
        _closeButton = New Button With {
            .Text = "Close",
            .Location = New Drawing.Point(864, 482),
            .Size = New Drawing.Size(80, 32)
        }

        AddHandler _exportButton.Click, AddressOf ExportButton_Click
        AddHandler _closeButton.Click, Sub() Close()

        Controls.Add(_tabs)
        Controls.Add(_exportButton)
        Controls.Add(_closeButton)

        LoadPartSelectionGrid()
        LoadMarkLogGrid()
    End Sub

    Private Shared Function CreateGrid() As DataGridView
        Return New DataGridView With {
            .Dock = DockStyle.Fill,
            .AllowUserToAddRows = False,
            .AllowUserToDeleteRows = False,
            .AllowUserToResizeRows = False,
            .AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            .BackgroundColor = Drawing.SystemColors.Window,
            .BorderStyle = BorderStyle.None,
            .ReadOnly = True,
            .RowHeadersVisible = False,
            .SelectionMode = DataGridViewSelectionMode.FullRowSelect
        }
    End Function

    Private Sub LoadPartSelectionGrid()
        _partSelectionGrid.Columns.Clear()
        _partSelectionGrid.Columns.Add("Timestamp", "Timestamp")
        _partSelectionGrid.Columns.Add("PartNumber", "Part Number")
        _partSelectionGrid.Columns.Add("SelectedBy", "Selected By")
        _partSelectionGrid.Columns.Add("Role", "Role")

        _partSelectionGrid.Rows.Clear()
        For Each log In _partSelectionLogs
            _partSelectionGrid.Rows.Add(FormatTimestamp(log.TimestampUtc), log.PartNumber, log.SelectedBy, log.SelectedRole.ToString())
        Next
    End Sub

    Private Sub LoadMarkLogGrid()
        _markLogGrid.Columns.Clear()
        _markLogGrid.Columns.Add("Timestamp", "Timestamp")
        _markLogGrid.Columns.Add("PartNumber", "Part Number")
        _markLogGrid.Columns.Add("GeneratedSerial", "Generated Serial")
        _markLogGrid.Columns.Add("HeatLot", "Heat/Lot")
        _markLogGrid.Columns.Add("EngravingData", "Engraving Data")
        _markLogGrid.Columns.Add("User", "User")
        _markLogGrid.Columns.Add("Result", "Result")

        _markLogGrid.Rows.Clear()
        For Each log In _markLogs
            _markLogGrid.Rows.Add(
                FormatTimestamp(log.TimestampUtc),
                log.PartNumber,
                log.GeneratedSerial.ToString(CultureInfo.InvariantCulture),
                log.HeatLotNumber,
                log.EngravingData,
                log.Username,
                log.Result)
        Next
    End Sub

    Private Sub ExportButton_Click(sender As Object, e As EventArgs)
        Using dialog = New SaveFileDialog()
            dialog.Title = "Export CSV"
            dialog.Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*"
            dialog.FileName = If(_tabs.SelectedIndex = 0, "part-selection-logs.csv", "mark-logs.csv")
            If dialog.ShowDialog(Me) <> DialogResult.OK Then
                Return
            End If

            Dim csv = If(_tabs.SelectedIndex = 0, BuildPartSelectionCsv(), BuildMarkLogCsv())
            File.WriteAllText(dialog.FileName, csv, New UTF8Encoding(True))
        End Using
    End Sub

    Private Function BuildPartSelectionCsv() As String
        Dim builder = New StringBuilder()
        AppendCsvLine(builder, {"Timestamp", "Part Number", "Selected By", "Role"})

        For Each log In _partSelectionLogs
            AppendCsvLine(builder, {
                FormatTimestamp(log.TimestampUtc),
                log.PartNumber,
                log.SelectedBy,
                log.SelectedRole.ToString()
            })
        Next

        Return builder.ToString()
    End Function

    Private Function BuildMarkLogCsv() As String
        Dim builder = New StringBuilder()
        AppendCsvLine(builder, {"Timestamp", "Part Number", "Generated Serial", "Heat/Lot", "Engraving Data", "User", "Result"})

        For Each log In _markLogs
            AppendCsvLine(builder, {
                FormatTimestamp(log.TimestampUtc),
                log.PartNumber,
                log.GeneratedSerial.ToString(CultureInfo.InvariantCulture),
                log.HeatLotNumber,
                log.EngravingData,
                log.Username,
                log.Result
            })
        Next

        Return builder.ToString()
    End Function

    Private Shared Sub AppendCsvLine(builder As StringBuilder, values As IEnumerable(Of String))
        Dim first = True
        For Each value In values
            If Not first Then
                builder.Append(","c)
            End If

            builder.Append(QuoteCsv(value))
            first = False
        Next

        builder.AppendLine()
    End Sub

    Private Shared Function QuoteCsv(value As String) As String
        Dim safeValue = If(value, "")
        Return $"""{safeValue.Replace("""", """""")}"""
    End Function

    Private Shared Function FormatTimestamp(timestampUtc As String) As String
        Dim parsed As DateTimeOffset
        If DateTimeOffset.TryParse(timestampUtc, parsed) Then
            Return parsed.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture)
        End If

        Return timestampUtc
    End Function
End Class
