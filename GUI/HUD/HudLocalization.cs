using System.Collections.Generic;
using AT_Utils.UI;
using KSP.Localization;
using TCA.UI;
using UnityEngine;
using UnityEngine.UI;

namespace ThrottleControlledAvionics
{
    public static class HudLocalization
    {
        private static readonly Dictionary<string, string> Keys = new Dictionary<string, string>
        {
            { "Click to dismiss", "Hud_DismissTip" },
            { "Toggle alert sounds", "Hud_SoundTip" },
            { "V.Spd", "Hud_VerticalSpeed" },
            { "Altitude", "Hud_Altitude" },
            { "Hover", "Hover" },
            { "Follow Terrain", "FollowTerrain" },
            { "AutoThrottle", "AutoThrottle" },
            { "Change altitude or vertical speed with throttle controls", "AutoThrottleTip" },
            { "Altitude, Vertical speed, Horizontal speed.", "Hud_ReadoutTip" },
            { "Desired vertical speed", "Hud_VerticalSpeedTip" },
            { "Maintain altitude", "HoverTip" },
            { "Keep altitude relative to the ground", "FollowTerrainTip" },
            { "Warp", "Hud_Warp" },
            { "Warp to the burn", "Hud_WarpTip" },
            { "Countdown", "Hud_CountdownTip" },
            { "Burn duration", "Hud_BurnDurationTip" },
            { "Abort Maneuver", "Hud_AbortManeuver" },
            { "Execute Node", "Hud_ExecuteNode" },
            { "OFF", "Hud_Off" },
            { "T-SAS", "Hud_TSAS" },
            { "Push to toggle attitude controls", "Hud_TSASTip" },
            { "Attitude error", "Hud_AttitudeErrorTip" },
            { "Kill", "Hud_KillRotationShort" },
            { "Hold", "AttitudeHoldShort" },
            { "Maneuver", "AttitudeManeuverShort" },
            { "PG", "Hud_ProgradeShort" },
            { "RG", "Hud_RetrogradeShort" },
            { "Err: 0.0°", "Hud_InitialError" },
            { "Kill rotation", "AttitudeKillRotation" },
            { "Hold current attitude", "AttitudeHoldAttitude" },
            { "Maneuver Node", "AttitudeManeuverNode" },
            { "Prograde", "AttitudePrograde" },
            { "Retrograde", "AttitudeRetrograde" },
            { "Radial", "AttitudeRadial" },
            { "Anti Radial", "AttitudeAntiRadial" },
            { "Normal", "AttitudeNormal" },
            { "Anti Normal", "AttitudeAntiNormal" },
            { "To Target", "AttitudeTarget" },
            { "From Target", "AttitudeAntiTarget" },
            { "Relative Velocity", "AttitudeRelVel" },
            { "Against Relative Velocity", "AttitudeAntiRelVel" },
            { "To Target, correcting lateral velocity", "AttitudeTargetCorrected" },
            { "Desired altitude is above the ground", "Hud_AltitudeAboveGroundTip" },
            { "Warning! Desired altitude is below the ground", "Hud_AltitudeBelowGroundTip" },
            { "Vertical speed control", "Hud_StatusVSC" },
            { "No electric charge", "Hud_StatusNoEC" },
            { "Collision with another vessel", "Hud_StatusVesselCollision" },
            { "Collision with terrain", "Hud_StatusTerrainCollision" },
            { "Automatic navigation", "Hud_StatusNavigation" },
            { "Gaining altitude", "Hud_StatusAscending" },
            { "No active engines", "Hud_StatusNoEngines" },
            { "Smart engines mode", "Hud_StatusSmartEngines" },
            { "Low control authority", "Hud_StatusLowControl" },
            { "Altitude control", "Hud_StatusAltitude" },
            { "Unbalanced engines", "Hud_StatusUnbalanced" },
            { "Losing altitude", "Hud_StatusLosingAltitude" },
            { "Copter-style control mode", "Hud_StatusVTOLMode" },
            { "Stop or Anchor", "Hud_StatusStop" },
            { "Stabilizing VTOL filght", "Hud_StatusStabilizing" },
            { "Vertical takeoff and landing assistance", "Hud_StatusVTOLAssist" },
            { "Yes", "Hud_Yes" },
            { "No", "Hud_No" },
            { "Cancel", "Hud_Cancel" },
            { "Close", "Hud_Close" },
            { "Warning", "Hud_Warning" },
            { "Increment", "Hud_IncrementTip" },
            { "Decrement", "Hud_DecrementTip" },
            { "Confirm changes", "Hud_ConfirmChangesTip" },
            { "Done", "Hud_Done" }
        };

        private static readonly HashSet<string> Tooltips = new HashSet<string>();
        private static Font chineseFont;
        private static Font previousTooltipFont;

        public static string Translate(string text)
        {
            return text != null && Keys.TryGetValue(text, out var key) ? Loc.T(key, text) : text;
        }

        public static void Apply(Component controller)
        {
            if(controller != null)
                Apply(controller.gameObject);
        }

        public static void Apply(GameObject root)
        {
            if(root == null)
                return;
            foreach(var text in root.GetComponentsInChildren<Text>(true))
            {
                var input = text.GetComponentInParent<InputField>();
                if(input == null || input.textComponent != text)
                    text.text = Translate(text.text);
                SetFont(text);
                if(Keys.ContainsKey(text.text) || ContainsChinese(text.text))
                {
                    text.resizeTextForBestFit = true;
                    text.resizeTextMinSize = Mathf.Min(10, text.fontSize);
                    text.resizeTextMaxSize = text.fontSize;
                }
            }
            foreach(var tooltip in root.GetComponentsInChildren<TooltipTrigger>(true))
                SetTooltip(tooltip, Translate(tooltip.text));
            var vertical = root.GetComponent<VFlightUI>();
            if(vertical != null)
            {
                vertical.VSC.SetOffLabel(Translate("OFF"));
                var above = Translate("Desired altitude is above the ground");
                var below = Translate("Warning! Desired altitude is below the ground");
                vertical.ALT.SetAltitudeTooltipTexts(above, below);
                Tooltips.Add(above);
                Tooltips.Add(below);
            }
            var maneuver = root.GetComponent<ManeuverUI>();
            if(maneuver != null)
                maneuver.ManeuverSwitch.SetLocalizedLabels(Translate("Abort Maneuver"), Translate("Execute Node"));
            var status = root.GetComponent<StatusUI>();
            if(status != null && Localizer.CurrentLanguage != null && Localizer.CurrentLanguage.StartsWith("zh"))
                LocalizeStopIcon(status.Stop);
        }

        private static void LocalizeStopIcon(Indicator indicator)
        {
            var foreground = indicator.foreground;
            if(foreground.GetComponentInChildren<HudStopIconLabel>(true) != null)
                return;
            var original = foreground.sprite;
            var rect = original.rect;
            var render = RenderTexture.GetTemporary(original.texture.width, original.texture.height, 0,
                RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            Graphics.Blit(original.texture, render);
            RenderTexture.active = render;
            var texture = new Texture2D((int)rect.width, (int)rect.height, TextureFormat.RGBA32, false);
            texture.ReadPixels(rect, 0, 0);
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(render);
            // The official 64px stop sprite paints STOP into its opaque black center.
            for(var y = 20; y < 44; y++)
                for(var x = 8; x < 56; x++)
                    texture.SetPixel(x, y, Color.black);
            texture.Apply();
            var sprite = Sprite.Create(texture, new Rect(0, 0, rect.width, rect.height),
                new Vector2(0.5f, 0.5f), original.pixelsPerUnit);
            foreground.sprite = sprite;
            var label = new GameObject("LocalizedStopLabel", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            label.transform.SetParent(foreground.transform, false);
            label.rectTransform.anchorMin = new Vector2(0.08f, 0.26f);
            label.rectTransform.anchorMax = new Vector2(0.92f, 0.74f);
            label.rectTransform.offsetMin = Vector2.zero;
            label.rectTransform.offsetMax = Vector2.zero;
            label.text = Loc.T("Hud_StopIcon", "STOP");
            label.alignment = TextAnchor.MiddleCenter;
            label.fontSize = 14;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 6;
            label.resizeTextMaxSize = 14;
            label.raycastTarget = false;
            SetFont(label);
            label.gameObject.AddComponent<HudStopIconLabel>().Init(foreground, label, sprite, texture);
        }

        public static void SetTooltip(TooltipTrigger tooltip, string text)
        {
            if(tooltip == null)
                return;
            tooltip.SetText(text);
            if(!string.IsNullOrEmpty(text))
                Tooltips.Add(text);
        }

        public static void UpdateTooltipFont()
        {
            var view = TooltipView.Instance;
            if(view == null)
                return;
            if(TooltipView.IsShown && Tooltips.Contains(TooltipView.CurrentTooltip))
            {
                if(previousTooltipFont == null)
                    previousTooltipFont = view.tooltipText.font;
                SetFont(view.tooltipText);
            }
            else if(previousTooltipFont != null)
            {
                view.tooltipText.font = previousTooltipFont;
                previousTooltipFont = null;
            }
        }

        private static void SetFont(Text text)
        {
            if(Localizer.CurrentLanguage == null || !Localizer.CurrentLanguage.StartsWith("zh"))
                return;
            if(chineseFont == null)
            {
                var skinFont = HighLogic.Skin != null ? HighLogic.Skin.font : null;
                if(skinFont != null && skinFont.HasCharacter('\u63a8'))
                    chineseFont = skinFont;
                else
                {
                    foreach(var font in Resources.FindObjectsOfTypeAll<Font>())
                    {
                        if(font.name.Contains("CJK") && font.HasCharacter('\u63a8'))
                        {
                            chineseFont = font;
                            break;
                        }
                    }
                    if(chineseFont == null)
                        chineseFont = Font.CreateDynamicFontFromOSFont(
                            new[] { "Microsoft YaHei", "Noto Sans CJK SC", "SimHei", "Arial Unicode MS" }, 14);
                }
            }
            if(chineseFont != null)
                text.font = chineseFont;
        }

        private static bool ContainsChinese(string text)
        {
            if(text == null)
                return false;
            foreach(var c in text)
                if(c >= '\u3400' && c <= '\u9fff')
                    return true;
            return false;
        }
    }

    public sealed class HudStopIconLabel : MonoBehaviour
    {
        private Image foreground;
        private Text label;
        private Sprite sprite;
        private Texture2D texture;

        internal void Init(Image source, Text text, Sprite icon, Texture2D pixels)
        {
            foreground = source;
            label = text;
            sprite = icon;
            texture = pixels;
            label.color = foreground.color;
        }

        private void LateUpdate() => label.color = foreground.color;

        private void OnDestroy()
        {
            Destroy(sprite);
            Destroy(texture);
        }
    }
}
