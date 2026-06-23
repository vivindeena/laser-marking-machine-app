Imports System
Imports System.Security.Cryptography

Public NotInheritable Class PasswordHasher
    Private Const Iterations As Integer = 120000
    Private Const SaltBytes As Integer = 16
    Private Const HashBytes As Integer = 32

    Private Sub New()
    End Sub

    Public Shared Function HashPassword(password As String) As String
        If password Is Nothing Then
            Throw New ArgumentNullException(NameOf(password))
        End If

        Dim salt(SaltBytes - 1) As Byte
        RandomNumberGenerator.Fill(salt)

        Dim hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            HashBytes)

        Return $"{Iterations}:{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}"
    End Function

    Public Shared Function Verify(password As String, storedHash As String) As Boolean
        If String.IsNullOrWhiteSpace(password) OrElse String.IsNullOrWhiteSpace(storedHash) Then
            Return False
        End If

        Dim parts = storedHash.Split(":"c)
        If parts.Length <> 3 Then
            Return False
        End If

        Dim parsedIterations As Integer
        If Not Integer.TryParse(parts(0), parsedIterations) Then
            Return False
        End If

        Try
            Dim salt = Convert.FromBase64String(parts(1))
            Dim expected = Convert.FromBase64String(parts(2))
            Dim actual = Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                parsedIterations,
                HashAlgorithmName.SHA256,
                expected.Length)

            Return CryptographicOperations.FixedTimeEquals(actual, expected)
        Catch ex As FormatException
            Return False
        End Try
    End Function
End Class
