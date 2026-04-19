using SeatSync.Web.Models;

namespace SeatSync.Web.Services;

public interface ICheckoutFlowState
{
    PendingCheckoutModel? Current { get; }
    void Set(PendingCheckoutModel checkout);
    void Clear();
    bool TryGet(Guid holdId, out PendingCheckoutModel checkout);
}

public sealed class CheckoutFlowState : ICheckoutFlowState
{
    public PendingCheckoutModel? Current { get; private set; }

    public void Set(PendingCheckoutModel checkout)
    {
        Current = checkout;
    }

    public void Clear()
    {
        Current = null;
    }

    public bool TryGet(Guid holdId, out PendingCheckoutModel checkout)
    {
        if (Current is not null && Current.HoldId == holdId)
        {
            checkout = Current;
            return true;
        }

        checkout = null!;
        return false;
    }
}
