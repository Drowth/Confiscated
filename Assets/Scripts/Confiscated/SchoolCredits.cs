namespace Confiscated
{
    /// <summary>Who made the game: one list for the end-of-run thanks screen and the title's classroom credits.</summary>
    public static class SchoolCredits
    {
        public const string Creator="Lee Grieve";
        public static readonly string[] Testers={"Jacob Grieve","Elliott King","Joseph Taylor","Harry"};
        /// <summary>"A, B, C and D"</summary>
        public static string TesterList=>Testers.Length<2?string.Join("",Testers):string.Join(", ",Testers,0,Testers.Length-1)+" and "+Testers[Testers.Length-1];
    }
}
