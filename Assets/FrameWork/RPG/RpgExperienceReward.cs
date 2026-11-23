using UnityEngine;

namespace FrameWork.FPS
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(FpsHealth))]
    public sealed class RpgExperienceReward : MonoBehaviour
    {
        [SerializeField, Min(0)] private int experience = 50;
        private FpsHealth health;

        private void Awake()
        {
            health = GetComponent<FpsHealth>();
        }

        private void OnEnable()
        {
            health.Defeated += AwardExperience;
        }

        private void OnDisable()
        {
            health.Defeated -= AwardExperience;
        }

        private void AwardExperience(GameObject instigator)
        {
            if (instigator == null) return;

            var recipient = instigator.GetComponentInParent<RpgProgression>();
            if (recipient != null && recipient != GetComponentInParent<RpgProgression>())
                recipient.GainExperience(experience);
        }
    }
}
