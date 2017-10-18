﻿Imports System.Globalization
Imports System.Net

Module variables

    'General
    Public Property timenow As String
    Public Property culture = CultureInfo.InvariantCulture
    Public Property miner_version = Application.ProductVersion
    Public Property p2pool_version = "0.0.0.0"
    Public Property amd_version = "0.0.0.0"
    Public Property nvidia_version = "0.0.0.0"
    Public Property cpu_version = "0.0.0.0"
    Public Property settingsfolder As String
    Public Property appdata As String
    Public Property settingsfile As String
    Public Property syslog As String
    Public Property amdfolder As String
    Public Property nvidiafolder As String
    Public Property cpufolder As String
    Public Property p2poolfolder As String
    Public Property scannerfolder As String
    Public Property command As String
    Public Property platform As Boolean 'true=64-bit false=32-bit
    Public Property otherminer As Boolean
    Public Property otherp2pool As Boolean
    Public Property p2pool_detected
    Public Property amd_detected
    Public Property nvidia_detected
    Public Property cpu_detected

    'Update Variables
    Public Property update_needed As Boolean
    Public Property autocheck_updates As Boolean
    Public Property update_complete As Boolean
    Public Property newestversion As New Version
    Public Property updatelink As String
    Public Property p2pool_update As Boolean
    Public Property amd_update As Boolean
    Public Property nvidia_update As Boolean
    Public Property cpu_update As Boolean
    Public Property downloadclient As New WebClient
    Public Property canceldownloadasync As Boolean

    'API Variables
    Public Property api_connected As Boolean
    Public Property api_network1_hosts() = {"http://scanner1.alwayshashing.com:8085/api", "https://scanner.vtconline.org/api"}
    Public Property api_network2_hosts() = {"http://scanner2.alwayshashing.com:8085/api"}
    Public Property network1data As DataSet
    Public Property network2data As DataSet

    ''Miner
    Public Property miner_hashrate As Decimal
    ''P2Pool
    Public Property p2pool_loaded As Boolean

    'Settings
    Public Property start_minimized As Boolean
    Public Property start_with_windows As Boolean
    Public Property autostart_p2pool As Boolean
    Public Property autostart_mining As Boolean
    Public Property show_cli As Boolean
    Public Property autostart_miner As String
    Public Property p2pool_network As String = 1
    Public Property p2pool_node_fee As Decimal = 0
    Public Property p2pool_fee_address As String = "VpBsRnN749jYHE9hT8dZreznHfmFMdE1yG"
    Public Property p2pool_donation As Decimal = 1
    Public Property max_connections As Integer = 50

    'P2Pool Variables
    Public Property p2pool_running As Boolean
    Public Property p2pool_port As String = "9346"
    Public Property keep_p2pool_alive As Boolean
    Public Property p2pool_installed As Boolean
    Public Property p2pool_initialized As Boolean
    Public Property use_upnp As Boolean
    Public Property p2pool_config_file As String
    Public Property p2pool_config As String
    Public Property config_server As String
    Public Property rpc_allowip As String
    Public Property rpc_user As String
    Public Property rpc_password As String
    Public Property rpc_port As String

    'Miner Variables
    Public Property zipPath As String
    Public Property extractpath As String
    Public Property exe As String
    Public Property dll As String
    Public Property miner_config_file As String
    Public Property miner_config As String
    Public Property additional_miner_config As String
    Public Property mining_installed As Boolean
    Public Property mining_initialized As Boolean

    'Miner Settings
    Public Property mining_running As Boolean
    Public Property default_miner As String
    Public Property amdminer As Boolean
    Public Property nvidiaminer As Boolean
    Public Property cpuminer As Boolean
    Public Property nvidiadownload As String
    Public Property amddownload As String
    Public Property minerprompt As Boolean
    Public Property mining_port As String = "9171"
    Public Property keep_miner_alive As Boolean

    'Miner Configuration
    Public Property minersettingsfile As String
    Public Property mining_intensity As Decimal = 0
    Public Property devices As String
    Public Property pool As String
    Public Property pools As New ArrayList()
    Public Property workers As New ArrayList()
    Public Property passwords As New ArrayList()
    Public Property selected As New ArrayList()

End Module
