namespace Blazr.ComponentStarter.Data
{
    public static class GuidExtensions
    {
        public static string ShortGuid(this Guid id)
            => id.ToString().Substring(0,6);
    }
}
