using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Switches for the full-screen PS1 look (Art/Shaders/PS1Screen.shader on Pixel_Renderer).
    /// The look is shared by every level, so a level that wants it different sets it while it's loaded:
    ///
    ///   void OnEnable()  => ScreenLook.CrtOff = true;   // PS1 pixels stay, the old-TV part goes
    ///   void OnDisable() => ScreenLook.CrtOff = false;
    /// </summary>
    public static class ScreenLook
    {
        private static readonly int CrtOffId = Shader.PropertyToID("_PS1CrtOff");
        private static readonly int LiftId = Shader.PropertyToID("_PS1Lift");

        /// <summary>True = no scanlines, phosphor mask, curved glass or dark corners.</summary>
        public static bool CrtOff
        {
            get => Shader.GetGlobalFloat(CrtOffId) > 0.5f;
            set => Shader.SetGlobalFloat(CrtOffId, value ? 1f : 0f);
        }

        /// <summary>Brightens the dark parts of the picture (0 = as is, 0.5 = a lot brighter shadows).</summary>
        public static float Lift
        {
            get => Shader.GetGlobalFloat(LiftId);
            set => Shader.SetGlobalFloat(LiftId, Mathf.Max(0f, value));
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            CrtOff = false;
            Lift = 0f;
        }
    }
}
