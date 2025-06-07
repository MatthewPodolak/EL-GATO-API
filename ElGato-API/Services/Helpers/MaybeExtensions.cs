namespace ElGato_API.Services.Helpers
{
    public static class MaybeExtensions
    {
        public static void Let<T>(this T? value, Action<T> action) where T : class
        {
            if (value != null) action(value);
        }
    }
}
