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
        PhotonNetwork.AutomaticallySyncScene = true;
        Application.runInBackground = true;

        if (PhotonNetwork.IsConnected)
        {
            PhotonNetwork.Disconnect();
        }
    }

    #region Local & AI Game Modes
    public void PlayVsAI()
    {
        PlayerPrefs.SetInt("GameMode", 0);
        SceneManager.LoadScene(aiSelectSceneName);
    }

    // --- HÀM BỊ THIẾU ĐÂY RỒI ---
    public void SelectDifficulty(int elo)
    {
        PlayerPrefs.SetInt("SelectedElo", elo);
        PlayerPrefs.SetInt("GameMode", 0); // Chế độ đánh với máy
        SceneManager.LoadScene(gameSceneName);
    }

    public void PlayVsHuman()
    {
        PlayerPrefs.SetInt("GameMode", 1);
        SceneManager.LoadScene(gameSceneName);
    }

    public void BackToMenu()
    {
        SceneManager.LoadScene("StartMenu");
    }
    #endregion

    #region Online Multiplayer Logic
    public void PlayOnline()
    {
        if (isConnecting) return;

        isConnecting = true;
        PlayerPrefs.SetInt("GameMode", 2);

        if (PhotonNetwork.IsConnectedAndReady)
        {
            PhotonNetwork.JoinRandomRoom();
        }
        else
        {
            PhotonNetwork.ConnectUsingSettings();
        }
    }

    public override void OnConnectedToMaster()
    {
        PhotonNetwork.JoinRandomRoom();
    }

    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        RoomOptions roomOptions = new RoomOptions { MaxPlayers = 2 };
        PhotonNetwork.CreateRoom(null, roomOptions);
    }

    public override void OnJoinedRoom()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            PhotonNetwork.LoadLevel(gameSceneName);
        }
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        isConnecting = false;
    }
    #endregion
}