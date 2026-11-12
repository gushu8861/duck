using System;
using System.Collections.Generic;
using UnityEngine;

namespace DuckGame.V04
{
    [DefaultExecutionOrder(-20)]
    public sealed class GoosePoseV04 : MonoBehaviour
    {
        public GooseMotorV04 motor;
        public Animator animator;
        public Transform cameraProxy, weaponProxy;
        public float cycleDistance = 1.0f;
        public float stepLift = .13f;
        public Transform LeftFoot => bones["Foot.L"];
        public Transform RightFoot => bones["Foot.R"];
        public float Phase { get; private set; }
        public float Compression { get; private set; }
        public bool LeftSupport { get; private set; }
        public bool RightSupport { get; private set; }
        public Vector3 BonePosition(string name) => bones[name].position;

        readonly Dictionary<string, Transform> bones = new Dictionary<string, Transform>();
        readonly List<Snapshot> beforePose = new List<Snapshot>();
        readonly Vector3[] planted = new Vector3[2];
        readonly Vector3[] swingStart = new Vector3[2];
        readonly Vector3[] previousFoot = new Vector3[2];
        readonly bool[] wasSupport = { true, false };
        readonly Quaternion[] footRotation = new Quaternion[2];
        readonly Vector3[] neutralOffset = new Vector3[2];
        bool initialized, wasAirborne;
        float air, slide, idleSettle;
        Vector3 travelDirection;

        struct Snapshot
        {
            public Transform bone;
            public Vector3 position, scale;
            public Quaternion rotation;
        }

        void Awake()
        {
            foreach (SkinnedMeshRenderer skin in animator.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            foreach (Transform t in skin.bones)
            {
                if (t != null && Array.IndexOf(new[] { "Body", "Neck1", "Neck2", "Neck3", "Head", "HatTip", "Scarf.L", "Scarf.R", "Tail", "Wing.L", "Wing.R", "Leg.L", "Leg.R", "Foot.L", "Foot.R" }, t.name) >= 0)
                    bones[t.name] = t;
            }
            for (int i = 0; i < 2; i++)
            {
                Transform foot = bones[i == 0 ? "Foot.L" : "Foot.R"];
                footRotation[i] = Quaternion.Inverse(transform.rotation) * foot.rotation;
                neutralOffset[i] = transform.InverseTransformPoint(foot.position);
                planted[i] = swingStart[i] = previousFoot[i] = foot.position;
            }
            travelDirection = transform.forward;

        }

        void Update()
        {
            // Undo ONLY the previous procedural layer before Animator evaluates this frame.
            // Repeated pose application cannot accumulate translations or rotations.
            foreach (Snapshot s in beforePose)
            {
                s.bone.localPosition = s.position;
                s.bone.localRotation = s.rotation;
                s.bone.localScale = s.scale;
            }
            beforePose.Clear();
        }

        void LateUpdate()
        {
            foreach (Transform bone in bones.Values)
                beforePose.Add(new Snapshot { bone = bone, position = bone.localPosition, rotation = bone.localRotation, scale = bone.localScale });

            float dt = Time.deltaTime;
            slide = Mathf.MoveTowards(slide, motor.State == MoveState.Slide ? 1 : 0, dt * 9);
            air = Mathf.MoveTowards(air, motor.State == MoveState.Jump || motor.State == MoveState.Hover ? 1 : 0, dt * 10);
            Compression = slide;
            float distance = Vector3.ProjectOnPlane(motor.FrameDisplacement, Vector3.up).magnitude;
            float turningDistance = Mathf.Abs(motor.FrameTurn) * Mathf.Deg2Rad * .28f;
            bool stepping = distance + turningDistance > .00015f && motor.OnGround && slide < .1f;
            if (motor.ActualVelocity.sqrMagnitude > .02f) travelDirection = motor.ActualVelocity.normalized;
            if (stepping) Phase += (distance + turningDistance) / Mathf.Max(.6f, cycleDistance);
            idleSettle = Mathf.MoveTowards(idleSettle, stepping ? 1 : 0, dt * 10);

            float t = Phase * Mathf.PI * 2;
            if (slide > .001f)
            {
                // Fold the continuous neck as an S; counter-rotate head and fold hat tip.
                // This reduces the actual silhouette rather than only reducing the capsule.
                Pitch("Body", .10f * slide);
                bones["Body"].position += Vector3.up * .045f * slide;
                Pitch("Neck1", 2.45f * slide);
                Pitch("Neck2", -1.35f * slide);
                Pitch("Neck3", .40f * slide);
                Pitch("Head", -.05f * slide);
                Pitch("HatTip", -.15f * slide);
                Pitch("Wing.L", -.22f * slide);
                Pitch("Wing.R", -.22f * slide);
            }
            else if (air > .001f)
            {
                float flap = motor.State == MoveState.Hover ? Mathf.Sin(motor.StateTime * 19) : 0;
                float secondJump = motor.DoubleJumpAge >= 0 && motor.DoubleJumpAge < .28f
                    ? Mathf.Sin(Mathf.PI * motor.DoubleJumpAge / .28f) : 0;
                Pitch("Body", (-.10f - .08f * secondJump) * air);
                Pitch("Neck1", -.10f * air);
                Pitch("Neck2", .08f * air);
                Pitch("Head", .06f * air);
                Rotate("Wing.L", transform.forward, (35 + 22 * flap + 28 * secondJump) * air);
                Rotate("Wing.R", transform.forward, -(35 + 22 * flap + 28 * secondJump) * air);
                Pitch("HatTip", .12f * Mathf.Sin(Time.time * 11) * air);
            }
            else
            {
                float aimMotion = 1 - motor.view.ADS;
                float lean = Mathf.Clamp(motor.FrameTurn / Mathf.Max(dt, .001f) / 600, -1, 1) * (1 - motor.view.ADS);
                Rotate("Body", transform.forward, (-4 * Mathf.Sin(t) * idleSettle * aimMotion - 9 * lean));
                Pitch("Neck1", .045f * Mathf.Sin(t - .5f) * idleSettle * aimMotion);
                Rotate("Head", transform.forward, 3 * Mathf.Sin(t) * idleSettle * aimMotion + 5 * lean);
                Rotate("Tail", Vector3.up, 12 * Mathf.Sin(t - .9f) * idleSettle);
                Pitch("HatTip", .07f * Mathf.Sin(t - .8f) * idleSettle * aimMotion);
                Pitch("Scarf.L", .10f * Mathf.Sin(t - .7f) * idleSettle);
                Pitch("Scarf.R", .08f * Mathf.Sin(t - 1.1f) * idleSettle);
                bones["Body"].position -= Vector3.up * motor.Landing * .22f;
            }

            bool airborne = !motor.OnGround;
            if (wasAirborne && !airborne)
            {
                for (int i = 0; i < 2; i++)
                {
                    planted[i] = swingStart[i] = previousFoot[i];
                    wasSupport[i] = false;
                }
            }
            for (int i = 0; i < 2; i++) PoseFoot(i, stepping, dt);
            wasAirborne = airborne;

            motor.view.StableADSAnchor = transform.position + Vector3.up * Mathf.Lerp(3.43f, .95f, slide)
                + transform.forward * (2.2f * slide);
            // Aim with the real hat tip. Counter the animated upper-body offset instead of
            // feeding that offset back into the camera position each frame.
            Vector3 aimCorrection = Vector3.ClampMagnitude(motor.view.StableADSAnchor - motor.view.hat.TipWorld, .30f);
            bones["Head"].position += aimCorrection * motor.view.ADS * (1 - slide) * (1 - air);
            float bodyAnchor = slide > .05f ? Mathf.Lerp(2.35f, .68f, slide) : 2.35f;
            if (cameraProxy != null) cameraProxy.position = transform.position + Vector3.up * (bodyAnchor - 2.35f);
            if (weaponProxy != null)
            {
                float height = slide > .05f ? Mathf.Max(.40f, bones["Head"].position.y - transform.position.y + .12f) : 1.8f;
                weaponProxy.position = transform.position + Vector3.up * (height - 1.8f);
            }
        }

        void PoseFoot(int index, bool stepping, float dt)
        {
            string side = index == 0 ? "L" : "R";
            Transform foot = bones["Foot." + side];
            Transform leg = bones["Leg." + side];
            Vector3 neutral = transform.TransformPoint(neutralOffset[index]);
            Vector3 target = neutral;
            float phase = Mathf.Repeat(Phase + index * .5f, 1);
            bool support = phase < .5f;
            bool actualSupport = support && stepping && air <= .01f && slide <= .01f && motor.OnGround;
            if (index == 0) LeftSupport = actualSupport;
            else RightSupport = actualSupport;
            if (!initialized)
            {
                planted[index] = swingStart[index] = previousFoot[index] = neutral;
            }
            if (slide > .01f)
                target = neutral + transform.forward * (.18f * slide) + Vector3.up * (.01f * slide);
            else if (air > .01f)
                target = neutral + Vector3.up * (.20f * air) - transform.forward * (.10f * air);
            else if (stepping)
            {
                if (support)
                {
                    if (!wasSupport[index]) planted[index] = previousFoot[index];
                    target = planted[index];
                }
                else
                {
                    if (wasSupport[index]) swingStart[index] = previousFoot[index];
                    float u = (phase - .5f) * 2;
                    Vector3 ahead = neutral + travelDirection * cycleDistance * .25f;
                    // The turn component also advances phase, so pivots visibly lift/replant.
                    target = Vector3.Lerp(swingStart[index], ahead, Mathf.SmoothStep(0, 1, u));
                    target.y += Mathf.Sin(Mathf.PI * u) * stepLift;
                }
                wasSupport[index] = support;
            }
            else
            {
                target = Vector3.MoveTowards(previousFoot[index], neutral, dt * 2.8f);
                planted[index] = target;
                wasSupport[index] = phase < .5f;
            }
            float groundFloor = float.NegativeInfinity;
            if (motor.OnGround && air < .05f)
            {
                if (Physics.Raycast(target + Vector3.up * .4f, Vector3.down, out var hit, .9f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    groundFloor = hit.point.y + .065f;
                    float lift = stepping && !support ? Mathf.Sin(Mathf.PI * (phase - .5f) * 2) * stepLift : 0;
                    target.y = hit.point.y + .065f + lift;
                    if (support || !stepping) target.y = hit.point.y + .065f;
                }
            }
            // Keep the solved world-space foot continuous when changing gait/air/slide targets.
            // Ground projection supplies a destination; it must not snap a lifted foot down.
            float solveSpeed = Mathf.Max(6f, motor.ActualVelocity.magnitude * 3f + 3f);
            target = Vector3.MoveTowards(previousFoot[index], target, solveSpeed * dt);
            target.y = Mathf.Max(target.y, groundFloor);
            if (actualSupport && !float.IsNegativeInfinity(groundFloor) && target.y > groundFloor + .015f)
            {
                if (index == 0) LeftSupport = false;
                else RightSupport = false;
            }
            leg.position += target - foot.position;
            foot.rotation = transform.rotation * footRotation[index];
            if (air > .01f) foot.rotation = Quaternion.AngleAxis(-18 * air, transform.right) * foot.rotation;
            previousFoot[index] = foot.position;
            if (air > .01f || slide > .01f || !motor.OnGround)
            {
                planted[index] = swingStart[index] = previousFoot[index];
                wasSupport[index] = false;
            }
            if (index == 1) initialized = true;
        }

        public void ResetForTeleport()
        {
            initialized = false;
            wasAirborne = false;
            Phase = 0;
            slide = air = idleSettle = 0;
            for (int i = 0; i < 2; i++)
            {
                planted[i] = swingStart[i] = previousFoot[i] = transform.TransformPoint(neutralOffset[i]);
                wasSupport[i] = i == 0;
            }
        }

        void Pitch(string name, float radians) => Rotate(name, transform.right, radians * Mathf.Rad2Deg);
        void Rotate(string name, Vector3 axis, float degrees) => bones[name].rotation = Quaternion.AngleAxis(degrees, axis) * bones[name].rotation;
    }
}

