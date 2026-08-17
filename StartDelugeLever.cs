using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace OpaliteMoonMod;

public class StartDelugeLever : NetworkBehaviour
{
    public bool leverHasBeenPulled;

    public InteractTrigger triggerScript;

    public StartOfRound playersManager;

    public Animator leverAnimatorObject;
    
    private float updateInterval;

    private bool clientSentRPC;
    
    private Coroutine shakeCoroutine;
    
    public ControlRoomManager controlRoomManager;
    
    public void LeverAnimation()
    {
        if (GameNetworkManager.Instance.localPlayerController.isPlayerDead)
        {
            return;
        }
        if (controlRoomManager.hasBeenPowered)
        {
            PullLeverAnim(leverPulled: true);
            clientSentRPC = true;
            SetStartingDelugeEffects();
            PlayLeverPullEffectsServerRpc(leverPulled: true);
        }
    }
    
    private void PullLeverAnim(bool leverPulled)
    {
        Debug.Log($"Lever animation: setting bool to {leverPulled}");
        leverAnimatorObject.SetBool("pullLever", leverPulled);
        leverHasBeenPulled = leverPulled;
        triggerScript.interactable = false;
    }
    
    [ServerRpc(RequireOwnership = false)]
    public void PlayLeverPullEffectsServerRpc(bool leverPulled)
    {
        PlayLeverPullEffectsClientRpc(leverPulled);
    }

    [ClientRpc]
    private void PlayLeverPullEffectsClientRpc(bool leverPulled)
    {
        PullLeverAnim(leverPulled);
        
        if (leverPulled)
        {
            SetStartingDelugeEffects();
        }
    }
    
    private void SetStartingDelugeEffects()
    {
        /*StartOfRound.Instance.startGameWhir.Play();
        StartOfRound.Instance.shipLandingAudio.pitch = Random.Range(0.92f, 1.08f);
        StartOfRound.Instance.shipDoorsClosingJingle.pitch = 1f;
        switch (Random.Range(1, 7))
        {
            case 1:
            case 2:
                StartOfRound.Instance.shipDoorsClosingJingle.pitch *= Mathf.Pow(1.05946f, 1f);
                break;
            case 3:
            case 4:
                StartOfRound.Instance.shipDoorsClosingJingle.pitch /= Mathf.Pow(1.05946f, 1f);
                break;
        }
        StartOfRound.Instance.securityCameraScreen.overrideCameraForOtherUse = true;
        StartOfRound.Instance.securityCameraScreen.cam.enabled = false;
        StartOfRound.Instance.insideCameraScreen.overrideCameraForOtherUse = true;
        StartOfRound.Instance.insideCameraScreen.cam.enabled = false;
        */
        shakeCoroutine = StartCoroutine(startShakeOnDelay());
    }
    
    private IEnumerator startShakeOnDelay()
    {
        yield return new WaitForSeconds(2f);
        /*StartOfRound.Instance.shipLandingAudio.Play();
        int num = 0;
        GrabbableObject[] array = FindObjectsOfType<GrabbableObject>();
        for (int i = 0; i < array.Length; i++)
        {
            if (array[i].itemProperties.isScrap)
            {
                num++;
                if (num > 6)
                {
                    StartOfRound.Instance.shipLandingScrapAudio.Play();
                    break;
                }
            }
        }*/
        HUDManager.Instance.ShakeCamera(ScreenShakeType.Constant);
        yield return new WaitForSeconds(30f);
        HUDManager.Instance.StopShakingCamera();
        shakeCoroutine = null;
    }
    
    public void CancelShakingEffects()
    {
        if (shakeCoroutine != null)
        {
            StopCoroutine(shakeCoroutine);
            shakeCoroutine = null;
        }
    }
    
    private void CancelStartingDelugeEffects()
    {
        //StartOfRound.Instance.startGameWhir.Stop();
        //StartOfRound.Instance.shipLandingAudio.Stop();
        //StartOfRound.Instance.securityCameraScreen.overrideCameraForOtherUse = false;
        //StartOfRound.Instance.insideCameraScreen.overrideCameraForOtherUse = false;
        if (shakeCoroutine != null)
        {
            StopCoroutine(shakeCoroutine);
            shakeCoroutine = null;
        }
        HUDManager.Instance.StopShakingCamera();
    }
    
    public void PullLever()
    {
        if (leverHasBeenPulled)
        {
            StartDeluge();
        }
    }
    
    public void StartDeluge()
    {
        OpaliteMoonPlugin.Log.LogDebug("[StartDelugeLever] Start Deluge A");
        if (!controlRoomManager.hasBeenPowered)
        {
            return;
        }
        OpaliteMoonPlugin.Log.LogDebug("[StartDelugeLever] Start Deluge successful");
        if (controlRoomManager != null)
        {
            controlRoomManager.StartDelugeServerRpc();
            return;
        }
        
        triggerScript.hoverTip = "[ Failed. ]";

        updateInterval = 4f;
        CancelStartDeluge();
    }
    
    [ClientRpc]
    public void CancelStartDelugeClientRpc()
    {
        CancelStartDeluge();
    }
        
    private void CancelStartDeluge()
    {
        CancelStartingDelugeEffects();
        leverHasBeenPulled = false;
        leverAnimatorObject.SetBool("pullLever", value: false);
    }
    
    public void BeginHoldingInteractOnLever()
    {
        /*if (playersManager.inShipPhase && !hasDisplayedTimeWarning && StartOfRound.Instance.currentLevel.planetHasTime)
        {
            hasDisplayedTimeWarning = true;
            if (TimeOfDay.Instance.daysUntilDeadline <= 0)
            {
                triggerScript.timeToHold = 4f;
                HUDManager.Instance.DisplayTip("HALT!", "You have 0 days left to meet the quota. Use the terminal to route to the company and sell.", isWarning: true);
            }
        }*/
    }
    
    private void Start()
    {
        /*if (!base.IsServer)
        {
            triggerScript.hoverTip = "[ Must be server host. ]";
            triggerScript.interactable = false;
        }*/
    }
    
    private void Update()
    {
        if (updateInterval <= 0f)
        {
            updateInterval = 2f;
            if (!leverHasBeenPulled)
            {
                if (!base.IsServer && !GameNetworkManager.Instance.gameHasStarted)
                {
                    return;
                }
                if (controlRoomManager.hasBeenPowered)
                {
                    triggerScript.hoverTip = "Activate Deluge Pumps : [LMB]";
                }
                else
                {
                    triggerScript.hoverTip = "[ Controls must be powered. ]";
                }
            }
            else
            {
                triggerScript.interactable = false;
                triggerScript.hoverTip = "[Locked]";
            }
        }
        else
        {
            updateInterval -= Time.deltaTime;
        }
    }
}