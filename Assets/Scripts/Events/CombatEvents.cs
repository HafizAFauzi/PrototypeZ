using System.Collections.Generic;
using LuckyDuelist.Combat;

namespace LuckyDuelist.Events
{
    /// <summary>
    /// Dispatched when the 3-reel slot cylinder begins spinning.
    /// </summary>
    public struct SlotSpinStartedEvent
    {
        public int TurnNumber;

        public SlotSpinStartedEvent(int turnNumber)
        {
            TurnNumber = turnNumber;
        }
    }

    /// <summary>
    /// Dispatched when an individual reel in the cylinder stops and locks in its symbol.
    /// </summary>
    public struct SlotReelStoppedEvent
    {
        public int ReelIndex;
        public SlotSymbol Symbol;

        public SlotReelStoppedEvent(int reelIndex, SlotSymbol symbol)
        {
            ReelIndex = reelIndex;
            Symbol = symbol;
        }
    }

    /// <summary>
    /// Dispatched when all 3 reels in the cylinder have finished spinning.
    /// </summary>
    public struct SlotSpinCompletedEvent
    {
        public int TurnNumber;
        public SlotSymbol[] Results;

        public SlotSpinCompletedEvent(int turnNumber, SlotSymbol[] results)
        {
            TurnNumber = turnNumber;
            Results = results;
        }
    }

    /// <summary>
    /// Dispatched when a single reel is re-spun (e.g. via Rewarded Ad in Group A or C).
    /// </summary>
    public struct SlotRespinTriggeredEvent
    {
        public int ReelIndex;
        public SlotSymbol OldSymbol;
        public SlotSymbol NewSymbol;
        public bool ViaRewardedAd;

        public SlotRespinTriggeredEvent(int reelIndex, SlotSymbol oldSymbol, SlotSymbol newSymbol, bool viaRewardedAd)
        {
            ReelIndex = reelIndex;
            OldSymbol = oldSymbol;
            NewSymbol = newSymbol;
            ViaRewardedAd = viaRewardedAd;
        }
    }

    /// <summary>
    /// Dispatched whenever hold slots change (a symbol was held, unheld, or cleared).
    /// </summary>
    public struct HoldSlotsChangedEvent
    {
        public SlotSymbol?[] HoldSlots;
        public int HeldCount;

        public HoldSlotsChangedEvent(SlotSymbol?[] holdSlots, int heldCount)
        {
            HoldSlots = holdSlots;
            HeldCount = heldCount;
        }
    }

    /// <summary>
    /// Dispatched when player commits their turn (locking in unheld symbols for combat actions).
    /// </summary>
    public struct TurnCommittedEvent
    {
        public int TurnNumber;
        public List<SlotSymbol> ExecutedSymbols;
        public SlotSymbol?[] PreservedHoldSlots;

        public TurnCommittedEvent(int turnNumber, List<SlotSymbol> executedSymbols, SlotSymbol?[] preservedHoldSlots)
        {
            TurnNumber = turnNumber;
            ExecutedSymbols = executedSymbols;
            PreservedHoldSlots = preservedHoldSlots;
        }
    }

    /// <summary>
    /// Dispatched whenever player health changes (damage taken, healed, or revived).
    /// </summary>
    public struct PlayerHealthChangedEvent
    {
        public int CurrentHp;
        public int MaxHp;
        public int Delta;
        public string Reason;

        public PlayerHealthChangedEvent(int currentHp, int maxHp, int delta, string reason)
        {
            CurrentHp = currentHp;
            MaxHp = maxHp;
            Delta = delta;
            Reason = reason;
        }
    }

    /// <summary>
    /// Dispatched when a Wild Joker symbol is resolved/replaced by the player or system.
    /// </summary>
    public struct WildJokerResolvedEvent
    {
        public int ReelIndex;
        public SlotSymbol ChosenSymbol;

        public WildJokerResolvedEvent(int reelIndex, SlotSymbol chosenSymbol)
        {
            ReelIndex = reelIndex;
            ChosenSymbol = chosenSymbol;
        }
    }
}
