namespace UnityEngine
{
    public static class Random
    {
        public readonly struct State
        {
            public State(int value)
            {
                Value = value;
            }

            public int Value { get; }
        }

        private static State currentState;

        public static State state
        {
            get => currentState;
            set => currentState = value;
        }

        public static Vector3 insideUnitSphere
        {
            get
            {
                currentState = new State(currentState.Value + 1);
                return Vector3.zero;
            }
        }
    }
}
