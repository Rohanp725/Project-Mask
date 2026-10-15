using System;
using System.Collections.Generic;
using UnityEngine;

namespace Semblance.Residents
{
    [Flags]
    public enum BehaviourFlags
    {
        None = 0,
        NoBlink = 1 << 0,
        NoBreath = 1 << 1,
        Stare = 1 << 2,
        AvoidGaze = 1 << 3,
        HeadSnap = 1 << 4,
        HeadTwitch = 1 << 5,
        Tremor = 1 << 6,
        Sway = 1 << 7,
        RapidBlink = 1 << 8,
        KnuckleCrack = 1 << 9,
        HumAloud = 1 << 10,
    }

    [Flags]
    public enum SpeechFlags
    {
        None = 0,
        Stutter = 1 << 0,
        Whisper = 1 << 1,
        NervousLaugh = 1 << 2,
        TalksToSelf = 1 << 3,
        Hums = 1 << 4,
    }

    /// <summary>Static description of one kind of detail and how it shows up on a resident.</summary>
    public sealed class FeatureDefinition
    {
        public string Id;
        public FeatureKind Kind;
        public FeatureCategory Category;
        public bool Sided;
        /// <summary>A physical, visible detail (can be mirrored or "lost" by the mimic).</summary>
        public bool Physical;
        /// <summary>Shown when the detective looks a resident over. Behaviour-only tells are left for the player to notice.</summary>
        public bool Listed = true;
        public string Observe;
        public BehaviourFlags Behaviour;
        public SpeechFlags Speech;
        public Action<AvatarRig, FeatureInstance> Apply;

        public string Describe(FeatureSide side) =>
            Observe.Replace("{side}", side == FeatureSide.Right ? "right" : "left");
    }

    /// <summary>
    /// Every detail a resident can have. Traits are normal Semblance oddities (noise), red herrings look
    /// suspicious but are innocent, and tells are mistakes only the mimic makes.
    /// Add new details here; profiles reference them by id.
    /// </summary>
    public static class FeatureCatalog
    {
        private static readonly Dictionary<string, FeatureDefinition> Definitions = new Dictionary<string, FeatureDefinition>();

        public static FeatureDefinition Get(string id) => id != null && Definitions.TryGetValue(id, out var d) ? d : null;
        public static IEnumerable<FeatureDefinition> All => Definitions.Values;

        private static readonly Color Scar = new Color(0.86f, 0.72f, 0.66f);
        private static readonly Color Blood = new Color(0.45f, 0.08f, 0.06f);
        private static readonly Color Bandage = new Color(0.86f, 0.84f, 0.78f);

        private static void Add(string id, FeatureKind kind, FeatureCategory category, bool sided, bool physical, string observe,
            Action<AvatarRig, FeatureInstance> apply = null, BehaviourFlags behaviour = BehaviourFlags.None,
            SpeechFlags speech = SpeechFlags.None, bool listed = true)
        {
            Definitions[id] = new FeatureDefinition
            {
                Id = id, Kind = kind, Category = category, Sided = sided, Physical = physical, Observe = observe,
                Apply = apply, Behaviour = behaviour, Speech = speech, Listed = listed,
            };
        }

        private static string S(string baseName, FeatureInstance f) => AvatarRig.Sided(baseName, f.side);
        private static float Sign(FeatureInstance f) => AvatarRig.Sign(f.side);
        private static Color Shade(Color c, float f) => new Color(c.r * f, c.g * f, c.b * f, 1f);

        static FeatureCatalog()
        {
            const FeatureKind T = FeatureKind.Trait, R = FeatureKind.RedHerring, X = FeatureKind.Tell;

            // ---------------------------------------------------------------- Eyes
            Add("eye_high", T, FeatureCategory.Eyes, true, true, "Their {side} eye sits noticeably higher than the other.",
                (r, f) => { r.Move(S("Eye", f), new Vector3(0, 0.022f, 0)); r.Move(S("Brow", f), new Vector3(0, 0.02f, 0)); });
            Add("eye_large", T, FeatureCategory.Eyes, true, true, "Their {side} eye is far larger than the other.",
                (r, f) => r.Scale(S("Eye", f), new Vector3(1.6f, 1.7f, 1f)));
            Add("eye_small", T, FeatureCategory.Eyes, true, true, "Their {side} eye is small and pinched, like a currant.",
                (r, f) => r.Scale(S("Eye", f), new Vector3(0.6f, 0.55f, 1f)));
            Add("eyes_wide_set", T, FeatureCategory.Eyes, false, true, "Their eyes are set unnervingly far apart.",
                (r, f) => { r.Move("EyeL", new Vector3(-0.02f, 0, -0.005f)); r.Move("EyeR", new Vector3(0.02f, 0, -0.005f)); });
            Add("eyes_close_set", T, FeatureCategory.Eyes, false, true, "Their eyes are crowded right up against their nose.",
                (r, f) => { r.Move("EyeL", new Vector3(0.017f, 0, 0)); r.Move("EyeR", new Vector3(-0.017f, 0, 0)); });
            Add("eye_cloudy", T, FeatureCategory.Eyes, true, true, "Their {side} eye is clouded over, milky white.",
                (r, f) => r.Tint(S("Pupil", f), new Color(0.72f, 0.74f, 0.72f)));
            Add("heterochromia", T, FeatureCategory.Eyes, true, true, "Their {side} eye is a pale, watery green. The other is brown.",
                (r, f) => r.Tint(S("Pupil", f), new Color(0.35f, 0.6f, 0.42f)));
            Add("eyepatch", T, FeatureCategory.Eyes, true, true, "They wear a leather patch over their {side} eye.",
                (r, f) =>
                {
                    r.Box("Eyepatch", "Head", new Vector3(Sign(f) * 0.05f, 0.16f, 0.131f), new Vector3(0.06f, 0.05f, 0.008f), new Color(0.08f, 0.06f, 0.05f));
                    r.Box("EyepatchStrap", "Head", new Vector3(0, 0.19f, 0.128f), new Vector3(0.23f, 0.01f, 0.006f), new Color(0.08f, 0.06f, 0.05f), new Vector3(0, 0, -Sign(f) * 12f));
                });
            Add("lazy_eye", T, FeatureCategory.Eyes, true, true, "Their {side} eye drifts off to the side and never quite looks at you.",
                (r, f) => r.Move(S("Pupil", f), new Vector3(Sign(f) * 0.013f, 0.003f, 0)));
            Add("droopy_lid", T, FeatureCategory.Eyes, true, true, "Their {side} eyelid hangs half shut.",
                (r, f) => r.Box(S("Lid", f), S("Eye", f), new Vector3(0, 0.007f, 0.007f), new Vector3(0.05f, 0.016f, 0.006f), r.Look.skin * 0.9f));
            Add("deep_set_eyes", T, FeatureCategory.Eyes, false, true, "Their eyes are sunk deep into their skull.",
                (r, f) =>
                {
                    foreach (var e in new[] { "EyeL", "EyeR" }) { r.Move(e, new Vector3(0, 0, -0.006f)); r.Scale(e, new Vector3(0.85f, 0.85f, 1f)); }
                    r.Box("SocketShadowL", "Head", new Vector3(-0.05f, 0.162f, 0.1255f), new Vector3(0.06f, 0.04f, 0.002f), Shade(r.Look.skin, 0.45f));
                    r.Box("SocketShadowR", "Head", new Vector3(0.05f, 0.162f, 0.1255f), new Vector3(0.06f, 0.04f, 0.002f), Shade(r.Look.skin, 0.45f));
                });

            // ---------------------------------------------------------------- Brows
            Add("unibrow", T, FeatureCategory.Brows, false, true, "A single heavy brow runs straight across their forehead.",
                (r, f) => r.Box("BrowBridge", "Head", new Vector3(0, 0.192f, 0.128f), new Vector3(0.06f, 0.014f, 0.01f), r.Look.hairColor));
            Add("no_brows", T, FeatureCategory.Brows, false, true, "They have no eyebrows at all.",
                (r, f) => { r.Remove("BrowL"); r.Remove("BrowR"); });
            Add("brow_scar", T, FeatureCategory.Brows, true, true, "A pale scar cuts straight through their {side} eyebrow.",
                (r, f) => r.Box(S("BrowScar", f), "Head", new Vector3(Sign(f) * 0.05f, 0.19f, 0.1295f), new Vector3(0.008f, 0.04f, 0.004f), Scar, new Vector3(0, 0, Sign(f) * 20f)));

            // ---------------------------------------------------------------- Nose
            Add("nose_crooked", T, FeatureCategory.Nose, false, true, "Their nose has been broken and set crooked.",
                (r, f) => { r.Rotate("Nose", new Vector3(0, 0, 18f)); r.Move("Nose", new Vector3(0.008f, 0, 0)); });
            Add("nose_long", T, FeatureCategory.Nose, false, true, "Their nose is long and hooked, hanging past their upper lip.",
                (r, f) => { r.Scale("Nose", new Vector3(1f, 1.7f, 1.5f)); r.Move("Nose", new Vector3(0, -0.02f, 0)); });
            Add("nose_bulbous", T, FeatureCategory.Nose, false, true, "Their nose is swollen and bulbous, veined red.",
                (r, f) => { r.Scale("Nose", new Vector3(1.7f, 1.3f, 1.4f)); r.Tint("NoseMesh", new Color(0.72f, 0.42f, 0.4f)); });
            Add("nose_missing", T, FeatureCategory.Nose, false, true, "Where their nose should be there are only two dark holes.",
                (r, f) =>
                {
                    r.Remove("Nose");
                    r.Box("NostrilL", "Head", new Vector3(-0.009f, 0.115f, 0.126f), new Vector3(0.008f, 0.014f, 0.004f), new Color(0.12f, 0.06f, 0.06f));
                    r.Box("NostrilR", "Head", new Vector3(0.009f, 0.115f, 0.126f), new Vector3(0.008f, 0.014f, 0.004f), new Color(0.12f, 0.06f, 0.06f));
                });

            // ---------------------------------------------------------------- Mouth & jaw
            Add("mouth_wide", T, FeatureCategory.Mouth, false, true, "Their mouth stretches almost from ear to ear.",
                (r, f) => r.Scale("Mouth", new Vector3(2f, 1f, 1f)));
            Add("mouth_crooked", T, FeatureCategory.Mouth, true, true, "Their mouth sags down on the {side} side.",
                (r, f) => { r.Rotate("Mouth", new Vector3(0, 0, -Sign(f) * 14f)); r.Move("Mouth", new Vector3(Sign(f) * 0.006f, -0.004f, 0)); });
            Add("teeth_showing", T, FeatureCategory.Mouth, false, true, "Their front teeth jut out over their bottom lip.",
                (r, f) => r.Box("Teeth", "Head", new Vector3(0, 0.062f, 0.129f), new Vector3(0.03f, 0.016f, 0.006f), new Color(0.82f, 0.78f, 0.62f)));
            Add("jaw_large", T, FeatureCategory.Mouth, false, true, "Their jaw is massive, jutting out past the rest of their face.",
                (r, f) => { r.Scale("Jaw", new Vector3(1.3f, 1.8f, 1.15f)); r.Move("Jaw", new Vector3(0, -0.02f, 0.015f)); r.Move("Mouth", new Vector3(0, -0.006f, 0.006f)); });
            Add("chin_long", T, FeatureCategory.Mouth, false, true, "Their chin hangs long and narrow, like a shovel blade.",
                (r, f) => { r.Scale("Jaw", new Vector3(0.75f, 2.2f, 0.9f)); r.Move("Jaw", new Vector3(0, -0.04f, 0.01f)); });

            // ---------------------------------------------------------------- Ears
            Add("ear_missing", T, FeatureCategory.Ears, true, true, "Their {side} ear is gone. There's just a knot of scar tissue.",
                (r, f) =>
                {
                    var ear = r.Part(S("Ear", f));
                    if (ear) { ear.localScale = new Vector3(0.012f, 0.025f, 0.022f); }
                    r.Tint(S("Ear", f), Scar);
                });
            Add("ears_large", T, FeatureCategory.Ears, false, true, "Their ears are enormous and stick straight out.",
                (r, f) =>
                {
                    foreach (var side in new[] { FeatureSide.Left, FeatureSide.Right })
                    {
                        r.Scale(AvatarRig.Sided("Ear", side), new Vector3(1.2f, 1.8f, 2f));
                        r.Rotate(AvatarRig.Sided("Ear", side), new Vector3(0, AvatarRig.Sign(side) * 35f, 0));
                    }
                });
            Add("ear_high", T, FeatureCategory.Ears, true, true, "Their {side} ear sits a good inch higher than the other.",
                (r, f) => r.Move(S("Ear", f), new Vector3(0, 0.035f, 0)));

            // ---------------------------------------------------------------- Head
            Add("head_large", T, FeatureCategory.Head, false, true, "Their head is too big for their body.",
                (r, f) => r.Scale("Head", Vector3.one * 1.25f));
            Add("head_small", T, FeatureCategory.Head, false, true, "Their head is small, almost childlike, on a full-grown body.",
                (r, f) => r.Scale("Head", Vector3.one * 0.78f));
            Add("head_flat", T, FeatureCategory.Head, false, true, "The top of their skull is strangely flat, as if pressed.",
                (r, f) => { r.Scale("HeadShape", new Vector3(1.12f, 0.82f, 1f)); r.Move("HeadShape", new Vector3(0, -0.02f, 0)); r.Move("Hair", new Vector3(0, -0.045f, 0)); });
            Add("head_tilt", T, FeatureCategory.Head, true, true, "Their head lists permanently toward their {side} shoulder.",
                (r, f) => r.Rotate("NeckPivot", new Vector3(0, 0, -Sign(f) * 15f)));
            Add("bald", T, FeatureCategory.Head, false, true, "They're completely bald.",
                (r, f) => r.Remove("Hair"));
            Add("patchy_hair", T, FeatureCategory.Head, false, true, "Their hair grows in thin, separate patches.",
                (r, f) =>
                {
                    r.Remove("Hair");
                    var c = r.Look.hairColor;
                    r.Box("HairPatch1", "Head", new Vector3(-0.05f, 0.278f, 0.03f), new Vector3(0.07f, 0.02f, 0.06f), c);
                    r.Box("HairPatch2", "Head", new Vector3(0.06f, 0.276f, -0.05f), new Vector3(0.06f, 0.02f, 0.08f), c);
                    r.Box("HairPatch3", "Head", new Vector3(-0.02f, 0.22f, -0.127f), new Vector3(0.1f, 0.06f, 0.01f), c);
                });

            // ---------------------------------------------------------------- Skin
            Add("birthmark", T, FeatureCategory.Skin, true, true, "A wine-coloured birthmark covers their {side} cheek.",
                (r, f) => r.Box(S("Birthmark", f), "Head", new Vector3(Sign(f) * 0.07f, 0.12f, 0.1265f), new Vector3(0.06f, 0.05f, 0.002f), new Color(0.48f, 0.16f, 0.2f)));
            Add("cheek_scar", T, FeatureCategory.Skin, true, true, "An old, ridged scar runs down their {side} cheek.",
                (r, f) => r.Box(S("CheekScar", f), "Head", new Vector3(Sign(f) * 0.075f, 0.11f, 0.1268f), new Vector3(0.008f, 0.07f, 0.003f), Scar, new Vector3(0, 0, Sign(f) * 15f)));
            Add("burn_scars", T, FeatureCategory.Skin, true, true, "Shiny burn scars cover the {side} side of their face and neck.",
                (r, f) =>
                {
                    var burn = new Color(0.78f, 0.5f, 0.48f);
                    r.Box(S("BurnFace", f), "Head", new Vector3(Sign(f) * 0.112f, 0.1f, 0.02f), new Vector3(0.003f, 0.12f, 0.16f), burn);
                    r.Box(S("BurnCheek", f), "Head", new Vector3(Sign(f) * 0.085f, 0.09f, 0.1265f), new Vector3(0.05f, 0.08f, 0.002f), burn);
                    r.Box(S("BurnNeck", f), "NeckPivot", new Vector3(Sign(f) * 0.051f, 0.05f, 0), new Vector3(0.003f, 0.1f, 0.09f), burn);
                });
            Add("pale", T, FeatureCategory.Skin, false, true, "Their skin is grey-pale, like something kept out of the sun.",
                (r, f) =>
                {
                    var pale = new Color(0.78f, 0.78f, 0.74f);
                    foreach (var part in new[] { "HeadShape", "Jaw", "Neck", "PalmL", "PalmR", "EarL", "EarR", "NoseMesh" })
                        r.Tint(part, part.StartsWith("Ear") || part == "NoseMesh" ? pale * 0.9f : pale);
                    for (int i = 0; i < 4; i++) { r.Tint("FingerL" + i, pale); r.Tint("FingerR" + i, pale); }
                });
            Add("freckles", T, FeatureCategory.Skin, false, true, "Their face is thick with freckles.",
                (r, f) =>
                {
                    var c = Shade(r.Look.skin, 0.65f);
                    float[] xs = { -0.08f, -0.06f, -0.035f, 0.035f, 0.06f, 0.08f, -0.07f, 0.07f };
                    float[] ys = { 0.12f, 0.105f, 0.125f, 0.12f, 0.105f, 0.125f, 0.135f, 0.137f };
                    for (int i = 0; i < xs.Length; i++)
                        r.Box("Freckle" + i, "Head", new Vector3(xs[i], ys[i], 0.1262f), new Vector3(0.008f, 0.008f, 0.002f), c);
                });

            // ---------------------------------------------------------------- Body
            Add("hunchback", T, FeatureCategory.Body, false, true, "They're badly hunched, a hump rising between their shoulders.",
                (r, f) =>
                {
                    r.Rotate("Spine", new Vector3(22f, 0, 0));
                    r.Rotate("NeckPivot", new Vector3(-30f, 0, 0));
                    r.Box("Hump", "Spine", new Vector3(0, 0.42f, -0.12f), new Vector3(0.3f * r.Look.build, 0.22f, 0.12f), r.Look.shirt);
                });
            Add("neck_long", T, FeatureCategory.Body, false, true, "Their neck is long, too long, like it's been stretched.",
                (r, f) => { r.Scale("Neck", new Vector3(0.85f, 2.4f, 0.85f)); r.Move("Neck", new Vector3(0, 0.07f, 0)); r.Move("Head", new Vector3(0, 0.14f, 0)); });
            Add("neck_short", T, FeatureCategory.Body, false, true, "Their head sits almost directly on their shoulders.",
                (r, f) => { r.Scale("Neck", new Vector3(1.2f, 0.3f, 1.2f)); r.Move("Neck", new Vector3(0, -0.035f, 0)); r.Move("Head", new Vector3(0, -0.07f, 0)); });
            Add("shoulders_uneven", T, FeatureCategory.Body, true, true, "Their {side} shoulder rides much higher than the other.",
                (r, f) => { r.Move(S("Arm", f), new Vector3(0, 0.06f, 0)); r.Rotate("Torso", new Vector3(0, 0, Sign(f) * 6f)); });
            Add("arm_long", T, FeatureCategory.Body, true, true, "Their {side} arm hangs noticeably longer than the other.",
                (r, f) => r.Scale(S("Arm", f), new Vector3(1f, 1.28f, 1f)));
            Add("arm_short", T, FeatureCategory.Body, true, true, "Their {side} arm is withered and short.",
                (r, f) => r.Scale(S("Arm", f), new Vector3(0.85f, 0.72f, 0.85f)));
            Add("gaunt", T, FeatureCategory.Body, false, true, "They're skeletally thin; their clothes hang off them.",
                (r, f) => { r.Scale("Torso", new Vector3(0.78f, 1f, 0.8f)); r.Scale("Neck", new Vector3(0.75f, 1f, 0.75f)); });
            Add("stout", T, FeatureCategory.Body, false, true, "They're barrel-shaped, with a thick neck and belly.",
                (r, f) => { r.Scale("Torso", new Vector3(1.25f, 1f, 1.45f)); r.Scale("Neck", new Vector3(1.4f, 1f, 1.4f)); });
            Add("limp", T, FeatureCategory.Body, true, true, "They stand with all their weight off their {side} leg.",
                (r, f) => { r.Rotate("Hips", new Vector3(0, 0, Sign(f) * 4f)); r.Scale(S("Leg", f), new Vector3(1f, 0.95f, 1f)); });

            // ---------------------------------------------------------------- Hands
            Add("finger_missing", T, FeatureCategory.Hands, true, true, "They're missing the little finger on their {side} hand.",
                (r, f) =>
                {
                    r.Remove(S("Finger", f) + 3);
                    r.Box(S("FingerStump", f), S("Hand", f), new Vector3(0, -0.095f, -0.03f), new Vector3(0.022f, 0.012f, 0.016f), Scar);
                });
            Add("finger_extra", T, FeatureCategory.Hands, true, true, "Their {side} hand has six fingers.",
                (r, f) => r.Box(S("Finger", f) + 4, S("Hand", f), new Vector3(0, -0.115f, -0.05f), new Vector3(0.02f, 0.055f, 0.016f), r.Look.skin));
            Add("hands_large", T, FeatureCategory.Hands, false, true, "Their hands are enormous, like shovels.",
                (r, f) => { r.Scale("HandL", Vector3.one * 1.45f); r.Scale("HandR", Vector3.one * 1.45f); });
            Add("ring", T, FeatureCategory.Hands, true, true, "They wear a wedding ring on their {side} hand.",
                (r, f) => r.Box(S("Ring", f), S("Hand", f), new Vector3(0, -0.1f, -0.01f), new Vector3(0.03f, 0.008f, 0.022f), new Color(0.85f, 0.68f, 0.25f)));
            Add("tremor", T, FeatureCategory.Hands, false, false, "Their hands never stop trembling.", behaviour: BehaviourFlags.Tremor);

            // ---------------------------------------------------------------- Behaviour & speech (normal oddities)
            Add("head_twitch", T, FeatureCategory.Behaviour, false, false, "Every so often their head jerks to one side.", behaviour: BehaviourFlags.HeadTwitch);
            Add("sway", T, FeatureCategory.Behaviour, false, false, "They sway gently on their feet, as if to music only they can hear.", behaviour: BehaviourFlags.Sway);
            Add("rapid_blink", T, FeatureCategory.Behaviour, false, false, "They blink constantly, fluttering.", behaviour: BehaviourFlags.RapidBlink);
            Add("stares", T, FeatureCategory.Behaviour, false, false, "They watch you without ever looking away.", behaviour: BehaviourFlags.Stare);
            Add("stutter", T, FeatureCategory.Speech, false, false, "They stutter.", speech: SpeechFlags.Stutter);
            Add("whisper", T, FeatureCategory.Speech, false, false, "They only ever speak in a whisper.", speech: SpeechFlags.Whisper);

            // ---------------------------------------------------------------- More details (step 17)
            Add("glass_eye", T, FeatureCategory.Eyes, true, true, "Their {side} eye is glass. It doesn't quite move with the other.",
                (r, f) => { r.Tint(S("Pupil", f), new Color(0.42f, 0.5f, 0.58f)); r.Scale(S("Eye", f), new Vector3(1.1f, 1.15f, 1f)); });
            Add("hearing_aid", T, FeatureCategory.Ears, true, true, "A flesh-coloured hearing aid is tucked behind their {side} ear.",
                (r, f) => r.Box(S("HearingAid", f), "Head", new Vector3(Sign(f) * 0.124f, 0.15f, -0.025f), new Vector3(0.018f, 0.045f, 0.022f), new Color(0.85f, 0.7f, 0.6f)));
            Add("wart", T, FeatureCategory.Nose, false, true, "A large wart sits on the end of their nose.",
                (r, f) => r.Box("Wart", "Nose", new Vector3(0.008f, -0.015f, 0.04f), new Vector3(0.016f, 0.016f, 0.012f), Shade(r.Look.skin, 0.75f)));
            Add("mole", T, FeatureCategory.Skin, true, true, "A dark mole sits high on their {side} cheek.",
                (r, f) => r.Box(S("Mole", f), "Head", new Vector3(Sign(f) * 0.065f, 0.135f, 0.1266f), new Vector3(0.011f, 0.011f, 0.003f), new Color(0.18f, 0.1f, 0.08f)));
            Add("gold_tooth", T, FeatureCategory.Mouth, false, true, "A gold tooth glints when they talk.",
                (r, f) => r.Box("GoldTooth", "Head", new Vector3(-0.012f, 0.066f, 0.1285f), new Vector3(0.012f, 0.012f, 0.004f), new Color(0.9f, 0.72f, 0.25f)));
            Add("gap_teeth", T, FeatureCategory.Mouth, false, true, "There's a wide gap between their front teeth.",
                (r, f) =>
                {
                    r.Box("ToothL", "Head", new Vector3(-0.012f, 0.066f, 0.1285f), new Vector3(0.011f, 0.012f, 0.004f), new Color(0.85f, 0.82f, 0.7f));
                    r.Box("ToothR", "Head", new Vector3(0.012f, 0.066f, 0.1285f), new Vector3(0.011f, 0.012f, 0.004f), new Color(0.85f, 0.82f, 0.7f));
                });
            Add("cleft_chin", T, FeatureCategory.Mouth, false, true, "Their chin is deeply cleft.",
                (r, f) => r.Box("Cleft", "Jaw", new Vector3(0, -0.1f, 0.5f), new Vector3(0.04f, 0.6f, 0.04f), Shade(r.Look.skin, 0.6f)));
            Add("neck_scar", T, FeatureCategory.Skin, false, true, "A thin pale scar runs right across their throat.",
                (r, f) => r.Box("NeckScar", "NeckPivot", new Vector3(0, 0.05f, 0.051f), new Vector3(0.08f, 0.007f, 0.003f), Scar));
            Add("tattoo_hand", T, FeatureCategory.Hands, true, true, "A blue anchor is tattooed on the back of their {side} hand.",
                (r, f) => r.Box(S("Tattoo", f), S("Hand", f), new Vector3(Sign(f) * 0.021f, -0.045f, 0f), new Vector3(0.002f, 0.05f, 0.04f), new Color(0.2f, 0.25f, 0.45f)));
            Add("hook_hand", T, FeatureCategory.Hands, true, true, "Their {side} hand is a steel hook.",
                (r, f) =>
                {
                    string hand = S("Hand", f);
                    for (int i = 0; i < 5; i++) r.Remove(S("Finger", f) + i);
                    r.Remove(S("Thumb", f));
                    r.Remove(S("Palm", f));
                    var steel = new Color(0.55f, 0.56f, 0.58f);
                    r.Box(S("HookCuff", f), hand, new Vector3(0, -0.02f, 0), new Vector3(0.07f, 0.06f, 0.07f), new Color(0.3f, 0.22f, 0.16f));
                    r.Box(S("HookShaft", f), hand, new Vector3(0, -0.09f, 0), new Vector3(0.015f, 0.1f, 0.015f), steel);
                    r.Box(S("HookCurve", f), hand, new Vector3(0, -0.145f, 0.02f), new Vector3(0.015f, 0.015f, 0.05f), steel);
                    r.Box(S("HookTip", f), hand, new Vector3(0, -0.125f, 0.04f), new Vector3(0.015f, 0.04f, 0.015f), steel);
                });
            Add("cigarette", T, FeatureCategory.Hands, false, true, "They're never without a cigarette.",
                (r, f) =>
                {
                    r.Box("Cigarette", "HandR", new Vector3(0, -0.13f, 0.035f), new Vector3(0.008f, 0.008f, 0.06f), new Color(0.92f, 0.9f, 0.85f));
                    r.Box("CigaretteTip", "HandR", new Vector3(0, -0.13f, 0.067f), new Vector3(0.009f, 0.009f, 0.006f), new Color(1f, 0.45f, 0.15f));
                });
            Add("knuckle_cracking", T, FeatureCategory.Hands, false, false, "They crack their knuckles constantly.", behaviour: BehaviourFlags.KnuckleCrack);
            Add("hums", T, FeatureCategory.Speech, false, false, "They hum hymns under their breath.", speech: SpeechFlags.Hums, behaviour: BehaviourFlags.HumAloud);
            Add("gloves", R, FeatureCategory.Hands, false, true, "They wear leather gloves, even indoors.",
                (r, f) =>
                {
                    var leather = new Color(0.3f, 0.2f, 0.13f);
                    foreach (var side in new[] { "L", "R" })
                    {
                        r.Tint("Palm" + side, leather);
                        r.Tint("Thumb" + side, leather);
                        for (int i = 0; i < 5; i++) r.Tint("Finger" + side + i, leather);
                    }
                });

            // ---------------------------------------------------------------- Red herrings (suspicious, but innocent)
            Add("bandaged_hand", R, FeatureCategory.Hands, true, true, "Their {side} hand is wrapped in a fresh bandage, spotted with blood.",
                (r, f) =>
                {
                    r.Box(S("Bandage", f), S("Hand", f), new Vector3(0, -0.07f, 0), new Vector3(0.06f, 0.13f, 0.11f), Bandage);
                    r.Box(S("BandageBlood", f), S("Hand", f), new Vector3(AvatarRig.Sign(f.side) * 0.031f, -0.08f, 0.01f), new Vector3(0.002f, 0.03f, 0.03f), Blood);
                });
            Add("fresh_scratches", R, FeatureCategory.Skin, true, true, "There are fresh scratches down their {side} cheek, like from fingernails.",
                (r, f) =>
                {
                    for (int i = 0; i < 3; i++)
                        r.Box(S("Scratch", f) + i, "Head", new Vector3(Sign(f) * (0.055f + i * 0.012f), 0.11f, 0.1266f), new Vector3(0.004f, 0.06f, 0.002f), Blood, new Vector3(0, 0, Sign(f) * 10f));
                });
            Add("dark_stains", R, FeatureCategory.Body, false, true, "There are dark, rust-coloured stains down the front of their clothes.",
                (r, f) =>
                {
                    var stain = new Color(0.25f, 0.08f, 0.06f);
                    float z = 0.123f * r.Look.build + 0.012f;
                    r.Box("Stain1", "Spine", new Vector3(-0.05f, 0.18f, z), new Vector3(0.12f, 0.16f, 0.004f), stain);
                    r.Box("Stain2", "Spine", new Vector3(0.06f, 0.3f, z), new Vector3(0.07f, 0.1f, 0.004f), stain);
                    r.Box("Stain3", "Spine", new Vector3(0.02f, 0.05f, z), new Vector3(0.09f, 0.08f, 0.004f), stain);
                });
            Add("insomnia", R, FeatureCategory.Eyes, false, true, "Dark rings hang under their eyes, like they haven't slept in days.",
                (r, f) =>
                {
                    var c = Shade(r.Look.skin, 0.55f);
                    r.Box("EyeBagL", "Head", new Vector3(-0.05f, 0.142f, 0.1262f), new Vector3(0.05f, 0.014f, 0.002f), c);
                    r.Box("EyeBagR", "Head", new Vector3(0.05f, 0.142f, 0.1262f), new Vector3(0.05f, 0.014f, 0.002f), c);
                });
            Add("dirty_nails", R, FeatureCategory.Hands, false, true, "Their fingernails are packed black with dirt.",
                (r, f) =>
                {
                    var dirt = new Color(0.1f, 0.07f, 0.05f);
                    foreach (var side in new[] { "L", "R" })
                        for (int i = 0; i < 4; i++)
                            r.Box("Nail" + side + i, "Hand" + side, new Vector3(0, -0.148f, 0.03f - i * 0.02f), new Vector3(0.023f, 0.008f, 0.017f), dirt);
                });
            Add("avoids_gaze", R, FeatureCategory.Behaviour, false, false, "They won't meet your eyes.", behaviour: BehaviourFlags.AvoidGaze);
            Add("nervous_laugh", R, FeatureCategory.Speech, false, false, "They laugh at the wrong moments.", speech: SpeechFlags.NervousLaugh);
            Add("talks_to_self", R, FeatureCategory.Speech, false, false, "They mutter to themselves between sentences.", speech: SpeechFlags.TalksToSelf);

            // ---------------------------------------------------------------- Tells (mimic only)
            Add("never_blinks", X, FeatureCategory.Behaviour, false, false, "They never blink.", behaviour: BehaviourFlags.NoBlink, listed: false);
            Add("no_breathing", X, FeatureCategory.Behaviour, false, false, "Their chest never rises or falls.", behaviour: BehaviourFlags.NoBreath, listed: false);
            Add("head_snaps", X, FeatureCategory.Behaviour, false, false, "Their head snaps to follow you, further than a neck should turn.", behaviour: BehaviourFlags.HeadSnap, listed: false);
            // Not listed when you look someone over: you only find out if you shake their hand.
            Add("cold_hands", X, FeatureCategory.Hands, false, false,
                "Their hand was cold and slightly damp, like meat from a cooler.", listed: false);
            // Speech tells are applied as dialogue overrides by the case generator; these entries exist for logging.
            Add("knows_name", X, FeatureCategory.Speech, false, false, "They called you by name before you'd introduced yourself.", listed: false);
            Add("echo_speech", X, FeatureCategory.Speech, false, false, "They repeated another resident's words exactly.", listed: false);
            Add("wrong_memory", X, FeatureCategory.Speech, false, false, "Their memory of Founders' Day doesn't match anyone else's.", listed: false);
        }
    }
}
