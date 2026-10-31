namespace LuckyDuelist.Experiment
{
    /// <summary>
    /// Experimental groups for A/B/C testing research.
    /// Group A: Rewarded Ads Dominant (No IAP Store, All Ads Active)
    /// Group B: In-App Purchase Dominant (IAP Store Open with Rp 20,000 quota, No Rewarded Ads)
    /// Group C: Hybrid Scheme (IAP Store Open with Rp 20,000 quota + All Ads Active)
    /// </summary>
    public enum ExperimentGroup
    {
        GroupA_DominantAds = 0,
        GroupB_DominantIAP = 1,
        GroupC_Hybrid = 2
    }
}
