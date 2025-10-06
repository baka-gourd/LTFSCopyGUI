﻿Public Class TapeCopy
    Public TapeA As String, TapeB As String, Operation_Cancel_Flag As Boolean = False
    Public FlushFlag As Boolean = False
    Public progval As Long = 0
    Public progLastVal As Long = 0
    Public lastIncVal As Long = 0
    Public FlushCounter As Long = 0
    Private Sub Button2_Click(sender As Object, e As EventArgs) Handles Button2.Click
        FlushFlag = True
    End Sub

    Private Sub TapeCopy_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        If Not New Security.Principal.WindowsPrincipal(Security.Principal.WindowsIdentity.GetCurrent()).IsInRole(Security.Principal.WindowsBuiltInRole.Administrator) Then
            Process.Start(New ProcessStartInfo With {.FileName = Application.ExecutablePath, .Verb = "runas", .Arguments = "-copy"})
            Me.Close()
            Exit Sub
        End If
        FlushCounter = My.Settings.LTFSWriter_AutoCleanTimeThreashould
    End Sub

    Private Sub Timer1_Tick(sender As Object, e As EventArgs) Handles Timer1.Tick
        Dim progsnap As Integer = progval
        lastIncVal = Math.Max(0, progsnap - progLastVal)
        progLastVal = progsnap

        If CheckBox1.Checked Then
            If lastIncVal <= My.Settings.LTFSWriter_AutoCleanUpperLim AndAlso lastIncVal >= My.Settings.LTFSWriter_AutoCleanDownLim Then
                FlushCounter -= 1
                If FlushCounter = 0 Then
                    FlushFlag = True
                    FlushCounter = My.Settings.LTFSWriter_AutoCleanTimeThreashould
                End If
            Else
                FlushCounter = My.Settings.LTFSWriter_AutoCleanTimeThreashould
            End If
        End If
    End Sub

    Public Sub PrintMsg(s As String)
        Invoke(Sub() Label4.Text = $"Status: {s}")
    End Sub
    Private Sub Button1_Click(sender As Object, e As EventArgs) Handles Button1.Click
        If Button1.Text = "Start" Then
            Button1.Text = "Stop"
            TapeA = TextBox1.Text
            TapeB = TextBox2.Text
            Dim handleA, handleB As IntPtr
            Dim drivertype As TapeUtils.DriverType = TapeUtils.DriverTypeSetting
            Dim IsFileA As Boolean = CheckBox2.Checked
            Dim IsFileB As Boolean = CheckBox3.Checked
            If IsFileA Then
                TapeUtils.DriverTypeSetting = TapeUtils.DriverType.TapeStream
            Else
                TapeUtils.DriverTypeSetting = drivertype
            End If
            TapeUtils.OpenTapeDrive(TapeA, handleA)

            If IsFileB Then
                TapeUtils.DriverTypeSetting = TapeUtils.DriverType.TapeStream
                If Not IO.File.Exists(TapeB) Then
                    IO.File.Create(TapeB).Close()
                End If
            Else
                TapeUtils.DriverTypeSetting = drivertype
            End If
            TapeUtils.OpenTapeDrive(TapeB, handleB)
            Dim BlockCount As Integer = NumericUpDown1.Value
            Dim BlockLen As UInteger = NumericUpDown2.Value
            progval = 0
            progLastVal = 0
            Dim running As Boolean = True
            Dim th As New Threading.Thread(
                Sub()
                    Dim sense(63) As Byte
                    Me.Invoke(Sub() Button1.Enabled = True)
                    Dim readData As Byte()
                    Dim Add_Key As UInt16
                    Dim i As Integer = 0
                    Do
                        i += 1
                        Try

                            If FlushFlag Then

                                If IsFileB Then
                                    TapeUtils.DriverTypeSetting = TapeUtils.DriverType.TapeStream
                                Else
                                    TapeUtils.DriverTypeSetting = drivertype
                                End If
                                TapeUtils.Flush(handleB)
                                FlushFlag = False
                            End If

                            If IsFileA Then
                                TapeUtils.DriverTypeSetting = TapeUtils.DriverType.TapeStream
                            Else
                                TapeUtils.DriverTypeSetting = drivertype
                            End If
                            readData = TapeUtils.ReadBlock(handleA, sense, BlockLen)
                            Add_Key = CInt(sense(12)) << 8 Or sense(13)
                            Dim succ As Boolean = False
                            While Not succ
                                Dim sense2 As Byte() = Nothing

                                If IsFileB Then
                                    TapeUtils.DriverTypeSetting = TapeUtils.DriverType.TapeStream
                                Else
                                    TapeUtils.DriverTypeSetting = drivertype
                                End If
                                If readData.Length > 0 Then
                                    sense2 = TapeUtils.Write(handleB, readData)
                                    progval = i
                                ElseIf Add_Key <> 0 AndAlso Not ((Add_Key > 1 And Add_Key <> 4)) Then
                                    sense2 = TapeUtils.WriteFileMark(handleB)
                                    progval = i
                                Else
                                    succ = True
                                End If
                                If sense2 IsNot Nothing AndAlso sense2.Length > 2 AndAlso (sense2(2) And &HF) <> 0 Then
                                    Select Case MessageBox.Show(New Form With {.TopMost = True}, $"sense err {TapeUtils.Byte2Hex(sense2, True)}", "Warning", MessageBoxButtons.AbortRetryIgnore)
                                        Case DialogResult.Abort
                                            Exit Do
                                        Case DialogResult.Retry
                                            succ = False
                                        Case DialogResult.Ignore
                                            succ = True
                                            Exit While
                                    End Select
                                Else
                                    succ = True
                                End If
                            End While

                            If (Add_Key > 1 And Add_Key <> 4) Then
                                running = False
                                Threading.Thread.Sleep(200)
                                PrintMsg($"EOD detected. {i} blocks transferred.")
                                Exit Do
                            ElseIf Operation_Cancel_Flag Then
                                running = False
                                Threading.Thread.Sleep(200)
                                Operation_Cancel_Flag = False
                                PrintMsg($"Operation cancelled. {i} blocks transferred.")
                                Exit Do
                            End If
                        Catch ex As Exception
                            MessageBox.Show(New Form With {.TopMost = True}, ex.ToString())
                        End Try
                    Loop While i < BlockCount OrElse BlockCount <= 0

                    If IsFileB Then
                        TapeUtils.DriverTypeSetting = TapeUtils.DriverType.TapeStream
                    Else
                        TapeUtils.DriverTypeSetting = drivertype
                    End If
                    TapeUtils.Flush(TapeB)

                    If IsFileA Then
                        TapeUtils.DriverTypeSetting = TapeUtils.DriverType.TapeStream
                    Else
                        TapeUtils.DriverTypeSetting = drivertype
                    End If
                    TapeUtils.CloseTapeDrive(handleA)

                    If IsFileB Then
                        TapeUtils.DriverTypeSetting = TapeUtils.DriverType.TapeStream
                    Else
                        TapeUtils.DriverTypeSetting = drivertype
                    End If
                    TapeUtils.CloseTapeDrive(handleB)
                    TapeUtils.DriverTypeSetting = drivertype
                    running = False
                    Invoke(Sub() Button1.Text = "Start")
                End Sub)
            Dim maxStr As String = ""
            If BlockCount > 0 Then maxStr = BlockCount.ToString Else maxStr = "unlimited"
            Dim thprog As New Threading.Thread(
                Sub()
                    While running
                        Dim prog As Integer = Math.Min(10000, Math.Max(0, progval * 10000 / Math.Max(1, BlockCount)))
                        Invoke(Sub() ProgressBar1.Value = prog)
                        PrintMsg($"{progval}/{maxStr} (+ {(lastIncVal * (BlockLen / 1048576)).ToString("F2")}MiB/s)")
                        Threading.Thread.Sleep(100)
                    End While
                End Sub)
            Button1.Enabled = False
            thprog.Start()
            th.Start()
        ElseIf Button1.Text = "Stop" Then
            Operation_Cancel_Flag = True
        End If
    End Sub
End Class