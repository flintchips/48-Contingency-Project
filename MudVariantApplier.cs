using UnityEngine;

namespace OpaliteMoonMod
{
    public class MudVariantApplier : MonoBehaviour
    {
        private GrabbableObject targetItem;
        private Renderer[] itemRenderers;

        public float addedWeight = 0.05f; 

        public static Material MudBaseMaterial; 
        public static AudioClip MuddyDropSFX;
        public static AudioClip MuddyPickupSFX;

        private AudioSource mudAudioSource;
        private bool wasHeldLastFrame;

        private void Awake()
        {
            targetItem = GetComponent<GrabbableObject>();
            if (targetItem == null) return;

            itemRenderers = GetComponentsInChildren<Renderer>();

            AudioSource originalSource = targetItem.GetComponent<AudioSource>();
            mudAudioSource = gameObject.AddComponent<AudioSource>();
            
            if (originalSource != null)
            {
                mudAudioSource.clip = null;
                mudAudioSource.outputAudioMixerGroup = originalSource.outputAudioMixerGroup;
                mudAudioSource.spatialBlend = originalSource.spatialBlend;
                mudAudioSource.minDistance = originalSource.minDistance;
                mudAudioSource.maxDistance = originalSource.maxDistance;
                mudAudioSource.rolloffMode = originalSource.rolloffMode;
                
                mudAudioSource.volume = originalSource.volume;
                mudAudioSource.pitch = originalSource.pitch;
                mudAudioSource.dopplerLevel = originalSource.dopplerLevel;
                mudAudioSource.spread = originalSource.spread;
                mudAudioSource.bypassEffects = originalSource.bypassEffects;
                mudAudioSource.bypassListenerEffects = originalSource.bypassListenerEffects;
                mudAudioSource.bypassReverbZones = originalSource.bypassReverbZones;
            }
            else
            {
                mudAudioSource.spatialBlend = 1.0f;
                mudAudioSource.minDistance = 1f;
                mudAudioSource.maxDistance = 30f;
            }
            
            UpdateScanNodeText();
        }

        private void Start()
        {
            ApplyMuddyEffects();
            ReplaceMaterialsWithMud();
            wasHeldLastFrame = targetItem.isHeld;
        }

        private void Update()
        {
            HandleAudioOverlayDetection();
        }

        private void ApplyMuddyEffects()
        {
            targetItem.itemProperties.weight += addedWeight;
        }

        private void HandleAudioOverlayDetection()
        {
            if (targetItem.isHeld && !wasHeldLastFrame)
            {
                if (MuddyPickupSFX != null && mudAudioSource != null)
                {
                    mudAudioSource.PlayOneShot(MuddyPickupSFX);
                }
            }
            else if (!targetItem.isHeld && wasHeldLastFrame)
            {
                if (MuddyDropSFX != null && mudAudioSource != null)
                {
                    mudAudioSource.PlayOneShot(MuddyDropSFX);
                }
            }
            wasHeldLastFrame = targetItem.isHeld;
        }
        
        private void UpdateScanNodeText()
        {
            ScanNodeProperties scanNode = GetComponentInChildren<ScanNodeProperties>();
            if (scanNode != null)
            {
                if (!scanNode.headerText.StartsWith("Muddy "))
                {
                    scanNode.headerText = "Muddy " + scanNode.headerText;
                    Debug.Log($"[MudVariantApplier] ScanNode updated to: {scanNode.headerText}");
                }
            }
        }

        private void ReplaceMaterialsWithMud()
        {
            if (MudBaseMaterial == null)
            {
                Debug.LogWarning("[MudVariantApplier] MudBaseMaterial is null! Skipping material replacement.");
                return;
            }


            float randomBrightness = UnityEngine.Random.Range(0.5f, 1.0f);
            Color mudTintMultiplier = new Color(randomBrightness, randomBrightness, randomBrightness, 1.0f);

            Material uniqueMudMaterialInstance = new Material(MudBaseMaterial);

            string colorProp = uniqueMudMaterialInstance.HasProperty("_BaseColor") ? "_BaseColor" : "_Color";
            if (uniqueMudMaterialInstance.HasProperty(colorProp))
            {
                Color baselineColor = uniqueMudMaterialInstance.GetColor(colorProp);
                uniqueMudMaterialInstance.SetColor(colorProp, baselineColor * mudTintMultiplier);
            }
            else
            {
                uniqueMudMaterialInstance.SetColor(colorProp, mudTintMultiplier);
            }

            foreach (Renderer renderer in itemRenderers)
            {
                if (renderer.gameObject.name.Contains("ScanNode") || renderer.name.Contains("ScanNode"))
                    continue;

                int materialCount = renderer.sharedMaterials.Length;
                if (materialCount == 0) continue;

                Material[] mudMats = new Material[materialCount];
                for (int i = 0; i < materialCount; i++)
                {
                    mudMats[i] = uniqueMudMaterialInstance;
                }

                renderer.materials = mudMats;
                Debug.Log($"[MudVariantApplier] Replaced all {materialCount} materials on {renderer.name} with an instanced MudMaterial tinted at brightness: {randomBrightness:F2}");
            }
        }
    }
}