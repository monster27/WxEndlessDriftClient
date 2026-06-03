using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class LoadingView : MonoBehaviour
{
    public Text progressText;
    public Slider progressSlider;

    private float minTaskTime = 0.1f;
    private float loadingStartTime;
    private List<string> completedTasks = new List<string>();
    private List<string> pendingTasks = new List<string>();
    private bool isLoadingComplete = false;
    private bool hasTriggeredComplete = false;
    private float completeTriggerTime = -1f;

    private float targetProgress = 0f;
    private float currentProgress = 0f;
    private float smoothSpeed = 3f;

    private string currentLoadingTask = "";
    private float currentTaskStartTime = 0f;

    public System.Action onAllLoadingComplete;

    private void Update()
    {
        if (isLoadingComplete)
        {
            targetProgress = 1f;

            if (progressSlider != null)
            {
                progressSlider.value = 1f;
            }

            if (hasTriggeredComplete && completeTriggerTime > 0 && Time.time - completeTriggerTime >= 0.5f)
            {
                Hide();
                completeTriggerTime = -1f;
            }
        }
        else
        {
            float elapsedTime = Time.time - loadingStartTime;
            int totalTasks = completedTasks.Count + pendingTasks.Count;

            if (totalTasks > 0)
            {
                float taskProgress = (float)completedTasks.Count / totalTasks;
                float timeProgress = Mathf.Clamp01((elapsedTime - minTaskTime) / (totalTasks * minTaskTime));
                targetProgress = taskProgress * timeProgress;
            }
            else
            {
                targetProgress = 1f;
            }

            if (progressSlider != null)
            {
                if (Mathf.Abs(currentProgress - targetProgress) > 0.01f)
                {
                    currentProgress = Mathf.Lerp(currentProgress, targetProgress, Time.deltaTime * smoothSpeed);
                    currentProgress = Mathf.Clamp01(currentProgress);
                }
                else
                {
                    currentProgress = targetProgress;
                }
                progressSlider.value = currentProgress;
            }

            if (AllTasksCompleted() && elapsedTime >= totalTasks * minTaskTime)
            {
                TriggerLoadingComplete();
            }
        }

        UpdateProgressText();
    }

    private void TriggerLoadingComplete()
    {
        if (hasTriggeredComplete) return;

        isLoadingComplete = true;
        targetProgress = 1f;
        currentProgress = 1f;
        completeTriggerTime = Time.time;

        if (progressSlider != null)
        {
            progressSlider.value = 1f;
        }

        if (progressText != null)
        {
            progressText.text = "加载完成!";
        }

        hasTriggeredComplete = true;

        if (onAllLoadingComplete != null)
        {
            onAllLoadingComplete();
        }
    }

    public void Init()
    {
        completedTasks.Clear();
        pendingTasks.Clear();
        isLoadingComplete = false;
        hasTriggeredComplete = false;
        completeTriggerTime = -1f;
        currentProgress = 0f;
        targetProgress = 0f;
        currentLoadingTask = "";

        if (progressSlider != null)
        {
            progressSlider.value = 0f;
        }
        if (progressText != null)
        {
            progressText.text = "";
        }
    }

    public void AddLoadingTask(string taskName)
    {
        if (isLoadingComplete) return;

        if (!pendingTasks.Contains(taskName))
        {
            pendingTasks.Add(taskName);
        }
        currentLoadingTask = taskName;
    }

    public void CompleteLoadingTask(string taskName)
    {
        if (isLoadingComplete) return;

        if (pendingTasks.Contains(taskName))
        {
            pendingTasks.Remove(taskName);
            completedTasks.Add(taskName);
        }

        if (pendingTasks.Count > 0)
        {
            currentLoadingTask = pendingTasks[0];
        }
        else
        {
            currentLoadingTask = "";
        }
    }

    private void UpdateProgressText()
    {
        if (progressText == null) return;

        string text = "";

        if (isLoadingComplete)
        {
            text = "加载完成!\n";
            text += "------------------------\n";
            foreach (var task in completedTasks)
            {
                text += $"✓ {task}\n";
            }
        }
        else
        {
            if (currentLoadingTask != "")
            {
                text += $"正在加载: {currentLoadingTask}\n";
            }
            text += "------------------------\n";
            foreach (var task in completedTasks)
            {
                text += $"✓ {task}\n";
            }
            foreach (var task in pendingTasks)
            {
                text += $"○ {task}\n";
            }
        }

        progressText.text = text;
    }

    private bool AllTasksCompleted()
    {
        return pendingTasks.Count == 0;
    }

    public void Show()
    {
        gameObject.SetActive(true);
        Init();
        loadingStartTime = Time.time;
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}