using DuckGame.Maps.V10;
using UnityEngine;

namespace DuckGame.Maps.V11
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class CameraStyle : MonoBehaviour
    {
        public enum Look { Cartoon, FinePixel, PrintGrain }

        [SerializeField] private Look look = Look.FinePixel;
        [SerializeField] private UpwardPlayerVisibility upwardFade;

        public float Strength
        {
            get
            {
                if (upwardFade == null || !upwardFade.isActiveAndEnabled)
                    return 1f;

                // Keep the camera filter from resampling partially dithered player geometry.
                return Mathf.SmoothStep(0f, 1f, Mathf.Abs(2f * upwardFade.Visibility - 1f));
            }
        }

        public Look Current
        {
            get => look;
            set => look = value;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F6)) Current = Look.Cartoon;
            if (Input.GetKeyDown(KeyCode.F7)) Current = Look.FinePixel;
            if (Input.GetKeyDown(KeyCode.F8)) Current = Look.PrintGrain;
        }
    }
}
