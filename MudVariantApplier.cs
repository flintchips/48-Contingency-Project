using UnityEngine;

namespace OpaliteMoonMod
{
    /// <summary>
    /// Presentation-only muddy overlay. Does NOT change itemProperties / Dawn identity,
    /// so ship save keeps working for the underlying registered Item.
    /// </summary>
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
        private float savedBrightness = 1.0f;
        private bool hasInitialized;

        private void Awake()
        {
            targetItem = GetComponent<GrabbableObject>();
            if (targetItem == null) return;

            itemRenderers = GetComponentsInChildren<Renderer>(true);

            AudioSource originalSource = targetItem.GetComponent<AudioSource>();
            mudAudioSource = gameObject.AddComponent<AudioSource>();

            if (originalSource != null)
            {
                mudAudioSource.outputAudioMixerGroup = originalSource.outputAudioMixerGroup;
                mudAudioSource.spatialBlend = originalSource.spatialBlend;
                mudAudioSource.minDistance = originalSource.minDistance;
                mudAudioSource.maxDistance = originalSource.maxDistance;
                mudAudioSource.rolloffMode = originalSource.rolloffMode;
                mudAudioSource.volume = originalSource.volume;
                mudAudioSource.pitch = originalSource.pitch;
            }
            else
            {
                mudAudioSource.spatialBlend = 1.0f;
                mudAudioSource.minDistance = 1f;
                mudAudioSource.maxDistance = 30f;
            }
        }

        private void Start()
        {
            if (!hasInitialized)
            {
                savedBrightness = UnityEngine.Random.Range(0.8f, 1.0f);
                if (UnityEngine.Random.Range(0f, 1f) > 0.7f) savedBrightness -= 0.3f;
                hasInitialized = true;

                // Instance weight only — do not mutate the shared Item ScriptableObject.
                if (targetItem != null)
                    targetItem.itemProperties.weight += addedWeight;
            }

            ApplyMudMaterials(savedBrightness);
            UpdateScanNodeText();
            if (targetItem != null)
                wasHeldLastFrame = targetItem.isHeld;
        }

        private void Update()
        {
            if (targetItem == null) return;

            if (targetItem.isHeld && !wasHeldLastFrame)
            {
                if (MuddyPickupSFX != null && mudAudioSource != null)
                    mudAudioSource.PlayOneShot(MuddyPickupSFX);
            }
            else if (!targetItem.isHeld && wasHeldLastFrame)
            {
                if (MuddyDropSFX != null && mudAudioSource != null)
                    mudAudioSource.PlayOneShot(MuddyDropSFX);
            }
            wasHeldLastFrame = targetItem.isHeld;
        }

        public void UpdateScanNodeText()
        {
            ScanNodeProperties scanNode = GetComponentInChildren<ScanNodeProperties>();
            if (scanNode != null && !string.IsNullOrEmpty(scanNode.headerText) &&
                !scanNode.headerText.StartsWith("Muddy "))
            {
                scanNode.headerText = "Muddy " + scanNode.headerText;
            }
        }

        private void ApplyMudMaterials(float brightness)
        {
            if (MudBaseMaterial == null || itemRenderers == null) return;

            Color mudTint = new Color(brightness, brightness, brightness, 1f);
            Material mudInstance = new Material(MudBaseMaterial);
            string colorProp = mudInstance.HasProperty("_BaseColor") ? "_BaseColor" : "_Color";
            if (mudInstance.HasProperty(colorProp))
                mudInstance.SetColor(colorProp, mudInstance.GetColor(colorProp) * mudTint);

            foreach (Renderer renderer in itemRenderers)
            {
                if (renderer == null) continue;
                if (renderer.gameObject.name.Contains("ScanNode") || renderer.name.Contains("ScanNode"))
                    continue;

                int count = renderer.sharedMaterials.Length;
                if (count == 0) continue;

                Material[] mats = new Material[count];
                for (int i = 0; i < count; i++) mats[i] = mudInstance;
                renderer.materials = mats;
            }
        }
    }
}
