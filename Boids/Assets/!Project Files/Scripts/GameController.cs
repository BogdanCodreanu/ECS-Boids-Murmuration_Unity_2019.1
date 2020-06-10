namespace BogdanCodreanu.ECS {
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEngine.UI;
    using TMPro;

    public class GameController : MonoBehaviour {
        public enum GameState {
            DivePredator,
            ControlCamera
        }

        public GameState CurrentGameState { get; set; } = GameState.DivePredator;


        private static GameController instance;
        public static GameController Instance {
            get {
                return instance ?? (instance = FindObjectOfType<GameController>());
            }
        }
        
        [SerializeField]
        private Player player;
        [SerializeField]
        private Button divePredatorButton, controlCameraButton;

        [SerializeField]
        private RectTransform divePredatorInfo, controlCameraInfo;
        [SerializeField]
        private GameObject particleDiedPrefab;

        private void Start() {
            divePredatorButton.onClick.AddListener(OnDivePredator);
            controlCameraButton.onClick.AddListener(OnControlCamera);
            OnDivePredator();
        }

        private void OnDivePredator() {
            CurrentGameState = GameState.DivePredator;
            divePredatorButton.interactable = false;
            controlCameraButton.interactable = true;
            player.ResetForDiving();
            divePredatorInfo.gameObject.SetActive(true);
            controlCameraInfo.gameObject.SetActive(false);
            Time.timeScale = 1f;
        }

        private void OnControlCamera() {
            CurrentGameState = GameState.ControlCamera;
            controlCameraButton.interactable = false;
            divePredatorButton.interactable = true;
            player.ResetForCameraControl();
            divePredatorInfo.gameObject.SetActive(false);
            controlCameraInfo.gameObject.SetActive(true);
        }

        private void Update() {
            if (Input.GetKeyDown(KeyCode.C)) {
                if (CurrentGameState == GameState.ControlCamera) {
                    OnDivePredator();
                } else {
                    OnControlCamera();
                }
            }
            if (Input.GetKeyDown(KeyCode.Space)) {
                float timescale1 = .3f;
                float timescale2 = 0.01f;
                Time.timeScale = Time.timeScale == 1 ? timescale1 : 
                    Time.timeScale == timescale1 ? timescale2 : 1;
            }
        }

        public void KilledBoidAt(Vector3 position) {
            GameObject spawn = Instantiate(particleDiedPrefab, position, 
                particleDiedPrefab.transform.rotation, null);
            Destroy(spawn, 3);
        }
    }
}
