using UnityEngine;
using System.Collections.Generic;
using Craquez.Sim;

namespace Craquez
{
    /// <summary>
    /// Sistema de partículas para efeitos de campo: grama, poeira, deslizamentos, impactos.
    /// Integra com assets de partículas CC0 baixados.
    /// </summary>
    public class FieldParticles : MonoBehaviour
    {
        [Header("Particle Systems")]
        public ParticleSystem grassKickUp;      // Grama voando no chute/passe
        public ParticleSystem slideTrail;       // Rastro de carrinho
        public ParticleSystem dustKick;         // Poeira ao correr/chutar
        public ParticleSystem ballImpact;       // Impacto da bola no chão/trave
        public ParticleSystem goalNetHit;       // Bola na rede
        public ParticleSystem slideSparks;      // Faíscas/poeira no carrinho forte
        public ParticleSystem celebrationConfetti; // Confete no gol

        [Header("Configuration")]
        public float grassKickForce = 2f;
        public float slideTrailDuration = 1.5f;
        public float dustIntensity = 1f;
        public LayerMask groundLayer = -1;

        [Header("Colors by Surface")]
        public Gradient grassColor;
        public Gradient dirtColor;
        public Gradient sandColor;
        public Gradient artificialColor;

        [Header("References")]
        public MatchSim sim;
        public MatchView view;

        // Pools for performance
        Queue<ParticleSystem> _grassPool = new Queue<ParticleSystem>();
        Queue<ParticleSystem> _slidePool = new Queue<ParticleSystem>();
        Queue<ParticleSystem> _dustPool = new Queue<ParticleSystem>();
        Queue<ParticleSystem> _impactPool = new Queue<ParticleSystem>();
        Queue<ParticleSystem> _netPool = new Queue<ParticleSystem>();

        const int POOL_SIZE = 20;

        void Awake()
        {
            InitializePools();
        }

        void InitializePools()
        {
            // Create default particle systems if not assigned
            if (grassKickUp == null) grassKickUp = CreateDefaultGrassKick();
            if (slideTrail == null) slideTrail = CreateDefaultSlideTrail();
            if (dustKick == null) dustKick = CreateDefaultDustKick();
            if (ballImpact == null) ballImpact = CreateDefaultBallImpact();
            if (goalNetHit == null) goalNetHit = CreateDefaultNetHit();
            if (slideSparks == null) slideSparks = CreateDefaultSparks();
            if (celebrationConfetti == null) celebrationConfetti = CreateDefaultConfetti();

            // Pre-warm pools
            for (int i = 0; i < POOL_SIZE; i++)
            {
                _grassPool.Enqueue(Instantiate(grassKickUp, transform));
                _slidePool.Enqueue(Instantiate(slideTrail, transform));
                _dustPool.Enqueue(Instantiate(dustKick, transform));
                _impactPool.Enqueue(Instantiate(ballImpact, transform));
                _netPool.Enqueue(Instantiate(goalNetHit, transform));
            }
        }

        ParticleSystem CreateDefaultGrassKick()
        {
            var ps = new GameObject("GrassKickUp").AddComponent<ParticleSystem>();
            ps.transform.SetParent(transform);
            var main = ps.main;
            main.duration = 0.5f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.05f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.2f, 0.5f, 0.1f, 0.8f),
                new Color(0.1f, 0.3f, 0.05f, 0.8f)
            );
            main.gravityModifier = 1.5f;
            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 15, 25) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 30f;
            shape.radius = 0.1f;
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.y = new ParticleSystem.MinMaxCurve(-2f, 1f);
            return ps;
        }

        ParticleSystem CreateDefaultSlideTrail()
        {
            var ps = new GameObject("SlideTrail").AddComponent<ParticleSystem>();
            ps.transform.SetParent(transform);
            var main = ps.main;
            main.duration = 2f;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 1f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.4f, 0.3f, 0.2f, 0.6f),
                new Color(0.2f, 0.15f, 0.1f, 0.3f)
            );
            main.gravityModifier = 0.5f;
            var emission = ps.emission;
            emission.rateOverTime = 30;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(0.5f, 0.05f, 0.5f);
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.y = new ParticleSystem.MinMaxCurve(0f, 0.5f);
            var limit = ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.limit = new ParticleSystem.MinMaxCurve(2f);
            limit.dampen = 0.5f;
            return ps;
        }

        ParticleSystem CreateDefaultDustKick()
        {
            var ps = new GameObject("DustKick").AddComponent<ParticleSystem>();
            ps.transform.SetParent(transform);
            var main = ps.main;
            main.duration = 0.3f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.6f, 0.5f, 0.4f, 0.5f),
                new Color(0.4f, 0.35f, 0.3f, 0.3f)
            );
            main.gravityModifier = 0.8f;
            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 8, 15) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.15f;
            shape.position = new Vector3(0, 0.02f, 0);
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.y = new ParticleSystem.MinMaxCurve(0.5f, 2f);
            return ps;
        }

        ParticleSystem CreateDefaultBallImpact()
        {
            var ps = new GameObject("BallImpact").AddComponent<ParticleSystem>();
            ps.transform.SetParent(transform);
            var main = ps.main;
            main.duration = 0.2f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 1f, 1f, 0.8f),
                new Color(0.9f, 0.9f, 0.8f, 0.4f)
            );
            main.gravityModifier = 2f;
            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 10, 20) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.05f;
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.y = new ParticleSystem.MinMaxCurve(-1f, 2f);
            var collision = ps.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.mode = ParticleSystemCollisionMode.Collision3D;
            collision.bounce = 0.3f;
            return ps;
        }

        ParticleSystem CreateDefaultNetHit()
        {
            var ps = new GameObject("NetHit").AddComponent<ParticleSystem>();
            ps.transform.SetParent(transform);
            var main = ps.main;
            main.duration = 0.8f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 1f, 1f, 0.9f),
                new Color(0.9f, 0.9f, 0.9f, 0.3f)
            );
            main.gravityModifier = 0.3f;
            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 30, 50) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.5f;
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.z = new ParticleSystem.MinMaxCurve(-2f, 2f);
            return ps;
        }

        ParticleSystem CreateDefaultSparks()
        {
            var ps = new GameObject("SlideSparks").AddComponent<ParticleSystem>();
            ps.transform.SetParent(transform);
            var main = ps.main;
            main.duration = 0.5f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.01f, 0.03f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.8f, 0.3f, 1f),
                new Color(1f, 0.5f, 0.1f, 0.5f)
            );
            main.gravityModifier = 1f;
            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 20, 40) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 15f;
            shape.radius = 0.3f;
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.y = new ParticleSystem.MinMaxCurve(-1f, 1f);
            return ps;
        }

        ParticleSystem CreateDefaultConfetti()
        {
            var ps = new GameObject("Confetti").AddComponent<ParticleSystem>();
            ps.transform.SetParent(transform);
            var main = ps.main;
            main.duration = 3f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2f, 5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.1f);
            // Use a gradient for confetti colors (random between 4 colors)
            var gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(new Color(1f, 0.2f, 0.2f, 1f), 0f),
                    new GradientColorKey(new Color(0.2f, 0.2f, 1f, 1f), 0.33f),
                    new GradientColorKey(new Color(1f, 1f, 0.2f, 1f), 0.66f),
                    new GradientColorKey(new Color(0.2f, 1f, 0.2f, 1f), 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 1f)
                }
            );
            main.startColor = new ParticleSystem.MinMaxGradient(gradient);
            main.gravityModifier = 0.5f;
            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 100, 200) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(20f, 5f, 20f);
            shape.position = new Vector3(0, 10f, 0);
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.y = new ParticleSystem.MinMaxCurve(-3f, -1f);
            var rotation = ps.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-180f, 180f);
            return ps;
        }

        void Start()
        {
            if (sim == null)
            {
                var mc = GameObject.FindObjectOfType<MatchController>();
                if (mc != null) sim = mc.sim;
            }
            if (view == null) view = GameObject.FindObjectOfType<MatchView>();
        }

        // Public methods for triggering effects

        public void OnGrassKick(Vector3 position, Vector3 direction, float force, SurfaceType surface = SurfaceType.Grass)
        {
            var ps = GetPooled(_grassPool, grassKickUp);
            ps.transform.position = position;
            ps.transform.rotation = Quaternion.LookRotation(direction);
            var main = ps.main;
            main.startColor = GetSurfaceColor(surface);
            var shape = ps.shape;
            shape.rotation = new Vector3(90, 0, 0);
            var velocity = ps.velocityOverLifetime;
            velocity.y = new ParticleSystem.MinMaxCurve(force * 0.5f, force * 1.5f);
            ps.Play();
            ReturnToPool(_grassPool, ps, 1f);
        }

        public void OnSlide(Vector3 position, Vector3 direction, float speed, float duration)
        {
            var ps = GetPooled(_slidePool, slideTrail);
            ps.transform.position = position + Vector3.up * 0.02f;
            ps.transform.rotation = Quaternion.LookRotation(direction);
            var main = ps.main;
            main.duration = duration;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, duration);
            var emission = ps.emission;
            emission.rateOverTime = Mathf.Lerp(10, 50, speed / 8f);
            ps.Play();
            ReturnToPool(_slidePool, ps, duration + 1f);
        }

        public void OnDustKick(Vector3 position, Vector3 direction, float intensity, SurfaceType surface = SurfaceType.Grass)
        {
            var ps = GetPooled(_dustPool, dustKick);
            ps.transform.position = position;
            ps.transform.rotation = Quaternion.LookRotation(direction);
            var main = ps.main;
            main.startColor = GetSurfaceColor(surface);
            var emission = ps.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, Mathf.RoundToInt(8 * intensity), Mathf.RoundToInt(15 * intensity)) });
            var velocity = ps.velocityOverLifetime;
            velocity.y = new ParticleSystem.MinMaxCurve(0.5f * intensity, 2f * intensity);
            ps.Play();
            ReturnToPool(_dustPool, ps, 0.5f);
        }

        public void OnBallImpact(Vector3 position, Vector3 velocity, float force, bool isGoal = false)
        {
            if (isGoal)
            {
                var ps = GetPooled(_netPool, goalNetHit);
                ps.transform.position = position;
                var main = ps.main;
                main.startSpeed = new ParticleSystem.MinMaxCurve(velocity.magnitude * 0.5f, velocity.magnitude * 1.5f);
                ps.Play();
                ReturnToPool(_netPool, ps, 1.5f);
            }
            else
            {
                var ps = GetPooled(_impactPool, ballImpact);
                ps.transform.position = position;
                var main = ps.main;
                main.startSpeed = new ParticleSystem.MinMaxCurve(force * 0.5f, force * 2f);
                ps.Play();
                ReturnToPool(_impactPool, ps, 0.5f);
            }
        }

        public void OnHardSlide(Vector3 position, Vector3 direction)
        {
            // Sparks + dust combo
            var sparks = Instantiate(slideSparks, position, Quaternion.LookRotation(direction), transform);
            sparks.Play();
            Destroy(sparks.gameObject, 1f);

            OnDustKick(position, direction, 1.5f, SurfaceType.Dirt);
        }

        public void OnGoal(Vector3 goalPosition, int scoringTeam)
        {
            // Confetti at goal
            var confetti = Instantiate(celebrationConfetti, goalPosition + Vector3.up * 5f, Quaternion.identity, transform);
            confetti.Play();
            Destroy(confetti.gameObject, 6f);

            // Net shake effect would be handled by Goal.cs
        }

        // Helper methods
        ParticleSystem GetPooled(Queue<ParticleSystem> pool, ParticleSystem prefab)
        {
            ParticleSystem ps;
            if (pool.Count > 0)
            {
                ps = pool.Dequeue();
            }
            else
            {
                ps = Instantiate(prefab, transform);
            }
            return ps;
        }

        void ReturnToPool(Queue<ParticleSystem> pool, ParticleSystem ps, float delay)
        {
            StartCoroutine(ReturnToPoolRoutine(pool, ps, delay));
        }

        System.Collections.IEnumerator ReturnToPoolRoutine(Queue<ParticleSystem> pool, ParticleSystem ps, float delay)
        {
            yield return new WaitForSeconds(delay);
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.transform.SetParent(transform);
            pool.Enqueue(ps);
        }

        Color GetSurfaceColor(SurfaceType surface)
        {
            switch (surface)
            {
                case SurfaceType.Grass: return grassColor.Evaluate(Random.value);
                case SurfaceType.Dirt: return dirtColor.Evaluate(Random.value);
                case SurfaceType.Sand: return sandColor.Evaluate(Random.value);
                case SurfaceType.Artificial: return artificialColor.Evaluate(Random.value);
                default: return grassColor.Evaluate(Random.value);
            }
        }

        // Public enum for surface types
        public enum SurfaceType
        {
            Grass,
            Dirt,
            Sand,
            Artificial
        }
    }
}