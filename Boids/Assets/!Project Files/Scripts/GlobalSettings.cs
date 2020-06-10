using BogdanCodreanu.ECS;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GlobalSettings : MonoBehaviour {

    public static GlobalSettings Instance { get; private set; }

    [SerializeField]
    private RectTransform containerForSettings;
    [SerializeField]
    private ControlBoidSetting settingsPrefab;
    private List<ControlBoidSetting> settings = new List<ControlBoidSetting>();

    [SerializeField]
    private Player playerController;

    [SerializeField]
    private GameObject uiSettings, uiGameplay;

    private bool isSettings = true;

    //  ---------- settings
    public float MoveSpeed = 1;

    public float SeparationWeight = 1;
    public float AlignmentWeight = 1;
    public float CohesionWeight = 1;

    public float WalkToFlockCenterWeight = 1;
    public float maintainYWeight = 1;
    public float yLength = 1;

    public float BoundaryWeight = 1;
    public float SphereBoundarySize = 1;
    public float goToTargetsWeight = 1;

    public float avoidObstaclesWeight = 1;
    public float avoidDistanceObstacles = 1;
    public float avoidTerrainWeight = 1;
    // end of settings


    private void Awake() {
        Instance = this;
        InitializeUISettings();
    }

    private void Start() {
        EnterSettings();
    }

    private void InitializeUISettings() {
        CreatePropSetting("Boids Speed", 10, 200, 110, f => MoveSpeed = f);

        CreatePropSetting("Separation", 0, 4, 2, f => SeparationWeight = f);
        CreatePropSetting("Alignment", 0, 3, 1, f => AlignmentWeight = f);
        CreatePropSetting("Cohesion", 0, .5f, .1f, f => CohesionWeight = f);

        CreatePropSetting("Gloab Cohesion", 0, .02f, .007f, f => WalkToFlockCenterWeight = f);
        CreatePropSetting("Maintain Y Weight", 0, .003f, .001f, f => maintainYWeight = f);
        CreatePropSetting("Y Length to maintain", 0, 300, 148.57f, f => yLength = f);

        CreatePropSetting("Sphere Boundary Weight", 0, 1, 0, f => BoundaryWeight = f);
        CreatePropSetting("Sphere Boundary Size", 50, 800, 500, f => SphereBoundarySize = f);
        CreatePropSetting("Go To Targets Weight", 0, 2, 1, f => goToTargetsWeight = f);

        CreatePropSetting("Avoid Predators Weight", 0, 15, 10, f => avoidObstaclesWeight = f);
        CreatePropSetting("Distance to avoid Predators", 50, 1500, 650, f => avoidDistanceObstacles = f);
        CreatePropSetting("Avoid Terrain Weight", 0, 200, 100, f => avoidTerrainWeight = f);

        void CreatePropSetting(string propName, float min, float max, float defaultVal, Action<float> sliderChanged) {
            sliderChanged(defaultVal);
            var setting = Instantiate(settingsPrefab.gameObject, containerForSettings.transform).GetComponent<ControlBoidSetting>();
            setting.Init(min, max, defaultVal, propName, sliderChanged);
            settings.Add(setting);
        }
    }

    // called by ui button
    public void SetDefault() {
        foreach (var setting in settings) {
            setting.SetToDefault();
        }
    }

    private void EnterSettings() {
        uiSettings.SetActive(true);
        uiGameplay.SetActive(false);
        playerController.Disable();
        isSettings = true;
    }

    private void EnterGameplay() {
        GameController.Instance.CurrentGameState = GameController.GameState.DivePredator;
        uiSettings.SetActive(false);
        uiGameplay.SetActive(true);
        playerController.Enable();
        isSettings = false;
    }

    private void Update() {
        if (Input.GetKeyDown(KeyCode.Escape)) {
            if (isSettings) {
                EnterGameplay();
            } else {
                EnterSettings();
            }
        }
    }
}