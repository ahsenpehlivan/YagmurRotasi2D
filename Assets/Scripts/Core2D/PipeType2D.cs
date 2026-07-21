namespace YagmurRotasi2D.Core2D
{
    // Numeric values are explicit and append-only: existing prefabs serialize
    // Straight/Corner by this number, so those two values must never change and
    // new types must only ever be added at the end.
    public enum PipeType2D
    {
        Straight = 0,
        Corner = 1,
        Tee = 2,
        Cross = 3
    }
}
