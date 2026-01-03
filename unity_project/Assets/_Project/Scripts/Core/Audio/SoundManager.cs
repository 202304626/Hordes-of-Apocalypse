using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [System.Serializable]
    public class Sound
    {
        public SoundType type;
        public AudioClip clip;
        [Range(0f, 1f)]
        public float volume = 1f;
        [Range(0.1f, 3f)]
        public float pitch = 1f;
        public bool loop = false;
        [HideInInspector]
        public AudioSource source;
    }

    public enum SoundType
    {
        TowerPlaced,
        TowerSold,
        TowerUpgraded,
        LifeLost,
        Victory,
        Defeat,
        ButtonClick,
        WaveStart,
        PreparationStart
    }

    [Header("Configuración de Audio")]
    public Sound[] sounds;
    public AudioSource musicSource;
    public float globalVolume = 1f;
    public bool muteAll = false;

    private Dictionary<SoundType, Sound> soundDictionary = new Dictionary<SoundType, Sound>();
    private bool isInitialized = false;

    private GameStateManager.GameState lastGameState;
    private bool hasPlayedVictory = false;
    private bool hasPlayedDefeat = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Initialize();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        UnsubscribeFromEvents();
        SubscribeToGameEvents();
        ResetStateFlags();
    }

    private void Initialize()
    {
        if (isInitialized) return;

        foreach (Sound sound in sounds)
        {
            sound.source = gameObject.AddComponent<AudioSource>();
            sound.source.clip = sound.clip;
            sound.source.volume = sound.volume * globalVolume;
            sound.source.pitch = sound.pitch;
            sound.source.loop = sound.loop;

            soundDictionary[sound.type] = sound;
        }

        SubscribeToGameEvents();

        if (GameStateManager.Instance != null)
        {
            lastGameState = GameStateManager.Instance.GetCurrentState();
        }

        isInitialized = true;
    }

    private void Update()
    {
        CheckGameStateChanges();
    }

    private void CheckGameStateChanges()
    {
        if (GameStateManager.Instance == null) return;

        GameStateManager.GameState currentState = GameStateManager.Instance.GetCurrentState();

        if (currentState != lastGameState)
        {
            switch (currentState)
            {
                case GameStateManager.GameState.Victory:
                    if (!hasPlayedVictory)
                    {
                        PlaySound(SoundType.Victory);
                        hasPlayedVictory = true;
                    }
                    break;

                case GameStateManager.GameState.Defeat:
                    if (!hasPlayedDefeat)
                    {
                        PlaySound(SoundType.Defeat);
                        hasPlayedDefeat = true;
                    }
                    break;

                case GameStateManager.GameState.Preparing:
                    ResetStateFlags();
                    break;
            }

            lastGameState = currentState;
        }
    }

    private void ResetStateFlags()
    {
        hasPlayedVictory = false;
        hasPlayedDefeat = false;
    }

    private void SubscribeToGameEvents()
    {
        UnsubscribeFromEvents();

        GameEvents.OnTowerPlaced += (tower) => PlaySound(SoundType.TowerPlaced);
        GameEvents.OnTowerSold += (tower) => PlaySound(SoundType.TowerSold);
        GameEvents.OnTowerUpgraded += (tower) => PlaySound(SoundType.TowerUpgraded);
        GameEvents.OnLivesChanged += OnLivesChanged;
        GameEvents.OnWaveChanged += (waveIndex) => PlaySound(SoundType.WaveStart);
        GameEvents.OnPreparationStarted += () => PlaySound(SoundType.PreparationStart);
        GameEvents.OnButtonClick += () => PlaySound(SoundType.ButtonClick);
    }

    private void UnsubscribeFromEvents()
    {
        GameEvents.OnTowerPlaced -= (tower) => PlaySound(SoundType.TowerPlaced);
        GameEvents.OnTowerSold -= (tower) => PlaySound(SoundType.TowerSold);
        GameEvents.OnTowerUpgraded -= (tower) => PlaySound(SoundType.TowerUpgraded);
        GameEvents.OnLivesChanged -= OnLivesChanged;
        GameEvents.OnWaveChanged -= (waveIndex) => PlaySound(SoundType.WaveStart);
        GameEvents.OnPreparationStarted -= () => PlaySound(SoundType.PreparationStart);
        GameEvents.OnButtonClick -= () => PlaySound(SoundType.ButtonClick);
    }

    private void OnLivesChanged(int newLives)
    {
        PlayerManager playerManager = FindObjectOfType<PlayerManager>();
        if (playerManager != null && newLives < playerManager.GetMaxLives())
        {
            PlaySound(SoundType.LifeLost);
        }
    }

    public void PlaySound(SoundType soundType)
    {
        if (muteAll || !isInitialized) return;

        if (soundDictionary.ContainsKey(soundType))
        {
            Sound sound = soundDictionary[soundType];
            if (sound.clip != null && sound.source != null)
            {
                sound.source.Play();
            }
        }
    }

    public void ResetSoundManager()
    {
        UnsubscribeFromEvents();
        ResetStateFlags();

        foreach (var sound in soundDictionary.Values)
        {
            if (sound.source != null)
            {
                Destroy(sound.source);
            }
        }

        soundDictionary.Clear();
        isInitialized = false;
        Initialize();
    }

    private void OnDestroy()
    {
        UnsubscribeFromEvents();
    }
}