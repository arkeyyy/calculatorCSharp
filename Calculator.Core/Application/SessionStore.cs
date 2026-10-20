using PeterO.Numbers;

namespace Calculator.Core;

public sealed record HistoryItem(ModeId Mode, string Expression, string Result, DateTime Time);

public sealed class SessionStore
{
    private readonly List<HistoryItem> history = [];
    public IReadOnlyList<HistoryItem> History => history.AsReadOnly();
    public EDecimal? Memory { get; private set; }
    public event Action? Changed;
    public void AddHistory(ModeId mode, string expression, string result)
    {
        history.Insert(0, new(mode, expression, result, DateTime.Now));
        if (history.Count > 200) history.RemoveAt(history.Count - 1);
        Changed?.Invoke();
    }
    public void ClearHistory() { history.Clear(); Changed?.Invoke(); }
    public void ClearMemory() { Memory = null; Changed?.Invoke(); }
    public void StoreMemory(EDecimal value) { Memory = value; Changed?.Invoke(); }
    public void AddMemory(EDecimal value) => StoreMemory(Numbers.Add(Memory ?? EDecimal.Zero, value));
    public void SubtractMemory(EDecimal value) => StoreMemory(Numbers.Subtract(Memory ?? EDecimal.Zero, value));
}
