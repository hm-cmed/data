namespace CodemedX.Core
{
    /// <summary>
    /// locomotion_mode のカタログ。VR酔いと観察行動（見落とし）の相関を解析するために必ず記録する。
    /// 値は schema/training-event.schema.json の enum と一致させること。
    /// </summary>
    public static class LocomotionModes
    {
        public const string Teleport = "teleport";
        public const string Continuous = "continuous";
        public const string Seated = "seated";
        public const string Desktop = "desktop";
        public const string Unknown = "unknown";

        public static bool IsValid(string value)
        {
            return value == Teleport
                || value == Continuous
                || value == Seated
                || value == Desktop
                || value == Unknown;
        }
    }
}
