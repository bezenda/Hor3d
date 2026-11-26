using System;
using System.Collections.Generic;

namespace AshenWoods.World
{
    /// <summary>
    /// Words in notes that are only known at run time, written as {name} in the text: the office code digits
    /// ({code1}..{code3}), the valve order ({valves}). Whoever owns the value registers it; InfoPoint passes its
    /// text through <see cref="Apply"/> before showing it. An unknown token is left as it is.
    /// </summary>
    public static class TextTokens
    {
        private static readonly Dictionary<string, Func<string>> Values = new Dictionary<string, Func<string>>();

        public static void Register(string name, Func<string> value)
        {
            if (!string.IsNullOrEmpty(name) && value != null)
            {
                Values[name] = value;
            }
        }

        public static string Apply(string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf('{') < 0)
            {
                return text;
            }

            foreach (KeyValuePair<string, Func<string>> pair in Values)
            {
                string token = "{" + pair.Key + "}";
                if (text.Contains(token))
                {
                    text = text.Replace(token, pair.Value());
                }
            }

            return text;
        }
    }
}
