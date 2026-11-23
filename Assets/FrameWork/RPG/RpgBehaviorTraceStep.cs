namespace FrameWork.FPS
{
    public readonly struct RpgBehaviorTraceStep
    {
        public RpgBehaviorNode Node { get; }
        public RpgBehaviorNode Parent { get; }
        public RpgBehaviorStatus Result { get; }

        internal RpgBehaviorTraceStep(RpgBehaviorNode node, RpgBehaviorNode parent, RpgBehaviorStatus result)
        {
            Node = node;
            Parent = parent;
            Result = result;
        }
    }
}
