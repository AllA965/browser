using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Net.NetworkInformation;

namespace MiniWorldBrowser.Helpers;

public static class PythonBridgeManager
{
    private static Process? _bridgeProcess;
    private const int BridgePort = 8000;

    /// <summary>
    /// 检查端口是否被占用
    /// </summary>
    private static bool IsPortInUse(int port)
    {
        try
        {
            var ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
            var tcpConnInfoArray = ipGlobalProperties.GetActiveTcpListeners();
            foreach (var endpoint in tcpConnInfoArray)
            {
                if (endpoint.Port == port)
                {
                    return true;
                }
            }
        }
        catch { }
        return false;
    }

    /// <summary>
    /// 寻找可用的 Python 可执行文件
    /// </summary>
    private static string FindPythonExecutable(string rootPath)
    {
        // 1. 优先尝试内嵌环境
        string embeddedPython = Path.Combine(rootPath, "python_env", "Scripts", "python.exe");
        if (File.Exists(embeddedPython)) return embeddedPython;

        // 2. 尝试系统路径中的常用名称
        string[] commonNames = { "python", "python3", "py -3", "py" };
        foreach (var name in commonNames)
        {
            try
            {
                var checkProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = name.Contains(" ") ? name.Split(' ')[0] : name,
                        Arguments = name.Contains(" ") ? name.Split(' ')[1] + " --version" : "--version",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true
                    }
                };
                checkProcess.Start();
                checkProcess.WaitForExit(1000);
                if (checkProcess.ExitCode == 0) return name;
            }
            catch { }
        }

        return "python"; // 最后兜底
    }

    public static void StartBridge()
    {
        try
        {
            // 1. 检查是否已经运行
            if (_bridgeProcess != null && !_bridgeProcess.HasExited)
            {
                return;
            }

            // 2. 检查端口是否被占用（如果被占用，说明可能已经有一个实例在运行了，比如手动启动的）
            if (IsPortInUse(BridgePort))
            {
                Debug.WriteLine($"Port {BridgePort} is already in use. Assuming bridge is already running.");
                return;
            }

            // 3. 寻找 main.py 路径
            string rootPath = AppDomain.CurrentDomain.BaseDirectory;
            string pythonBridgePath = Path.GetFullPath(Path.Combine(rootPath, "python_bridge", "main.py"));
            string triedPaths = $"Tried: {pythonBridgePath}";

            if (!File.Exists(pythonBridgePath))
            {
                string? currentDir = rootPath;
                while (currentDir != null)
                {
                    string testPath = Path.Combine(currentDir, "python_bridge", "main.py");
                    triedPaths += $"\n{testPath}";
                    if (File.Exists(testPath))
                    {
                        pythonBridgePath = testPath;
                        break;
                    }
                    currentDir = Path.GetDirectoryName(currentDir);
                    if (currentDir == Path.GetPathRoot(currentDir)) break;
                }
            }

            if (!File.Exists(pythonBridgePath))
            {
                string logMsg = $"Python Bridge not found.\n{triedPaths}";
                Debug.WriteLine(logMsg);
                File.WriteAllText(Path.Combine(rootPath, "bridge_error.log"), logMsg + Environment.NewLine);
                return;
            }

            // 4. 寻找 Python 执行文件
            string pythonExe = FindPythonExecutable(rootPath);
            Debug.WriteLine($"Using Python executable: {pythonExe}");
            
            string pythonLogPath = Path.Combine(rootPath, "python_bridge.log");
            File.AppendAllText(pythonLogPath, $"[{DateTime.Now:HH:mm:ss}] [INFO] Using Python: {pythonExe}{Environment.NewLine}");

            var startInfo = new ProcessStartInfo
            {
                FileName = pythonExe.Contains(" ") ? pythonExe.Split(' ')[0] : pythonExe,
                Arguments = (pythonExe.Contains(" ") ? pythonExe.Split(' ')[1] + " " : "") + $"\"{pythonBridgePath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = Path.GetDirectoryName(pythonBridgePath)
            };

            startInfo.Environment["PYTHONUTF8"] = "1";
            startInfo.StandardOutputEncoding = System.Text.Encoding.UTF8;
            startInfo.StandardErrorEncoding = System.Text.Encoding.UTF8;

            _bridgeProcess = new Process { StartInfo = startInfo };
            
            _bridgeProcess.OutputDataReceived += (s, e) => {
                if (!string.IsNullOrEmpty(e.Data)) {
                    Debug.WriteLine($"[Python] {e.Data}");
                    File.AppendAllText(pythonLogPath, $"[{DateTime.Now:HH:mm:ss}] [OUT] {e.Data}{Environment.NewLine}");
                }
            };
            _bridgeProcess.ErrorDataReceived += (s, e) => {
                if (!string.IsNullOrEmpty(e.Data)) {
                    Debug.WriteLine($"[Python Error] {e.Data}");
                    File.AppendAllText(pythonLogPath, $"[{DateTime.Now:HH:mm:ss}] [ERR] {e.Data}{Environment.NewLine}");
                }
            };

            File.WriteAllText(pythonLogPath, $"--- Python Bridge Start Attempt at {DateTime.Now} ---{Environment.NewLine}");
            File.AppendAllText(pythonLogPath, $"Command: {startInfo.FileName} {startInfo.Arguments}{Environment.NewLine}");
            File.AppendAllText(pythonLogPath, $"Working Directory: {startInfo.WorkingDirectory}{Environment.NewLine}");

            _bridgeProcess.Start();
            _bridgeProcess.BeginOutputReadLine();
            _bridgeProcess.BeginErrorReadLine();

            Debug.WriteLine("Python Bridge started.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to start Python Bridge: {ex.Message}");
            string rootPath = AppDomain.CurrentDomain.BaseDirectory;
            string pythonLogPath = Path.Combine(rootPath, "python_bridge.log");
            File.AppendAllText(pythonLogPath, $"[{DateTime.Now:HH:mm:ss}] [FATAL] Failed to start Python Bridge: {ex.Message}{Environment.NewLine}{ex.StackTrace}{Environment.NewLine}");
        }
    }

    public static void StopBridge()
    {
        try
        {
            if (_bridgeProcess != null && !_bridgeProcess.HasExited)
            {
                _bridgeProcess.Kill(true);
                _bridgeProcess.Dispose();
                _bridgeProcess = null;
                Debug.WriteLine("Python Bridge stopped.");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error stopping Python Bridge: {ex.Message}");
        }
    }
}
