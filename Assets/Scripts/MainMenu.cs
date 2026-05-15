using UnityEngine;
using UnityEngine.SceneManagement;
using Photon.Pun;
using Photon.Realtime;

public class MainMenu : MonoBehaviourPunCallbacks
{
    [Header("Configuration")]
    [SerializeField] private string gameSceneName = "SampleScene";
    [SerializeField] private string aiSelectSceneName = "SelectLevel";

    private bool isConnecting = false;

    private void Start()
    {
        // 1. ĐỒNG BỘ: Đảm bảo khi chủ phòng chuyển Scene, khách sẽ chuyển theo
        PhotonNetwork.AutomaticallySyncScene = true;

        // 2. CHẠY NGẦM: Giúp duy trì kết nối khi thu nhỏ cửa sổ game
        Application.runInBackground = true;

        // 3. DỌN DẸP: Nếu còn rác kết nối từ ván trước, ngắt ngay để làm sạch bộ nhớ
        if (PhotonNetwork.IsConnected)
        {
            Debug.Log(">> [System] Đang dọn dẹp kết nối cũ...");
            PhotonNetwork.Disconnect();
        }

        isConnecting = false;
    }

    #region Chế độ chơi Offline & AI
    public void PlayVsAI()
    {
        PlayerPrefs.SetInt("GameMode", 0);
        SceneManager.LoadScene(aiSelectSceneName);
    }

    public void SelectDifficulty(int elo)
    {
        PlayerPrefs.SetInt("SelectedElo", elo);
        PlayerPrefs.SetInt("GameMode", 0);
        SceneManager.LoadScene(gameSceneName);
    }

    public void PlayVsHuman()
    {
        PlayerPrefs.SetInt("GameMode", 1); // Đấu tại chỗ (2 người 1 máy)
        SceneManager.LoadScene(gameSceneName);
    }

    public void BackToMenu()
    {
        SceneManager.LoadScene("StartMenu");
    }
    #endregion

    #region Chế độ Online (Chống xung đột)
    public void PlayOnline()
    {
        // 4. CHẶN SPAM: Nếu đang xử lý thì không cho bấm thêm
        if (isConnecting) return;

        isConnecting = true;
        PlayerPrefs.SetInt("GameMode", 2);

        // 5. PHÂN BIỆT MÁY: Đặt tên khác nhau để Server không nhầm lẫn
#if UNITY_EDITOR
        PhotonNetwork.LocalPlayer.NickName = "Máy Unity Editor";
#else
            PhotonNetwork.LocalPlayer.NickName = "Máy Bản Build";
#endif

        Debug.Log(">> [Network] Đang kiểm tra trạng thái kết nối...");

        if (PhotonNetwork.IsConnectedAndReady)
        {
            SafeJoinRoom();
        }
        else
        {
            PhotonNetwork.ConnectUsingSettings();
        }
    }

    public override void OnConnectedToMaster()
    {
        Debug.Log(">> [Network] Đã vào Master Server.");
        // 6. TRÁNH XUNG ĐỘT TRẠNG THÁI: Đợi 0.5s để hệ thống ổn định rồi mới tìm phòng
        Invoke(nameof(SafeJoinRoom), 0.5f);
    }

    private void SafeJoinRoom()
    {
        if (PhotonNetwork.InRoom) return;
        Debug.Log(">> [Network] Đang tìm phòng trống...");
        PhotonNetwork.JoinRandomRoom();
    }

    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        Debug.Log(">> [Network] Không có phòng, đang tạo phòng mới...");
        RoomOptions roomOptions = new RoomOptions { MaxPlayers = 2 };
        PhotonNetwork.CreateRoom(null, roomOptions);
    }

    public override void OnJoinedRoom()
    {
        Debug.Log($">> [Network] Đã vào phòng. Số người: {PhotonNetwork.CurrentRoom.PlayerCount}/2");

        // 7. CHỈ CHỦ PHÒNG MỚI ĐƯỢC LOAD SCENE: Tránh việc 2 máy cùng gọi lệnh load gây lỗi
        if (PhotonNetwork.IsMasterClient)
        {
            Debug.Log(">> [Network] Chủ phòng đang khởi động bàn cờ...");
            PhotonNetwork.LoadLevel(gameSceneName);
        }
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        isConnecting = false;
        Debug.LogWarning($">> [Network] Ngắt kết nối do: {cause}");
    }
    #endregion
}