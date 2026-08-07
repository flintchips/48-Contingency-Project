using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;
using GameNetcodeStuff;
using Object = UnityEngine.Object;

namespace OpaliteMoonMod
{
    public class ApparatusDockHandler : NetworkBehaviour
    {
        public bool isPowered;

        public RoundManager roundManager;

        public Animator thisDockAnimator;

        public AudioSource dockingPointAudio;
        
        private Coroutine connectAnimation;

        public AudioClip[] dockingAudios;

        public Transform apparatusPoint;

        private LungProp dockedApparatus;

        public float timeAtLastUse;

        private InteractTrigger triggerScript;

        private float playbackTime;

        private void Awake()
        {
            isPowered = false;
            triggerScript = base.gameObject.GetComponent<InteractTrigger>();
        }

        private void Start()
        {
            roundManager = FindObjectOfType<RoundManager>();
        }

        public void FinishOpening()
        {
            if (!GetDockAnimators())
                return;
            
            thisDockAnimator.SetBool("Open", false);
            
            NetworkObject apparatus = GetApparatusFromInteractingPlayer();
            if (apparatus == null)
            {
                CancelOpening();
                return;
            }

            PlaceApparatusServerRpc(apparatus);
        }
        
        public void CancelOpening()
        {
            if (!GetDockAnimators())
            {
                return;
            }

            thisDockAnimator.SetBool("Open", value: false);
            PlaySFX(DockingInteractions.Cancel);
            SyncCancelOpeningRpc();
        }
        
        [Rpc(SendTo.NotMe, RequireOwnership = false)]
        public void SyncCancelOpeningRpc()
        {
            if (!GetDockAnimators())
            {
                return;
            }

            thisDockAnimator.SetBool("Open", value: false);
            PlaySFX(DockingInteractions.Cancel);
        }

        private NetworkObject? GetApparatusFromInteractingPlayer()
        {
            PlayerControllerB player = GameNetworkManager.Instance.localPlayerController;
            if (player == null || !player.isHoldingObject || player.currentlyHeldObjectServer == null)
                return null;
            
            GrabbableObject held = player.currentlyHeldObjectServer;
            if (held is not LungProp) return null;
            
            return held.NetworkObject;
        }
        
        private bool LocalPlayerHoldingApparatus()
        {
            PlayerControllerB player = GameNetworkManager.Instance.localPlayerController;
            if (player == null || !player.isHoldingObject)
                return false;
            return player.currentlyHeldObjectServer is LungProp;
        }

        [Rpc(SendTo.Server, RequireOwnership = false)]
        private void PlaceApparatusServerRpc(NetworkObjectReference apparatusRef)
        {
            if (isPowered) return;
            if (!apparatusRef.TryGet(out NetworkObject apparatus)) return;
            LungProp prop = apparatus.GetComponent<LungProp>();
            if (prop == null) return;
            if (prop.playerHeldBy == null || prop.playerHeldBy.currentlyHeldObjectServer != prop)
                return;
            isPowered = true;
            PlaceApparatusClientRpc(apparatusRef);
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void PlaceApparatusClientRpc(NetworkObjectReference apparatusRef)
        {
            if (!apparatusRef.TryGet(out NetworkObject apparatus)) return;
            
            LungProp prop = apparatus.GetComponent<LungProp>();
            if (prop == null) return;

            if (GetDockAnimators())
                thisDockAnimator.SetBool("Open", false);
            
            PlaySFX(DockingInteractions.Late);
            
            isPowered = true;
            
            PlayerControllerB player = prop.playerHeldBy;
            Vector3 placePos = apparatusPoint != null ? apparatusPoint.position : Vector3.zero;
            NetworkObject parentObject = apparatusPoint!.gameObject.GetComponent<NetworkObject>();
            if (parentObject == null)
            {
                isPowered = false;
                return;
            }
            if (player != null)
            {
                Debug.Log("Apparatus dock: discarding held object");
                Debug.Log("Apparatus dock: parenting app to point");
                
                player.DiscardHeldObject(
                    placeObject: true,
                    parentObjectTo: parentObject,
                    placePosition: placePos,
                    matchRotationOfParent: true);
                
                apparatus.TrySetParent(parentObject);
                prop.parentObject = parentObject.transform;
                prop.hasHitGround = true;
                apparatus.transform.localPosition = Vector3.zero;
                apparatus.transform.localEulerAngles = Vector3.zero + new Vector3(0, 180, 0);
            }else
            {
                Debug.Log("Apparatus dock: player null");
                apparatus.TrySetParent(parentObject);
                apparatus.transform.localPosition = Vector3.zero;
                apparatus.transform.localEulerAngles = Vector3.zero;
            }
            
            prop.grabbable = false;
            prop.grabbableToEnemies = false;
            prop.isHeld = false;
            prop.playerHeldBy = null;
            
            Debug.Log("Apparatus spawned at: " + prop.transform.position);
            
            // copying the collider of the apparatus to the trigger so hovering over
            // says [locked] when looking more at the apparatus than the dock
            if (triggerScript != null)
            {
                BoxCollider apparatusCollider = prop.GetComponent<BoxCollider>();
                if (apparatusCollider != null)
                {
                    apparatusCollider.enabled = false;
                    BoxCollider thisColliderAddition = triggerScript.gameObject.AddComponent<BoxCollider>();
                    thisColliderAddition.center = triggerScript.transform.InverseTransformPoint(
                        apparatusCollider.transform.TransformPoint(apparatusCollider.center));
                    thisColliderAddition.size = triggerScript.transform.InverseTransformVector(
                        apparatusCollider.transform.TransformVector(apparatusCollider.size));
                    thisColliderAddition.size = new Vector3(
                        Mathf.Abs(thisColliderAddition.size.x),
                        Mathf.Abs(thisColliderAddition.size.y),
                        Mathf.Abs(thisColliderAddition.size.z));
                    thisColliderAddition.isTrigger = apparatusCollider.isTrigger;
                }
                triggerScript.interactable = false;
                triggerScript.hoverTip = "[Locked]";
            }
            
            
            if (GetDockAnimators())
            {
                thisDockAnimator.SetBool("Powered", true);
                thisDockAnimator.SetBool("Open", false);
            }
            
            if (connectAnimation == null)
                connectAnimation = StartCoroutine(ConnectToMachinery());

        }

        private IEnumerator ConnectToMachinery()
        {
            if (dockingAudios.Length > 2)
            {
                dockingPointAudio.Stop();
                dockingPointAudio.PlayOneShot(dockingAudios[2], 0.7f);
            }

            yield return new WaitForSeconds(1f);

            roundManager.FlickerLights();
            
            yield return new WaitForSeconds(1f);
            connectAnimation = null;
            yield return null;
        }

        public void StartOpening()
        {
            if (isPowered || !LocalPlayerHoldingApparatus())
                return;
            if (!GetDockAnimators() || Time.realtimeSinceStartup - timeAtLastUse < 0.5f)
                return;
            timeAtLastUse = Time.realtimeSinceStartup;
            thisDockAnimator.SetBool("Open", true);
            PlaySFX(DockingInteractions.Early);
            SyncStartOpeningRpc();
        }
        [Rpc(SendTo.NotMe, RequireOwnership = false)]
        public void SyncStartOpeningRpc()
        {
            if (!GetDockAnimators()) return;
            thisDockAnimator.SetBool("Open", true);
            PlaySFX(DockingInteractions.Early);
        }
        
        private void PlaySFX(DockingInteractions interaction)
        {
            if (dockingAudios.Length < 2) return;
            int num = UnityEngine.Random.Range(0, dockingAudios.Length);
            dockingPointAudio.clip = interaction == DockingInteractions.Early ? dockingAudios[0] : dockingAudios[1];
            dockingPointAudio.Play();
        }

        private bool GetDockAnimators()
        {
            return thisDockAnimator != null;
        }

        private void LateUpdate()
        {
            if (triggerScript == null)
                return;
            if (!isPowered) thisDockAnimator.SetBool("Powered", value: false);
            
            bool canDock = LocalPlayerHoldingApparatus() && !isPowered;
            triggerScript.interactable = canDock;
            
            
            triggerScript.hoverTip = canDock ? "[Insert Apparatus]" : "";
            triggerScript.disabledHoverTip = isPowered ? "[Locked]" : "[Requires Apparatus]";
        }
    }
    // end class

    enum DockingInteractions
    {
        Early,
        Late,
        Cancel
    }
}

