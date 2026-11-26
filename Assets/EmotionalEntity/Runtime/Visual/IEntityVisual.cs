namespace EmotionalEntity
{
    public interface IEntityVisual
    {
        void Apply(VisualProfile profile, EntityVisualState state);
        void SetVisible(bool visible);
        void Dispose();
    }
}
