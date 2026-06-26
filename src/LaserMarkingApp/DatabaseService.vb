Imports System
Imports System.Collections.Generic
Imports System.Data
Imports System.IO
Imports Microsoft.Data.Sqlite

Public Class DatabaseService
    Private ReadOnly _connectionString As String

    Public Sub New(databasePath As String)
        If String.IsNullOrWhiteSpace(databasePath) Then
            Throw New ArgumentException("Database path is required.", NameOf(databasePath))
        End If

        Directory.CreateDirectory(Path.GetDirectoryName(databasePath))
        Dim builder = New SqliteConnectionStringBuilder With {
            .DataSource = databasePath
        }
        _connectionString = builder.ToString()
    End Sub

    Public Sub Initialize()
        SQLitePCL.Batteries_V2.Init()

        Using connection = OpenConnection()
            ExecuteNonQuery(connection, "PRAGMA journal_mode=WAL;")
            ExecuteNonQuery(connection, "
CREATE TABLE IF NOT EXISTS Parts (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    PartNumber TEXT NOT NULL UNIQUE,
    VendorCode TEXT NOT NULL,
    PlantCode TEXT NOT NULL DEFAULT '',
    CustomerCode TEXT NOT NULL DEFAULT '',
    QRPrefix TEXT NOT NULL DEFAULT '',
    QRFormat TEXT NOT NULL DEFAULT '{VendorCode}|{PartNumber}|{Serial}',
    CustomerItemCode TEXT NOT NULL DEFAULT '7201097',
    Material TEXT NOT NULL DEFAULT 'FG260',
    Pattern TEXT NOT NULL DEFAULT '#',
    ProductName TEXT NOT NULL DEFAULT 'FLYWHEEL',
    SupplierName TEXT NOT NULL DEFAULT 'SREERAMENGG',
    TemplateFile TEXT NOT NULL DEFAULT '',
    IsActive INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS Users (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Username TEXT NOT NULL UNIQUE COLLATE NOCASE,
    PasswordHash TEXT NOT NULL,
    Role INTEGER NOT NULL
);

CREATE TABLE IF NOT EXISTS Settings (
    Key TEXT PRIMARY KEY,
    Value TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS MarkLog (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    PartNumber TEXT NOT NULL,
    SerialNumber TEXT NOT NULL,
    QRData TEXT NOT NULL,
    TimestampUtc TEXT NOT NULL,
    Username TEXT NOT NULL,
    Result TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS PartSelectionLog (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    PartId INTEGER NOT NULL,
    PartNumber TEXT NOT NULL,
    SelectedBy TEXT NOT NULL,
    SelectedRole INTEGER NOT NULL,
    TimestampUtc TEXT NOT NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS UX_MarkLog_Part_Serial
ON MarkLog (PartNumber, SerialNumber);
")
            EnsureColumn(connection, "Parts", "CustomerItemCode", "TEXT NOT NULL DEFAULT '7201097'")
            EnsureColumn(connection, "Parts", "Material", "TEXT NOT NULL DEFAULT 'FG260'")
            EnsureColumn(connection, "Parts", "Pattern", "TEXT NOT NULL DEFAULT '#'")
            EnsureColumn(connection, "Parts", "ProductName", "TEXT NOT NULL DEFAULT 'FLYWHEEL'")
            EnsureColumn(connection, "Parts", "SupplierName", "TEXT NOT NULL DEFAULT 'SREERAMENGG'")
            EnsureColumn(connection, "MarkLog", "HeatLotNumber", "TEXT NOT NULL DEFAULT ''")
            EnsureColumn(connection, "MarkLog", "GeneratedSerial", "INTEGER NOT NULL DEFAULT 0")
            SeedDefaults(connection)
        End Using
    End Sub

    Public Function LoadSettings() As AppSettingsRecord
        Dim settings = New AppSettingsRecord()

        Using connection = OpenConnection()
            settings.QrOutputPath = GetSetting(connection, "QrOutputPath", settings.QrOutputPath)
            settings.ActiveTemplateDirectory = GetSetting(connection, "ActiveTemplateDirectory", settings.ActiveTemplateDirectory)
            settings.SerialRegex = GetSetting(connection, "SerialRegex", settings.SerialRegex)
            settings.ExternalCommand = GetSetting(connection, "ExternalCommand", settings.ExternalCommand)

            Dim timeoutText = GetSetting(connection, "AutoLogoutMinutes", settings.AutoLogoutMinutes.ToString())
            Dim timeout As Integer
            If Integer.TryParse(timeoutText, timeout) AndAlso timeout > 0 Then
                settings.AutoLogoutMinutes = timeout
            End If
        End Using

        Return settings
    End Function

    Public Sub SaveSettings(settings As AppSettingsRecord)
        Using connection = OpenConnection()
            SaveSetting(connection, "QrOutputPath", settings.QrOutputPath)
            SaveSetting(connection, "ActiveTemplateDirectory", settings.ActiveTemplateDirectory)
            SaveSetting(connection, "AutoLogoutMinutes", settings.AutoLogoutMinutes.ToString())
            SaveSetting(connection, "SerialRegex", settings.SerialRegex)
            SaveSetting(connection, "ExternalCommand", settings.ExternalCommand)
        End Using
    End Sub

    Public Function GetParts() As List(Of PartRecord)
        Dim parts = New List(Of PartRecord)()

        Using connection = OpenConnection()
            Using command = connection.CreateCommand()
                command.CommandText = BuildPartSelectSql() & "ORDER BY PartNumber;"
                Using reader = command.ExecuteReader()
                    While reader.Read()
                        parts.Add(ReadPart(reader))
                    End While
                End Using
            End Using
        End Using

        Return parts
    End Function

    Public Function GetActivePart() As PartRecord
        Using connection = OpenConnection()
            Using command = connection.CreateCommand()
                command.CommandText = BuildPartSelectSql() & "
WHERE IsActive = 1
ORDER BY Id
LIMIT 1;"
                Using reader = command.ExecuteReader()
                    If reader.Read() Then
                        Return ReadPart(reader)
                    End If
                End Using
            End Using
        End Using

        Return Nothing
    End Function

    Public Function SavePart(part As PartRecord) As Integer
        If part Is Nothing Then
            Throw New ArgumentNullException(NameOf(part))
        End If

        Using connection = OpenConnection()
            If part.Id > 0 Then
                Using command = connection.CreateCommand()
                    command.CommandText = "
UPDATE Parts
SET PartNumber = $partNumber,
    VendorCode = $vendorCode,
    PlantCode = $plantCode,
    CustomerCode = $customerCode,
    QRPrefix = $qrPrefix,
    QRFormat = $qrFormat,
    CustomerItemCode = $customerItemCode,
    Material = $material,
    Pattern = $pattern,
    ProductName = $productName,
    SupplierName = $supplierName,
    TemplateFile = $templateFile
WHERE Id = $id;"
                    AddPartParameters(command, part)
                    command.Parameters.AddWithValue("$id", part.Id)
                    command.ExecuteNonQuery()
                    Return part.Id
                End Using
            End If

            Using command = connection.CreateCommand()
                command.CommandText = "
INSERT INTO Parts (PartNumber, VendorCode, PlantCode, CustomerCode, QRPrefix, QRFormat, CustomerItemCode, Material, Pattern, ProductName, SupplierName, TemplateFile, IsActive)
VALUES ($partNumber, $vendorCode, $plantCode, $customerCode, $qrPrefix, $qrFormat, $customerItemCode, $material, $pattern, $productName, $supplierName, $templateFile, 0);
SELECT last_insert_rowid();"
                AddPartParameters(command, part)
                Return Convert.ToInt32(command.ExecuteScalar())
            End Using
        End Using
    End Function

    Public Sub SetActivePart(partId As Integer, selectedBy As UserRecord)
        If selectedBy Is Nothing Then
            Throw New ArgumentNullException(NameOf(selectedBy))
        End If

        Using connection = OpenConnection()
            Using transaction = connection.BeginTransaction()
                Dim partNumber As String = Nothing

                Using partCommand = connection.CreateCommand()
                    partCommand.Transaction = transaction
                    partCommand.CommandText = "SELECT PartNumber FROM Parts WHERE Id = $id LIMIT 1;"
                    partCommand.Parameters.AddWithValue("$id", partId)
                    Dim value = partCommand.ExecuteScalar()
                    If value Is Nothing OrElse value Is DBNull.Value Then
                        Throw New InvalidOperationException("Part was not found.")
                    End If

                    partNumber = Convert.ToString(value)
                End Using

                Using clearCommand = connection.CreateCommand()
                    clearCommand.Transaction = transaction
                    clearCommand.CommandText = "UPDATE Parts SET IsActive = 0;"
                    clearCommand.ExecuteNonQuery()
                End Using

                Using setCommand = connection.CreateCommand()
                    setCommand.Transaction = transaction
                    setCommand.CommandText = "UPDATE Parts SET IsActive = 1 WHERE Id = $id;"
                    setCommand.Parameters.AddWithValue("$id", partId)
                    setCommand.ExecuteNonQuery()
                End Using

                Using logCommand = connection.CreateCommand()
                    logCommand.Transaction = transaction
                    logCommand.CommandText = "
INSERT INTO PartSelectionLog (PartId, PartNumber, SelectedBy, SelectedRole, TimestampUtc)
VALUES ($partId, $partNumber, $selectedBy, $selectedRole, $timestampUtc);"
                    logCommand.Parameters.AddWithValue("$partId", partId)
                    logCommand.Parameters.AddWithValue("$partNumber", partNumber)
                    logCommand.Parameters.AddWithValue("$selectedBy", selectedBy.Username)
                    logCommand.Parameters.AddWithValue("$selectedRole", CInt(selectedBy.Role))
                    logCommand.Parameters.AddWithValue("$timestampUtc", DateTime.UtcNow.ToString("O"))
                    logCommand.ExecuteNonQuery()
                End Using

                transaction.Commit()
            End Using
        End Using
    End Sub

    Public Sub DeletePart(partId As Integer)
        Using connection = OpenConnection()
            Using transaction = connection.BeginTransaction()
                Dim wasActive = False
                Using activeCommand = connection.CreateCommand()
                    activeCommand.Transaction = transaction
                    activeCommand.CommandText = "SELECT IsActive FROM Parts WHERE Id = $id LIMIT 1;"
                    activeCommand.Parameters.AddWithValue("$id", partId)
                    Dim activeValue = activeCommand.ExecuteScalar()
                    If activeValue Is Nothing Then
                        Throw New InvalidOperationException("Part was not found.")
                    End If

                    wasActive = Convert.ToInt32(activeValue) = 1
                End Using

                Using deleteCommand = connection.CreateCommand()
                    deleteCommand.Transaction = transaction
                    deleteCommand.CommandText = "DELETE FROM Parts WHERE Id = $id;"
                    deleteCommand.Parameters.AddWithValue("$id", partId)
                    deleteCommand.ExecuteNonQuery()
                End Using

                If wasActive Then
                    Using setActiveCommand = connection.CreateCommand()
                        setActiveCommand.Transaction = transaction
                        setActiveCommand.CommandText = "
UPDATE Parts
SET IsActive = 1
WHERE Id = (
    SELECT Id
    FROM Parts
    ORDER BY PartNumber
    LIMIT 1
);"
                        setActiveCommand.ExecuteNonQuery()
                    End Using
                End If

                transaction.Commit()
            End Using
        End Using
    End Sub

    Public Function FindUser(username As String) As UserRecord
        Using connection = OpenConnection()
            Using command = connection.CreateCommand()
                command.CommandText = "SELECT Id, Username, PasswordHash, Role FROM Users WHERE Username = $username LIMIT 1;"
                command.Parameters.AddWithValue("$username", username)

                Using reader = command.ExecuteReader()
                    If reader.Read() Then
                        Return New UserRecord With {
                            .Id = reader.GetInt32(0),
                            .Username = reader.GetString(1),
                            .PasswordHash = reader.GetString(2),
                            .Role = CType(reader.GetInt32(3), UserRole)
                        }
                    End If
                End Using
            End Using
        End Using

        Return Nothing
    End Function

    Public Function GetUsers() As List(Of UserRecord)
        Dim users = New List(Of UserRecord)()

        Using connection = OpenConnection()
            Using command = connection.CreateCommand()
                command.CommandText = "SELECT Id, Username, PasswordHash, Role FROM Users ORDER BY Username;"
                Using reader = command.ExecuteReader()
                    While reader.Read()
                        users.Add(New UserRecord With {
                            .Id = reader.GetInt32(0),
                            .Username = reader.GetString(1),
                            .PasswordHash = reader.GetString(2),
                            .Role = CType(reader.GetInt32(3), UserRole)
                        })
                    End While
                End Using
            End Using
        End Using

        Return users
    End Function

    Public Sub SaveUser(username As String, password As String, role As UserRole)
        If String.IsNullOrWhiteSpace(username) Then
            Throw New ArgumentException("Username is required.", NameOf(username))
        End If

        If String.IsNullOrWhiteSpace(password) OrElse password.Length < 6 Then
            Throw New ArgumentException("Password must be at least 6 characters.", NameOf(password))
        End If

        Using connection = OpenConnection()
            Using command = connection.CreateCommand()
                command.CommandText = "
INSERT INTO Users (Username, PasswordHash, Role)
VALUES ($username, $passwordHash, $role)
ON CONFLICT(Username) DO UPDATE SET
    PasswordHash = excluded.PasswordHash,
    Role = excluded.Role;"
                command.Parameters.AddWithValue("$username", username.Trim())
                command.Parameters.AddWithValue("$passwordHash", PasswordHasher.HashPassword(password))
                command.Parameters.AddWithValue("$role", CInt(role))
                command.ExecuteNonQuery()
            End Using
        End Using
    End Sub

    Public Function SerialExists(partNumber As String, serialNumber As String) As Boolean
        Using connection = OpenConnection()
            Using command = connection.CreateCommand()
                command.CommandText = "
SELECT COUNT(1)
FROM MarkLog
WHERE PartNumber = $partNumber
  AND SerialNumber = $serialNumber;"
                command.Parameters.AddWithValue("$partNumber", partNumber)
                command.Parameters.AddWithValue("$serialNumber", serialNumber)
                Return Convert.ToInt32(command.ExecuteScalar()) > 0
            End Using
        End Using
    End Function

    Public Function PeekNextSerialNumber() As Integer
        Using connection = OpenConnection()
            Return Math.Max(1, GetIntegerSetting(connection, "NextSerialNumber", 1))
        End Using
    End Function

    Public Sub InsertMarkLog(partNumber As String, generatedSerial As Integer, heatLotNumber As String, qrData As String, username As String, result As String)
        Using connection = OpenConnection()
            Using transaction = connection.BeginTransaction()
                Dim currentSerial = GetIntegerSetting(connection, "NextSerialNumber", 1)
                If generatedSerial < currentSerial Then
                    Throw New InvalidOperationException($"Serial number {generatedSerial} is lower than the current counter value {currentSerial}.")
                End If

                Using command = connection.CreateCommand()
                    command.Transaction = transaction
                    command.CommandText = "
INSERT INTO MarkLog (PartNumber, SerialNumber, QRData, TimestampUtc, Username, Result, HeatLotNumber, GeneratedSerial)
VALUES ($partNumber, $serialNumber, $qrData, $timestampUtc, $username, $result, $heatLotNumber, $generatedSerial);"
                    command.Parameters.AddWithValue("$partNumber", partNumber)
                    command.Parameters.AddWithValue("$serialNumber", generatedSerial.ToString())
                    command.Parameters.AddWithValue("$qrData", qrData)
                    command.Parameters.AddWithValue("$timestampUtc", DateTime.UtcNow.ToString("O"))
                    command.Parameters.AddWithValue("$username", username)
                    command.Parameters.AddWithValue("$result", result)
                    command.Parameters.AddWithValue("$heatLotNumber", heatLotNumber)
                    command.Parameters.AddWithValue("$generatedSerial", generatedSerial)
                    command.ExecuteNonQuery()
                End Using

                Using settingCommand = connection.CreateCommand()
                    settingCommand.Transaction = transaction
                    settingCommand.CommandText = "
INSERT INTO Settings (Key, Value)
VALUES ('NextSerialNumber', $value)
ON CONFLICT(Key) DO UPDATE SET Value = excluded.Value;"
                    settingCommand.Parameters.AddWithValue("$value", (generatedSerial + 1).ToString())
                    settingCommand.ExecuteNonQuery()
                End Using

                transaction.Commit()
            End Using
        End Using
    End Sub

    Public Sub InsertMarkLog(partNumber As String, serialNumber As String, qrData As String, username As String, result As String)
        Using connection = OpenConnection()
            Using command = connection.CreateCommand()
                command.CommandText = "
INSERT INTO MarkLog (PartNumber, SerialNumber, QRData, TimestampUtc, Username, Result)
VALUES ($partNumber, $serialNumber, $qrData, $timestampUtc, $username, $result);"
                command.Parameters.AddWithValue("$partNumber", partNumber)
                command.Parameters.AddWithValue("$serialNumber", serialNumber)
                command.Parameters.AddWithValue("$qrData", qrData)
                command.Parameters.AddWithValue("$timestampUtc", DateTime.UtcNow.ToString("O"))
                command.Parameters.AddWithValue("$username", username)
                command.Parameters.AddWithValue("$result", result)
                command.ExecuteNonQuery()
            End Using
        End Using
    End Sub

    Public Function GetPartSelectionLogs(limit As Integer) As List(Of PartSelectionLogRecord)
        Dim logs = New List(Of PartSelectionLogRecord)()
        Dim safeLimit = Math.Max(1, limit)

        Using connection = OpenConnection()
            Using command = connection.CreateCommand()
                command.CommandText = "
SELECT Id, PartId, PartNumber, SelectedBy, SelectedRole, TimestampUtc
FROM PartSelectionLog
ORDER BY Id DESC
LIMIT $limit;"
                command.Parameters.AddWithValue("$limit", safeLimit)

                Using reader = command.ExecuteReader()
                    While reader.Read()
                        logs.Add(New PartSelectionLogRecord With {
                            .Id = reader.GetInt32(0),
                            .PartId = reader.GetInt32(1),
                            .PartNumber = reader.GetString(2),
                            .SelectedBy = reader.GetString(3),
                            .SelectedRole = CType(reader.GetInt32(4), UserRole),
                            .TimestampUtc = reader.GetString(5)
                        })
                    End While
                End Using
            End Using
        End Using

        Return logs
    End Function

    Public Function GetMarkLogs(limit As Integer) As List(Of MarkLogRecord)
        Dim logs = New List(Of MarkLogRecord)()
        Dim safeLimit = Math.Max(1, limit)

        Using connection = OpenConnection()
            Using command = connection.CreateCommand()
                command.CommandText = "
SELECT Id, PartNumber, GeneratedSerial, HeatLotNumber, QRData, TimestampUtc, Username, Result
FROM MarkLog
ORDER BY Id DESC
LIMIT $limit;"
                command.Parameters.AddWithValue("$limit", safeLimit)

                Using reader = command.ExecuteReader()
                    While reader.Read()
                        logs.Add(New MarkLogRecord With {
                            .Id = reader.GetInt32(0),
                            .PartNumber = reader.GetString(1),
                            .GeneratedSerial = reader.GetInt32(2),
                            .HeatLotNumber = reader.GetString(3),
                            .EngravingData = reader.GetString(4),
                            .TimestampUtc = reader.GetString(5),
                            .Username = reader.GetString(6),
                            .Result = reader.GetString(7)
                        })
                    End While
                End Using
            End Using
        End Using

        Return logs
    End Function

    Private Function OpenConnection() As SqliteConnection
        Dim connection = New SqliteConnection(_connectionString)
        connection.Open()
        Return connection
    End Function

    Private Shared Sub AddPartParameters(command As SqliteCommand, part As PartRecord)
        command.Parameters.AddWithValue("$partNumber", part.PartNumber.Trim())
        command.Parameters.AddWithValue("$vendorCode", If(String.IsNullOrWhiteSpace(part.VendorCode), part.CustomerItemCode, part.VendorCode).Trim())
        command.Parameters.AddWithValue("$plantCode", part.PlantCode.Trim())
        command.Parameters.AddWithValue("$customerCode", If(String.IsNullOrWhiteSpace(part.CustomerCode), part.CustomerItemCode, part.CustomerCode).Trim())
        command.Parameters.AddWithValue("$qrPrefix", part.QrPrefix.Trim())
        command.Parameters.AddWithValue("$qrFormat", part.QrFormat.Trim())
        command.Parameters.AddWithValue("$customerItemCode", part.CustomerItemCode.Trim())
        command.Parameters.AddWithValue("$material", part.Material.Trim())
        command.Parameters.AddWithValue("$pattern", part.Pattern.Trim())
        command.Parameters.AddWithValue("$productName", part.ProductName.Trim())
        command.Parameters.AddWithValue("$supplierName", part.SupplierName.Trim())
        command.Parameters.AddWithValue("$templateFile", part.TemplateFile.Trim())
    End Sub

    Private Shared Function BuildPartSelectSql() As String
        Return "
SELECT Id, PartNumber, VendorCode, PlantCode, CustomerCode, QRPrefix, QRFormat,
       CustomerItemCode, Material, Pattern, ProductName, SupplierName, TemplateFile, IsActive
FROM Parts
"
    End Function

    Private Shared Function ReadPart(reader As IDataRecord) As PartRecord
        Return New PartRecord With {
            .Id = reader.GetInt32(0),
            .PartNumber = reader.GetString(1),
            .VendorCode = reader.GetString(2),
            .PlantCode = reader.GetString(3),
            .CustomerCode = reader.GetString(4),
            .QrPrefix = reader.GetString(5),
            .QrFormat = reader.GetString(6),
            .CustomerItemCode = reader.GetString(7),
            .Material = reader.GetString(8),
            .Pattern = reader.GetString(9),
            .ProductName = reader.GetString(10),
            .SupplierName = reader.GetString(11),
            .TemplateFile = reader.GetString(12),
            .IsActive = reader.GetInt32(13) = 1
        }
    End Function

    Private Shared Sub ExecuteNonQuery(connection As SqliteConnection, sql As String)
        Using command = connection.CreateCommand()
            command.CommandText = sql
            command.ExecuteNonQuery()
        End Using
    End Sub

    Private Shared Sub EnsureColumn(connection As SqliteConnection, tableName As String, columnName As String, columnDefinition As String)
        Using checkCommand = connection.CreateCommand()
            checkCommand.CommandText = $"PRAGMA table_info({tableName});"
            Using reader = checkCommand.ExecuteReader()
                While reader.Read()
                    If String.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase) Then
                        Return
                    End If
                End While
            End Using
        End Using

        Using alterCommand = connection.CreateCommand()
            alterCommand.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnDefinition};"
            alterCommand.ExecuteNonQuery()
        End Using
    End Sub

    Private Shared Function GetSetting(connection As SqliteConnection, key As String, defaultValue As String) As String
        Using command = connection.CreateCommand()
            command.CommandText = "SELECT Value FROM Settings WHERE Key = $key;"
            command.Parameters.AddWithValue("$key", key)
            Dim value = command.ExecuteScalar()
            If value Is Nothing OrElse value Is DBNull.Value Then
                Return defaultValue
            End If

            Return Convert.ToString(value)
        End Using
    End Function

    Private Shared Function GetIntegerSetting(connection As SqliteConnection, key As String, defaultValue As Integer) As Integer
        Dim value = GetSetting(connection, key, defaultValue.ToString())
        Dim parsed As Integer
        If Integer.TryParse(value, parsed) Then
            Return parsed
        End If

        Return defaultValue
    End Function

    Private Shared Sub SaveSetting(connection As SqliteConnection, key As String, value As String)
        Using command = connection.CreateCommand()
            command.CommandText = "
INSERT INTO Settings (Key, Value)
VALUES ($key, $value)
ON CONFLICT(Key) DO UPDATE SET Value = excluded.Value;"
            command.Parameters.AddWithValue("$key", key)
            command.Parameters.AddWithValue("$value", If(value, ""))
            command.ExecuteNonQuery()
        End Using
    End Sub

    Private Shared Sub SeedDefaults(connection As SqliteConnection)
        EnsureDefaultUser(connection, "operator", "operator123", UserRole.OperatorUser)
        EnsureDefaultUser(connection, "setter", "setter123", UserRole.Setter)
        EnsureDefaultUser(connection, "admin", "admin123", UserRole.Admin)

        SeedWorkbookParts(connection)

        SaveSettingIfMissing(connection, "QrOutputPath", "C:\Laser\QRDATA.TXT")
        SaveSettingIfMissing(connection, "ActiveTemplateDirectory", "C:\Laser\ActiveTemplate")
        SaveSettingIfMissing(connection, "AutoLogoutMinutes", "2")
        SaveSettingIfMissing(connection, "SerialRegex", "")
        UpdateSettingValue(connection, "SerialRegex", "^\d{6}$", "")
        UpdateSettingValue(connection, "SerialRegex", "^\d{2}-[A-Z]\d-\d{4}$", "")
        SaveSettingIfMissing(connection, "ExternalCommand", "")
        SaveSettingIfMissing(connection, "NextSerialNumber", "2498")
    End Sub

    Private Shared Sub SeedWorkbookParts(connection As SqliteConnection)
        Dim seeds = {
            New PartRecord With {.PartNumber = "B3F00401", .CustomerItemCode = "7201097", .Material = "FG260", .Pattern = "#.3", .ProductName = "FLYWHEEL", .SupplierName = "SREERAMENGG", .TemplateFile = "C:\Laser\Templates\B3F00401.ezd", .IsActive = True},
            New PartRecord With {.PartNumber = "B3F02001", .CustomerItemCode = "7201097", .Material = "FG260", .Pattern = "#.0", .ProductName = "FLYWHEEL", .SupplierName = "SREERAMENGG", .TemplateFile = "C:\Laser\Templates\B3F02001.ezd"},
            New PartRecord With {.PartNumber = "B3F02301", .CustomerItemCode = "7201097", .Material = "FG260", .Pattern = "#.0", .ProductName = "FLYWHEEL", .SupplierName = "SREERAMENGG", .TemplateFile = "C:\Laser\Templates\B3F02301.ezd"},
            New PartRecord With {.PartNumber = "B3F03901", .CustomerItemCode = "7201097", .Material = "FG260", .Pattern = "B", .ProductName = "FLYWHEEL", .SupplierName = "SREERAMENGG", .TemplateFile = "C:\Laser\Templates\B3F03901.ezd"},
            New PartRecord With {.PartNumber = "B3F07601", .CustomerItemCode = "7201097", .Material = "FG260", .Pattern = "#1", .ProductName = "FLYWHEEL", .SupplierName = "SREERAMENGG", .TemplateFile = "C:\Laser\Templates\B3F07601.ezd"},
            New PartRecord With {.PartNumber = "B3F11901", .CustomerItemCode = "7201097", .Material = "FG260", .Pattern = "#1", .ProductName = "FLYWHEEL", .SupplierName = "SREERAMENGG", .TemplateFile = "C:\Laser\Templates\B3F11901.ezd"},
            New PartRecord With {.PartNumber = "B3F13201", .CustomerItemCode = "7201097", .Material = "FG260", .Pattern = "#", .ProductName = "FLYWHEEL", .SupplierName = "SREERAMENGG", .TemplateFile = "C:\Laser\Templates\B3F13201.ezd"},
            New PartRecord With {.PartNumber = "B3F15301", .CustomerItemCode = "7201097", .Material = "FG260", .Pattern = "#", .ProductName = "FLYWHEEL", .SupplierName = "SREERAMENGG", .TemplateFile = "C:\Laser\Templates\B3F15301.ezd"},
            New PartRecord With {.PartNumber = "B3F16301", .CustomerItemCode = "7201097", .Material = "FG260", .Pattern = "#", .ProductName = "FLYWHEEL", .SupplierName = "SREERAMENGG", .TemplateFile = "C:\Laser\Templates\B3F16301.ezd"},
            New PartRecord With {.PartNumber = "B3F16401", .CustomerItemCode = "7201097", .Material = "FG260", .Pattern = "#", .ProductName = "FLYWHEEL", .SupplierName = "SREERAMENGG", .TemplateFile = "C:\Laser\Templates\B3F16401.ezd"},
            New PartRecord With {.PartNumber = "B3F18701", .CustomerItemCode = "7201097", .Material = "FG260", .Pattern = "K", .ProductName = "FLYWHEEL", .SupplierName = "SREERAMENGG", .TemplateFile = "C:\Laser\Templates\B3F18701.ezd"},
            New PartRecord With {.PartNumber = "B3F19901", .CustomerItemCode = "7201097", .Material = "FG260", .Pattern = "#", .ProductName = "FLYWHEEL", .SupplierName = "SREERAMENGG", .TemplateFile = "C:\Laser\Templates\B3F19901.ezd"},
            New PartRecord With {.PartNumber = "B8761001", .CustomerItemCode = "7201097", .Material = "FG260", .Pattern = "K", .ProductName = "FLYWHEEL", .SupplierName = "SREERAMENGG", .TemplateFile = "C:\Laser\Templates\B8761001.ezd"}
        }

        For Each seed In seeds
            seed.VendorCode = seed.CustomerItemCode
            seed.CustomerCode = seed.CustomerItemCode
            seed.QrFormat = "{CustomerItemCode}${PartNumber}${DatePrefixSerial}${MarkDate}${MonthLabel}${HeatLot}${Material}${Pattern}${ProductName}${SupplierName}$"

            Using command = connection.CreateCommand()
                command.CommandText = "
INSERT INTO Parts (PartNumber, VendorCode, PlantCode, CustomerCode, QRPrefix, QRFormat, CustomerItemCode, Material, Pattern, ProductName, SupplierName, TemplateFile, IsActive)
VALUES ($partNumber, $vendorCode, '', $customerCode, '', $qrFormat, $customerItemCode, $material, $pattern, $productName, $supplierName, $templateFile, $isActive)
ON CONFLICT(PartNumber) DO UPDATE SET
    VendorCode = excluded.VendorCode,
    CustomerCode = excluded.CustomerCode,
    QRFormat = excluded.QRFormat,
    CustomerItemCode = excluded.CustomerItemCode,
    Material = excluded.Material,
    Pattern = excluded.Pattern,
    ProductName = excluded.ProductName,
    SupplierName = excluded.SupplierName,
    TemplateFile = excluded.TemplateFile;"
                AddPartParameters(command, seed)
                command.Parameters.AddWithValue("$isActive", If(seed.IsActive, 1, 0))
                command.ExecuteNonQuery()
            End Using
        Next

        MigrateLegacyDemoPart(connection)

        Using activeCommand = connection.CreateCommand()
            activeCommand.CommandText = "SELECT COUNT(1) FROM Parts WHERE IsActive = 1;"
            If Convert.ToInt32(activeCommand.ExecuteScalar()) = 0 Then
                Using setActiveCommand = connection.CreateCommand()
                    setActiveCommand.CommandText = "UPDATE Parts SET IsActive = 1 WHERE PartNumber = 'B3F00401';"
                    setActiveCommand.ExecuteNonQuery()
                End Using
            End If
        End Using
    End Sub

    Private Shared Sub MigrateLegacyDemoPart(connection As SqliteConnection)
        Using activeCommand = connection.CreateCommand()
            activeCommand.CommandText = "SELECT COUNT(1) FROM Parts WHERE PartNumber = 'ABC123' AND IsActive = 1;"
            If Convert.ToInt32(activeCommand.ExecuteScalar()) > 0 Then
                Using setActiveCommand = connection.CreateCommand()
                    setActiveCommand.CommandText = "
UPDATE Parts SET IsActive = 0;
UPDATE Parts SET IsActive = 1 WHERE PartNumber = 'B3F00401';"
                    setActiveCommand.ExecuteNonQuery()
                End Using
            End If
        End Using

        Using deleteCommand = connection.CreateCommand()
            deleteCommand.CommandText = "
DELETE FROM Parts
WHERE PartNumber = 'ABC123';"
            deleteCommand.ExecuteNonQuery()
        End Using
    End Sub

    Private Shared Sub InsertUser(connection As SqliteConnection, username As String, password As String, role As UserRole)
        Using command = connection.CreateCommand()
            command.CommandText = "
INSERT INTO Users (Username, PasswordHash, Role)
VALUES ($username, $passwordHash, $role);"
            command.Parameters.AddWithValue("$username", username)
            command.Parameters.AddWithValue("$passwordHash", PasswordHasher.HashPassword(password))
            command.Parameters.AddWithValue("$role", CInt(role))
            command.ExecuteNonQuery()
        End Using
    End Sub

    Private Shared Sub EnsureDefaultUser(connection As SqliteConnection, username As String, password As String, role As UserRole)
        Using command = connection.CreateCommand()
            command.CommandText = "SELECT COUNT(1) FROM Users WHERE Username = $username;"
            command.Parameters.AddWithValue("$username", username)
            If Convert.ToInt32(command.ExecuteScalar()) > 0 Then
                Return
            End If
        End Using

        InsertUser(connection, username, password, role)
    End Sub

    Private Shared Sub SaveSettingIfMissing(connection As SqliteConnection, key As String, value As String)
        Using command = connection.CreateCommand()
            command.CommandText = "
INSERT INTO Settings (Key, Value)
VALUES ($key, $value)
ON CONFLICT(Key) DO NOTHING;"
            command.Parameters.AddWithValue("$key", key)
            command.Parameters.AddWithValue("$value", value)
            command.ExecuteNonQuery()
        End Using
    End Sub

    Private Shared Sub UpdateSettingValue(connection As SqliteConnection, key As String, oldValue As String, newValue As String)
        Using command = connection.CreateCommand()
            command.CommandText = "
UPDATE Settings
SET Value = $newValue
WHERE Key = $key
  AND Value = $oldValue;"
            command.Parameters.AddWithValue("$key", key)
            command.Parameters.AddWithValue("$oldValue", oldValue)
            command.Parameters.AddWithValue("$newValue", newValue)
            command.ExecuteNonQuery()
        End Using
    End Sub
End Class
