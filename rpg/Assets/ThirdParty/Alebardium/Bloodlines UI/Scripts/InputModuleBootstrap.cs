using UnityEngine;
using UnityEngine.EventSystems;

namespace BloodlinesUI
{
    /// <summary>
    /// Keeps the EventSystem compatible with whatever "Active Input Handling" the
    /// project uses (Input Manager / Input System / Both).
    ///
    /// The scene ships with the legacy <see cref="StandaloneInputModule"/>, which
    /// works under "Old" and "Both" but throws an InvalidOperationException when the
    /// new Input System is the only active backend. In that single case this
    /// component swaps the legacy module for an InputSystemUIInputModule at runtime,
    /// so the demo works with no manual setup.
    ///
    /// Notes on why this is gated the way it is:
    /// - The swap only runs when ENABLE_INPUT_SYSTEM is defined and
    ///   ENABLE_LEGACY_INPUT_MANAGER is NOT, i.e. "Input System Package (New)" only.
    ///   Under "Old" or "Both" the legacy module is fine and is left untouched.
    /// - ENABLE_INPUT_SYSTEM reflects the Player Settings choice, not whether the
    ///   com.unity.inputsystem package is actually installed. So the Input System
    ///   type is resolved via reflection rather than a direct reference; that keeps
    ///   this file compiling even when the define is set but the package is absent.
    /// </summary>
    [RequireComponent(typeof(EventSystem))]
    [DefaultExecutionOrder(-100)]
    public class InputModuleBootstrap : MonoBehaviour
    {
        private void Awake()
        {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            var inputModuleType = System.Type.GetType(
                "UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (inputModuleType == null)
            {
                // New backend selected in Player Settings but the Input System
                // package is not installed: nothing we can do from here.
                return;
            }

            // Disabling first unregisters the legacy module from the EventSystem
            // immediately, so it never reaches Update and never throws; Destroy then
            // removes the component.
            var legacyModule = GetComponent<StandaloneInputModule>();
            if (legacyModule != null)
            {
                legacyModule.enabled = false;
                Destroy(legacyModule);
            }

            if (GetComponent(inputModuleType) == null)
            {
                var module = gameObject.AddComponent(inputModuleType);

                // A runtime-added module has no actions asset, so the UI would not
                // respond. AssignDefaultActions wires up the built-in UI actions.
                // Called via reflection so this also works on older Input System
                // versions that may not expose the method.
                var assignDefaults = inputModuleType.GetMethod(
                    "AssignDefaultActions",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (assignDefaults != null)
                {
                    assignDefaults.Invoke(module, null);
                }
            }
#endif
            // "Old" or "Both": the existing StandaloneInputModule works as-is.
        }
    }
}
