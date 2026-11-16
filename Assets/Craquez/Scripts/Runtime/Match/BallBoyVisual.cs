using UnityEngine;
using System.Collections.Generic;

namespace Craquez
{
    /// <summary>
    /// Visual component para gandulas/ball boys: animações de corrida, arremesso,
    /// pegar/colocar bola no cone, transições suaves entre estados.
    /// </summary>
    [RequireComponent(typeof(AthleteView))]
    public class BallBoyVisual : MonoBehaviour
    {
        [Header("State")]
        public BoyTask task = BoyTask.Idle;
        public bool carryingBall = false;
        public int stationIndex = -1;

        [Header("Movement")]
        public float walkSpeed = 1.4f;
        public float runSpeed = 6f;
        public float carrySpeed = 4f;
        public float acceleration = 8f;
        public float turnSpeed = 10f;

        [Header("Animation Clips")]
        public string idleClip = "Idle";
        public string walkClip = "Walk";
        public string runClip = "Run";
        public string carryWalkClip = "Walk";
        public string carryRunClip = "Run";
        public string throwOverheadClip = "GkThrow";    // Lateral
        public string throwUnderhandClip = "GkRoll";    // Goal kick / ground pass
        public string placeClip = "GkPlace";            // Colocar no cone
        public string pickupClip = "GkPickup";          // Pegar bola (se existir)
        public string celebrateClip = "Celebrate";      // Comemoração (raro)

        [Header("Ball Visual")]
        public Transform ballCarryPoint;    // Onde a bola fica quando carregada
        public GameObject carriedBallVisual; // Visual da bola sendo carregada

        [Header("Throw Settings")]
        public float throwPreparationTime = 0.3f;
        public float throwReleaseTime = 0.5f;
        public float throwPower = 15f;

        // Components
        AthleteView _av;
        AthleteRig _rig;
        Transform _ballVisual;
        MatchView.BallBoys.Boy _boyData;

        // State
        Vector3 _targetPos;
        Vector3 _velocity;
        BoyTask _currentTask = BoyTask.Idle;
        bool _isCarrying = false;
        float _taskTimer = 0f;
        bool _throwPrepared = false;
        float _throwTimer = 0f;
        Vector3 _throwTarget;
        Vector3 _lastPos;

        void Awake()
        {
            _av = GetComponent<AthleteView>();
            _rig = GetComponentInParent<AthleteRig>();
        }

        public void Initialize(MatchView.BallBoys.Boy data)
        {
            _boyData = data;
            _targetPos = data.home;
        }

        void Update()
        {
            if (_boyData == null) return;

            float dt = Time.deltaTime;
            _taskTimer += dt;

            // Update task from data
            if (_currentTask != _boyData.task)
            {
                OnTaskChanged(_boyData.task);
            }

            // Update carrying state
            if (_isCarrying != _boyData.carry)
            {
                _isCarrying = _boyData.carry;
                UpdateCarriedBallVisual();
            }

            // Update position/rotation based on task
            UpdateMovement();

            // Handle task-specific logic
            switch (task)
            {
                case BoyTask.Throwing:
                    UpdateThrowing();
                    break;
                case BoyTask.PlacingBall:
                    UpdatePlacing();
                    break;
                case BoyTask.PickingUp:
                    UpdatePickingUp();
                    break;
            }

            // Update ball visual position
            UpdateCarriedBallPosition();

            // Update animation
            UpdateAnimation();
        }

        void OnTaskChanged(BoyTask newTask)
        {
            var oldTask = task;
            task = newTask;
            _taskTimer = 0f;
            _throwPrepared = false;
            _throwTimer = 0f;

            // Handle task transitions
            if (newTask == BoyTask.Throwing)
            {
                _throwPrepared = false;
                _throwTimer = 0f;
            }
            else if (newTask == BoyTask.PlacingBall)
            {
                // Will play place animation
            }
            else if (newTask == BoyTask.PickingUp)
            {
                // Will play pickup animation
            }
        }

        void UpdateMovement()
        {
            if (AthleteAnimator.Mocap) return; // Let mocap handle it

            if (task == BoyTask.Idle)
            {
                // Return home
                _targetPos = _boyData?.home ?? transform.position;
                MoveTowards(_targetPos, walkSpeed);
            }
            else if (task == BoyTask.GoingToBall || task == BoyTask.GoingToKeeper)
            {
                _targetPos = _boyData?.target ?? transform.position;
                MoveTowards(_targetPos, runSpeed);
            }
            else if (task == BoyTask.CarryingToCone)
            {
                _targetPos = _boyData?.target ?? transform.position;
                MoveTowards(_targetPos, carrySpeed);
            }
            else if (task == BoyTask.ReturningHome)
            {
                _targetPos = _boyData?.home ?? transform.position;
                MoveTowards(_targetPos, runSpeed);
            }
            else if (task == BoyTask.Throwing || task == BoyTask.PlacingBall)
            {
                // Stay in place during action
                _velocity = Vector3.zero;
            }
        }

        void MoveTowards(Vector3 target, float speed)
        {
            var toTarget = target - transform.position;
            toTarget.y = 0;
            float dist = toTarget.magnitude;

            if (dist > 0.1f)
            {
                Vector3 desiredVel = toTarget.normalized * Mathf.Min(speed, dist * 3f);
                _velocity = Vector3.MoveTowards(_velocity, desiredVel, 10f * Time.deltaTime);
                transform.position += _velocity * Time.deltaTime;

                // Smooth rotation
                if (_velocity.sqrMagnitude > 0.01f)
                {
                    transform.rotation = Quaternion.Slerp(
                        transform.rotation,
                        Quaternion.LookRotation(_velocity.normalized),
                        turnSpeed * Time.deltaTime
                    );
                }
            }
            else
            {
                _velocity = Vector3.zero;
            }
        }

        void UpdateThrowing()
        {
            _throwTimer += Time.deltaTime;

            if (!_throwPrepared && _taskTimer > 0.1f)
            {
                PrepareThrow();
            }

            if (_taskTimer > 0.5f && !_throwPrepared)
            {
                ExecuteThrow();
            }

            // Auto-complete after animation
            if (_taskTimer > 2f)
            {
                CompleteThrow();
            }
        }

        void PrepareThrow()
        {
            _throwPrepared = true;
            _throwTimer = 0f;

            // Face target
            var target = _boyData?.target ?? transform.position;
            transform.rotation = Quaternion.LookRotation((target - transform.position).normalized);

            // Play throw animation
            PlayThrowAnimation();
        }

        void ExecuteThrow()
        {
            // Ball is released at this point - handled by MatchView.BallBoyToss
            // We just ensure visual is correct
        }

        void CompleteThrow()
        {
            // Task will be changed by BallBoys logic
        }

        void UpdatePlacing()
        {
            if (_taskTimer > 0.5f)
            {
                // Ball placed on cone
                if (carryingBall)
                {
                    carryingBall = false;
                    UpdateCarriedBallVisual();
                }
            }
        }

        void UpdatePickingUp()
        {
            if (_taskTimer > 0.5f)
            {
                // Ball picked up
                if (!carryingBall)
                {
                    carryingBall = true;
                    UpdateCarriedBallVisual();
                }
            }
        }

        void UpdateCarriedBallVisual()
        {
            if (_ballVisual == null && _rig != null)
            {
                // Try to find or create ball visual
                _ballVisual = _rig.transform.Find("BallCarryVisual");
                if (_ballVisual == null && carriedBallVisual != null)
                {
                    _ballVisual = Instantiate(carriedBallVisual, _rig.transform).transform;
                    _ballVisual.name = "BallCarryVisual";
                }
            }

            if (_ballVisual != null)
            {
                _ballVisual.gameObject.SetActive(carryingBall);
            }
        }

        void UpdateCarriedBallPosition()
        {
            if (_ballVisual != null && carryingBall)
            {
                // Position ball in front of body at waist height
                Vector3 carryPos = transform.position + transform.forward * 0.35f + Vector3.up * 0.9f;
                _ballVisual.position = Vector3.Lerp(_ballVisual.position, carryPos, 15f * Time.deltaTime);
                _ballVisual.rotation = transform.rotation * Quaternion.Euler(90, 0, 0);
            }
        }

        void UpdateAnimation()
        {
            if (_av == null || _av.anim == null) return;

            float speed = _velocity.magnitude;
            bool moving = speed > 0.2f;

            string targetClip = idleClip;

            if (task == BoyTask.Throwing)
            {
                targetClip = _throwPrepared ? "GkThrow" : "GkThrow"; // Will be overridden
            }
            else if (task == BoyTask.PlacingBall)
            {
                targetClip = "GkPlace";
            }
            else if (task == BoyTask.PickingUp)
            {
                targetClip = "GkPlace"; // Reverse
            }
            else if (carryingBall)
            {
                targetClip = moving ? carryRunClip : carryWalkClip;
            }
            else if (moving)
            {
                float speedRatio = speed / runSpeed;
                if (speedRatio > 0.7f) targetClip = runClip;
                else targetClip = walkClip;
            }
            else
            {
                targetClip = idleClip;
            }

            // Set animation via AthleteAnimator
            if (_av.anim != null && _av.anim.CurrentAction != targetClip)
            {
                // Let AthleteView handle animation via its normal pipeline
                // We just ensure the right parameters are set
            }

            // Set animation parameters for AthleteAnimator
            if (_av.anim != null)
            {
                _av.anim.cadence = carryingBall ? 1.1f : (moving ? Mathf.Lerp(1f, 1.5f, speed / runSpeed) : 1f);
            }
        }

        public void SetTask(BoyTask newTask, Vector3 target = default)
        {
            task = newTask;
            if (target != default) _targetPos = target;
        }

        public void SetCarrying(bool carry)
        {
            _isCarrying = carry;
        }

        public void SetThrowTarget(Vector3 target)
        {
            _throwTarget = target;
        }

        public void OnBallReleased()
        {
            // Called when ball is actually thrown
            carryingBall = false;
            UpdateCarriedBallVisual();
        }

        // Animation events (called from animation clips)
        public void OnThrowRelease()
        {
            // Ball leaves hands - notify MatchView
            if (_boyData != null)
            {
                MatchView.BallBoyToss?.Invoke(); // If static event exists
            }
        }

        public void OnPlaceComplete()
        {
            // Ball placed on cone
        }

        public void OnPickupComplete()
        {
            // Ball picked up
        }

        void OnDrawGizmosSelected()
        {
            if (_ballVisual != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(_ballVisual.position, 0.1f);
            }
        }
    }

    public enum BoyTask
    {
        Idle = 0,
        GoingToBall = 1,
        CarryingToCone = 2,
        ReturningHome = 3,
        Throwing = 4,
        PlacingBall = 5,
        PickingUp = 6,
        GoingToKeeper = 7
    }
}