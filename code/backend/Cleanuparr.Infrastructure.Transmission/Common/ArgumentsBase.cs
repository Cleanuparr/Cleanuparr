using System.Collections.Generic;

namespace Transmission.API.RPC.Common
{
    /// <summary>
    /// Absract class for arguments
    /// </summary>
    public abstract class ArgumentsBase
    {
        internal Dictionary<string, object> Data = new Dictionary<string, object>();

        internal object this[string name]
        {
            set { SetValue(name, value); }
        }

        private void SetValue(string name, object value)
        {
            if (this.Data.ContainsKey(name)) Data[name] = value;
            else Data.Add(name, value);
        }

        internal T GetValue<T>(string name)
        {
            return this.Data.ContainsKey(name) ? (T)Data[name] : default(T);
        }
    }
}
