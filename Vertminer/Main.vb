﻿
Imports System.IO
Imports System.IO.Compression
Imports System.Environment
Imports System.Net
Imports System.Net.Sockets
Imports Open.Nat
Imports System.Text
Imports System.Web.Script.Serialization
Imports Newtonsoft.Json
Imports Newtonsoft.Json.Linq
Imports System.Globalization
Imports VertcoinOneClickMiner.Core

Public Class Main

    Dim JSONConverter As JavaScriptSerializer = New JavaScriptSerializer()
    Private _logger as ILogger

    Private Sub Form1_Load(sender As Object, e As EventArgs) Handles MyBase.Load

        Try
            'Styling
            Invoke(New MethodInvoker(AddressOf Style))
            If Environment.Is64BitOperatingSystem = True Then
                platform = True '64-bit
            Else
                platform = False '32-bit
            End If
            settingsfolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) & "\Vertcoin One-Click Miner"
            settingsfile = settingsfolder & "\Settings.json"
            syslog = settingsfolder & "\SysLog.txt"
            p2poolfolder = settingsfolder & "\p2pool"
            scannerfolder = settingsfolder & "\scanner"
            amdfolder = settingsfolder & "\amd"
            nvidiafolder = settingsfolder & "\nvidia"
            cpufolder = settingsfolder & "\cpu"

            _logger = New FileLogger(syslog)

            If System.IO.Directory.Exists(settingsfolder) = False Then
                System.IO.Directory.CreateDirectory(settingsfolder)
            End If
            If System.IO.File.Exists(settingsfolder & "\Settings.ini") = True Then
                Invoke(New MethodInvoker(AddressOf Update_Settings))
            Else
                If System.IO.File.Exists(settingsfile) = False Then
                    Invoke(New MethodInvoker(AddressOf SaveSettingsJSON))
                End If
            End If
            If System.IO.File.Exists(syslog) = False Then
                File.Create(syslog).Dispose()
            End If
            'Load Settings
            Invoke(New MethodInvoker(AddressOf LoadSettingsJSON))
            'Check for default miner selected.  This is kept separate from autostart to allow user to see checkbox.
            If default_miner = "amd" Then
                ComboBox1.SelectedItem = "AMD"
            ElseIf default_miner = "nvidia" Then
                ComboBox1.SelectedItem = "NVIDIA"
            ElseIf default_miner = "cpu" Then
                ComboBox1.SelectedItem = "CPU"
            End If
            'Window state on start
            If start_minimized = True Then
                Me.WindowState = FormWindowState.Minimized
            Else
                Me.WindowState = FormWindowState.Normal
            End If
            'Check if p2pool or miner are already running
            Invoke(New MethodInvoker(AddressOf Process_Check))
            If p2pool_detected = True Then
                'P2Pool is already running
                CheckBox1.Checked = True
            End If
            'Autostart variables
            If autostart_mining = True Then
                If default_miner = "amd" Then
                    If System.IO.File.Exists(amdfolder & "\ocm_sgminer.exe") = True Then
                        amdminer = True
                    End If
                    mining_initialized = True
                    BeginInvoke(New MethodInvoker(AddressOf Start_Miner))
                ElseIf default_miner = "nvidia" Then
                    If System.IO.File.Exists(nvidiafolder & "\ocm_vertminer.exe") = True Then
                        nvidiaminer = True
                    End If
                    mining_initialized = True
                    BeginInvoke(New MethodInvoker(AddressOf Start_Miner))
                ElseIf default_miner = "cpu" Then
                    If System.IO.File.Exists(cpufolder & "\ocm_cpuminer.exe") = True Then
                        cpuminer = True
                    End If
                    mining_initialized = True
                    BeginInvoke(New MethodInvoker(AddressOf Start_Miner))
                End If
            End If
            If autostart_p2pool = True Then
                If System.IO.File.Exists(p2poolfolder & "\ocm_p2pool.exe") = True Then
                    BeginInvoke(New MethodInvoker(AddressOf Start_P2Pool))
                End If
            End If
            DataGridView1.DataSource = Nothing
            Invoke(New MethodInvoker(AddressOf Uptime_Checker_Status_Text))
            Invoke(New MethodInvoker(AddressOf Update_Miner_Text))
            BeginInvoke(New MethodInvoker(AddressOf Detected))
            UpdateStatsInterval.Start()
            Uptime_Timer.Start()
        Catch ex As Exception
            MsgBox(ex.Message)
            _logger.LogError(ex)
        Finally
            _logger.Trace("Loaded: OK, V:" & Application.ProductVersion)
        End Try

    End Sub

    Private Sub Main_Size_Change(sender As Object, e As EventArgs) Handles MyBase.SizeChanged

        NotifyIcon1.ContextMenu = New ContextMenu
        If Me.WindowState = FormWindowState.Minimized Then
            If start_minimized = True Then
                Me.ShowInTaskbar = False
                NotifyIcon1.Visible = True
                NotifyIcon1.ShowBalloonTip(5, "System", "Vertcoin One-Click Miner is minimized.", ToolTipIcon.Info)
                NotifyIcon1.ContextMenu.MenuItems.Add("Open", AddressOf Menu_Open)
                NotifyIcon1.ContextMenu.MenuItems.Add("Close", AddressOf Menu_Close)
            Else
                NotifyIcon1.Visible = False
                Me.Show()
            End If
        ElseIf Me.WindowState = FormWindowState.Normal Then
            NotifyIcon1.Visible = False
            Me.Show()
        End If

    End Sub

    Private Sub Menu_Close()

        Me.Close()

    End Sub

    Private Sub Menu_Open()

        Me.WindowState = FormWindowState.Normal

    End Sub

    Private Sub Main_Closing(sender As Object, e As System.ComponentModel.CancelEventArgs) Handles MyBase.Closing

        _logger.Trace(Environment.NewLine)
        _logger.Trace("Closing: OK")

        NotifyIcon1.Dispose()
        If ComboBox1.SelectedItem = "AMD" Then
            default_miner = "amd"
        ElseIf ComboBox1.SelectedItem = "NVIDIA" Then
            default_miner = "nvidia"
        ElseIf ComboBox1.SelectedItem = "CPU" Then
            default_miner = "cpu"
        End If
        Invoke(New MethodInvoker(AddressOf SaveSettingsJSON))
        Invoke(New MethodInvoker(AddressOf Kill_Miner))
        Invoke(New MethodInvoker(AddressOf Kill_P2Pool))

        _logger.Trace("================================================================================")
        Application.Exit()

    End Sub

    Public Sub Process_Check()

        For Each p As Process In System.Diagnostics.Process.GetProcesses
            If p.ProcessName.Contains("ocm_p2pool") Then
                p2pool_detected = True
                Exit For
            Else
                p2pool_detected = False
            End If
        Next
        For Each p As Process In System.Diagnostics.Process.GetProcesses
            If p.ProcessName.Contains("ocm_sgminer") Then
                amd_detected = True
                Exit For
            Else
                amd_detected = False
            End If
        Next
        For Each p As Process In System.Diagnostics.Process.GetProcesses
            If p.ProcessName.Contains("ocm_vertminer") Then
                nvidia_detected = True
                Exit For
            Else
                nvidia_detected = False
            End If
        Next
        For Each p As Process In System.Diagnostics.Process.GetProcesses
            If p.ProcessName.Contains("ocm_cpuminer") Then
                cpu_detected = True
                Exit For
            Else
                cpu_detected = False
            End If
        Next
        For Each p As Process In System.Diagnostics.Process.GetProcesses
            If (p.ProcessName.Contains("run_p2pool") Or p.ProcessName.Contains("p2pool")) And Not p.ProcessName.Contains("ocm") Then
                otherp2pool = True
                Exit For
            Else
                otherp2pool = False
            End If
        Next
        For Each p As Process In System.Diagnostics.Process.GetProcesses
            If p.ProcessName = ("vertminer") Or p.ProcessName = ("sgminer") Or p.ProcessName = ("cpuminer") And Not p.ProcessName.Contains("ocm") Then
                otherminer = True
                Exit For
            Else
                otherminer = False
            End If
        Next

    End Sub

    Public Sub Detected()

        Me.Text = "Vertcoin OCM - BETA V:" & miner_version
        If otherp2pool = True Then
            MsgBox("One-Click Miner has detected other p2pool software running.  Be aware of potential port conflicts.")
        End If
        If otherminer = True Then
            MsgBox("One-Click Miner has detected other miner software running." & Environment.NewLine & "It is not recommended to run multiple miners on the same machine simultaneously.")
        End If

    End Sub

    Public Sub UPnP()

        Try
            'using native upnp
            'Dim ipAddress As String = GetIPv4Address()
            'Dim upnpnat As New NATUPNPLib.UPnPNAT()
            'Dim mappings As NATUPNPLib.IStaticPortMappingCollection = upnpnat.StaticPortMappingCollection
            'mappings.Add(mining_port, "TCP", mining_port, ipAddress, True, "P2Pool Mining Server")
            'mappings.Add(p2pool_port, "TCP", p2pool_port, ipAddress, True, "P2Pool P2P")

            'using native upnp
            'Dim upnpnat As New NATUPNPLib.UPnPNAT()
            'Dim lMapper As NATUPNPLib.IStaticPortMappingCollection = upnpnat.StaticPortMappingCollection
            'Dim lMappedPort As NATUPNPLib.IStaticPortMapping
            'lMappedPort = DirectCast(lMapper.Add(mining_port, "TCP", mining_port, ipAddress, True, "P2Pool Mining Server"), NATUPNPLib.IStaticPortMapping)
            'lMappedPort = DirectCast(lMapper.Add(p2pool_port, "TCP", p2pool_port, ipAddress, True, "P2Pool P2P"), NATUPNPLib.IStaticPortMapping)

            'Using open.nat
            Dim discoverer As New NatDiscoverer()
            ' using SSDP protocol, it discovers NAT device.
            Dim device = discoverer.DiscoverDeviceAsync()
            ' create a New mapping in the router [external_ip1702 -> host_machine:1602]
            device.Equals(New Mapping(Protocol.Tcp, mining_port, mining_port, "P2Pool Mining Server"))
            device.Equals(New Mapping(Protocol.Tcp, p2pool_port, p2pool_port, "P2Pool P2P"))
            ' configure a TCP socket listening on port 1602
            Dim EndPoint As New IPEndPoint(IPAddress.Any, mining_port)
            Dim socket As New Socket(EndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
            socket.SetIPProtectionLevel(IPProtectionLevel.Unrestricted)
            socket.Bind(EndPoint)
            socket.Listen(4)
            Dim EndPoint2 As New IPEndPoint(IPAddress.Any, p2pool_port)
            Dim socket2 As New Socket(EndPoint2.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
            socket2.SetIPProtectionLevel(IPProtectionLevel.Unrestricted)
            socket2.Bind(EndPoint2)
            socket2.Listen(4)

            'Using Open.Nat
            'Dim discoverer As New Open.Nat.NatDiscoverer()
            'Dim cts As New System.Threading.CancellationTokenSource(10000)
            'Dim device = discoverer.DiscoverDeviceAsync(Open.Nat.PortMapper.Upnp, cts)
            'device.CreatePortMapAsync(New Open.Nat.Mapping(Open.Nat.Protocol.Tcp, 1600, 1700, "The mapping name"))

            _logger.Trace("SET OK. Ports set: " & mining_port & "," & p2pool_port)

        Catch ex As Exception
            MsgBox(ex.Message)
            _logger.LogError(ex)
            Invoke(New MethodInvoker(AddressOf SaveSettingsJSON))
        End Try

    End Sub

    Public Sub Download_Miner()

        Try
            progress.progress_text.Text = "Downloading Miner"
            progress.Show()
            downloadclient = New WebClient
            AddHandler downloadclient.DownloadProgressChanged, AddressOf Client_ProgressChanged
            AddHandler downloadclient.DownloadFileCompleted, AddressOf Client_MinerDownloadCompleted
            'Compares the current version of the AMD miner with the latest available.
            If amdminer = True Then
                If (newestversion > System.Version.Parse(amd_version)) Or mining_installed = False Then
                    If System.IO.Directory.Exists(amdfolder) = False Then
                        'If AMD miner doesn't already exist, create folder and download
                        System.IO.Directory.CreateDirectory(amdfolder)
                    Else
                        System.IO.Directory.Delete(amdfolder, True)
                        System.Threading.Thread.Sleep(100)
                        System.IO.Directory.CreateDirectory(amdfolder)
                    End If
                    downloadclient.DownloadFileAsync(New Uri(updatelink), settingsfolder & "\amd\sgminer.zip", True)
                Else
                    progress.Close()
                End If
            End If
            'Compares the current version of the Nvidia miner with the latest available.
            If nvidiaminer = True Then
                If newestversion > System.Version.Parse(nvidia_version) Or mining_installed = False Then
                    If System.IO.Directory.Exists(nvidiafolder) = False Then
                        'If Nvidia miner doesn't already exist, create folder and download
                        System.IO.Directory.CreateDirectory(nvidiafolder)
                    Else
                        System.IO.Directory.Delete(nvidiafolder, True)
                        System.Threading.Thread.Sleep(100)
                        System.IO.Directory.CreateDirectory(nvidiafolder)
                    End If
                    downloadclient.DownloadFileAsync(New Uri(updatelink), settingsfolder & "\nvidia\vertminer.zip", True)
                Else
                    progress.Close()
                End If
            End If
            'Compares the current version of the CPU miner with the latest available.
            If cpuminer = True Then
                If newestversion > System.Version.Parse(cpu_version) Or mining_installed = False Then
                    If System.IO.Directory.Exists(cpufolder) = False Then
                        'If CPU miner doesn't already exist, create folder and download
                        System.IO.Directory.CreateDirectory(cpufolder)
                    Else
                        System.IO.Directory.Delete(cpufolder, True)
                        System.Threading.Thread.Sleep(100)
                        System.IO.Directory.CreateDirectory(cpufolder)
                    End If
                    downloadclient.DownloadFileAsync(New Uri(updatelink), settingsfolder & "\cpu\cpuminer.zip", True)
                Else
                    progress.Close()
                End If
            End If
        Catch ex As Exception
            MsgBox(ex.Message)
            _logger.LogError(ex)
            Invoke(New MethodInvoker(AddressOf SaveSettingsJSON))
        Finally
            _logger.Trace("Downloaded OK.")
        End Try

    End Sub

    Public Sub Download_P2Pool()

        Try
            progress.progress_text.Text = "Downloading P2Pool"
            progress.Show()
            downloadclient = New WebClient
            AddHandler downloadclient.DownloadProgressChanged, AddressOf Client_ProgressChanged
            AddHandler downloadclient.DownloadFileCompleted, AddressOf Client_P2PoolDownloadCompleted
            If (newestversion > System.Version.Parse(p2pool_version)) Or p2pool_installed = False Then
                'Create p2pool directory and download/extract p2pool components into directory
                If System.IO.Directory.Exists(p2poolfolder) = False Then
                    System.IO.Directory.CreateDirectory(p2poolfolder)
                End If
                MsgBox("Please note, you must also run a Vertcoin Wallet to use P2Pool locally.")
                downloadclient.DownloadFileAsync(New Uri(updatelink), p2poolfolder & "\p2pool.zip", True)
            End If
        Catch ex As Exception
            MsgBox("An issue occurred during the download.  Please try again.")
            _logger.LogError(ex)
            Invoke(New MethodInvoker(AddressOf SaveSettingsJSON))
        Finally
            _logger.Trace("Downloaded OK.")
        End Try

    End Sub

    Public Sub Download_P2PoolInterface()

        Try
            'Better P2Pool Site UI
            Dim link As String = "http://github.com/justino/p2pool-ui-punchy/archive/master.zip"
            progress.progress_text.Text = "Downloading P2Pool UI"
            progress.Show()
            downloadclient = New WebClient
            AddHandler downloadclient.DownloadProgressChanged, AddressOf Client_ProgressChanged
            AddHandler downloadclient.DownloadFileCompleted, AddressOf Client_P2PoolInterfaceDownloadCompleted
            downloadclient.DownloadFileAsync(New Uri(link), p2poolfolder & "\interface.zip")
        Catch ex As Exception
            MsgBox("An issue occurred during the download.  Please try again.")
            _logger.LogError(ex)
            Invoke(New MethodInvoker(AddressOf SaveSettingsJSON))
        Finally
            _logger.Trace("Downloaded OK.")
        End Try

    End Sub

    Private Sub Client_ProgressChanged(ByVal sender As Object, ByVal e As DownloadProgressChangedEventArgs)

        Dim bytesIn As Double = Double.Parse(e.BytesReceived.ToString())
        Dim totalBytes As Double = Double.Parse(e.TotalBytesToReceive.ToString())
        Dim percentage As Double = bytesIn / totalBytes * 100
        If Int32.Parse(Math.Truncate(percentage).ToString()) >= 0 Then
            progress.ProgressBar1.Value = Int32.Parse(Math.Truncate(percentage).ToString())
        End If

    End Sub

    Private Sub Client_MinerDownloadCompleted(ByVal sender As Object, ByVal e As System.ComponentModel.AsyncCompletedEventArgs)

        Try
            If canceldownloadasync = False Then
                'Download proper miner and extract into respective directories
                If amdminer = True Then
                    zipPath = settingsfolder & "\amd\sgminer.zip"
                    exe = settingsfolder & "\amd\ocm_sgminer.exe"
                    miner_config_file = settingsfolder & "\amd\config.bat"
                ElseIf nvidiaminer = True Then
                    zipPath = settingsfolder & "\nvidia\vertminer.zip"
                    exe = settingsfolder & "\nvidia\ocm_vertminer.exe"
                    dll = settingsfolder & "\nvidia\msvcr120.dll"
                    miner_config_file = settingsfolder & "\nvidia\config.bat"
                ElseIf cpuminer = True Then
                    zipPath = settingsfolder & "\cpu\cpuminer.zip"
                    exe = settingsfolder & "\cpu\ocm_cpuminer.exe"
                    miner_config_file = settingsfolder & "\cpu\config.bat"
                End If
                Using archive As ZipArchive = ZipFile.OpenRead(zipPath)
                    'If platform = True Then '64-bit
                    For Each entry As ZipArchiveEntry In archive.Entries
                        If nvidiaminer = True Then 'Nvidia
                            If (entry.FullName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) Or entry.FullName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) Then
                                If entry.FullName.EndsWith(".exe") Then
                                    extractpath = exe
                                ElseIf entry.FullName.EndsWith(".dll") Then
                                    extractpath = dll
                                End If
                                entry.ExtractToFile(extractpath, True)
                            End If
                        ElseIf amdminer = True Then 'AMD
                            If (entry.FullName.Contains("kernel") And entry.FullName.EndsWith(".cl", StringComparison.OrdinalIgnoreCase)) Then
                                If System.IO.Directory.Exists(amdfolder & "\kernel\") = False Then
                                    System.IO.Directory.CreateDirectory(amdfolder & "\kernel\")
                                End If
                                entry.ExtractToFile(amdfolder & "\kernel\" & entry.Name, True)
                            Else
                                If entry.FullName.EndsWith(".exe") Then
                                    entry.ExtractToFile(exe, True)
                                ElseIf entry.FullName.EndsWith(".dll") Then
                                    entry.ExtractToFile(amdfolder & "\" & entry.Name, True)
                                End If
                            End If
                        ElseIf cpuminer = True Then 'CPU
                            If (entry.FullName.EndsWith("corei7.exe", StringComparison.OrdinalIgnoreCase)) Then
                                If entry.FullName.EndsWith(".exe") Then
                                    extractpath = exe
                                ElseIf entry.FullName.EndsWith(".dll") Then
                                    extractpath = dll
                                End If
                                entry.ExtractToFile(extractpath, True)
                            End If
                        End If
                    Next
                    'Else '32-bit
                    '    For Each entry As ZipArchiveEntry In archive.Entries
                    '        If NvidiaMiner = True Then 'Nvidia
                    '            If (entry.FullName.Contains("32") And entry.FullName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) Or (entry.FullName.Contains("32") And entry.FullName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) Then
                    '                If entry.FullName.EndsWith(".exe") Then
                    '                    extractpath = exe
                    '                ElseIf entry.FullName.EndsWith(".dll") Then
                    '                    extractpath = dll
                    '                End If
                    '                entry.ExtractToFile(extractpath, True)
                    '            End If
                    '        Else 'AMD
                    '            If (entry.FullName.Contains("kernel") And entry.FullName.EndsWith(".cl", StringComparison.OrdinalIgnoreCase)) Then
                    '                If System.IO.Directory.Exists(AmdFolder & "\kernel\") = False Then
                    '                    System.IO.Directory.CreateDirectory(AmdFolder & "\kernel\")
                    '                End If
                    '                entry.ExtractToFile(AmdFolder & "\kernel\" & entry.Name, True)
                    '            Else
                    '                If Not entry.Name = "" Then
                    '                    entry.ExtractToFile(AmdFolder & "\" & entry.Name, True)
                    '                End If
                    '            End If
                    '        End If
                    '       Next
                    'End If
                End Using
                'Create default miner config
                If System.IO.File.Exists(miner_config_file) = False Then
                    Dim objWriter As New System.IO.StreamWriter(miner_config_file)
                    objWriter.WriteLine(miner_config)
                    objWriter.Close()
                End If
                Dim result1 As DialogResult = MsgBox("Miner is ready to run", MessageBoxButtons.OK)
                If result1 = DialogResult.OK Then
                    progress.Close()
                    update_complete = True
                End If
                If amdminer = True Then
                    amd_version = System.Convert.ToString(newestversion)
                ElseIf nvidiaminer = True Then
                    nvidia_version = System.Convert.ToString(newestversion)
                ElseIf cpuminer = True Then
                    cpu_version = System.Convert.ToString(newestversion)
                End If
                amdminer = False
                nvidiaminer = False
                cpuminer = False
                update_needed = False
            End If
        Catch ex As Exception
            MsgBox("An issue occurred during the download.  Please try again.")
            _logger.LogError(ex)
            Invoke(New MethodInvoker(AddressOf SaveSettingsJSON))
        Finally
            _logger.Trace("MinerDownloadCompleted: OK.")
        End Try

    End Sub

    Private Sub Client_P2PoolDownloadCompleted(ByVal sender As Object, ByVal e As System.ComponentModel.AsyncCompletedEventArgs)

        Try
            If canceldownloadasync = False Then
                'Download proper miner and extract into respective directories
                zipPath = p2poolfolder & "\p2pool.zip"
                exe = p2poolfolder & "\ocm_p2pool.exe"
                p2pool_config_file = p2poolfolder & "\start_p2pool.bat"
                p2pool_config = "ocm_p2pool.exe" & Environment.NewLine & "exit /B"
                ZipFile.ExtractToDirectory(zipPath, p2poolfolder)
                Dim folders() As String = IO.Directory.GetDirectories(p2poolfolder)
                For Each folder As String In folders
                    Dim files() As String = IO.Directory.GetFiles(folder)
                    For Each file As String In files
                        If file.Contains("run_p2pool") Then
                            My.Computer.FileSystem.MoveFile(file, p2poolfolder & "\" & "ocm_p2pool.exe", True)
                        Else
                            My.Computer.FileSystem.MoveFile(file, p2poolfolder & "\" & System.IO.Path.GetFileName(file), True)
                        End If
                    Next
                    Dim subfolders() As String = IO.Directory.GetDirectories(folder)
                    For Each subfolder As String In subfolders
                        Dim split As String() = subfolder.Split("\")
                        Dim newfolder As String = split(split.Length - 1)
                        My.Computer.FileSystem.MoveDirectory(subfolder, p2poolfolder & "\" & newfolder, True)
                    Next
                    System.IO.Directory.Delete(folder)
                Next
                If System.IO.File.Exists(p2poolfolder & "\Start P2Pool Network 1.bat") = True Then
                    System.IO.File.Delete(p2poolfolder & "\Start P2Pool Network 1.bat")
                End If
                If System.IO.File.Exists(p2poolfolder & "\Start P2Pool Network 2.bat") = True Then
                    System.IO.File.Delete(p2poolfolder & "\Start P2Pool Network 2.bat")
                End If
                'Create default p2pool config
                If System.IO.File.Exists(p2pool_config_file) = False Then
                    Dim objWriter As New System.IO.StreamWriter(p2pool_config_file)
                    objWriter.WriteLine(p2pool_config)
                    objWriter.Close()
                End If
                Invoke(New MethodInvoker(AddressOf Download_P2PoolInterface))
            End If
        Catch ex As Exception
            MsgBox("An issue occurred during the download.  Please try again.")
            _logger.LogError(ex)
            Invoke(New MethodInvoker(AddressOf SaveSettingsJSON))
        Finally
            _logger.Trace("P2PoolDownloadCompleted: OK.")
        End Try

    End Sub

    Private Sub Client_P2PoolInterfaceDownloadCompleted(ByVal sender As Object, ByVal e As System.ComponentModel.AsyncCompletedEventArgs)

        Try
            'Download proper miner and extract into respective directories
            zipPath = p2poolfolder & "\interface.zip"
            ZipFile.ExtractToDirectory(zipPath, p2poolfolder)
            Dim folders() As String = IO.Directory.GetDirectories(p2poolfolder)
            If System.IO.Directory.Exists(p2poolfolder & "\web-static") = True Then
                System.IO.Directory.Delete(p2poolfolder & "\web-static", True)
            End If
            My.Computer.FileSystem.RenameDirectory(p2poolfolder & "\p2pool-ui-punchy-master", "web-static")
            Dim result1 As DialogResult = MsgBox("P2Pool has been installed.", MessageBoxButtons.OK)
            If result1 = DialogResult.OK Then
                progress.Close()
                Invoke(New MethodInvoker(AddressOf Check_RPC_Settings))
                update_complete = True
                BeginInvoke(New MethodInvoker(AddressOf Start_P2Pool))
            End If
            p2pool_version = System.Convert.ToString(newestversion)
        Catch ex As Exception
            MsgBox("An issue occurred during the download.  Please try again.")
            _logger.LogError(ex)
            Invoke(New MethodInvoker(AddressOf SaveSettingsJSON))
        Finally
            _logger.Trace("P2PoolInterfaceDownloadCompleted: OK.")
        End Try

    End Sub

    Public Sub StartWithWindows()

        Dim regKey As Microsoft.Win32.RegistryKey
        Dim KeyName As String = "VertcoinOneClick"
        Dim KeyValue As String = System.Windows.Forms.Application.StartupPath & "\Vertcoin One-Click Miner.exe"
        regKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("Software\Microsoft\Windows\CurrentVersion\Run", True)
        If start_with_windows = True Then
            'create key if it doesn't already exist
            If regKey.GetValue(KeyName) = Nothing Then
                regKey.SetValue(KeyName, KeyValue, Microsoft.Win32.RegistryValueKind.String)
            End If
        Else
            'Delete key if it exists
            If Not regKey.GetValue(KeyName) = Nothing Then
                regKey.DeleteValue(KeyName, False)
            End If
        End If

    End Sub

    Public Sub Uptime_Checker_Status_Text()

        'Miner Info
        If amd_detected = True Or nvidia_detected = True Or cpu_detected = True Then
            If miner_hashrate > 0 Then
                TextBox2.Text = "Running"
            Else
                TextBox2.Text = "Waiting for share"
            End If
            Button3.Text = "Stop"
        Else
            TextBox2.Text = "Offline"
            Button3.Text = "Start"
            TextBox3.Text = "0 kh/s"
        End If
        'P2Pool Info
        If p2pool_detected = True Then
            If p2pool_loaded = True Then
                TextBox1.Text = "Running: Network " & p2pool_network
            Else
                TextBox1.Text = "Loading"
            End If
            CheckBox1.Checked = True
        Else
            TextBox1.Text = "Offline"
            CheckBox1.Checked = False
        End If

    End Sub

    Public Sub Update_Miner_Text()

        ''Miner Info
        'If amd_detected = True Or nvidia_detected = True Or cpu_detected = True Then
        '    If miner_hashrate > 0 Then
        '        TextBox2.Text = "Running"
        '    Else
        '        TextBox2.Text = "Waiting for share"
        '    End If
        '    Button3.Text = "Stop"
        'Else
        '    TextBox2.Text = "Offline"
        '    Button3.Text = "Start"
        'End If
        ''P2Pool Info
        'If p2pool_detected = True Then
        '    If p2pool_loaded = True Then
        '        TextBox1.Text = "Running: Network " & p2pool_network
        '    Else
        '        TextBox1.Text = "Loading"
        '    End If
        '    CheckBox1.Checked = True
        'Else
        '    TextBox1.Text = "Offline"
        '    CheckBox1.Checked = False
        'End If
        'Miner Hashrate
        If api_connected = True Then
            If miner_hashrate < 1000 Then
                miner_hashrate = Math.Round(miner_hashrate, 2)
                TextBox3.Text = miner_hashrate.ToString & " kh/s"
            ElseIf miner_hashrate >= 1000 And miner_hashrate < 1000000 Then
                miner_hashrate = Math.Round((miner_hashrate / 1000), 2)
                TextBox3.Text = miner_hashrate.ToString & " Mh/s"
            ElseIf miner_hashrate >= 1000000 And miner_hashrate < 1000000000 Then
                miner_hashrate = Math.Round((miner_hashrate / 1000000), 2)
                TextBox3.Text = miner_hashrate.ToString & " Gh/s"
            ElseIf miner_hashrate >= 1000000000 And miner_hashrate < 1000000000000 Then
                miner_hashrate = Math.Round((miner_hashrate / 1000000000), 2)
                TextBox3.Text = miner_hashrate.ToString & " Th/s"
            End If
        Else
            TextBox3.Text = "0 kh/s"
        End If

    End Sub

    Public Sub Update_Pool_Info()

        'Miner Info
        DataGridView1.DataSource = Nothing
        DataGridView1.Rows.Clear()
        DataGridView1.Columns.Clear()
        Dim poolcount = pools.Count()
        Dim workercount = workers.Count()
        Dim passwordcount = passwords.Count()
        Dim count As Decimal = Decimal.MaxValue
        count = Math.Min(count, poolcount)
        count = Math.Min(count, workercount)
        count = Math.Min(count, passwordcount)
        Dim poolbuff As New ArrayList
        For Each line As String In pools
            If Not line.Contains("http://") And Not line.Contains("stratum+tcp://") Then
                line = "stratum+tcp://" & line
            Else
                line = line.Replace("http://", "stratum+tcp://")
            End If
            poolbuff.Add(line)
        Next
        pools.Clear()
        pools = poolbuff
        Dim chk As New DataGridViewCheckBoxColumn()
        DataGridView1.Columns.Add(chk)
        chk.HeaderText = "Select"
        chk.Name = "Select"
        DataGridView1.ColumnCount = 4
        With DataGridView1.Columns(1)
            .Name = "Pool"
            .AutoSizeMode = DataGridViewAutoSizeColumnsMode.AllCells
        End With
        With DataGridView1.Columns(2)
            .Name = "Worker"
        End With
        With DataGridView1.Columns(3)
            .Name = "Password"
        End With

        If count > 0 Then
            For x As Integer = 0 To count - 1
                Dim row As String() = New String() {selected(x), pools(x), workers(x), passwords(x)}
                DataGridView1.Rows.Add(row)
            Next
        End If

    End Sub


    Public Sub Update_Settings()

        Try
            'Dim worker = ""
            'Dim pass = ""
            'Dim Old_Settings = File.ReadAllLines(settingsfolder & "\settings.ini")
            'For Each line In Old_Settings
            '    If Not line = "" And line.Contains("Start Minimized=") Then
            '        line = line.Replace("Start Minimized=", "")
            '        If line = "" Or line.Contains("false") Then
            '            start_minimized = "false"
            '        ElseIf line.Contains("true") Then
            '            start_minimized = "true"
            '        End If
            '    End If
            '    If line.Contains("Start With Windows=") Then
            '        line = line.Replace("Start With Windows=", "")
            '        If line = "" Or line.Contains("false") Then
            '            start_with_windows = "false"
            '        ElseIf line.Contains("true") Then
            '            start_with_windows = "true"
            '        End If
            '    End If
            '    If line.Contains("Autostart P2Pool=") Then
            '        line = line.Replace("Autostart P2Pool=", "")
            '        If line = "" Or line.Contains("false") Then
            '            autostart_p2pool = "false"
            '        ElseIf line.Contains("true") Then
            '            autostart_p2pool = "true"
            '        End If
            '    End If
            '    If line.Contains("Autostart Mining=") Then
            '        line = line.Replace("Autostart Mining=", "")
            '        If line = "" Or line.Contains("false") Then
            '            autostart_mining = "false"
            '        ElseIf line.Contains("true") Then
            '            autostart_mining = "true"
            '        End If
            '    End If
            '    If line.Contains("Keep Miner Alive=") Then
            '        line = line.Replace("Keep Miner Alive=", "")
            '        If line = "" Or line.Contains("false") Then
            '            keep_miner_alive = "false"
            '        ElseIf line.Contains("true") Then
            '            keep_miner_alive = "true"
            '        End If
            '    End If
            '    If line.Contains("Keep P2Pool Alive=") Then
            '        line = line.Replace("Keep P2Pool Alive=", "")
            '        If line = "" Or line.Contains("false") Then
            '            keep_p2pool_alive = "false"
            '        ElseIf line.Contains("true") Then
            '            keep_p2pool_alive = "true"
            '        End If
            '    End If
            '    If line.Contains("Use UPnP=") Then
            '        line = line.Replace("Use UPnP=", "")
            '        If line = "" Or line.Contains("false") Then
            '            use_upnp = "false"
            '        ElseIf line.Contains("true") Then
            '            use_upnp = "true"
            '        End If
            '    End If
            '    If line.Contains("P2Pool Network=") Then
            '        line = line.Replace("P2Pool Network=", "")
            '        If line = "" Or line.Contains("1") Then
            '            p2pool_network = 1
            '        ElseIf line.Contains("2") Then
            '            p2pool_network = 2
            '        End If
            '    End If
            '    If line.Contains("P2Pool Node Fee (%)=") Then
            '        line = line.Replace("P2Pool Node Fee (%)=", "")
            '        If Not line = "" And Decimal.TryParse(line, 0.0) = True Then
            '            p2pool_node_fee = Convert.ToDecimal(line)
            '        Else
            '            p2pool_node_fee = 0
            '        End If
            '    End If
            '    If line.Contains("P2Pool Donation (%)=") Then
            '        line = line.Replace("P2Pool Donation (%)=", "")
            '        If Not line = "" And Decimal.TryParse(line, 0.0) = True Then
            '            p2pool_donation = Convert.ToDecimal(line)
            '        Else
            '            p2pool_donation = 0
            '        End If
            '    End If
            '    If line.Contains("Maximum P2Pool Connections=") Then
            '        line = line.Replace("Maximum P2Pool Connections=", "")
            '        If Not line = "" And Integer.TryParse(line, 0) = True Then
            '            max_connections = Convert.ToInt32(line)
            '        Else
            '            max_connections = 0
            '        End If
            '    End If
            '    If line.Contains("P2Pool Port=") Then
            '        line = line.Replace("P2Pool Port=", "")
            '        Dim check As Long
            '        If Not line = "" And Long.TryParse(line, check) = True Then
            '            p2pool_port = line
            '        Else
            '            p2pool_port = "9346"
            '        End If
            '    End If
            '    If line.Contains("Mining Port=") Then
            '        line = line.Replace("Mining Port=", "")
            '        Dim check As Long
            '        If Not line = "" And Long.TryParse(line, check) = True Then
            '            mining_port = line
            '        Else
            '            mining_port = "9171"
            '        End If
            '    End If
            '    If line.Contains("Mining Intensity=") Then
            '        line = line.Replace("Mining Intensity=", "")
            '        If Not line = "" And Decimal.TryParse(line, 0.0) = True Then
            '            mining_intensity = Convert.ToDecimal(line)
            '        Else
            '            mining_intensity = 0
            '        End If
            '    End If
            '    If line.Contains("P2Pool Fee Address=") Then
            '        line = line.Replace("P2Pool Fee Address=", "")
            '        If Not line = "" Then
            '            p2pool_fee_address = line
            '        Else
            '            p2pool_fee_address = "VpBsRnN749jYHE9hT8dZreznHfmFMdE1yG"
            '        End If
            '    End If
            '    If line.Contains("Pool URL=") Then
            '        line = line.Replace("Pool URL=", "")
            '        If Not line = "" Then
            '            If Not line.Contains("http://") And Not line.Contains("stratum+tcp://") Then
            '                line = "stratum+tcp://" & line
            '            Else
            '                line = line.Replace("http://", "stratum+tcp://")
            '            End If
            '            pools.Add(line)
            '            workers.Add(worker)
            '            passwords.Add(pass)
            '            BeginInvoke(New MethodInvoker(AddressOf Update_Miner_Text))
            '        End If
            '    End If
            '    If line.Contains("P2Pool Version=") Then
            '        line = line.Replace("P2Pool Version=", "")
            '        Dim check As Version = Version.Parse("0.0.0.0")
            '        If Not line = "" And Version.TryParse(line, check) = True Then
            '            p2pool_version = line
            '        End If
            '    End If
            '    If line.Contains("AMD Miner Version=") Then
            '        line = line.Replace("AMD Miner Version=", "")
            '        Dim check As Version = Version.Parse("0.0.0.0")
            '        If Not line = "" And Version.TryParse(line, check) = True Then
            '            amd_version = line
            '        End If
            '    End If
            '    If line.Contains("Nvidia Miner Version=") Then
            '        line = line.Replace("Nvidia Miner Version=", "")
            '        Dim check As Version = Version.Parse("0.0.0.0")
            '        If Not line = "" And Version.TryParse(line, check) = True Then
            '            nvidia_version = line
            '        End If
            '    End If
            '    If line.Contains("CPU Miner Version=") Then
            '        line = line.Replace("CPU Miner Version=", "")
            '        Dim check As Version = Version.Parse("0.0.0.0")
            '        If Not line = "" And Version.TryParse(line, check) = True Then
            '            cpu_version = line
            '        End If
            '    End If
            '    If line.Contains("Default Miner=") Then
            '        line = line.Replace("Default Miner=", "")
            '        If Not line = "" Then
            '            default_miner = line
            '        End If
            '    End If
            '    If line.Contains("Worker Name=") Then
            '        line = line.Replace("Worker Name=", "")
            '        If Not line = "" Then
            '            worker = line
            '        End If
            '    End If
            '    If line.Contains("Worker Password=") Then
            '        line = line.Replace("Worker Password=", "")
            '        If Not line = "" Then
            '            pass = line
            '        End If
            '    End If
            'Next
            'Dim newjson As Settings_JSON = New Settings_JSON()
            'newjson.appdata = appdata
            'newjson.start_minimized = start_minimized
            'newjson.start_with_windows = start_with_windows
            'newjson.autostart_p2pool = autostart_p2pool
            'newjson.autostart_mining = autostart_mining
            'newjson.keep_miner_alive = keep_miner_alive
            'newjson.keep_p2pool_alive = keep_p2pool_alive
            'newjson.use_upnp = use_upnp
            'newjson.p2pool_network = p2pool_network
            'newjson.p2pool_node_fee = p2pool_node_fee
            'newjson.p2pool_donation = p2pool_donation
            'newjson.max_connections = max_connections
            'newjson.p2pool_port = p2pool_port
            'newjson.mining_port = mining_port
            'newjson.mining_intensity = mining_intensity
            'newjson.p2pool_fee_address = p2pool_fee_address
            'newjson.p2pool_version = p2pool_version
            'newjson.amd_version = amd_version
            'newjson.nvidia_version = nvidia_version
            'newjson.cpu_version = cpu_version
            'newjson.default_miner = default_miner
            'newjson.pools.Clear()
            'Dim poolcount = pools.Count()
            'Dim workercount = workers.Count()
            'Dim passwordcount = passwords.Count()
            'Dim count As Decimal = Decimal.MaxValue
            'count = Math.Min(count, poolcount)
            'count = Math.Min(count, workercount)
            'count = Math.Min(count, passwordcount)
            'If Not count = 0 Then
            '    For x = 0 To count - 1
            '        If Not pools(x) = "" And Not workers(x) = "" And Not passwords(x) = "" Then
            '            Dim pooljson As Pools_JSON = New Pools_JSON()
            '            pooljson.url = pools(x)
            '            pooljson.user = workers(x)
            '            pooljson.pass = passwords(x)
            '            newjson.pools.Add(pooljson)
            '        End If
            '    Next
            'End If
            'Dim jsonstring = JSONConverter.Serialize(newjson)
            'Dim jsonFormatted As String = JValue.Parse(jsonstring).ToString(Formatting.Indented)
            'File.WriteAllText(settingsfile, jsonFormatted)
            System.IO.File.Delete(settingsfolder & "\settings.ini")
        Catch ex As IOException
            _logger.LogError(ex)
            Invoke(New MethodInvoker(AddressOf SaveSettingsJSON))
        Finally
            _logger.Trace("UpdateSettings: OK.")
        End Try

    End Sub

    Public Sub LoadSettingsJSON()

        Try
            Dim settingsJSON As Settings_JSON = New Settings_JSON()
            Dim settings_string As String = File.ReadAllText(settingsfile)
            settingsJSON = JSONConverter.Deserialize(Of Settings_JSON)(settings_string)
            appdata = settingsJSON.appdata
            start_minimized = settingsJSON.start_minimized
            start_with_windows = settingsJSON.start_with_windows
            autostart_p2pool = settingsJSON.autostart_p2pool
            autostart_mining = settingsJSON.autostart_mining
            keep_miner_alive = settingsJSON.keep_miner_alive
            keep_p2pool_alive = settingsJSON.keep_p2pool_alive
            use_upnp = settingsJSON.use_upnp
            show_cli = settingsJSON.show_cli
            p2pool_network = settingsJSON.p2pool_network
            p2pool_node_fee = settingsJSON.p2pool_node_fee
            p2pool_donation = settingsJSON.p2pool_donation
            max_connections = settingsJSON.max_connections
            p2pool_port = settingsJSON.p2pool_port
            mining_port = settingsJSON.mining_port
            mining_intensity = settingsJSON.mining_intensity
            p2pool_fee_address = settingsJSON.p2pool_fee_address
            p2pool_version = settingsJSON.p2pool_version
            amd_version = settingsJSON.amd_version
            nvidia_version = settingsJSON.nvidia_version
            cpu_version = settingsJSON.cpu_version
            default_miner = settingsJSON.default_miner
            devices = settingsJSON.devices
            pools.Clear()
            workers.Clear()
            passwords.Clear()
            selected.Clear()
            Dim count = settingsJSON.pools.Count
            If Not count = 0 Then
                For x = 0 To count - 1
                    If Not settingsJSON.pools(x).url = "" And Not settingsJSON.pools(x).user = "" And Not settingsJSON.pools(x).pass = "" Then
                        Dim jsonstring = JSONConverter.Serialize(settingsJSON.pools(x))
                        Dim poolJSON = JSONConverter.Deserialize(Of Pools_JSON)(jsonstring)
                        pools.Add(poolJSON.url)
                        workers.Add(poolJSON.user)
                        passwords.Add(poolJSON.pass)
                        selected.Add(poolJSON.checked)
                    End If
                Next
            End If
            Invoke(New MethodInvoker(AddressOf Update_Pool_Info))
        Catch ex As IOException
            _logger.LogError(ex)
        Finally
            _logger.Trace("LoadSettings: OK.")
        End Try

    End Sub

    Public Sub SaveSettingsJSON()

        Try
            pools.Clear()
            workers.Clear()
            passwords.Clear()
            selected.Clear()
            For Each row As DataGridViewRow In DataGridView1.Rows
                Dim chk As DataGridViewCheckBoxCell = row.Cells(DataGridView1.Columns(0).Name)
                If chk.Value IsNot Nothing Then
                    pools.Add(DataGridView1.Rows(chk.RowIndex).Cells(1).Value)
                    workers.Add(DataGridView1.Rows(chk.RowIndex).Cells(2).Value)
                    passwords.Add(DataGridView1.Rows(chk.RowIndex).Cells(3).Value)
                    If chk.Value = False Then
                        selected.Add(False)
                    Else
                        selected.Add(True)
                    End If
                End If
            Next
            Dim newjson As Settings_JSON = New Settings_JSON()
            newjson.appdata = appdata
            newjson.start_minimized = start_minimized
            newjson.start_with_windows = start_with_windows
            newjson.autostart_p2pool = autostart_p2pool
            newjson.autostart_mining = autostart_mining
            newjson.keep_miner_alive = keep_miner_alive
            newjson.keep_p2pool_alive = keep_p2pool_alive
            newjson.use_upnp = use_upnp
            newjson.show_cli = show_cli
            newjson.p2pool_network = p2pool_network
            newjson.p2pool_node_fee = p2pool_node_fee
            newjson.p2pool_donation = p2pool_donation
            newjson.max_connections = max_connections
            newjson.p2pool_port = p2pool_port
            newjson.mining_port = mining_port
            newjson.mining_intensity = mining_intensity
            newjson.p2pool_fee_address = p2pool_fee_address
            newjson.p2pool_version = p2pool_version
            newjson.amd_version = amd_version
            newjson.nvidia_version = nvidia_version
            newjson.cpu_version = cpu_version
            newjson.default_miner = default_miner
            newjson.devices = devices
            newjson.pools.Clear()
            Dim poolcount = pools.Count()
            Dim workercount = workers.Count()
            Dim passwordcount = passwords.Count()
            Dim count As Decimal = Decimal.MaxValue
            count = Math.Min(count, poolcount)
            count = Math.Min(count, workercount)
            count = Math.Min(count, passwordcount)
            If Not count = 0 Then
                For x = 0 To count - 1
                    If Not pools(x) = "" And Not workers(x) = "" And Not passwords(x) = "" Then
                        Dim pooljson As Pools_JSON = New Pools_JSON()
                        pooljson.url = pools(x)
                        pooljson.user = workers(x)
                        pooljson.pass = passwords(x)
                        pooljson.checked = selected(x)
                        newjson.pools.Add(pooljson)
                    End If
                Next
            End If
            Dim jsonstring = JSONConverter.Serialize(newjson)
            Dim jsonFormatted As String = JValue.Parse(jsonstring).ToString(Formatting.Indented)
            File.WriteAllText(settingsfile, jsonFormatted)
        Catch ex As IOException
            _logger.LogError(ex)
        Finally
            _logger.Trace("SaveSettings: OK.")
        End Try

    End Sub

    Private Sub CheckBox1_CheckedChanged(sender As Object, e As EventArgs) Handles CheckBox1.CheckedChanged

        If p2pool_network = "1" Then
            If mining_port = "9181" Or mining_port = "" Then
                mining_port = "9171"
            End If
        ElseIf p2pool_network = "2" Then
            If mining_port = "9171" Or mining_port = "" Then
                mining_port = "9181"
            End If
        End If
        Dim pool_list = String.Join(",", pools.ToArray())
        If CheckBox1.Checked = True Then
            If Not pool_list.Contains("stratum+tcp://localhost:" & mining_port) Then
                dim dialog = new AddPool(_logger)
                dialog.Show()
                dialog.Pool_Address.Text = "stratum+tcp://localhost:" & mining_port
            End If
        End If
        'See if P2Pool has already been downloaded/installed
        If System.IO.Directory.Exists(p2poolfolder) = True Then
            For Each file As String In Directory.GetFiles(p2poolfolder)
                If file.Contains("ocm_p2pool.exe") And Not (System.Version.Parse(p2pool_version) = System.Version.Parse("0.0.0.0")) Then
                    p2pool_installed = True
                    Exit For
                Else
                    p2pool_installed = False
                End If
            Next
        End If
        'Starts p2pool if p2pool software is already detected.  If not, downloads p2pool software.
        If CheckBox1.Checked = True Then
            If p2pool_installed = True Then
                Invoke(New MethodInvoker(AddressOf Check_RPC_Settings))
                If p2pool_initialized = True Then
                    BeginInvoke(New MethodInvoker(AddressOf Start_P2Pool))
                End If
            Else
                If Not Updater.IsBusy Then
                    p2pool_update = True
                    canceldownloadasync = False
                    Updater.RunWorkerAsync()
                End If
            End If
        Else
            p2pool_initialized = False
            BeginInvoke(New MethodInvoker(AddressOf Kill_P2Pool))
        End If
        p2pool_installed = False

    End Sub

    Private Sub EditToolStripMenuItem_Click(sender As Object, e As EventArgs) Handles EditToolStripMenuItem.Click

        dim dialog = New settings(_logger)
        dialog.Show()

    End Sub

    Public Sub Update_P2Pool_Config()

        Try
            exe = settingsfolder & "\p2pool\ocm_p2pool.exe"
            p2pool_config_file = settingsfolder & "\p2pool\start_p2pool.bat"
            Dim network As String = ""
            If p2pool_network = "0" Or p2pool_network = "1" Then
                network = " --net vertcoin"
                'Allows any custom port other than default ports.
                If p2pool_port = "9347" Then
                    p2pool_port = "9346"
                End If
                If mining_port = "9181" Then
                    mining_port = "9171"
                End If
            ElseIf p2pool_network = "2" Then
                network = " --net vertcoin2"
                'Allows any custom port other than default ports.
                If p2pool_port = "9346" Then
                    p2pool_port = "9347"
                End If
                If mining_port = "9171" Then
                    mining_port = "9181"
                End If
            End If
            If Not appdata.Contains("AppData") Then
                p2pool_config = "ocm_p2pool.exe" & network & " --give-author " & p2pool_donation & " --fee " & p2pool_node_fee & " --address " & p2pool_fee_address & " --max-conns " & max_connections & " --worker-port " & mining_port & " --p2pool-port " & p2pool_port & " --bitcoind-config-path " & appdata & "\vertcoin.conf" & Environment.NewLine & "exit /B"
            Else
                p2pool_config = "ocm_p2pool.exe" & network & " --give-author " & p2pool_donation & " --fee " & p2pool_node_fee & " --address " & p2pool_fee_address & " --max-conns " & max_connections & " --worker-port " & mining_port & " --p2pool-port " & p2pool_port & Environment.NewLine & "exit /B"
            End If
            If System.IO.File.Exists(p2pool_config_file) = True Then
                command = File.ReadAllText(p2pool_config_file)
            End If
            If Not p2pool_config = command Then
                command = p2pool_config
                File.WriteAllText(p2pool_config_file, command)
            End If
        Catch ex As Exception
            MsgBox(ex.Message)
            _logger.LogError(ex)
            Invoke(New MethodInvoker(AddressOf SaveSettingsJSON))
        Finally
            _logger.Trace("Update_P2Pool_Config: OK.")
        End Try

    End Sub

    Public Sub Update_Miner_Config()

        Try
            'JSON Configuration
            pools.Clear()
            workers.Clear()
            passwords.Clear()
            Dim newjson
            Dim jsonstring As String
            For Each row As DataGridViewRow In DataGridView1.Rows
                Dim chk As DataGridViewCheckBoxCell = row.Cells(DataGridView1.Columns(0).Name)
                If chk.Value IsNot Nothing AndAlso chk.Value = True Then 'add AndAlso chk.Value = True to only add pools that are checked
                    pools.Add(DataGridView1.Rows(chk.RowIndex).Cells(1).Value)
                    workers.Add(DataGridView1.Rows(chk.RowIndex).Cells(2).Value)
                    passwords.Add(DataGridView1.Rows(chk.RowIndex).Cells(3).Value)
                End If
            Next
            Dim poolcount = pools.Count()
            Dim workercount = workers.Count()
            Dim passwordcount = passwords.Count()
            Dim count As Decimal = Decimal.MaxValue
            count = Math.Min(count, poolcount)
            count = Math.Min(count, workercount)
            count = Math.Min(count, passwordcount)
            If amdminer = True Then
                newjson = New AMD_Miner_Settings_JSON()
                minersettingsfile = amdfolder & "\sgminer.conf"
                'For x As Integer = count - 1 To 0 Step -1
                Dim pooljson As AMD_Pools_JSON = New AMD_Pools_JSON()
                'pooljson.url = pools(x)
                'pooljson.user = workers(x)
                'pooljson.pass = passwords(x)
                'Until amd support is ready in vertminer, sgminer will not allow failover pool support. Use first selected pool.
                If count > 0 Then
                        pooljson.url = pools(0)
                        pooljson.user = workers(0)
                        pooljson.pass = passwords(0)
                    End If
                    newjson.pools.Add(pooljson)
                'Next
                newjson.algorithm = "Lyra2REv2"
                newjson.intensity = mining_intensity
                newjson.device = devices
                jsonstring = JSONConverter.Serialize(newjson)
                jsonstring = jsonstring.Insert(jsonstring.Length - 1, ",""no-extranonce""" & ": " & "true")
            ElseIf nvidiaminer = True Then
                newjson = New NVIDIA_Miner_Settings_JSON()
                minersettingsfile = nvidiafolder & "\vertminer.conf"
                newjson.algo = "lyra2v2"
                newjson.intensity = mining_intensity
                newjson.devices = devices
                For x As Integer = count - 1 To 0 Step -1
                    Dim pooljson As NVIDIA_Pools_JSON = New NVIDIA_Pools_JSON()
                    pooljson.url = pools(x)
                    pooljson.user = workers(x)
                    pooljson.pass = passwords(x)
                    newjson.pools.Add(pooljson)
                Next
                jsonstring = JSONConverter.Serialize(newjson)
            ElseIf cpuminer = True Then
                newjson = New CPU_Miner_Settings_JSON()
                minersettingsfile = cpufolder & "\cpuminer-conf.json"
                'No failover pool support in cpuminer. Use first selected pool.
                If count > 0 Then
                    newjson.url = pools(0)
                    newjson.user = workers(0)
                    newjson.pass = passwords(0)
                End If
                newjson.algo = "lyra2rev2"
                newjson.intensity = mining_intensity
                jsonstring = JSONConverter.Serialize(newjson)
            End If
            Dim jsonFormatted As String = JValue.Parse(jsonstring).ToString(Formatting.Indented)
            File.WriteAllText(minersettingsfile, jsonFormatted)
        Catch ex As Exception
            MsgBox(ex.Message)
            _logger.LogError(ex)
            Invoke(New MethodInvoker(AddressOf SaveSettingsJSON))
        Finally
            _logger.Trace("Update_Miner_Config: OK.")
        End Try

    End Sub

    Public Sub Start_Miner()

        Try
            Dim poolcount = pools.Count()
            Dim workercount = workers.Count()
            Dim passwordcount = passwords.Count()
            Dim count As Decimal = Decimal.MaxValue
            count = Math.Min(count, poolcount)
            count = Math.Min(count, workercount)
            count = Math.Min(count, passwordcount)
            If count > 0 Then
                Invoke(New MethodInvoker(AddressOf Update_Miner_Config))
            End If
            If amd_detected = True Or nvidia_detected = True Or cpu_detected = True Then
                ' Process is running
            Else
                ' Process is not running
                'JSON Configuration
                Dim psi As New ProcessStartInfo
                If amdminer = True Then
                    'miner_config = amdfolder & "\ocm_sgminer.exe"
                    psi = New ProcessStartInfo("cmd")
                    psi.Arguments = ("/K cd /d" & amdfolder & " & " & "setx GPU_MAX_ALLOC_PERCENT 100" & " & " & "setx GPU_SINGLE_ALLOC_PERCENT 100" & " & " & "ocm_sgminer.exe --api-listen --config " & "sgminer.conf" & " & " & " exit /B")
                ElseIf nvidiaminer = True Then
                    'miner_config = nvidiafolder & "\ocm_vertminer.exe"
                    psi = New ProcessStartInfo(nvidiafolder & "\ocm_vertminer.exe")
                ElseIf cpuminer = True Then
                    'miner_config = cpufolder & "\ocm_cpuminer.exe"
                    psi = New ProcessStartInfo(cpufolder & "\ocm_cpuminer.exe")
                End If
                mining_running = True
                'Dim psi As New ProcessStartInfo(miner_config)
                If show_cli = True Then
                    psi.CreateNoWindow = False
                    psi.UseShellExecute = True
                Else
                    psi.CreateNoWindow = True
                    psi.UseShellExecute = False
                End If
                Process.Start(psi)
            End If
        Catch ex As Exception
            _logger.LogError(ex)
        Finally
            _logger.Trace("Start_Miner: OK.")
        End Try

    End Sub

    Public Sub Stop_Miner()

        Button3.Text = "Start"
        miner_hashrate = 0

    End Sub

    Public Sub Stop_P2Pool()

        CheckBox1.Checked = False
        TextBox1.Text = "Offline"

    End Sub

    Public Sub Kill_Miner()

        Try
            mining_running = False
            For Each p As Process In System.Diagnostics.Process.GetProcesses
                If p.ProcessName.Contains("ocm_vertminer") Or p.ProcessName.Contains("ocm_sgminer") Or p.ProcessName.Contains("ocm_cpuminer") Then
                    p.Kill()
                End If
            Next
            miner_hashrate = 0
        Catch ex As Exception
            _logger.LogError(ex)
            Invoke(New MethodInvoker(AddressOf SaveSettingsJSON))
        Finally
            _logger.Trace("Kill_Miner: OK.")
        End Try

    End Sub

    Public Sub Start_P2Pool()

        Try
            Invoke(New MethodInvoker(AddressOf Update_P2Pool_Config))
            Invoke(New MethodInvoker(AddressOf SaveSettingsJSON))
            If use_upnp = True Then
                Invoke(New MethodInvoker(AddressOf UPnP))
            End If
            If p2pool_detected = True Then
                ' Process is running
                'MsgBox("P2Pool is already running.")
            Else
                ' Process is not running
                Dim psi As New ProcessStartInfo("cmd")
                If show_cli = True Then
                    psi.CreateNoWindow = False
                    psi.UseShellExecute = True
                Else
                    psi.CreateNoWindow = True
                    psi.UseShellExecute = False
                End If
                command = File.ReadAllText(p2pool_config_file)
                command = command.Replace(Environment.NewLine, " & ")
                psi.Arguments = ("/K cd /d" & p2poolfolder & " & " & command)
                Process.Start(psi)
            End If
        Catch ex As Exception
            _logger.LogError(ex)
            Invoke(New MethodInvoker(AddressOf SaveSettingsJSON))
        Finally
            _logger.Trace("Start_P2Pool: OK.")
        End Try

    End Sub

    Public Sub Kill_P2Pool()

        Try
            p2pool_running = False
            For Each p As Process In System.Diagnostics.Process.GetProcesses
                If p.ProcessName.Contains("ocm_p2pool") Then
                    p.Kill()
                End If
            Next
        Catch ex As Exception
            _logger.LogError(ex)
            Invoke(New MethodInvoker(AddressOf SaveSettingsJSON))
        Finally
            _logger.Trace("Kill_P2Pool: OK.")
        End Try

    End Sub

    Private Sub UpdateInterval_Tick(sender As Object, e As EventArgs) Handles UpdateStatsInterval.Tick

        'Updates stats every X seconds
        If Not (UpdateStats.IsBusy) Then
            UpdateStats.RunWorkerAsync()
        End If
        'Automatically checks for updates after launch and prompts user via label on top-right main screen.
        If autocheck_updates = False Then
            Auto_Update_Notify.RunWorkerAsync()
            autocheck_updates = True
        End If

    End Sub

    Private Sub Auto_Update_Notify_DoWork(sender As Object, e As System.ComponentModel.DoWorkEventArgs) Handles Auto_Update_Notify.DoWork

        Dim url As New System.Uri("http://vtc.alwayshashing.com")
        Dim connection
        'Request connection
        Dim req As System.Net.WebRequest
        req = System.Net.WebRequest.Create(url)
        Dim resp As System.Net.WebResponse
        Try
            resp = req.GetResponse()
            resp.Close()
            req = Nothing
            connection = True
        Catch ex As Exception
            req = Nothing
            connection = False
        End Try
        If connection = True Then
            Dim tempnewestversion As New Version
            Dim templink As String = ""
            Dim request As System.Net.HttpWebRequest = System.Net.HttpWebRequest.Create("http://alwayshashing.com/ocm_update.txt")
            Dim response As System.Net.HttpWebResponse = request.GetResponse()
            Dim sr As System.IO.StreamReader = New System.IO.StreamReader(response.GetResponseStream())
            'Compares current One-Click Miner version with the latest available.
            tempnewestversion = System.Version.Parse(sr.ReadLine.Replace("miner=", ""))
            templink = sr.ReadLine
            If tempnewestversion > System.Version.Parse(miner_version) Then
                update_needed = True
            End If
            'Compares the current version of P2Pool with the latest available.
            tempnewestversion = System.Version.Parse(sr.ReadLine.Replace("p2pool=", ""))
            templink = sr.ReadLine
            If (tempnewestversion > System.Version.Parse(p2pool_version)) And Not (System.Version.Parse(p2pool_version) = System.Version.Parse("0.0.0.0")) Then
                update_needed = True
            End If
            'Compares the current version of the AMD miner with the latest available.
            tempnewestversion = System.Version.Parse(sr.ReadLine.Replace("amd=", ""))
            templink = sr.ReadLine
            If (tempnewestversion > System.Version.Parse(amd_version)) And Not (System.Version.Parse(amd_version) = System.Version.Parse("0.0.0.0")) And (System.IO.File.Exists(amdfolder & "\ocm_sgminer.exe") = True) Then
                update_needed = True
            End If
            'Compares the current version of the Nvidia miner with the latest available.
            tempnewestversion = System.Version.Parse(sr.ReadLine.Replace("nvidia=", ""))
            templink = sr.ReadLine
            If (tempnewestversion > System.Version.Parse(nvidia_version)) And Not (System.Version.Parse(nvidia_version) = System.Version.Parse("0.0.0.0")) And (System.IO.File.Exists(nvidiafolder & "\ocm_vertminer.exe") = True) Then
                update_needed = True
            End If
            'Compares the current version of the CPU miner with the latest available.
            tempnewestversion = System.Version.Parse(sr.ReadLine.Replace("cpu=", ""))
            templink = sr.ReadLine
            If (tempnewestversion > System.Version.Parse(cpu_version)) And Not (System.Version.Parse(cpu_version) = System.Version.Parse("0.0.0.0")) And (System.IO.File.Exists(cpufolder & "\ocm_cpuminer.exe") = True) Then
                update_needed = True
            End If
            Invoke(New MethodInvoker(AddressOf Update_Notification))
            response.Dispose()
            sr.Dispose()
        End If
        Auto_Update_Notify.CancelAsync()

    End Sub

    Public Sub Update_Notification()

        If update_needed = True Then
            Label7.Enabled = True
            Label7.Visible = True
        Else
            Label7.Enabled = False
            Label7.Visible = False
        End If
        update_needed = False

    End Sub

    Private Sub UpdateStats_DoWork(sender As Object, e As System.ComponentModel.DoWorkEventArgs) Handles UpdateStats.DoWork

        Try
            'Miner API
            System.Threading.Thread.CurrentThread.CurrentCulture = New CultureInfo("en-US")
            System.Threading.Thread.CurrentThread.CurrentUICulture = System.Threading.Thread.CurrentThread.CurrentCulture
            If amd_detected = True Or nvidia_detected = True Or cpu_detected = True Then
                Dim tcpClient As New System.Net.Sockets.TcpClient()
                If amd_detected = True Then
                    tcpClient.Connect("127.0.0.1", 4028)
                ElseIf nvidia_detected = True Then
                    tcpClient.Connect("127.0.0.1", 4068)
                ElseIf cpu_detected = True Then
                    tcpClient.Connect("127.0.0.1", 4048)
                End If
                Dim RawData() As String
                Dim networkStream As NetworkStream = tcpClient.GetStream()
                If networkStream.CanWrite And networkStream.CanRead Then
                    api_connected = True
                    Dim sendBytes As [Byte]() = Encoding.ASCII.GetBytes("summary")
                    networkStream.Write(sendBytes, 0, sendBytes.Length)
                    Dim bytes(tcpClient.ReceiveBufferSize) As Byte
                    networkStream.Read(bytes, 0, CInt(tcpClient.ReceiveBufferSize))
                    Dim returndata As String = Encoding.ASCII.GetString(bytes)
                    If amd_detected = True Then
                        RawData = returndata.Split(",")
                    Else
                        RawData = returndata.Split(";")
                    End If
                    For Each line As String In RawData
                        If line.Contains("KHS=") And Not line.Contains("NETKHS=") Then
                            miner_hashrate = Convert.ToDecimal((line.Replace("KHS=", "")))
                        ElseIf line.Contains("KHS av=") And Not line.Contains("NETKHS=") Then
                            miner_hashrate = Convert.ToDecimal((line.Replace("KHS av=", "")))
                        End If
                    Next
                Else
                    If Not networkStream.CanRead Or Not networkStream.CanWrite Then
                        MsgBox("Miner API is refusing connection.")
                        tcpClient.Close()
                        networkStream.Close()
                    End If
                End If
                tcpClient.Close()
                networkStream.Close()
                BeginInvoke(New MethodInvoker(AddressOf Update_Miner_Text))
            End If
            'P2Pool API
            If p2pool_detected = True Then
                Dim url As New System.Uri("http://localhost:" & mining_port & "/rate")
                Dim cancel = False
                'Request for connection
                Dim req As System.Net.WebRequest
                req = System.Net.WebRequest.Create(url)
                Dim resp As System.Net.WebResponse
                Try
                    resp = req.GetResponse()
                    resp.Close()
                    req = Nothing
                    p2pool_loaded = True
                Catch ex As Exception
                    req = Nothing
                    p2pool_loaded = False
                End Try
            End If
            'Invoke(New MethodInvoker(AddressOf Uptime_Checker_Status_Text))
        Catch ex As Exception
            Invoke(New MethodInvoker(AddressOf SaveSettingsJSON))
        Finally
        End Try

    End Sub

    Private Sub FileDirectoryToolStripMenuItem_Click(sender As Object, e As EventArgs) Handles FileDirectoryToolStripMenuItem.Click

        Try
            If default_miner = "amd" Then
                miner_config_file = settingsfolder & "\amd\sgminer.conf"
            ElseIf default_miner = "nvidia" Then
                miner_config_file = settingsfolder & "\nvidia\vertminer.conf"
            ElseIf default_miner = "cpu" Then
                miner_config_file = settingsfolder & "\cpu\cpuminer-conf.json"
            End If
            If System.IO.File.Exists(miner_config_file) = True Then
                Process.Start("notepad.exe", miner_config_file)
            Else
                MsgBox("No miner config file found.")
            End If
        Catch ex As Exception
            MsgBox(ex.Message)
            _logger.LogError(ex)
            Invoke(New MethodInvoker(AddressOf SaveSettingsJSON))
        Finally
            _logger.Trace("MinerConfigToolStripMenuItem(), Load Miner Config: OK")
        End Try

    End Sub

    Private Sub SystemLogToolStripMenuItem_Click(sender As Object, e As EventArgs) Handles SystemLogToolStripMenuItem.Click

        Try
            Process.Start("notepad.exe", syslog)
        Catch ex As Exception
            MsgBox(ex.Message)
            _logger.LogError(ex)
            Invoke(New MethodInvoker(AddressOf SaveSettingsJSON))
        Finally
            _logger.Trace("SystemLogToolStripMenuItem(), Load Miner Log: OK")
        End Try

    End Sub

    Private Sub P2PoolConfigToolStripMenuItem_Click(sender As Object, e As EventArgs) Handles P2PoolConfigToolStripMenuItem.Click

        Try
            p2pool_config_file = settingsfolder & "\p2pool\start_p2pool.bat"
            If System.IO.File.Exists(p2pool_config_file) = True Then
                Process.Start("notepad.exe", p2pool_config_file)
            Else
                MsgBox("No p2pool config file found.")
            End If
        Catch ex As Exception
            MsgBox(ex.Message)
            _logger.LogError(ex)
            Invoke(New MethodInvoker(AddressOf SaveSettingsJSON))
        Finally
            _logger.Trace("P2PoolConfigToolStripMenuItem(), Load P2Pool Config: OK")
        End Try

    End Sub

    Private Sub AboutToolStripMenuItem_Click(sender As Object, e As EventArgs) Handles AboutToolStripMenuItem.Click

        dim dialog = new about(_logger)
        dialog.Show()

    End Sub

    Private Sub ContactToolStripMenuItem_Click(sender As Object, e As EventArgs) Handles ContactToolStripMenuItem.Click

        Try
            Dim url As String = "http://slack.vtconline.org"
            Process.Start(url)
        Catch ex As Exception
            MsgBox(ex.Message)
            _logger.LogError(ex)
            Invoke(New MethodInvoker(AddressOf SaveSettingsJSON))
        Finally
            _logger.Trace("Contact(), Load Browser: OK")
        End Try

    End Sub

    Private Sub ExitToolStripMenuItem_Click(sender As Object, e As EventArgs) Handles ExitToolStripMenuItem.Click

        Application.Exit()

    End Sub

    Private Sub Uptime_Timer_Tick(sender As Object, e As EventArgs) Handles Uptime_Timer.Tick

        'Checks to ensure p2pool and miner are always running.
        If Not (Uptime_Checker.IsBusy) Then
            Uptime_Checker.RunWorkerAsync()
        End If

    End Sub

    Private Sub Uptime_Checker_DoWork(sender As Object, e As System.ComponentModel.DoWorkEventArgs) Handles Uptime_Checker.DoWork

        Try
            Invoke(New MethodInvoker(AddressOf Process_Check))
            If (amd_detected = True Or nvidia_detected = True Or cpu_detected = True) And p2pool_detected = True Then
                'P2Pool and Miner are running, do nothing
            Else
                If mining_running = True Then 'Miner initialized
                    If amd_detected = False And nvidia_detected = False And cpu_detected = False Then
                        If keep_miner_alive = True Then
                            BeginInvoke(New MethodInvoker(AddressOf Start_Miner))
                        Else
                            BeginInvoke(New MethodInvoker(AddressOf Stop_Miner))
                        End If
                    End If
                Else 'Miner not initialized
                End If
                If p2pool_running = True Then 'P2Pool initialized
                    If p2pool_detected = False Then
                        If keep_p2pool_alive = True Then
                            BeginInvoke(New MethodInvoker(AddressOf Start_P2Pool))
                        End If
                    End If
                Else 'P2Pool not initialized
                End If
            End If
            BeginInvoke(New MethodInvoker(AddressOf Uptime_Checker_Status_Text))
            Uptime_Checker.CancelAsync()
        Catch ex As Exception
        End Try

    End Sub

    Public Sub Check_RPC_Settings()

        Try
            'Check for RPC information from Vertcoin Wallet
            If appdata = "" Then
                appdata = GetFolderPath(SpecialFolder.ApplicationData) & "\Vertcoin"
            End If
            Dim conf_check As Boolean = False
            Dim cancel As Boolean = False
            Dim buffer As String = ""
            If System.IO.Directory.Exists(appdata) = False Then
                Dim result1 As DialogResult = MsgBox("The default Vertcoin data directory was not found.  Would you like to select where it can be found?", MessageBoxButtons.OKCancel)
                If result1 = DialogResult.OK Then
                    Dim result2 As Windows.Forms.DialogResult = Select_Data_Dir.ShowDialog()
                    If result2 = Windows.Forms.DialogResult.OK Then
                        appdata = Select_Data_Dir.SelectedPath
                        p2pool_initialized = True
                    ElseIf result2 = Windows.Forms.DialogResult.Cancel Then
                        cancel = True
                    End If
                Else
                    cancel = True
                End If
            End If
            If cancel = False Then
                Dim files() As String = IO.Directory.GetFiles(appdata)
                For Each file As String In files
                    If conf_check = False Then
                        If file.Contains("vertcoin.conf") Then
                            Using reader As New System.IO.StreamReader(file)
                                While Not reader.EndOfStream
                                    buffer = reader.ReadLine
                                    If buffer.Contains("rpcuser") Then
                                        rpc_user = buffer.Replace("rpcuser=", "")
                                    ElseIf buffer.Contains("rpcpassword") Then
                                        rpc_password = buffer.Replace("rpcpassword=", "")
                                    ElseIf buffer.Contains("rpcport") Then
                                        rpc_port = buffer.Replace("rpcport=", "")
                                    ElseIf buffer.Contains("server=0") Then 'forces server=1
                                        config_server = ""
                                    ElseIf buffer.Contains("server=1") Then
                                        config_server = "server=1"
                                    ElseIf buffer = "server=" Then 'forces server=1
                                        config_server = ""
                                    ElseIf buffer.Contains("rpcallowip=") Then
                                        rpc_allowip = buffer.Replace("rpcallowip=", "")
                                    End If
                                End While
                            End Using
                            conf_check = True
                        End If
                    End If
                Next
                If (rpc_user = "" Or rpc_password = "" Or rpc_port = "" Or config_server = "" Or rpc_allowip = "") And conf_check = True Then
                    Dim result1 As DialogResult = MsgBox("RPC setting(s) not found in wallet configuration file. Would you like Vertcoin One-Click Miner to generate and append RPC settings to configuration file?", MessageBoxButtons.OKCancel)
                    If result1 = DialogResult.OK Then
                        MsgBox("If your Vertcoin wallet is currently running, please close it and click OK.")
                        Invoke(New MethodInvoker(AddressOf Generate_RPC_Settings))
                        Dim do_once_config_server As Boolean = False
                        Dim do_once_rpcallow As Boolean = False
                        Dim do_once_rpcuser As Boolean = False
                        Dim do_once_rpcpassword As Boolean = False
                        Dim do_once_rpcport As Boolean = False
                        Do
                            Try
                                Dim Config_Buffer As String = File.ReadAllText(appdata & "\vertcoin.conf")
                                Dim objWriter As New System.IO.StreamWriter(appdata & "\vertcoin_buffer.conf")
                                For Each Line As String In File.ReadLines(appdata & "\vertcoin.conf")
                                    If do_once_config_server = False Then
                                        config_server = "server=1"
                                        objWriter.WriteLine(config_server) 'ALWAYS Changes server= to server=1 regardless of setting so p2pool can connect
                                        do_once_config_server = True
                                    End If
                                    If do_once_rpcallow = False Then
                                        If (Line.Contains("rpcallowip=") And Line.Length >= 18) Or rpc_allowip = "" Then
                                            If Not Line = "rpcallowip=127.0.0.1" Then
                                                objWriter.WriteLine(Line) 'Moves previous rpcallowip's to new config and adds localhost
                                            End If
                                            objWriter.WriteLine("rpcallowip=127.0.0.1")
                                            do_once_rpcallow = True
                                        End If
                                    End If
                                    If do_once_rpcuser = False Then
                                        If Line.Contains("rpcuser=") And Line.Length >= 9 Then
                                            objWriter.WriteLine(Line)
                                            do_once_rpcuser = True
                                        Else
                                            objWriter.WriteLine("rpcuser=" & rpc_user)
                                            do_once_rpcuser = True
                                        End If
                                    End If
                                    If do_once_rpcpassword = False Then
                                        If Line.Contains("rpcpassword=") And Line.Length >= 13 Then
                                            objWriter.WriteLine(Line)
                                            do_once_rpcpassword = True
                                        Else
                                            objWriter.WriteLine("rpcpassword=" & rpc_password)
                                            do_once_rpcpassword = True
                                        End If
                                    End If
                                    If do_once_rpcport = False Then
                                        If Line.Contains("rpcport=") And Line.Length >= 9 Then
                                            objWriter.WriteLine(Line)
                                            do_once_rpcport = True
                                        Else
                                            objWriter.WriteLine("rpcport=" & rpc_port)
                                            do_once_rpcport = True
                                        End If
                                    End If
                                    If Not Line.Contains("rpcallowip=") And Not Line.Contains("server=") And Not Line.Contains("rpcuser=") And Not Line.Contains("rpcpassword=") And Not Line.Contains("rpcport=") Then
                                        objWriter.WriteLine(Line)
                                    End If
                                Next
                                My.Computer.FileSystem.MoveFile(appdata & "\vertcoin.conf", appdata & "\vertcoin_old.conf", True)
                                objWriter.Close()
                                My.Computer.FileSystem.MoveFile(appdata & "\vertcoin_buffer.conf", appdata & "\vertcoin.conf", True)
                                Exit Do
                            Catch ex As IOException
                                'vertcoin.conf is still in use so pause before trying again.
                                System.Threading.Thread.Sleep(100)
                            End Try
                        Loop
                        p2pool_initialized = True
                    End If
                End If
                If conf_check = False Then
                    Dim result2 As DialogResult = MsgBox("Wallet configuration file not found, would you like Vertcoin One-Click Miner to create one?", MessageBoxButtons.OKCancel)
                    If result2 = DialogResult.OK Then
                        If System.IO.File.Exists(appdata & "\vertcoin.conf") = False Then
                            File.Create(appdata & "\vertcoin.conf").Dispose()
                            Invoke(New MethodInvoker(AddressOf Generate_RPC_Settings))
                            Do
                                Try
                                    Dim objWriter As New System.IO.StreamWriter(appdata & "\vertcoin.conf")
                                    objWriter.WriteLine("server=1")
                                    objWriter.WriteLine("rpcallowip=127.0.0.1")
                                    objWriter.WriteLine("rpcuser=" & rpc_user)
                                    objWriter.WriteLine("rpcpassword=" & rpc_password)
                                    objWriter.WriteLine("rpcport=" & rpc_port)
                                    objWriter.Close()
                                    Exit Do
                                Catch ex As IOException
                                    'vertcoin.conf is still in use so pause before trying again.
                                    System.Threading.Thread.Sleep(100)
                                End Try
                            Loop
                        End If
                        p2pool_initialized = True
                    End If
                Else
                    p2pool_initialized = True
                End If
                rpc_user = ""
                rpc_password = ""
                rpc_port = ""
                config_server = ""
                rpc_allowip = ""
            End If
        Catch ex As Exception
            MsgBox(ex.Message)
            _logger.LogError(ex)
            Invoke(New MethodInvoker(AddressOf SaveSettingsJSON))
        Finally
            _logger.Trace("Main() Check_RPC_Settings: OK.")
        End Try

    End Sub

    Public Sub Generate_RPC_Settings()

        Try
            'If no RPC settings are detected, generate a random user and password to save to append to the config file.
            Dim rnd As New Random
            Dim str As String = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*()"
            If rpc_user = "" Then
                For value As Integer = 0 To 24
                    rpc_user &= str.Chars(rnd.Next(0, str.Length - 1))
                Next
            End If
            If rpc_password = "" Then
                For value As Integer = 0 To 24
                    rpc_password &= str.Chars(rnd.Next(0, str.Length - 1))
                Next
            End If
            If rpc_port = "" Then
                rpc_port = "5888"
            End If
        Catch ex As Exception
            MsgBox(ex.Message)
            _logger.LogError(ex)
            Invoke(New MethodInvoker(AddressOf SaveSettingsJSON))
        Finally
            _logger.Trace("Generate_RPC_Settings: OK.")
        End Try

    End Sub

    Private Sub UpdateToolStripMenuItem_Click(sender As Object, e As EventArgs) Handles UpdateToolStripMenuItem.Click

        If Not Updater.IsBusy Then
            Updater.RunWorkerAsync()
        End If

    End Sub

    Private Sub Updater_DoWork(sender As Object, e As System.ComponentModel.DoWorkEventArgs) Handles Updater.DoWork

        'Call url
        Dim url As New System.Uri("http://google.com")
        Dim connection
        Dim cancel = False
        'Request for connection
        Dim req As System.Net.WebRequest
        req = System.Net.WebRequest.Create(url)
        Dim resp As System.Net.WebResponse
        Try
            resp = req.GetResponse()
            resp.Close()
            req = Nothing
            connection = True
        Catch ex As Exception
            req = Nothing
            connection = False
            MsgBox("No internet connection is present, please connect to the internet to check for updates.")
        End Try
        If connection = True Then
            update_needed = False
            Dim request As System.Net.HttpWebRequest = System.Net.HttpWebRequest.Create("http://alwayshashing.com/ocm_update.txt")
            Dim response As System.Net.HttpWebResponse = request.GetResponse()
            Dim sr As System.IO.StreamReader = New System.IO.StreamReader(response.GetResponseStream())
            'Compares current One-Click Miner version with the latest available.
            newestversion = System.Version.Parse(sr.ReadLine.Replace("miner=", ""))
            updatelink = sr.ReadLine
            If (newestversion > System.Version.Parse(miner_version)) Then
                Dim result1 As DialogResult = MsgBox("Update found for One-Click Miner! Click OK to download." & Environment.NewLine & "Please close program before installing.", MessageBoxButtons.OKCancel)
                If result1 = DialogResult.OK Then
                    update_needed = True
                    Process.Start(updatelink)
                ElseIf result1 = DialogResult.Cancel Then
                    update_complete = True
                End If
            Else
                update_needed = False
            End If
            If update_needed = True Then
                Do Until update_complete = True
                    'Wait until previous update has finished.
                Loop
                update_complete = False
            End If
            'Compares the current version of P2Pool with the latest available.
            newestversion = System.Version.Parse(sr.ReadLine.Replace("p2pool=", ""))
            updatelink = sr.ReadLine
            If p2pool_update = True Then
                Dim result1 As DialogResult = MsgBox("Update found for P2Pool! Click OK to download and install.", MessageBoxButtons.OKCancel)
                If result1 = DialogResult.OK Then
                    update_needed = True
                    Invoke(New MethodInvoker(AddressOf Download_P2Pool))
                End If
            Else
                If newestversion > System.Version.Parse(p2pool_version) And amd_update = False And nvidia_update = False And cpu_update = False And Not (System.Version.Parse(p2pool_version) = System.Version.Parse("0.0.0.0")) Then
                    Dim result1 As DialogResult = MsgBox("Update found for P2Pool! Click OK to download and install.", MessageBoxButtons.OKCancel)
                    update_needed = True
                    If result1 = DialogResult.OK Then
                        Invoke(New MethodInvoker(AddressOf Download_P2Pool))
                    ElseIf result1 = DialogResult.Cancel Then
                        update_complete = True
                    End If
                Else
                    update_needed = False
                End If
            End If
            If update_needed = True Then
                Do Until update_complete = True
                    'Wait until previous update has finished.
                Loop
                update_complete = False
            End If
            'Compares the current version of the AMD miner with the latest available.
            newestversion = System.Version.Parse(sr.ReadLine.Replace("amd=", ""))
            updatelink = sr.ReadLine
            If amd_update = True Then
                Dim result1 As DialogResult = MsgBox("Update found for AMD Miner! Click OK to download.", MessageBoxButtons.OKCancel)
                If result1 = DialogResult.OK Then
                    update_needed = True
                    Invoke(New MethodInvoker(AddressOf Download_Miner))
                ElseIf result1 = DialogResult.Cancel Then
                    cancel = True
                End If
            Else
                If newestversion > System.Version.Parse(amd_version) And p2pool_update = False And nvidia_update = False And cpu_update = False Then
                    Dim result1 As DialogResult = MsgBox("Update found for AMD Miner! Click OK to download.", MessageBoxButtons.OKCancel)
                    update_needed = True
                    If result1 = DialogResult.OK Then
                        amdminer = True
                        Invoke(New MethodInvoker(AddressOf Download_Miner))
                    ElseIf result1 = DialogResult.Cancel Then
                        update_complete = True
                        cancel = True
                    End If
                Else
                    update_needed = False
                End If
            End If
            If update_needed = True Then
                Do Until update_complete = True
                    'Wait until previous update has finished.
                Loop
                update_complete = False
            End If
            'Compares the current version of the Nvidia miner with the latest available.
            newestversion = System.Version.Parse(sr.ReadLine.Replace("nvidia=", ""))
            updatelink = sr.ReadLine
            If nvidia_update = True Then
                Dim result1 As DialogResult = MsgBox("Update found for Nvidia Miner! Click OK to download.", MessageBoxButtons.OKCancel)
                If result1 = DialogResult.OK Then
                    update_needed = True
                    Invoke(New MethodInvoker(AddressOf Download_Miner))
                ElseIf result1 = DialogResult.Cancel Then
                    cancel = True
                End If
            Else
                If newestversion > System.Version.Parse(nvidia_version) And p2pool_update = False And amd_update = False And cpu_update = False Then
                    Dim result1 As DialogResult = MsgBox("Update found for Nvidia Miner! Click OK to download.", MessageBoxButtons.OKCancel)
                    update_needed = True
                    If result1 = DialogResult.OK Then
                        nvidiaminer = True
                        Invoke(New MethodInvoker(AddressOf Download_Miner))
                    ElseIf result1 = DialogResult.Cancel Then
                        update_complete = True
                        cancel = True
                    End If
                Else
                    update_needed = False
                End If
            End If
            If update_needed = True Then
                Do Until update_complete = True
                    'Wait until previous update has finished.
                Loop
                update_complete = False
            End If
            'Compares the current version of the CPU miner with the latest available.
            newestversion = System.Version.Parse(sr.ReadLine.Replace("cpu=", ""))
            updatelink = sr.ReadLine
            If cpu_update = True Then
                Dim result1 As DialogResult = MsgBox("Update found for CPU Miner! Click OK to download.", MessageBoxButtons.OKCancel)
                If result1 = DialogResult.OK Then
                    update_needed = True
                    Invoke(New MethodInvoker(AddressOf Download_Miner))
                ElseIf result1 = DialogResult.Cancel Then
                    cancel = True
                End If
            Else
                If newestversion > System.Version.Parse(cpu_version) And p2pool_update = False And amd_update = False And nvidia_update = False Then
                    Dim result1 As DialogResult = MsgBox("Update found for CPU Miner! Click OK to download.", MessageBoxButtons.OKCancel)
                    update_needed = True
                    If result1 = DialogResult.OK Then
                        cpuminer = True
                        Invoke(New MethodInvoker(AddressOf Download_Miner))
                    ElseIf result1 = DialogResult.Cancel Then
                        cancel = True
                    End If
                Else
                    update_needed = False
                End If
            End If
            If update_needed = True Then
                Do Until update_complete = True
                    'Wait until previous update has finished.
                Loop
                update_complete = False
            End If
            If update_needed = False And p2pool_update = False And amd_update = False And nvidia_update = False And cpu_update = False And cancel = False Then
                MsgBox("There are no updates available.")
            End If
            sr.Close()
        End If
        p2pool_update = False
        amd_update = False
        nvidia_update = False
        cpu_update = False
        Invoke(New MethodInvoker(AddressOf Update_Notification))
        Invoke(New MethodInvoker(AddressOf SaveSettingsJSON))
        Updater.CancelAsync()

    End Sub

    Private Sub Label7_Click(sender As Object, e As EventArgs) Handles Label7.Click

        If Not Updater.IsBusy Then
            Updater.RunWorkerAsync()
        End If

    End Sub

    Private Sub Intensity_Text_KeyPress(ByVal sender As System.Object, ByVal e As System.Windows.Forms.KeyPressEventArgs)

        If e.KeyChar > "31" And (e.KeyChar < "0" Or e.KeyChar > "9") Then
            e.Handled = True
        End If

    End Sub

    Private Sub NotifyIcon1_MouseDoubleClick(sender As Object, e As MouseEventArgs) Handles NotifyIcon1.MouseDoubleClick

        Me.WindowState = FormWindowState.Normal
        Me.ShowInTaskbar = True
        NotifyIcon1.Visible = False

    End Sub

    Private Sub P2PoolWebInterfaceToolStripMenuItem_Click(sender As Object, e As EventArgs) Handles P2PoolWebInterfaceToolStripMenuItem.Click

        Try
            Dim url As String = pools(0).replace("stratum+tcp", "http")
            Process.Start(url)
        Catch ex As Exception
            MsgBox(ex.Message)
            _logger.LogError(ex)
            Invoke(New MethodInvoker(AddressOf SaveSettingsJSON))
        Finally
            _logger.Trace("LoadP2PoolInterface(), Load Browser: OK")
        End Try

    End Sub

    Private Sub Button1_Click(sender As Object, e As EventArgs) Handles Button1.Click

        dim dialog = new P2Pool(_logger)
        dialog.Show()

    End Sub

    Public Sub Style()

        Panel1.BackColor = Color.FromArgb(27, 92, 46)
        Button1.BackColor = Color.FromArgb(27, 92, 46)
        Button3.BackColor = Color.FromArgb(27, 92, 46)
        Button2.BackColor = Color.FromArgb(27, 92, 46)
        Button4.BackColor = Color.FromArgb(27, 92, 46)
        Panel3.BackColor = Color.FromArgb(41, 54, 61)
        TextBox3.BackColor = Color.FromArgb(41, 54, 61)
        MenuStrip.BackColor = Color.FromArgb(27, 92, 46)
        DataGridView1.ForeColor = Color.Black
        DataGridView1.RowsDefaultCellStyle.Font = New Font(DataGridView1.Font, FontStyle.Regular)
        DataGridView1.ColumnHeadersDefaultCellStyle.Font = New Font(DataGridView1.Font, FontStyle.Regular)
        DataGridView1.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill

    End Sub

    Dim drag As Boolean = False
    Dim mousex As Integer, mousey As Integer

    Private Sub Panel1_MouseDown(ByVal sender As Object, ByVal e As System.Windows.Forms.MouseEventArgs) Handles Panel1.MouseDown, Panel3.MouseDown, PictureBox1.MouseDown

        drag = True
        mousex = Windows.Forms.Cursor.Position.X - Me.Left
        mousey = Windows.Forms.Cursor.Position.Y - Me.Top

    End Sub

    Private Sub Panel1_MouseMove(ByVal sender As Object, ByVal e As System.Windows.Forms.MouseEventArgs) Handles Panel1.MouseMove, Panel3.MouseMove, PictureBox1.MouseMove

        If drag Then
            Me.Left = Windows.Forms.Cursor.Position.X - mousex
            Me.Top = Windows.Forms.Cursor.Position.Y - mousey
        End If

    End Sub

    Private Sub Panel1_MouseUp(ByVal sender As Object, ByVal e As System.Windows.Forms.MouseEventArgs) Handles Panel1.MouseUp, Panel3.MouseUp, PictureBox1.MouseUp

        drag = False

    End Sub

    Private Sub ComboBox1_SelectedIndexChanged(sender As Object, e As EventArgs) Handles ComboBox1.SelectedIndexChanged

        If ComboBox1.SelectedItem = "AMD" Then
            default_miner = "amd"
        ElseIf ComboBox1.SelectedItem = "NVIDIA" Then
            default_miner = "nvidia"
        ElseIf ComboBox1.SelectedItem = "CPU" Then
            default_miner = "cpu"
        End If

    End Sub

    Private Sub Button3_Click(sender As Object, e As EventArgs) Handles Button3.Click

        Dim checkcount = 0
        For Each row As DataGridViewRow In DataGridView1.Rows
            Dim chk As DataGridViewCheckBoxCell = row.Cells(DataGridView1.Columns(0).Name)
            If chk.Value = True Then
                checkcount += 1
            End If
        Next
        Invoke(New MethodInvoker(AddressOf SaveSettingsJSON))
        'Starts mining if miner software is already detected.  If not, downloads miner software.
        If Button3.Text = "Start" Then
            Button3.Text = "Stop"
            If checkcount > 0 Then
                If default_miner = "amd" Then
                    'Checks if AMD miner has already been downloaded and installed
                    If System.IO.Directory.Exists(amdfolder) = True Then
                        For Each file As String In Directory.GetFiles(amdfolder)
                            If file.Contains("ocm_sgminer.exe") And Not (System.Version.Parse(amd_version) = System.Version.Parse("0.0.0.0")) Then
                                mining_installed = True
                                Exit For
                            Else
                                mining_installed = False
                            End If
                        Next
                    End If
                    cpuminer = False
                    amdminer = True
                    nvidiaminer = False
                    If mining_installed = True Then
                        mining_initialized = True
                        BeginInvoke(New MethodInvoker(AddressOf Start_Miner))
                    Else
                        If Not Updater.IsBusy Then
                            amd_update = True
                            canceldownloadasync = False
                            Updater.RunWorkerAsync()
                        End If
                    End If
                ElseIf default_miner = "nvidia" Then
                    'Checks if NVIDIA miner has already been downloaded and installed
                    If System.IO.Directory.Exists(nvidiafolder) = True Then
                        For Each file As String In Directory.GetFiles(nvidiafolder)
                            If file.Contains("ocm_vertminer.exe") And Not (System.Version.Parse(nvidia_version) = System.Version.Parse("0.0.0.0")) Then
                                mining_installed = True
                                Exit For
                            Else
                                mining_installed = False
                            End If
                        Next
                    End If
                    cpuminer = False
                    amdminer = False
                    nvidiaminer = True
                    If mining_installed = True Then
                        mining_initialized = True
                        BeginInvoke(New MethodInvoker(AddressOf Start_Miner))
                    Else
                        If Not Updater.IsBusy Then
                            nvidia_update = True
                            canceldownloadasync = False
                            Updater.RunWorkerAsync()
                        End If
                    End If
                ElseIf default_miner = "cpu" Then
                    'Checks if CPU miner has already been downloaded and installed
                    If System.IO.Directory.Exists(cpufolder) = True Then
                        For Each file As String In Directory.GetFiles(cpufolder)
                            If file.Contains("ocm_cpuminer.exe") And Not (System.Version.Parse(cpu_version) = System.Version.Parse("0.0.0.0")) Then
                                mining_installed = True
                                Exit For
                            Else
                                mining_installed = False
                            End If
                        Next
                    End If
                    cpuminer = True
                    amdminer = False
                    nvidiaminer = False
                    If mining_installed = True Then
                        mining_initialized = True
                        BeginInvoke(New MethodInvoker(AddressOf Start_Miner))
                    Else
                        If Not Updater.IsBusy Then
                            cpu_update = True
                            canceldownloadasync = False
                            Updater.RunWorkerAsync()
                        End If
                    End If
                End If
                mining_installed = False
            Else
                MsgBox("Please select at least one pool before starting miner.")
            End If
        ElseIf Button3.Text = "Stop" Then
            Button3.Text = "Start"
            amdminer = False
            nvidiaminer = False
            cpuminer = False
            mining_initialized = False
            BeginInvoke(New MethodInvoker(AddressOf Kill_Miner))
        End If

    End Sub

    Private Sub PictureBox6_Click(sender As Object, e As EventArgs) Handles PictureBox6.Click

        Me.WindowState = FormWindowState.Minimized

    End Sub

    Private Sub Clock_Tick(sender As Object, e As EventArgs) Handles Clock.Tick

        Dim time As DateTime = DateTime.Now
        timenow = time.ToString("r", culture)

    End Sub

    Private Sub Button4_Click(sender As Object, e As EventArgs) Handles Button4.Click

        For Each row As DataGridViewRow In DataGridView1.Rows
            Dim chk As DataGridViewCheckBoxCell = row.Cells(DataGridView1.Columns(0).Name)
            If chk.Value IsNot Nothing AndAlso chk.Value = True Then
                DataGridView1.Rows.RemoveAt(chk.RowIndex)
                pools.RemoveAt(chk.RowIndex + 1)
            End If
        Next

    End Sub

    Private Sub Button2_Click(sender As Object, e As EventArgs) Handles Button2.Click

        dim dialog = New AddPool(_logger)
        dialog.Show()

    End Sub

    Private Sub MinerWindowToolStripMenuItem_Click(sender As Object, e As EventArgs)

    End Sub

    Private Sub CheckBox2_CheckedChanged(sender As Object, e As EventArgs) Handles CheckBox2.CheckedChanged

        If CheckBox2.Checked = True Then
            For Each row As DataGridViewRow In DataGridView1.Rows
                Dim chk As DataGridViewCheckBoxCell = row.Cells(DataGridView1.Columns(0).Name)
                If chk.Value = False Then
                    chk.Value = True
                End If
            Next
        Else
            For Each row As DataGridViewRow In DataGridView1.Rows
                Dim chk As DataGridViewCheckBoxCell = row.Cells(DataGridView1.Columns(0).Name)
                If chk.Value = True Then
                    chk.Value = False
                End If
            Next
        End If

    End Sub

    Private Sub PictureBox4_Click(sender As Object, e As EventArgs) Handles PictureBox4.Click

        Me.Close()

    End Sub

End Class
