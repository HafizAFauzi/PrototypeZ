using System;

namespace LuckyDuelist.Experiment
{
    /// <summary>
    /// Configuration data container encapsulating feature gates and parameters for each experiment group.
    /// Derived from GDD Section 4.3 & TSD Section 2.5.
    /// </summary>
    [Serializable]
    public class ExperimentConfig
    {
        public ExperimentGroup Group { get; private set; }
        public string GroupId { get; private set; }
        public string GroupName { get; private set; }
        public bool IsAdsEnabled { get; private set; }
        public bool IsIAPStoreEnabled { get; private set; }
        public float InitialWalletBudget { get; private set; }
        public bool IsRespinAdEnabled { get; private set; }
        public bool IsReviveAdEnabled { get; private set; }
        public bool IsDoubleGoldAdEnabled { get; private set; }
        public float AdRevenuePerImpression { get; private set; }

        public const float StandardInitialBudget = 20000.0f; // Rp 20.000,00
        public const float StandardAdRewardIdr = 250.0f;      // Rp 250,00 per impression

        public ExperimentConfig(
            ExperimentGroup group,
            string groupId,
            string groupName,
            bool isAdsEnabled,
            bool isIAPStoreEnabled,
            float initialBudget,
            bool isRespinAdEnabled,
            bool isReviveAdEnabled,
            bool isDoubleGoldAdEnabled,
            float adRevenue)
        {
            Group = group;
            GroupId = groupId;
            GroupName = groupName;
            IsAdsEnabled = isAdsEnabled;
            IsIAPStoreEnabled = isIAPStoreEnabled;
            InitialWalletBudget = initialBudget;
            IsRespinAdEnabled = isRespinAdEnabled;
            IsReviveAdEnabled = isReviveAdEnabled;
            IsDoubleGoldAdEnabled = isDoubleGoldAdEnabled;
            AdRevenuePerImpression = adRevenue;
        }

        /// <summary>
        /// Generates the strict configuration corresponding to an ExperimentGroup according to the research matrix.
        /// </summary>
        public static ExperimentConfig CreateForGroup(ExperimentGroup group)
        {
            switch (group)
            {
                case ExperimentGroup.GroupA_DominantAds:
                    return new ExperimentConfig(
                        group: ExperimentGroup.GroupA_DominantAds,
                        groupId: "GROUP_A",
                        groupName: "Dominan Rewarded Ads",
                        isAdsEnabled: true,
                        isIAPStoreEnabled: false,
                        initialBudget: 0.0f,
                        isRespinAdEnabled: true,
                        isReviveAdEnabled: true,
                        isDoubleGoldAdEnabled: true,
                        adRevenue: StandardAdRewardIdr
                    );

                case ExperimentGroup.GroupB_DominantIAP:
                    return new ExperimentConfig(
                        group: ExperimentGroup.GroupB_DominantIAP,
                        groupId: "GROUP_B",
                        groupName: "Dominan IAP Premium Cosmetics",
                        isAdsEnabled: false,
                        isIAPStoreEnabled: true,
                        initialBudget: StandardInitialBudget,
                        isRespinAdEnabled: false,
                        isReviveAdEnabled: false,
                        isDoubleGoldAdEnabled: false,
                        adRevenue: StandardAdRewardIdr
                    );

                case ExperimentGroup.GroupC_Hybrid:
                default:
                    return new ExperimentConfig(
                        group: ExperimentGroup.GroupC_Hybrid,
                        groupId: "GROUP_C",
                        groupName: "Skema Hibrida (Ads + IAP)",
                        isAdsEnabled: true,
                        isIAPStoreEnabled: true,
                        initialBudget: StandardInitialBudget,
                        isRespinAdEnabled: true,
                        isReviveAdEnabled: true,
                        isDoubleGoldAdEnabled: true,
                        adRevenue: StandardAdRewardIdr
                    );
            }
        }
    }
}
