using System.Diagnostics;
using System.IO;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class StockfishManager : MonoBehaviour
{
    [Header("Engine")]
    public string engineFileName = "stockfish-windows-x86-64-avx2.exe";
    public int moveTimeMs = 800;

    private Process engineProcess;
    private StreamWriter engineInput;
    private StreamReader engineOutput;

    public bool IsRunning => engineProcess != null && !engineProcess.HasExited;

    private void Start()
    {
        StartEngine();
    }

    public void StartEngine()
    {
        if (IsRunning) return;

        string enginePath = GetEnginePath();
        Debug.Log("Engine path: " + enginePath);

        if (!File.Exists(enginePath))
        {
            Debug.LogError("Không tìm thấy engine tại: " + enginePath);
            return;
        }

        engineProcess = new Process
        {
            StartInfo = CreateStartInfo(enginePath)
        };
        engineProcess.Start();

        engineInput = engineProcess.StandardInput;
        engineOutput = engineProcess.StandardOutput;

        if (!InitializeEngine())
        {
            StopEngine();
            return;
        }

        Debug.Log("Stockfish started successfully.");
    }

    public void StopEngine()
    {
        if (IsRunning)
        {
            try
            {
                SendCommand("quit");

                if (!engineProcess.HasExited)
                    engineProcess.Kill();
            }
            catch
            {
                // ignore
            }
        }

        ClearEngineState();
    }

    public void SendCommand(string command)
    {
        if (!IsRunning) return;

        engineInput.WriteLine(command);
        engineInput.Flush();
        Debug.Log("[UCI >>] " + command);
    }

    public string ReadLine()
    {
        if (!IsRunning) return null;

        string line = engineOutput.ReadLine();
        Debug.Log("[UCI <<] " + line);
        return line;
    }

    private bool WaitFor(string token)
    {
        while (IsRunning)
        {
            string line = ReadLine();
            if (line == null) return false;
            if (line.Contains(token)) return true;
        }

        return false;
    }

    public string GetBestMoveFromFen(string fen)
    {
        return GetBestMoveFromFen(fen, moveTimeMs);
    }

    public string GetBestMoveFromFen(string fen, int thinkTimeMs)
    {
        if (!IsRunning) return null;

        SendCommand($"position fen {fen}");
        SendCommand($"go movetime {thinkTimeMs}");

        while (IsRunning)
        {
            string line = ReadLine();
            if (string.IsNullOrEmpty(line)) continue;

            if (line.StartsWith("bestmove "))
            {
                string[] parts = line.Split(' ');
                if (parts.Length >= 2)
                {
                    string bestMove = parts[1];
                    Debug.Log("Stockfish best move = " + bestMove);
                    return bestMove;
                }
            }
        }

        return null;
    }

    private void OnApplicationQuit()
    {
        StopEngine();
    }

    private void OnDestroy()
    {
        StopEngine();
    }

    private string GetEnginePath()
    {
        return Path.Combine(Application.streamingAssetsPath, "Stockfish", engineFileName);
    }

    private ProcessStartInfo CreateStartInfo(string enginePath)
    {
        return new ProcessStartInfo
        {
            FileName = enginePath,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
    }

    private bool InitializeEngine()
    {
        SendCommand("uci");
        if (!WaitFor("uciok")) return false;

        int targetElo = PlayerPrefs.GetInt("SelectedElo", 550);

        // Bật chế độ giới hạn sức mạnh
        SendCommand("setoption name UCI_LimitStrength value true");

        if (targetElo <= 300)
        {
            // Mức Dễ: Ép Skill Level về 0 để máy đánh ngẫu nhiên hơn
            SendCommand("setoption name Skill Level value 0");
            SendCommand("setoption name UCI_Elo value 1320");
        }
        else if (targetElo <= 600)
        {
            SendCommand("setoption name Skill Level value 5");
            SendCommand("setoption name UCI_Elo value 1500");
        }
        else
        {
            // Mức Khó: Thả xích cho Engine
            SendCommand("setoption name Skill Level value 20");
            SendCommand("setoption name UCI_Elo value " + targetElo);
        }

        SendCommand("isready");
        return WaitFor("readyok");
        /*SendCommand("uci");
        if (!WaitFor("uciok"))
        {
            Debug.LogError("Stockfish không phản hồi uciok.");
            return false;
        }

        // Lấy mức Elo đã chọn (Mặc định 550 nếu có lỗi)
        int targetElo = PlayerPrefs.GetInt("SelectedElo", 550);

        // Bật giới hạn sức mạnh và gán Elo
        SendCommand("setoption name UCI_LimitStrength value true");
        SendCommand("setoption name UCI_Elo value " + targetElo);

        SendCommand("isready");

        if (!WaitFor("readyok"))
        {
            Debug.LogError("Stockfish không phản hồi readyok.");
            return false;
        }

        Debug.Log("Stockfish đã sẵn sàng với mức Elo: " + targetElo);
        return true;*/
    }

    private void ClearEngineState()
    {
        engineProcess = null;
        engineInput = null;
        engineOutput = null;
    }
}