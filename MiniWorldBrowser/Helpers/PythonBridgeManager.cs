using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MiniWorldBrowser.Helpers;

public static class PythonBridgeManager
{
    private static Process? _bridgeProcess;
    private const int BridgePort = 8000;

    public static void StartBridge()
    {
        try
        {
            // Check if process is already running
            if (_bridgeProcess != null && !_bridgeProcess.HasExited)
            {
                return;
            }

            // 1. 尝试相对于可执行文件的位置 (bin/Debug/net.../python_bridge/main.py)
            string rootPath = AppDomain.CurrentDomain.BaseDirectory;
            string pythonBridgePath = Path.GetFullPath(Path.Combine(rootPath, "python_bridge", "main.py"));
            string triedPaths = $"Tried: {pythonBridgePath}";

            // 2. 尝试向上寻找项目根目录 (开发环境下通常在 bin/Debug/net... 向上 4-5 层)
            if (!File.Exists(pythonBridgePath))
            {                // 递归向上查找，直到找到包含 python_bridge 文件夹的目录
                string? currentDir = rootPath;
                while (currentDir != null)
                {                    string testPath = Path.Combine(currentDir, "python_bridge", "main.py");
                    triedPaths += $"\n{testPath}";
                    if (File.Exists(testPath))
                    {                        pythonBridgePath = testPath;
                        break;
                    }
                    currentDir = Path.GetDirectoryName(currentDir);
                    if (currentDir == Path.GetPathRoot(currentDir)) break;
                }
            }

            // 3. 尝试当前工作目录 (如果是通过命令行在项目根目录启动)
            if (!File.Exists(pythonBridgePath))
            {                string testPath = Path.Combine(Directory.GetCurrentDirectory(), "python_bridge", "main.py");
                triedPaths += $"\n{testPath}";
                if (File.Exists(testPath))
                {                    pythonBridgePath = testPath;
                }
            }

            if (!File.Exists(pythonBridgePath))
            {                string logMsg = $"Python Bridge not found.\n{triedPaths}";
                Debug.WriteLine(logMsg);
                File.WriteAllText(Path.Combine(rootPath, "bridge_error.log"), logMsg + Environment.NewLine);
                return;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = "python",
                Arguments = $"\"{pythonBridgePath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = Path.GetDirectoryName(pythonBridgePath)
            };

            _bridgeProcess = new Process { StartInfo = startInfo };
            _bridgeProcess.OutputDataReceived += (s, e) => Debug.WriteLine($"[Python] {e.Data}");
            _bridgeProcess.ErrorDataReceived += (s, e) => Debug.WriteLine($"[Python Error] {e.Data}");

            _bridgeProcess.Start();
            _bridgeProcess.BeginOutputReadLine();
            _bridgeProcess.BeginErrorReadLine();

            Debug.WriteLine("Python Bridge started.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to start Python Bridge: {ex.Message}");
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
