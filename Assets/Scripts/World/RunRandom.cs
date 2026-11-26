namespace AshenWoods.World
{
    /// <summary>
    /// One seed per run, so random choices (the office code, the valve order, where quest items lie) differ
    /// between runs but stay the same when the player retries from a checkpoint: the notes they already read
    /// still tell the truth. A fresh run (CheckpointSystem.Clear) draws a new seed.
    /// </summary>
    public static class RunRandom
    {
        private static int seed;

        public static int Seed
        {
            get
            {
                if (seed == 0)
                {
                    NewRun();
                }

                return seed;
            }
        }

        public static void NewRun()
        {
            seed = System.Environment.TickCount ^ UnityEngine.Random.Range(1, int.MaxValue);
            if (seed == 0)
            {
                seed = 1;
            }
        }

        /// <summary>A generator for one named choice: the same name gives the same sequence for the whole run.</summary>
        public static System.Random For(string name)
        {
            int hash = 17;
            if (name != null)
            {
                foreach (char c in name)
                {
                    hash = hash * 31 + c;
                }
            }

            return new System.Random(Seed ^ hash);
        }
    }
}
