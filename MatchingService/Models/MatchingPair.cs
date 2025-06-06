namespace MatchingApp.Models
{
    public class MatchingPair
    {
        public Description Description1 { get; set; } = null!;
        public Description Description2 { get; set; } = null!;
        public int CurrentPairIndex { get; set; }
        public int TotalPairs { get; set; }
        public Guid SessionId { get; set; }
    }
}