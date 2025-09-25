using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TurnTimer : MonoBehaviour
{
    public float targetTime = 1.8f;
    public float currentTime;
    public bool paused = true;
    private bool done = false;

    public TurnManager turnManager;

    private void Start()
    {
        currentTime = targetTime;
    }

    private void Update()
    {
        if (!paused && !done)
        {
            currentTime -= Time.deltaTime;
        }

        if (currentTime <= 0.0f && !done)
        {
            currentTime = 0.0f;
            timerEnded();
        }
    }

    public void StartTimer()
    {
        paused = false;
        Debug.Log("TIMER STARTED");
    }

    public void PauseTimer()
    {
        paused = true;
    }

    public void ResetTimer()
    {
        currentTime = targetTime;
        done = false;
    }

    private void timerEnded()
    {
        Debug.Log("TIMER ENDED");
        done = true;
        turnManager.SwitchTurn();
        PauseTimer();
        ResetTimer();
    }
}
