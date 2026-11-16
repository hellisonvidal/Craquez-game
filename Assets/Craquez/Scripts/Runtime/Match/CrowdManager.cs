using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using Craquez.Sim;

namespace Craquez
{
    /// <summary>
    /// Gerencia a atmosfera da torcida: sons, animações, reações a eventos do jogo.
    /// Integra com os assets de som CC0 baixados.
    /// </summary>
    public class CrowdManager : MonoBehaviour
    {
        [Header("Audio Configuration")]
        public AudioMixerGroup mixerGroup;
        public AudioSource ambientSource;
        public AudioSource reactionSource;
        public AudioSource chantSource;

        [Header("Ambient Crowd Sounds")]
        public AudioClip[] ambientLow;      // Murmúrio baixo
        public AudioClip[] ambientMedium;   // Conversas normais
        public AudioClip[] ambientHigh;     // Torcida animada

        [Header("Reaction Sounds")]
        public AudioClip[] cheerGoal;           // Gol
        public AudioClip[] cheerSave;           // Defesa difícil
        public AudioClip[] cheerChance;         // Chance de gol
        public AudioClip[] booFoul;             // Falta contra
        public AudioClip[] booReferee;          // Decisão do juiz
        public AudioClip[] chantTeam;           // Cânticos do time
        public AudioClip[] whistleReaction;     // Apito final/início

        [Header("Stadium Sounds")]
        public AudioClip[] stadiumAmbience;     // Ambiente geral do estádio
        public AudioClip[] drumBeats;           // Batucada/torcida organizada

        [Header("Configuration")]
        [Range(0f, 1f)] public float baseVolume = 0.6f;
        [Range(0f, 1f)] public float maxVolume = 1f;
        public float fadeSpeed = 2f;
        public float reactionCooldown = 1.5f;

        [Header("References")]
        public MatchSim sim;
        public MatchView view;

        // State
        float _currentAmbientIntensity = 0f;
        float _targetAmbientIntensity = 0f;
        float _lastReactionTime = -999f;
        int _currentAmbientIndex = -1;
        float _ambientChangeTimer = 0f;
        float _ambientChangeInterval = 30f;
        Coroutine _chantCoroutine;

        void Awake()
        {
            SetupAudioSources();
        }

        void SetupAudioSources()
        {
            if (ambientSource == null)
            {
                ambientSource = gameObject.AddComponent<AudioSource>();
                ambientSource.loop = true;
                ambientSource.playOnAwake = false;
                ambientSource.spatialBlend = 0f; // 2D
                if (mixerGroup != null) ambientSource.outputAudioMixerGroup = mixerGroup;
            }

            if (reactionSource == null)
            {
                reactionSource = gameObject.AddComponent<AudioSource>();
                reactionSource.loop = false;
                reactionSource.playOnAwake = false;
                reactionSource.spatialBlend = 0f;
                if (mixerGroup != null) reactionSource.outputAudioMixerGroup = mixerGroup;
            }

            if (chantSource == null)
            {
                chantSource = gameObject.AddComponent<AudioSource>();
                chantSource.loop = true;
                chantSource.playOnAwake = false;
                chantSource.spatialBlend = 0f;
                if (mixerGroup != null) chantSource.outputAudioMixerGroup = mixerGroup;
            }
        }

        void Start()
        {
            if (sim == null)
            {
                var mc = GameObject.FindObjectOfType<MatchController>();
                if (mc != null) sim = mc.sim;
            }
            if (view == null) view = GameObject.FindObjectOfType<MatchView>();

            // Start ambient
            PlayRandomAmbient();
            if (stadiumAmbience.Length > 0)
            {
                PlayStadiumAmbience();
            }
        }

        void Update()
        {
            if (sim == null) return;

            UpdateAmbientIntensity();
            UpdateAmbientTrack();
            HandleGameEvents();
            UpdateChants();
        }

        void UpdateAmbientIntensity()
        {
            // Base intensity on game state
            float target = baseVolume;

            switch (sim.state)
            {
                case MatchState.Playing:
                    target = baseVolume + 0.2f;
                    // Increase with ball near goals
                    if (Mathf.Abs(sim.ball.pos.x) > sim.halfLen * 0.7f)
                        target += 0.15f;
                    // Increase with shots
                    if (sim.lastShotTick > 0 && (sim.tick - sim.lastShotTick) < 120)
                        target += 0.2f;
                    break;
                case MatchState.Foul:
                case MatchState.Restart:
                    target = baseVolume + 0.1f;
                    break;
                case MatchState.Goal:
                    target = maxVolume;
                    break;
                case MatchState.Halftime:
                case MatchState.Fulltime:
                    target = baseVolume * 0.5f;
                    break;
            }

            _targetAmbientIntensity = Mathf.Clamp(target, 0f, maxVolume);
            _currentAmbientIntensity = Mathf.MoveTowards(_currentAmbientIntensity, _targetAmbientIntensity, fadeSpeed * Time.deltaTime);
            ambientSource.volume = _currentAmbientIntensity;
        }

        void UpdateAmbientTrack()
        {
            _ambientChangeTimer += Time.deltaTime;
            if (_ambientChangeTimer >= _ambientChangeInterval)
            {
                _ambientChangeTimer = 0f;
                PlayRandomAmbient();
            }
        }

        void PlayRandomAmbient()
        {
            if (ambientLow.Length == 0) return;

            int newIndex;
            do { newIndex = Random.Range(0, ambientLow.Length); }
            while (newIndex == _currentAmbientIndex && ambientLow.Length > 1);

            _currentAmbientIndex = newIndex;
            ambientSource.clip = ambientLow[_currentAmbientIndex];
            ambientSource.Play();
        }

        void PlayStadiumAmbience()
        {
            if (stadiumAmbience.Length > 0)
            {
                // Could add a second AudioSource for layered ambience
            }
        }

        void HandleGameEvents()
        {
            if (sim.events.Count == 0) return;

            foreach (var e in sim.events)
            {
                if (Time.time - _lastReactionTime < reactionCooldown) break;

                switch (e.type)
                {
                    case "goal":
                        PlayReaction(cheerGoal, 1f);
                        TriggerChant();
                        break;
                    case "save":
                        if (e.kind == "dive" || e.kind == "catch")
                            PlayReaction(cheerSave, 0.8f);
                        break;
                    case "kick":
                        if (e.kind == "shot")
                            PlayReaction(cheerChance, 0.6f);
                        break;
                    case "foul":
                        if (e.team != sim.ball.owner) // Foul against ball carrier
                            PlayReaction(booFoul, 0.7f);
                        break;
                    case "whistle":
                        if (e.kind == "halftime" || e.kind == "fulltime")
                            PlayReaction(whistleReaction, 0.8f);
                        else if (e.kind == "freekick" || e.kind == "throwin")
                            PlayReaction(cheerChance, 0.4f);
                        break;
                    case "card":
                        if (e.kind == "yellow" || e.kind == "red")
                            PlayReaction(booReferee, 0.7f);
                        break;
                }
            }
        }

        void PlayReaction(AudioClip[] clips, float volume)
        {
            if (clips == null || clips.Length == 0) return;
            if (Time.time - _lastReactionTime < reactionCooldown) return;

            var clip = clips[Random.Range(0, clips.Length)];
            reactionSource.PlayOneShot(clip, volume * _currentAmbientIntensity);
            _lastReactionTime = Time.time;
        }

        void TriggerChant()
        {
            if (chantTeam.Length == 0 || chantSource.isPlaying) return;

            if (_chantCoroutine != null) StopCoroutine(_chantCoroutine);
            _chantCoroutine = StartCoroutine(PlayChantRoutine());
        }

        System.Collections.IEnumerator PlayChantRoutine()
        {
            chantSource.clip = chantTeam[Random.Range(0, chantTeam.Length)];
            chantSource.volume = _currentAmbientIntensity * 0.8f;
            chantSource.Play();

            yield return new WaitForSeconds(chantSource.clip.length + Random.Range(5f, 15f));

            // Chance to repeat
            if (Random.value < 0.3f)
            {
                _chantCoroutine = StartCoroutine(PlayChantRoutine());
            }
            else
            {
                _chantCoroutine = null;
            }
        }

        void UpdateChants()
        {
            // Auto-start chants during exciting moments
            if (_chantCoroutine == null && chantTeam.Length > 0 && !chantSource.isPlaying)
            {
                if (sim.state == MatchState.Playing)
                {
                    int attackingTeam = sim.ball.owner >= 0 ? sim.players[sim.ball.owner].team : -1;
                    if (attackingTeam >= 0 && sim.ball.pos.x * sim.TeamDir(attackingTeam) > sim.halfLen * 0.5f)
                    {
                        if (Random.value < 0.001f * Time.deltaTime)
                            TriggerChant();
                    }
                }
            }
        }

        // Public methods for external triggers
        public void OnGoal(int team) => PlayReaction(cheerGoal, 1f);
        public void OnSave() => PlayReaction(cheerSave, 0.8f);
        public void OnBigChance() => PlayReaction(cheerChance, 0.6f);
        public void OnFoulAgainstUs() => PlayReaction(booFoul, 0.7f);
        public void OnRefereeDecision() => PlayReaction(booReferee, 0.7f);
        public void OnWhistle(string kind) => PlayReaction(whistleReaction, 0.5f);
    }
}