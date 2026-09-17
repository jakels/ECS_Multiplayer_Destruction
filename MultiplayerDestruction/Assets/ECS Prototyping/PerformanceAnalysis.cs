using System;
using UnityEngine;

namespace ECSRBExample
{
    public class PerformanceAnalysis : MonoBehaviour
    {
        private int frameCount;
        private float framesInTheLastSecond;
        private float oneSecondTimer;
        private float avgFPS;
        private float instantFPS;


        private void Update()
        {
            oneSecondTimer += Time.deltaTime;

            if (oneSecondTimer < 1f)
            {
                framesInTheLastSecond++;
            }
            else
            {
                avgFPS = framesInTheLastSecond;
                oneSecondTimer = 0f;
                framesInTheLastSecond = 0;
            }

            instantFPS = 1f / Time.deltaTime;
            frameCount++;
        }

        void OnGUI()
        {
            // GUI Label with total frame count and AVG FPS
            GUI.Label(new Rect(10, 10, 200, 20), "Total Frames: " + frameCount);
            GUI.Label(new Rect(10, 30, 200, 20), "Average FPS (1 sec): " + avgFPS.ToString("F2"));
            GUI.Label(new Rect(10, 50, 200, 20), "Instant FPS: " + instantFPS.ToString("F2"));
            // GUI Label with time elapsed
            GUI.Label(new Rect(10, 70, 200, 20), "Time Elapsed: " + Time.time.ToString("F2") + "s");
            // Overall average FPS
            float overallAvgFPS = frameCount / Time.time;
            GUI.Label(new Rect(10, 90, 200, 20), "Session Average FPS: " + overallAvgFPS.ToString("F2"));
        }
    }
}