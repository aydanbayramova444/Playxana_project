using UnityEngine;
using UnityEngine.Video;

public class IntroVideo : MonoBehaviour
{
    public VideoPlayer videoPlayer;
    public GameObject videoPanel;
    public GameObject gameplayUI;
    public GameTimer gameTimer;

    void Start()
    {
        gameplayUI.SetActive(false);
        videoPanel.SetActive(true);

        videoPlayer.loopPointReached += OnVideoEnd;
        videoPlayer.Play();
    }

    void OnVideoEnd(VideoPlayer vp)
    {
        videoPanel.SetActive(false);
        gameplayUI.SetActive(true);
        gameTimer.StartTimer();
    }

    // İstəyə bağlı: Space ilə videonu keç
    void Update()
    {
        if (videoPanel.activeSelf && Input.GetKeyDown(KeyCode.Space))
            OnVideoEnd(videoPlayer);
    }
}