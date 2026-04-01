namespace Server.Game.Room
{
    public class PatternZone
    {
        public int ZoneId { get; set; }
        public int PosX { get; set; }
        public int PosY { get; set; }
        public int Radius { get; set; }

        public bool IsSatisfied { get; set; }
    }
}