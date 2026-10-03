using UnityEngine;
using Unity.Netcode;
using GameNetcodeStuff;

namespace OpaliteMoonMod
{
    public class ShipPositionTracker : MonoBehaviour
    {
        private StartOfRound roundManager;
        private Transform shipTransform;
        private bool trackedInitialPosition;

        private void Start()
        {
            // Cache the core game reference tracking models
            roundManager = StartOfRound.Instance;
            
            if (roundManager != null && roundManager.shipAnimatorObject != null)
            {
                shipTransform = roundManager.shipAnimatorObject.transform;
            }
            
            Debug.Log("[OpaliteMoon Debug] Ship Position Tracker initialized successfully. Press [H] in-game to log values.");
        }

        private void Update()
        {
            // Verify our active context layers before parsing file logs
            if (roundManager == null) roundManager = StartOfRound.Instance;
            if (shipTransform == null && roundManager != null && roundManager.shipAnimatorObject != null)
            {
                shipTransform = roundManager.shipAnimatorObject.transform;
            }

            // Track map initialization event frames cleanly
            HandleInitialLoadTracking();

            // Handle active troubleshooting hotkey actions
            HandleHotkeyLogging();
        }

        private void HandleInitialLoadTracking()
        {
            if (roundManager == null || shipTransform == null) return;

            // Detect precisely when the terrain generation is complete, but before the landing animation plays
            if (!roundManager.inShipPhase && !trackedInitialPosition && GameNetworkManager.Instance.gameHasStarted)
            {
                trackedInitialPosition = true;
                LogShipCoordinates("MAP GENERATION INITIAL POSITION (Orbit Baseline)");
            }
            
            // Reset the toggle tracking logic once players return to outer orbit phases
            if (roundManager.inShipPhase && trackedInitialPosition)
            {
                trackedInitialPosition = false;
            }
        }

        private void HandleHotkeyLogging()
        {
            if (IngamePlayerSettings.Instance != null && IngamePlayerSettings.Instance.settings.keyBindings != null)
            {
                PlayerControllerB localPlayer = GameNetworkManager.Instance?.localPlayerController;
                if (localPlayer != null && !localPlayer.isTypingChat && !localPlayer.quickMenuManager.isMenuOpen)
                {
                    // REFLECTION FALLBACK: Query the modern InputSystem state directly without needing an assembly import reference
                    try
                    {
                        System.Type keyboardType = System.Type.GetType("UnityEngine.InputSystem.Keyboard, UnityEngine.InputSystem");
                        if (keyboardType != null)
                        {
                            var currentProperty = keyboardType.GetProperty("current", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                            object keyboardInstance = currentProperty?.GetValue(null);

                            if (keyboardInstance != null)
                            {
                                var hKeyProperty = keyboardType.GetProperty("hKey", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                                object hKeyInstance = hKeyProperty?.GetValue(keyboardInstance);

                                if (hKeyInstance != null)
                                {
                                    var wasPressedProperty = hKeyInstance.GetType().GetProperty("wasPressedThisFrame", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                                    bool wasPressed = (bool)(wasPressedProperty?.GetValue(hKeyInstance) ?? false);

                                    if (wasPressed)
                                    {
                                        LogShipCoordinates("HOTKEY PRESSED [H] (Current Location)");
                                    }
                                }
                            }
                        }
                    }
                    catch (System.Exception ex)
                    {
                        OpaliteMoonPlugin.Log.LogError($"[ShipTracker] Failed to reflect new input system hooks: {ex.Message}");
                    }
                }
            }
        }

        private void LogShipCoordinates(string contextLabel)
        {
            if (shipTransform == null)
            {
                Debug.LogWarning($"[OpaliteMoon Debug] Cannot log ship coordinates for '{contextLabel}' because shipTransform reference is missing!");
                return;
            }

            Vector3 worldPos = shipTransform.position;
            Vector3 localPos = shipTransform.localPosition;
            Vector3 worldRot = shipTransform.eulerAngles;
            Vector3 localRot = shipTransform.localEulerAngles;

            // Generate an explicit, highly scannable log print block inside your BepInEx console
            string border = new string('=', 60);
            string logOutput = $"\n{border}\n" +
                               $"[SHIP TRACKER] {contextLabel}\n" +
                               $"{border}\n" +
                               $"-> World Position: Vector3({worldPos.x:F4}f, {worldPos.y:F4}f, {worldPos.z:F4}f)\n" +
                               $"-> Local Position: Vector3({localPos.x:F4}f, {localPos.y:F4}f, {localPos.z:F4}f)\n" +
                               $"-> World Rotation: Vector3({worldRot.x:F4}f, {worldRot.y:F4}f, {worldRot.z:F4}f)\n" +
                               $"-> Local Rotation: Vector3({localRot.x:F4}f, {localRot.y:F4}f, {localRot.z:F4}f)\n" +
                               $"{border}";

            // Send output to both default Unity console channels and BepInEx logger frameworks
            Debug.Log(logOutput);
            OpaliteMoonPlugin.Log.LogInfo(logOutput);
        }
    }
}