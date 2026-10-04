using UnityEngine;
using Random = UnityEngine.Random;

namespace OpaliteMoonMod;

public class MuddyItem : MonoBehaviour
{
    public GrabbableObject baseGrabbableObject;
    public ScanNodeProperties scanNodeProperties;
    
    public AudioSource audioSource;
    public AudioClip crackleClipSFX;
    
    private float crackleTimer = 0f;
    
    private float minCrackleInterval = 3f;
    private float maxCrackleInterval = 7f;
    
    public float muddy = 1;

    public static MuddyItem Create(GrabbableObject grabbableObject)
    {
        MuddyItem muddyComponent = grabbableObject.gameObject.AddComponent<MuddyItem>();
    
        muddyComponent.baseGrabbableObject = grabbableObject;
    
        return muddyComponent;
    }

    public AudioClip LoadCrackleSFX()
    {
        if (crackleClipSFX == null)
        { 
            // load the sfx from the asset bundle of Assets/muddyassets
        }
        return crackleClipSFX;
    }

    public void Start()
    {
        if (baseGrabbableObject != null)
        {
            if (audioSource == null) 
            {
                audioSource = baseGrabbableObject.gameObject.AddComponent<AudioSource>();
                
                AudioSource originalSfx = baseGrabbableObject.gameObject.GetComponent<AudioSource>();
                if (originalSfx != null) audioSource.volume = originalSfx.volume;
            }
            
            crackleClipSFX = LoadCrackleSFX();
            audioSource.clip = crackleClipSFX;
            
            muddy = 1;
            AddMuddyName();
            
            crackleTimer = Random.Range(minCrackleInterval, maxCrackleInterval);
        }
    }

    public void LateUpdate()
    {
        if (baseGrabbableObject != null)
        {
            if (baseGrabbableObject.hasBeenHeld && muddy >= 0)
            {
                float mudLostThisFrame = Time.deltaTime * 0.01f;
                bool isBeingHeld = baseGrabbableObject.isHeld && !baseGrabbableObject.isPocketed;
                
                if (isBeingHeld)
                {
                    mudLostThisFrame += Time.deltaTime * 0.06f;
                    Debug.Log("Muddy value of " + (baseGrabbableObject.name) + " is: " + muddy);
                }
                
                muddy -= mudLostThisFrame;
                
                float timerSpeedMultiplier = isBeingHeld ? 7f : 1f;
                crackleTimer -= Time.deltaTime * timerSpeedMultiplier;
                
                if (crackleTimer <= 0f)
                {
                    TriggerCrackleSFX();
                    
                    // Reset
                    crackleTimer = Random.Range(minCrackleInterval, maxCrackleInterval);
                }
            }
            
            if (muddy < 0)
            {
                RemoveMuddyName();
            }
            
        }
    }

    public void TriggerCrackleSFX()
    {
        Debug.Log("[Muddy Item] Crackled from "+baseGrabbableObject.name);

        if (audioSource != null && crackleClipSFX != null)
        {
            audioSource.PlayOneShot(crackleClipSFX);
        }
        
        if (baseGrabbableObject != null && baseGrabbableObject.isHeld)
        {
            var holdingPlayer = baseGrabbableObject.playerHeldBy;
            
            if (holdingPlayer != null && holdingPlayer.isTestingPlayer)
            {
                HUDManager.Instance.ShakeCamera(ScreenShakeType.Small);
            }
        }
    }

    public void AddMuddyName()
    {
        if (baseGrabbableObject != null)
        {
            bool log = false;
            Debug.Log("[MUDDY ITEM] HI IM MUDDY " + baseGrabbableObject.transform.name);

            if (!baseGrabbableObject.name.StartsWith("Muddy "))
            {
                log = true;
                baseGrabbableObject.name = "Muddy " + baseGrabbableObject.name;
            }

            if (scanNodeProperties == null)
            {
                scanNodeProperties = baseGrabbableObject.GetComponentInChildren<ScanNodeProperties>();
            }
            else if (!scanNodeProperties.headerText.StartsWith("Muddy "))
            {
                log = true;
                scanNodeProperties.headerText = "Muddy " + scanNodeProperties.headerText;
            }
            
            if(log) Debug.Log("[MUDDY ITEM] HI IM MUDDY " + baseGrabbableObject.transform.name);
        }
    }

    public void RemoveMuddyName()
    {
        if (baseGrabbableObject != null)
        {
            bool log = false;
            

            if (baseGrabbableObject.name.StartsWith("Muddy "))
            {
                log = true;
                // always strips the first 6 characters which are rn "Muddy ".
                baseGrabbableObject.name = baseGrabbableObject.name.Substring(6);
            }
                
            if (scanNodeProperties == null)
            {
                scanNodeProperties = baseGrabbableObject.GetComponentInChildren<ScanNodeProperties>();
            }
            else if(scanNodeProperties.headerText.StartsWith("Muddy "))
            {
                log = true;
                scanNodeProperties.headerText = scanNodeProperties.headerText.Substring(6);
            }
            
            if(log) Debug.Log("[MUDDY ITEM] BYE IM (NOT) MUDDY "+ baseGrabbableObject.transform.name);
        }
    }
}