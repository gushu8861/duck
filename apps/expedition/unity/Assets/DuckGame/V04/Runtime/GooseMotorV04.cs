using UnityEngine;

namespace DuckGame.V04
{
    public enum MoveState { Grounded, Jump, Hover, Slide }

    [DefaultExecutionOrder(-40)]
    public sealed class GooseMotorV04 : MonoBehaviour
    {
        public CharacterController controller;
        public Animator animator;
        public ShoulderCamera view;
        public float walkSpeed = 2.2f;
        public float runSpeed = 4.4f;
        public float acceleration = 55f;
        public float braking = 75f;
        public float reversalAcceleration = 85f;
        public float airAcceleration = 18f;
        public float airBraking = 10f;
        public float turnDegrees = 600f;
        public float jumpSpeed = 5.5f;
        public float doubleJumpSpeed = 5.5f;
        public float glideDelay = .18f;
        public float hoverDuration = .65f;
        public float slideDuration = .70f;
        public float slideHeight = 1.2f;
        public bool InjectInput;
        public Vector2 TestMove;
        public bool TestRun, TestJump, TestSlide, TestJumpHeld;

        public MoveState State { get; private set; }
        public Vector3 ActualVelocity { get; private set; }
        public Vector3 FrameDisplacement { get; private set; }
        public float FrameTurn { get; private set; }
        public float VerticalSpeed { get; private set; }
        public float StateTime { get; private set; }
        public bool HoverUsed { get; private set; }
        public bool DoubleJumpUsed { get; private set; }
        public float DoubleJumpAge { get; private set; } = -1;
        public bool StandBlocked { get; private set; }
        public bool OnGround { get; private set; }
        public float Landing { get; private set; }
        public bool AimHeld => InjectInput ? view.TestADS : Input.GetMouseButton(1);

        Vector3 commandVelocity, slideDirection;
        float standingHeight, previousYaw;
        bool slideLaunched;

        void Awake()
        {
            view.StableADS = true;
            standingHeight = controller.height;
            previousYaw = transform.eulerAngles.y;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0) return;
            OnGround = controller.isGrounded;
            bool groundBeforeMove = OnGround;
            Landing = Mathf.MoveTowards(Landing, 0, dt);
            StateTime += dt;
            Vector2 input = InjectInput ? TestMove : new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            input = Vector2.ClampMagnitude(input, 1);
            bool jump = InjectInput ? TestJump : Input.GetKeyDown(KeyCode.Space);
            bool jumpHeld = InjectInput ? TestJumpHeld : Input.GetKey(KeyCode.Space);
            if (DoubleJumpAge >= 0) DoubleJumpAge += dt;
            bool slide = InjectInput ? TestSlide : Input.GetKeyDown(KeyCode.LeftControl) || Input.GetKeyDown(KeyCode.RightControl);
            bool run = InjectInput ? TestRun : Input.GetKey(KeyCode.LeftShift);
            TestJump = TestSlide = false;
            Vector3 wish = Quaternion.Euler(0, view.Yaw, 0) * new Vector3(input.x, 0, input.y);

            if (jump && State != MoveState.Slide)
            {
                if (OnGround)
                {
                    State = MoveState.Jump;
                    StateTime = 0;
                    VerticalSpeed = jumpSpeed;
                    OnGround = false;
                }
                else if (!DoubleJumpUsed)
                {
                    // One extra jump per airborne episode, including walking off a ledge.
                    // Assign the impulse rather than accumulating upwards speed.
                    DoubleJumpUsed = true;
                    DoubleJumpAge = 0;
                    State = MoveState.Jump;
                    StateTime = 0;
                    VerticalSpeed = doubleJumpSpeed;
                }
            }
            // Glide cannot swallow the second impulse: wait for its descent and delay.
            // Consumption happens on entry; releasing or exhausting it never re-arms it.
            if (!OnGround && State == MoveState.Jump && DoubleJumpUsed && !HoverUsed &&
                DoubleJumpAge >= glideDelay && VerticalSpeed <= 0 && jumpHeld)
            {
                State = MoveState.Hover;
                StateTime = 0;
                HoverUsed = true;
                VerticalSpeed = 0;
            }
            if (slide && OnGround && State != MoveState.Slide && ActualVelocity.sqrMagnitude > .15f && CanBeginSlide())
            {
                State = MoveState.Slide;
                StateTime = 0;
                slideDirection = ActualVelocity.normalized;
                slideLaunched = false;
                controller.height = slideHeight;
                controller.center = Vector3.up * slideHeight * .5f;
            }
            if (State == MoveState.Hover && (!jumpHeld || StateTime >= hoverDuration))
            {
                State = MoveState.Jump;
                StateTime = 0;
                VerticalSpeed = -.5f;
            }
            if (State == MoveState.Slide && StateTime >= slideDuration)
            {
                StandBlocked = !CanStand();
                if (!StandBlocked)
                {
                    controller.height = standingHeight;
                    controller.center = Vector3.up * standingHeight * .5f;
                    State = OnGround ? MoveState.Grounded : MoveState.Jump;
                    StateTime = 0;
                }
            }
            else if (State != MoveState.Slide) StandBlocked = false;

            Vector3 target = wish * (run ? runSpeed : walkSpeed);
            if (State == MoveState.Slide)
                target = StateTime < slideDuration
                    ? slideDirection * Mathf.Lerp(5.4f, 1.2f, Mathf.Clamp01((StateTime - .12f) / (slideDuration - .12f)))
                    : wish * 1.2f;
            if (State == MoveState.Slide && StateTime < .12f)
            {
                target = Vector3.zero;
                commandVelocity = Vector3.zero;
            }
            if (State == MoveState.Slide && StateTime >= .12f && StateTime < slideDuration && !slideLaunched)
            {
                commandVelocity = slideDirection * 5.4f;
                slideLaunched = true;
            }
            float response;
            if (State == MoveState.Slide) response = input.sqrMagnitude > .01f ? 12f : 22f;
            else if (!OnGround) response = input.sqrMagnitude > .01f ? airAcceleration : airBraking;
            else if (input.sqrMagnitude <= .01f) response = braking;
            else response = Vector3.Dot(commandVelocity, target) < 0 ? reversalAcceleration : acceleration;
            commandVelocity = Vector3.MoveTowards(commandVelocity, target, response * dt);
            if (State == MoveState.Hover) VerticalSpeed = 0;
            else if (OnGround && VerticalSpeed < 0) VerticalSpeed = -2;
            else VerticalSpeed += -16f * dt;

            Vector3 before = transform.position;
            Vector3 horizontal = commandVelocity * dt;
            if (State == MoveState.Slide && horizontal.sqrMagnitude > .000001f)
            {
                {
                    Bounds bounds = SlideSweepBounds();
                    if (Physics.BoxCast(bounds.center, bounds.extents, horizontal.normalized, out var wall,
                        transform.rotation, horizontal.magnitude + .025f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                        horizontal = horizontal.normalized * Mathf.Min(horizontal.magnitude, Mathf.Max(0, wall.distance - .025f));
                }
            }
            CollisionFlags flags = controller.Move(horizontal + Vector3.up * VerticalSpeed * dt);
            if ((flags & CollisionFlags.Above) != 0 && VerticalSpeed > 0) VerticalSpeed = 0;
            FrameDisplacement = transform.position - before;
            ActualVelocity = Vector3.ProjectOnPlane(FrameDisplacement / dt, Vector3.up);
            OnGround = (flags & CollisionFlags.Below) != 0;
            if (OnGround && !groundBeforeMove && VerticalSpeed <= 0)
            {
                Landing = .22f;
                if (State != MoveState.Slide) State = MoveState.Grounded;
                HoverUsed = DoubleJumpUsed = false;
                DoubleJumpAge = -1;
            }
            if (!OnGround && State == MoveState.Grounded) State = MoveState.Jump;
            float targetYaw = transform.eulerAngles.y;
            if (State == MoveState.Slide) targetYaw = transform.eulerAngles.y;
            else if (AimHeld) targetYaw = view.Yaw;
            else if (wish.sqrMagnitude > .01f) targetYaw = Mathf.Atan2(wish.x, wish.z) * Mathf.Rad2Deg;
            else if (ActualVelocity.sqrMagnitude > .05f) targetYaw = Mathf.Atan2(ActualVelocity.x, ActualVelocity.z) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, AimHeld && State != MoveState.Slide ? targetYaw : Mathf.MoveTowardsAngle(transform.eulerAngles.y, targetYaw, turnDegrees * dt), 0);
            FrameTurn = Mathf.DeltaAngle(previousYaw, transform.eulerAngles.y);
            previousYaw = transform.eulerAngles.y;
            // Idle supplies breathing and blinking; v04 owns distance-coupled lower-body locomotion.
            animator.SetFloat("Speed", 0);
            if (!InjectInput && State == MoveState.Grounded)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1)) animator.SetTrigger("Honk");
                if (Input.GetKeyDown(KeyCode.Alpha2)) animator.SetTrigger("Peck");
                if (Input.GetKeyDown(KeyCode.Alpha3)) animator.SetTrigger("WingFlap");
                if (Input.GetKeyDown(KeyCode.Alpha4)) animator.SetTrigger("Startle");
            }
        }

        public void ResetForTeleport()
        {
            commandVelocity = ActualVelocity = FrameDisplacement = Vector3.zero;
            previousYaw = transform.eulerAngles.y;
            FrameTurn = 0;
            VerticalSpeed = -2;
            State = MoveState.Grounded;
            StateTime = Landing = 0;
            HoverUsed = DoubleJumpUsed = StandBlocked = false;
            DoubleJumpAge = -1;
            controller.height = standingHeight;
            controller.center = Vector3.up * standingHeight * .5f;
        }

        // Conservative local envelope includes forward head/hat, rear body and lateral scarf.
        // Verification bakes the actual meshes and checks these bounds every sampled frame.
        public Bounds SlideLocalEnvelope => new Bounds(new Vector3(0, .78f, .85f), new Vector3(1.8f, 1.44f, 3.3f));

        Bounds SlideSweepBounds()
        {
            Bounds local = SlideLocalEnvelope;
            return new Bounds(transform.TransformPoint(local.center), local.size);
        }

        bool CanBeginSlide()
        {
            Bounds local = SlideLocalEnvelope;
            // Folding starts from a tall pose: reserve the whole transition before shrinking the capsule.
            Vector3 max = local.max;
            max.y = standingHeight;
            local.SetMinMax(local.min, max);
            return !Physics.CheckBox(transform.TransformPoint(local.center), local.extents, transform.rotation,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        }

        public bool CanStand()
        {
            {
                Bounds envelope = SlideSweepBounds();
                Vector3 max = envelope.max;
                max.y = transform.position.y + standingHeight;
                envelope.SetMinMax(envelope.min, max);
                if (Physics.CheckBox(envelope.center, envelope.extents, transform.rotation,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return false;
            }
            Vector3 lower = transform.position + Vector3.up * (controller.radius + .04f);
            Vector3 upper = transform.position + Vector3.up * (standingHeight - controller.radius);
            return !Physics.CheckCapsule(lower, upper, controller.radius * .96f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                && !Physics.CheckBox(transform.position + Vector3.up * (standingHeight * .5f + .02f) + transform.forward * .35f,
                    new Vector3(.75f, standingHeight * .5f, 1.1f), transform.rotation, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        }
    }
}
