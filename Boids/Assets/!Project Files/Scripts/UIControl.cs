namespace BogdanCodreanu.ECS {
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEngine.UI;
    using TMPro;

    public class UIControl : MonoBehaviour {
        [SerializeField]
        private Player playerFlying;
        [SerializeField]
        private TMP_Text currentSpeedText, currentHeightText, nrOfObstaclesText,
            nrOfKillsText;

        private static UIControl instance;
        public static UIControl Instance {
            get {
                return instance ?? (instance = FindObjectOfType<UIControl>());
            }
        }
        public int NrOfObstacles { get; set; }

        public int NrOfBoidsAlive { get; set; }
        public int NrOfBoidsInitial { get; set; }

        private void Update() {
            currentSpeedText.text = $"Current Speed: {playerFlying.CurrentSpeed.ToString("F1")}";
            currentHeightText.text = $"Current Height: " +
                $"{playerFlying.transform.position.y.ToString("F1")}";
            nrOfObstaclesText.text = $"Nr of Obstacles: " +
                $"{NrOfObstacles.ToString("D")}";
            nrOfKillsText.text = $"Killed boids: " +
                $"{(NrOfBoidsInitial - NrOfBoidsAlive).ToString("D")}";
        }
    }
}
