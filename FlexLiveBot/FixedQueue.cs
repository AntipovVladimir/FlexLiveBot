namespace FlexLiveBot;

public sealed class FixedQueue<T>: Queue<T>
{
    public int FixedCapacity { get; }

    public FixedQueue(int fixedCapacity)
    {
        FixedCapacity = fixedCapacity;
    }

    public new void Enqueue(T item)
    {
        base.Enqueue(item);
        if (Count > FixedCapacity)
            Dequeue();
    }
}