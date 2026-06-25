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

CREATE UNIQUE INDEX IF NOT EXISTS UX_MarkLog_Part_Serial
ON MarkLog (PartNumber, SerialNumber);
")
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
                command.CommandText = "
SELECT Id, PartNumber, VendorCode, PlantCode, CustomerCode, QRPrefix, QRFormat, TemplateFile, IsActive
FROM Parts
ORDER BY PartNumber;"
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
                command.CommandText = "
SELECT Id, PartNumber, VendorCode, PlantCode, CustomerCode, QRPrefix, QRFormat, TemplateFile, IsActive
FROM Parts
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
INSERT INTO Parts (PartNumber, VendorCode, PlantCode, CustomerCode, QRPrefix, QRFormat, TemplateFile, IsActive)
VALUES ($partNumber, $vendorCode, $plantCode, $customerCode, $qrPrefix, $qrFormat, $templateFile, 0);
SELECT last_insert_rowid();"
                AddPartParameters(command, part)
                Return Convert.ToInt32(command.ExecuteScalar())
            End Using
        End Using
    End Function

    Public Sub SetActivePart(partId As Integer)
        Using connection = OpenConnection()
            Using transaction = connection.BeginTransaction()
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

    Private Function OpenConnection() As SqliteConnection
        Dim connection = New SqliteConnection(_connectionString)
        connection.Open()
        Return connection
    End Function

    Private Shared Sub AddPartParameters(command As SqliteCommand, part As PartRecord)
        command.Parameters.AddWithValue("$partNumber", part.PartNumber.Trim())
        command.Parameters.AddWithValue("$vendorCode", part.VendorCode.Trim())
        command.Parameters.AddWithValue("$plantCode", part.PlantCode.Trim())
        command.Parameters.AddWithValue("$customerCode", part.CustomerCode.Trim())
        command.Parameters.AddWithValue("$qrPrefix", part.QrPrefix.Trim())
        command.Parameters.AddWithValue("$qrFormat", part.QrFormat.Trim())
        command.Parameters.AddWithValue("$templateFile", part.TemplateFile.Trim())
    End Sub

    Private Shared Function ReadPart(reader As IDataRecord) As PartRecord
        Return New PartRecord With {
            .Id = reader.GetInt32(0),
            .PartNumber = reader.GetString(1),
            .VendorCode = reader.GetString(2),
            .PlantCode = reader.GetString(3),
            .CustomerCode = reader.GetString(4),
            .QrPrefix = reader.GetString(5),
            .QrFormat = reader.GetString(6),
            .TemplateFile = reader.GetString(7),
            .IsActive = reader.GetInt32(8) = 1
        }
    End Function

    Private Shared Sub ExecuteNonQuery(connection As SqliteConnection, sql As String)
        Using command = connection.CreateCommand()
            command.CommandText = sql
            command.ExecuteNonQuery()
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
        Using command = connection.CreateCommand()
            command.CommandText = "SELECT COUNT(1) FROM Users;"
            Dim userCount = Convert.ToInt32(command.ExecuteScalar())
            If userCount = 0 Then
                InsertUser(connection, "operator", "operator123", UserRole.OperatorUser)
                InsertUser(connection, "setter", "setter123", UserRole.Setter)
                InsertUser(connection, "admin", "admin123", UserRole.Admin)
            End If
        End Using

        Using command = connection.CreateCommand()
            command.CommandText = "SELECT COUNT(1) FROM Parts;"
            Dim partCount = Convert.ToInt32(command.ExecuteScalar())
            If partCount = 0 Then
                Using insertCommand = connection.CreateCommand()
                    insertCommand.CommandText = "
INSERT INTO Parts (PartNumber, VendorCode, PlantCode, CustomerCode, QRPrefix, QRFormat, TemplateFile, IsActive)
VALUES ('ABC123', 'V001', 'P01', 'C123', 'A', '{VendorCode}|{PartNumber}|{Serial}', 'C:\Laser\Templates\ABC123.ezd', 1);"
                    insertCommand.ExecuteNonQuery()
                End Using
            End If
        End Using

        SaveSettingIfMissing(connection, "QrOutputPath", "C:\Laser\QRDATA.TXT")
        SaveSettingIfMissing(connection, "ActiveTemplateDirectory", "C:\Laser\ActiveTemplate")
        SaveSettingIfMissing(connection, "AutoLogoutMinutes", "2")
        SaveSettingIfMissing(connection, "SerialRegex", "^\d{2}-[A-Z]\d-\d{4}$")
        UpdateSettingValue(connection, "SerialRegex", "^\d{6}$", "^\d{2}-[A-Z]\d-\d{4}$")
        SaveSettingIfMissing(connection, "ExternalCommand", "")
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
