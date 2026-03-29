using UnityEngine;
using UnityEngine.Video;
using UnityEngine.SceneManagement;

public class LogoIntro : MonoBehaviour
{
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    private void Start()
    {
        if (videoPlayer == null)
            videoPlayer = GetComponentInChildren<VideoPlayer>();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        videoPlayer.loopPointReached += OnVideoFinished;
        videoPlayer.Play();
    }

    private void OnVideoFinished(VideoPlayer vp)
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        SceneManager.LoadScene(mainMenuSceneName);
    }

    private void OnDestroy()
    {
        if (videoPlayer != null)
            videoPlayer.loopPointReached -= OnVideoFinished;
    }
}