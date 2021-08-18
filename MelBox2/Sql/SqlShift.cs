using System;
using System.Collections.Generic;
using System.Data;
using System.Net.Mail;

namespace MelBox2
{
    partial class Sql
    {
        #region Feiertage

        // Aus VB konvertiert
        private static DateTime DateOsterSonntag(DateTime pDate)
        {
            int viJahr, viMonat, viTag;
            int viC, viG, viH, viI, viJ, viL;

            viJahr = pDate.Year;
            viG = viJahr % 19;
            viC = viJahr / 100;
            viH = (viC - viC / 4 - (8 * viC + 13) / 25 + 19 * viG + 15) % 30;
            viI = viH - viH / 28 * (1 - 29 / (viH + 1) * (21 - viG) / 11);
            viJ = (viJahr + viJahr / 4 + viI + 2 - viC + viC / 4) % 7;
            viL = viI - viJ;
            viMonat = 3 + (viL + 40) / 44;
            viTag = viL + 28 - 31 * (viMonat / 4);

            return new DateTime(viJahr, viMonat, viTag);
        }

        // Aus VB konvertiert
        internal static List<DateTime> Holydays(DateTime pDate)
        {
            int viJahr = pDate.Year;
            DateTime vdOstern = DateOsterSonntag(pDate);
            List<DateTime> feiertage = new List<DateTime>
            {
                new DateTime(viJahr, 1, 1),    // Neujahr
                new DateTime(viJahr, 5, 1),    // Erster Mai
                vdOstern.AddDays(-2),          // Karfreitag
                vdOstern.AddDays(1),           // Ostermontag
                vdOstern.AddDays(39),          // Himmelfahrt
                vdOstern.AddDays(50),          // Pfingstmontag
                new DateTime(viJahr, 10, 3),   // TagderDeutschenEinheit
                new DateTime(viJahr, 10, 31),  // Reformationstag
                new DateTime(viJahr, 12, 24),  // Heiligabend
                new DateTime(viJahr, 12, 25),  // Weihnachten 1
                new DateTime(viJahr, 12, 26),  // Weihnachten 2
                new DateTime(viJahr, 12, DateTime.DaysInMonth(viJahr, 12)) // Silvester
            };

            return feiertage;
        }

        internal static bool IsHolyday(DateTime date)
        {
            return Holydays(date).Contains(date);
        }

        #endregion

        /// <summary>
        /// Prüft, ob aktuell an die Bereitschaft gesendet werden sollte.
        /// </summary>
        /// <returns>false in der regulären Geschäftszeit (Mo-Do 8-17 Uhr, Fr 8-15 Uhr) und kein Feiertag ist - sonst true </returns>
        internal static bool IsWatchTime()
        {
            if (IsHolyday(DateTime.Now))
                return true;

            switch (DateTime.Now.DayOfWeek)
            {
                case DayOfWeek.Saturday:
                case DayOfWeek.Sunday:
                    return true;
                case DayOfWeek.Friday:
                    return DateTime.Now.Hour < 8 || DateTime.Now.Hour >= 15;                   
                default:
                    return DateTime.Now.Hour < 8 || DateTime.Now.Hour >= 17;
            }
        }

        internal static DateTime ShiftStartTimeUtc(DateTime dateLocal)
        {
            int hour = 17;
            DayOfWeek day = dateLocal.DayOfWeek;
            if (day == DayOfWeek.Friday) hour = 15;
            if (day == DayOfWeek.Saturday || day == DayOfWeek.Sunday || IsHolyday(dateLocal)) hour = 8;

            return dateLocal.Date.AddHours(hour).ToUniversalTime();
        }

        internal static DateTime ShiftEndTimeUtc(DateTime dateLocal)
        {
            int hour = 8;

            return dateLocal.Date.AddHours(hour).ToUniversalTime();
        }

        internal static List<string> GetCurrentShiftPhoneNumbers()
        {
            const string query = "SELECT Phone FROM Person WHERE Phone NOT NULL AND ID IN (SELECT PersonId FROM Shift WHERE CURRENT_TIMESTAMP BETWEEN Start AND End) AND Via IN (1,3); ";

            DataTable dt = SelectDataTable(query, null);

            if (dt.Rows.Count == 0) //Bereitschaftshandy einfügen
            {
                dt = SelectDataTable("SELECT Phone FROM Person WHERE Name = 'Bereitschaftshandy'; ", null);
            }

            List<string> phoneNumbers = new List<string>();

            for (int i = 0; i < dt.Rows.Count; i++)
            {
                phoneNumbers.Add(dt.Rows[i]["Phone"].ToString());
            }

            return phoneNumbers;
        }

        internal static MailAddressCollection GetCurrentShiftEmailAddresses(bool permanentRecievers = false)
        {
            const string query1 = "SELECT Email, Name FROM Person WHERE Email NOT NULL AND Via = 4;"; //Dauerempfänger
            const string query2 = "SELECT Email, Name FROM Person WHERE Email NOT NULL AND (ID IN (SELECT PersonId FROM Shift WHERE CURRENT_TIMESTAMP BETWEEN Start AND End) AND Via IN (2,3) )"; //Bereitschaft per Email

            DataTable dt = SelectDataTable(permanentRecievers ? query1 : query2, null);

            MailAddressCollection emailAddresses = new MailAddressCollection();

            for (int i = 0; i < dt.Rows.Count; i++)
            {
                emailAddresses.Add(
                    new MailAddress(
                        dt.Rows[i]["Email"].ToString(), dt.Rows[i]["Name"].ToString()
                        )
                    );
            }

            return emailAddresses;
        }

        internal static DataTable SelectShiftsCalendar()
        {
            string query = "SELECT * FROM View_Calendar " +
                "UNION " +
                "SELECT NULL AS ID, NULL AS PersonId, NULL AS Name, NULL AS Via, DATE(d, 'weekday 1') AS Start, NULL AS End, " +
                "strftime('%W', d) AS KW, " +
                "NULL AS Mo, NULL AS Di, NULL AS Mi, NULL AS Do, NULL AS Fr, NULL AS Sa, NULL AS So, NULL AS mehr " +
                " FROM(WITH RECURSIVE dates(d) AS(VALUES(date('now')) " +
                "UNION ALL " +
                "SELECT date(d, '+7 day', 'weekday 1') FROM dates WHERE d < date('now', '+1 year')) SELECT d FROM dates) " +
                " WHERE KW NOT IN(SELECT KW FROM View_Calendar WHERE date(Start) >= date('now', '-7 day', 'weekday 1') ) " +
                "ORDER BY Start; ";

            return SelectDataTable(query, null);
        }

        public static DataTable SelectShift(int shiftId)
        {
            Dictionary<string, object> args = new Dictionary<string, object>
            {
                { "@ShiftId", shiftId}
            };

            const string query = "SELECT ID, PersonId, Start, End FROM Shift WHERE ID = @ShiftId; ";

            return Sql.SelectDataTable(query, args);
        }

        internal static bool InsertShift(int personId, DateTime startUtc, DateTime endUtc)
        {
            Dictionary<string, object> args = new Dictionary<string, object>
            {
                { "@PersonId", personId},
                { "@Start", startUtc.ToString("yyyy-MM-dd HH:mm:ss")},
                { "@End", endUtc.ToString("yyyy-MM-dd HH:mm:ss")}
            };

            const string query = "INSERT INTO Shift (PersonId, Start, End) VALUES (@PersonId, @Start, @End); ";

            return NonQuery(query, args);
        }

        internal static bool UpdateShift(Shift shift) //ungetestet
        {
            Dictionary<string, object> args = new Dictionary<string, object>
            {
                { "@ShiftId", shift.Id},
                { "@PersonId", shift.PersonId},
                { "@Start", shift.StartUtc.ToString("yyyy-MM-dd HH:mm:ss")},
                { "@End", shift.EndUtc.ToString("yyyy-MM-dd HH:mm:ss")}
            };

            const string query = "UPDATE Shift SET PersonId = @PersonId, Start = @Start, End = @End WHERE ID = @ShiftId; ";

            return NonQuery(query, args);
        }


        public static bool DeleteShift(int shiftId)
        {
            Dictionary<string, object> args = new Dictionary<string, object>
            {
                { "@ShiftId", shiftId}
            };
            string query = $"DELETE FROM Shift WHERE ID = @ShiftId; ";

            return NonQuery(query, args);
        }

        internal static Shift GetShift(Dictionary<string, string> payload)
        {
            Shift s = new Shift();

            if (payload.ContainsKey("Id") && payload.TryGetValue("Id", out string shiftIdStr) && int.TryParse(shiftIdStr, out int shiftId))
                s.Id = shiftId;

            if (payload.ContainsKey("PersonId") && payload.TryGetValue("PersonId", out string personIdStr) && int.TryParse(personIdStr, out int personId))
                s.PersonId = personId;

            if (payload.ContainsKey("Start") && payload.TryGetValue("Start", out string startStr) && DateTime.TryParse(startStr, out DateTime start))
                s.StartUtc= start;

            if (payload.ContainsKey("End") && payload.TryGetValue("End", out string endStr) && DateTime.TryParse(endStr, out DateTime end))
                s.EndUtc = end;

            return s;
        }

        internal static Shift GetShift(DataTable dt)
        {
            Shift s = new Shift();

            if (dt.Columns.Contains("ID") && int.TryParse(dt.Rows[0]["ID"].ToString(), out int shiftId))
                s.Id = shiftId;

            if (dt.Columns.Contains("PersonId") && int.TryParse(dt.Rows[0]["PersonId"].ToString(), out int personId))
                s.PersonId = personId;

            if (dt.Columns.Contains("Start") && DateTime.TryParse(dt.Rows[0]["Start"].ToString(), out DateTime start))
                s.StartUtc = start;

            if (dt.Columns.Contains("End") && DateTime.TryParse(dt.Rows[0]["End"].ToString(), out DateTime end))
                s.EndUtc = end;

            return s;
        }

    }

    internal class Shift
    {
        public int Id { get; set; }

        public int PersonId { get; set; }

        public DateTime StartUtc { get; set; }

        public DateTime EndUtc { get; set; }
       
    }

}
