using System;

namespace ND.Inventory
{
    public static class Parser
    {
        public static T Convert<T>(string value)
        {
            return (T)System.Convert.ChangeType(value, typeof(T));
        }
    }
}
