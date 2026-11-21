using UnityEngine;
using TMPro;

public class GameTimer : MonoBehaviour
{
    public float duration = 60f;
    public TextMeshProUGUI timerText;

    private float timeLeft;
    private bool running;

    public void StartTimer()
    {
        timeLeft = duration;
        running = true;
    }

    void Update()
    {
        if (!running) return;

        timeLeft -= Time.deltaTime;

        if (timeLeft <= 0f)
        {
            timeLeft = 0f;
            running = false;
            OnTimeUp();
        }

        timerText.text = Mathf.CeilToInt(timeLeft).ToString();
    }

    void OnTimeUp()
    {
        Debug.Log("Vaxt bitdi!");
    }
}