using System.Collections.Generic;

namespace MelBox2
{
    partial class Sql
    {
        #region INI-Tabelle

        internal static bool InsertIniProperty(string property, string val)
        {
            if (val == null)
                return false;

            Dictionary<string, object> args = new Dictionary<string, object>
            {
                { "@Property", property },
                { "@Value", val }
            };

            return NonQuery($"INSERT INTO Ini (Property, Value) VALUES (@Property, @Value);", args);
        }

        internal static bool UpdateIniProperty(string property, string val)
        {
            Dictionary<string, object> args = new Dictionary<string, object>
            {
                { "@Property", property },
                { "@Value", val }
            };

            return NonQuery($"UPDATE Ini SET Value = @Value WHERE Property = @Property;", args);
        }

        internal static object SelectIniProperty(string property)
        {
            string query = "SELECT Value FROM Ini WHERE Property = @Property;";

            Dictionary<string, object> args = new Dictionary<string, object>
            {
                { "@Property", property }
            };

            return SelectValue(query, args);
        }

        #endregion

    }
}
