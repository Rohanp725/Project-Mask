using UnityEngine;

namespace Semblance.Residents
{
    /// <summary>
    /// Idle life for a resident: breathing, blinking and turning their head toward the player, plus
    /// behaviour quirks from their features (twitches, tremors, staring) and the mimic's tells
    /// (never blinking, not breathing, head snapping round too far).
    /// </summary>
    [RequireComponent(typeof(ResidentAvatar))]
    public class ResidentBehaviour : MonoBehaviour
    {
        [SerializeField] private float noticeDistance = 7f;
        [SerializeField] private float stareDistance = 15f;
        [SerializeField] private float maxHeadYaw = 70f;

        /// <summary>Set by the dialogue window while this resident's line is being typed out.</summary>
        public bool IsTalking { get; set; }

        private float glitchUntil;
        private float nextHabit;
        private float habitUntil;

        /// <summary>For a moment the face doesn't hold together (the mimic, when confronted).</summary>
        public void Glitch(float seconds)
        {
            glitchUntil = Time.time + seconds;
            Audio.GameAudio.PlayAt("sting_soft", transform.position + Vector3.up * 1.6f, 0.35f, 1.3f, 8f);
        }

        private ResidentAvatar avatar;
        private Transform player;
        private Transform mouth;
        private Vector3 mouthRest;
        private BehaviourFlags flags;

        private Transform spine, head, neckPivot, eyeL, eyeR, handL, handR, hips;
        private Transform legL, legR, armL, armR;
        private Quaternion legRestL, legRestR, armRestL, armRestR;
        private ResidentMover mover;
        private float walkPhase;
        private float limpSide;
        private Quaternion neckRest, hipsRest;
        private Vector3 spineScaleRest, eyeScaleL, eyeScaleR, handRestL, handRestR;

        private float seed;
        private float nextBlink, blinkEnd;
        private float nextTwitch, twitchEnd;
        private float twitchAngle;
        private float headYaw, headPitch;
        private float idleYaw, nextIdleLook;

        private void Awake()
        {
            avatar = GetComponent<ResidentAvatar>();
            avatar.Rebuilt += CacheRig;
            seed = Random.value * 100f;
        }

        private void OnDestroy()
        {
            if (avatar != null)
                avatar.Rebuilt -= CacheRig;
        }

        private void Start()
        {
            var playerGO = GameObject.FindGameObjectWithTag("Player");
            if (playerGO != null)
                player = playerGO.transform;
            CacheRig();
        }

        private void CacheRig()
        {
            var rig = avatar.Rig;
            if (rig == null)
                return;
            flags = avatar.GetBehaviourFlags();
            spine = rig.Part("Spine");
            head = rig.Part("Head");
            neckPivot = rig.Part("NeckPivot");
            eyeL = rig.Part("EyeL");
            eyeR = rig.Part("EyeR");
            handL = rig.Part("HandL");
            handR = rig.Part("HandR");
            hips = rig.Part("Hips");
            mouth = rig.Part("Mouth");
            legL = rig.Part("LegL"); legR = rig.Part("LegR");
            armL = rig.Part("ArmL"); armR = rig.Part("ArmR");
            legRestL = legL ? legL.localRotation : Quaternion.identity;
            legRestR = legR ? legR.localRotation : Quaternion.identity;
            armRestL = armL ? armL.localRotation : Quaternion.identity;
            armRestR = armR ? armR.localRotation : Quaternion.identity;
            mover = GetComponent<ResidentMover>();
            resident = GetComponent<Resident>();
            hipsPosStored = false;
            limpSide = 0f;
            foreach (var f in avatar.ActiveFeatures)
                if (f.id == "limp") limpSide = AvatarRig.Sign(f.side);
            mouthRest = mouth ? mouth.localScale : Vector3.one;
            neckRest = neckPivot ? neckPivot.localRotation : Quaternion.identity;
            hipsRest = hips ? hips.localRotation : Quaternion.identity;
            spineScaleRest = spine ? spine.localScale : Vector3.one;
            eyeScaleL = eyeL ? eyeL.localScale : Vector3.one;
            eyeScaleR = eyeR ? eyeR.localScale : Vector3.one;
            handRestL = handL ? handL.localPosition : Vector3.zero;
            handRestR = handR ? handR.localPosition : Vector3.zero;
            ScheduleBlink();
            nextTwitch = Time.time + Random.Range(2f, 6f);
        }

        private bool Has(BehaviourFlags flag)
        {
            if ((flags & flag) != 0) return true;
            // Once it knows you're onto it, the mimic stops pretending not to watch you; so do people who are sick of you.
            return flag == BehaviourFlags.Stare && waryMimic;
        }

        private Resident resident;
        private bool waryMimic;
        private float nextWaryCheck;

        private void Update()
        {
            if (!Application.isPlaying || avatar.Rig == null || spine == null)
                return;
            float t = Time.time;
            if (t > nextWaryCheck)
            {
                nextWaryCheck = t + 2f;
                waryMimic = resident != null && resident.IsWary;
            }

            // Breathing
            if (!Has(BehaviourFlags.NoBreath))
            {
                float breath = Mathf.Sin((t + seed) * Mathf.PI * 2f * 0.24f);
                spine.localScale = Vector3.Scale(spineScaleRest, new Vector3(1f + breath * 0.012f, 1f + breath * 0.008f, 1f + breath * 0.02f));
            }

            UpdateBlink(t);
            UpdateHead(t);

            if (mouth != null)
            {
                float open = IsTalking ? Mathf.Abs(Mathf.Sin(t * 17f + seed)) * Mathf.Abs(Mathf.Sin(t * 5.3f)) : 0f;
                bool glitching = t < glitchUntil;
                mouth.localScale = glitching
                    ? new Vector3(mouthRest.x * 2.6f, mouthRest.y * 4f, mouthRest.z)
                    : new Vector3(mouthRest.x, mouthRest.y * (1f + open * 3.5f), mouthRest.z);
                if (glitching && neckPivot) neckPivot.localRotation *= Quaternion.Euler(0, 0, Random.Range(-12f, 12f));
            }

            if (Has(BehaviourFlags.Tremor) && handL && handR)
            {
                handL.localPosition = handRestL + Random.insideUnitSphere * 0.004f;
                handR.localPosition = handRestR + Random.insideUnitSphere * 0.004f;
            }
            UpdateWalk();
            UpdateSeated();
            UpdateHabits(t);
            if (hips && !Has(BehaviourFlags.Sway) && (mover == null || !mover.IsWalking) && limpSide == 0)
                hips.localRotation = hipsRest * Quaternion.Euler(0, 0, Mathf.Sin((t + seed) * 0.35f) * 1.6f);

            if (Has(BehaviourFlags.Sway) && hips)
                hips.localRotation = hipsRest * Quaternion.Euler(0, 0, Mathf.Sin((t + seed) * 0.9f) * 3f);
        }

        private float seatBlend;
        private Vector3 hipsRestPos;
        private bool hipsPosStored;

        /// <summary>In a church pew the resident sits: hips drop back onto the seat behind them, legs forward.</summary>
        private void UpdateSeated()
        {
            if (hips == null || legL == null || legR == null) return;
            if (!hipsPosStored) { hipsRestPos = hips.localPosition; hipsPosStored = true; }
            var spot = mover != null ? mover.CurrentSpot : null;
            bool seated = spot != null && !mover.IsWalking && spot.Id != null && spot.Id.StartsWith("Church:Seat", System.StringComparison.Ordinal)
                          && Vector3.Distance(transform.position, spot.transform.position) < 0.4f;
            seatBlend = Mathf.MoveTowards(seatBlend, seated ? 1f : 0f, Time.deltaTime * 2f);
            if (seatBlend <= 0f && !seated)
            {
                hips.localPosition = hipsRestPos;
                return;
            }
            float k = Mathf.SmoothStep(0f, 1f, seatBlend);
            hips.localPosition = Vector3.Lerp(hipsRestPos, new Vector3(hipsRestPos.x, 0.52f, hipsRestPos.z - 0.42f), k);
            legL.localRotation = Quaternion.Slerp(legL.localRotation, legRestL * Quaternion.Euler(-80f, 0, 0), k);
            legR.localRotation = Quaternion.Slerp(legR.localRotation, legRestR * Quaternion.Euler(-80f, 0, 0), k);
        }

        /// <summary>Legs and arms swing while walking; a limp shortens one leg's stride and dips the hips.</summary>
        private void UpdateWalk()
        {
            if (legL == null || legR == null) return;
            float speed = mover != null ? new Vector3(mover.Velocity.x, 0, mover.Velocity.z).magnitude : 0f;
            float amount = Mathf.Clamp01(speed / 1.2f);
            walkPhase += speed * Time.deltaTime * 4.2f;
            float swing = Mathf.Sin(walkPhase) * 28f * amount;
            float left = limpSide < 0 ? swing * 0.45f : swing;
            float right = limpSide > 0 ? -swing * 0.45f : -swing;
            legL.localRotation = legRestL * Quaternion.Euler(left, 0, 0);
            legR.localRotation = legRestR * Quaternion.Euler(right, 0, 0);
            if (armL) armL.localRotation = armRestL * Quaternion.Euler(-swing * 0.6f, 0, 0);
            if (armR) armR.localRotation = armRestR * Quaternion.Euler(swing * 0.6f, 0, 0);
            if (hips && limpSide != 0 && amount > 0.1f && !Has(BehaviourFlags.Sway))
                hips.localRotation = hipsRest * Quaternion.Euler(0, 0, Mathf.Max(0, Mathf.Sin(walkPhase) * limpSide) * 6f * amount);
        }

        /// <summary>Audible, visible habits: knuckle-cracking, humming. Only bother when the player is near.</summary>
        private void UpdateHabits(float t)
        {
            bool cracks = Has(BehaviourFlags.KnuckleCrack), hums = Has(BehaviourFlags.HumAloud);
            if (!cracks && !hums || player == null) return;
            if (t < habitUntil && cracks && armL && armR)
            {
                armL.localRotation = armRestL * Quaternion.Euler(-55f, 0, -25f);
                armR.localRotation = armRestR * Quaternion.Euler(-55f, 0, 25f);
            }
            if (t < nextHabit) return;
            nextHabit = t + Random.Range(7f, 14f);
            if (Vector3.Distance(player.position, transform.position) > 7f) return;
            if (cracks)
            {
                habitUntil = t + 0.8f;
                Audio.GameAudio.PlayAt("knuckle_crack", transform.position + Vector3.up * 1.2f, 0.7f, Random.Range(0.9f, 1.1f), 8f);
            }
            if (hums)
                Audio.GameAudio.PlayAt("hum_hymn", transform.position + Vector3.up * 1.6f, 0.35f, Random.Range(0.95f, 1.05f), 7f);
        }

        private void ScheduleBlink()
        {
            float interval = Has(BehaviourFlags.RapidBlink) ? Random.Range(0.3f, 0.9f) : Random.Range(2f, 6f);
            nextBlink = Time.time + interval;
        }

        private void UpdateBlink(float t)
        {
            if (Has(BehaviourFlags.NoBlink) || eyeL == null || eyeR == null)
                return;
            if (t >= nextBlink && blinkEnd < nextBlink)
                blinkEnd = t + 0.12f;
            bool closed = t < blinkEnd;
            eyeL.localScale = closed ? Vector3.Scale(eyeScaleL, new Vector3(1f, 0.1f, 1f)) : eyeScaleL;
            eyeR.localScale = closed ? Vector3.Scale(eyeScaleR, new Vector3(1f, 0.1f, 1f)) : eyeScaleR;
            if (!closed && t >= nextBlink)
                ScheduleBlink();
        }

        private void UpdateHead(float t)
        {
            if (neckPivot == null)
                return;

            float targetYaw = idleYaw, targetPitch = 0f;
            if (t > nextIdleLook)
            {
                nextIdleLook = t + Random.Range(3f, 8f);
                idleYaw = Random.value < 0.4f ? 0f : Random.Range(-40f, 40f);
            }
            if (player != null)
            {
                Vector3 toPlayer = player.position + Vector3.up * 1.6f - neckPivot.position;
                float distance = toPlayer.magnitude;
                float range = Has(BehaviourFlags.Stare) || Has(BehaviourFlags.HeadSnap) ? stareDistance : noticeDistance;
                if (distance < range)
                {
                    Vector3 local = transform.InverseTransformDirection(toPlayer);
                    float yaw = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
                    float pitch = -Mathf.Asin(Mathf.Clamp(local.y / distance, -1f, 1f)) * Mathf.Rad2Deg;
                    float limit = Has(BehaviourFlags.HeadSnap) ? 160f : maxHeadYaw;
                    targetYaw = Mathf.Clamp(yaw, -limit, limit);
                    targetPitch = Mathf.Clamp(pitch, -25f, 25f);

                    if (Has(BehaviourFlags.AvoidGaze))
                    {
                        targetYaw = -Mathf.Sign(yaw) * 50f;
                        targetPitch = 20f;
                    }
                }
            }

            float speed = Has(BehaviourFlags.HeadSnap) ? 40f : Has(BehaviourFlags.Stare) ? 8f : 3f;
            headYaw = Mathf.Lerp(headYaw, targetYaw, 1f - Mathf.Exp(-speed * Time.deltaTime));
            headPitch = Mathf.Lerp(headPitch, targetPitch, 1f - Mathf.Exp(-speed * Time.deltaTime));

            float twitch = 0f;
            if (Has(BehaviourFlags.HeadTwitch))
            {
                if (t >= nextTwitch)
                {
                    twitchEnd = t + 0.09f;
                    twitchAngle = Random.Range(-25f, 25f);
                    nextTwitch = t + Random.Range(1.5f, 5f);
                }
                if (t < twitchEnd)
                    twitch = twitchAngle;
            }

            neckPivot.localRotation = neckRest * Quaternion.Euler(headPitch, headYaw, twitch);
        }
    }
}
