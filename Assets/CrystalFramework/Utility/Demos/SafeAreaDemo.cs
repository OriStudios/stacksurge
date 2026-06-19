using UnityEngine;
using UnityEngine.InputSystem; // Added namespace
using System;

namespace Crystal
{
    public class SafeAreaDemo : MonoBehaviour
    {
        [SerializeField] InputActionReference toggleAction; // Assign in Inspector
        [SerializeField] SafeArea.SimDevice StartupSim = SafeArea.SimDevice.None;
        
        SafeArea.SimDevice[] Sims;
        int SimIdx;

        void Awake ()
        {
            if (!Application.isEditor)
                Destroy (this);

            Sims = (SafeArea.SimDevice[])Enum.GetValues (typeof (SafeArea.SimDevice));

            if (StartupSim != SafeArea.SimDevice.None)
            {
                SetSafeArea (StartupSim);
            }
        }

        private void OnEnable()
        {
            // Subscribe to the performed event
            if (toggleAction != null)
            {
                toggleAction.action.performed += OnTogglePerformed;
                toggleAction.action.Enable();
            }
        }

        private void OnDisable()
        {
            // Unsubscribe to prevent memory leaks
            if (toggleAction != null)
            {
                toggleAction.action.performed -= OnTogglePerformed;
                toggleAction.action.Disable();
            }
        }

        private void OnTogglePerformed(InputAction.CallbackContext context)
        {
            ToggleSafeArea();
        }

        public void ToggleSafeArea ()
        {
            SimIdx++;
            if (SimIdx >= Sims.Length)
                SimIdx = 0;

            SafeArea.Sim = Sims[SimIdx];
            Debug.LogFormat ("Switched to sim device {0}", Sims[SimIdx]);
        }

        public void SetSafeArea (SafeArea.SimDevice sim)
        {
            SafeArea.Sim = sim;
            Debug.LogFormat ("Switched to sim device {0}", sim);
        }
    }
}