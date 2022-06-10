using System;
using System.Collections.Generic;
using System.Data;
using System.Net.Mail;

namespace MelBox2
{
    partial class Sql
    {
        public static int PermanentEmailRecievers { get; private set; }

        public static int StartOfBusinessDay { get; set; } = 8;
        public static int EndOfBusinessDay { get; set; } = 17;
        public static int EndOfBusinessFriday { get; set; } = 15;

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

        private static bool IsHolyday(DateTime date)
        {
            return Holydays(date).Contains(date);
        }

        #endregion

        /// <summary>
        /// Prüft, ob aktuell an die Bereitschaft gesendet werden sollte.
        /// </summary>
        /// <returns>false wenn jetzt reguläre Geschäftszeit (Mo-Do 8-17 Uhr, Fr 8-15 Uhr) und kein Feiertag ist - sonst true </returns>
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
                    return DateTime.Now.Hour < StartOfBusinessDay || DateTime.Now.Hour >= EndOfBusinessFriday;
                default:
                    return DateTime.Now.Hour < StartOfBusinessDay || DateTime.Now.Hour >= EndOfBusinessDay;
            }
        }

        internal static DateTime ShiftStartTimeUtc(DateTime dateLocal)
        {
            int hour = EndOfBusinessDay;
            DayOfWeek day = dateLocal.DayOfWeek;
            if (day == DayOfWeek.Friday) hour = EndOfBusinessFriday;
            if (day == DayOfWeek.Saturday || day == DayOfWeek.Sunday || IsHolyday(dateLocal)) hour = StartOfBusinessDay;

            return dateLocal.Date.AddHours(hour).ToUniversalTime();
        }

        internal static DateTime ShiftEndTimeUtc(DateTime dateLocal)
        {
            int hour = StartOfBusinessDay;
            return dateLocal.Date.AddHours(hour).ToUniversalTime();
        }

        /// <summary>
        /// Gibt die Telefonnummern der aktuell gültigen Bereitschaft aus. Wenn zum aktuellen Zeitpunkt keine Bereitschaft definiert ist, wird die Nummer des Bereitschaftshandys ausgegeben.
        /// </summary>
        /// <returns>Nummern der zum jetzigen Zeitpunkt gültige Rufbereitschaft</returns>
        internal static List<string> GetCurrentShiftPhoneNumbers()
        {

            //Definierte Bereitschaft (SMS) aus Datenbank; Telefonnumer vorhanden, hat zur Zeit Bereitschaft, SMS freigegeben
            const string query1 = "SELECT Phone FROM Person WHERE Phone NOT NULL AND ID IN (SELECT PersonId FROM Shift WHERE CURRENT_TIMESTAMP BETWEEN Start AND End) AND Via IN (1,3,5); ";

            DataTable dt = SelectDataTable(query1, null);

            if (dt.Rows.Count == 0) //keine vordefinierte Bereitschaft (SMS) gefunden
            {
                //an Bereitschafshandy, wenn zur Zeit keine Bereitschaft definiert ist (für den Fall, dass sich die Bereitschaft nur per EMail benachrichtigen lässt).
                const string query2 = "SELECT Phone FROM Person WHERE Name = 'Bereitschaftshandy' AND NOT EXISTS (SELECT PersonId FROM Shift WHERE CURRENT_TIMESTAMP BETWEEN Start AND End)";

                dt = SelectDataTable(query2, null);
            }

            List<string> phoneNumbers = new List<string>();

            for (int i = 0; i < dt.Rows.Count; i++)
            {
                phoneNumbers.Add(dt.Rows[i]["Phone"].ToString());
            }

            return phoneNumbers;
        }


        /// <summary>
        /// Gibt die aktuelle Rufweiterleitungsnummer aus. Ist aktuell keine Rufannahme definiert, wird die Nummer des Bereitschaftshandys ausgegeben.
        /// Gibt es keinen Eintrag für das Bereitschaftshandy, wird der Eintrag erzeugt.
        /// Gibt es mehr als eine eingeteilte Bereitschaft, wird nur der erste Eintrag zurückgegeben.
        /// </summary>
        /// <param name="overrideCallForwardingNumber">Überschreibt die automatisch ermittelte Weiterleitungsnummer</param>
        /// <returns>Telefonnumer an die aktuell Sprachanrufe weitergeleitet werden sollen.</returns>
        internal static string GetCurrentCallForwardingNumber(string overrideCallForwardingNumber)
        {
            if (overrideCallForwardingNumber.Length > 9)
                return overrideCallForwardingNumber;

            const string query1 = "INSERT INTO Person (Name, Password, Level, Company, Phone, Email, Via)  " +
                                  @"SELECT 'Bereitschaftshandy', '�u�q�_��)vIh�ҷ\z�(yC[B���^|�', 2000, 'Kreutzträger Kältetechnik, Bremen', '+491729441694', 'Bereitschaftshandy@kreutztraeger.de', 1 " +
                                  "WHERE NOT EXISTS (SELECT Phone FROM Person WHERE Name = 'Bereitschaftshandy'); " +
                                  "SELECT Phone FROM Person WHERE Phone NOT NULL AND ID IN (SELECT PersonId FROM Shift WHERE CURRENT_TIMESTAMP BETWEEN Start AND End) AND Via < 16 " + //Via weglassen damit Rufumleitung immer zum Handy aktuellen Bereitschaft geht?
                                  "UNION SELECT Phone FROM Person WHERE Name = 'Bereitschaftshandy' " +
                                  // "AND NOT EXISTS (SELECT Phone FROM Person WHERE Phone NOT NULL AND ID IN (SELECT PersonId FROM Shift WHERE CURRENT_TIMESTAMP BETWEEN Start AND End) AND Via IN (1,3,5)) " + // unnötig ?
                                  "LIMIT 1; ";

            return SelectValue(query1, null).ToString();
        }

        internal static MailAddressCollection GetCurrentEmailRecievers(bool permanentRecieversOnly = true)
        {
            const string query1 = "SELECT Email, Name FROM Person WHERE Email NOT NULL AND Via IN (4,5);"; //nur Dauerempfänger
            const string query2 = "SELECT Email, Name FROM Person WHERE Email NOT NULL AND (ID IN (SELECT PersonId FROM Shift WHERE CURRENT_TIMESTAMP BETWEEN Start AND End) AND Via IN (2,3,4) )"; //Bereitschaft per Email + Dauerempfänger

            DataTable dt = SelectDataTable(permanentRecieversOnly ? query1 : query2, null);

            if (permanentRecieversOnly) PermanentEmailRecievers = dt.Rows.Count;

            MailAddressCollection emailAddresses = new MailAddressCollection();

            for (int i = 0; i < dt.Rows.Count; i++)
            {
                string email = dt.Rows[i]["Email"].ToString();

                try
                {
                    emailAddresses.Add(
                        new MailAddress(
                            email, dt.Rows[i]["Name"].ToString()
                            )
                        );
#if DEBUG
                    Log.Info($"DEBUG: Bereitschaft: Sende Email an >{email}<", 61312);
#endif
                }
#pragma warning disable CA1031 // Do not catch general exception types
                catch (Exception ex)
                {
                    Log.Warning($"Die Emailadresse >{email}< ist ungültig: " + ex.Message, 11313);
                }
#pragma warning restore CA1031 // Do not catch general exception types
            }

            return emailAddresses;
        }

        internal static DataTable SelectShiftsCalendar()
        {
            const string query = "SELECT * FROM View_Calendar_Full;";

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

        /// <summary>
        /// Teilt eine Bereitschaft auf, wenn sie über mehrere Kalenderwochen geht.
        /// </summary>
        /// <param name="shift"></param>
        /// <returns>Bereitschaften, jeweils in einer Kalenderwoche</returns>
        internal static List<Shift> SplitShift(Shift shift)
        {
            List<Shift> shifts = new List<Shift>();
            DateTime localStart = shift.StartUtc;
            DateTime localEnd = shift.StartUtc;

            if (shift.StartUtc.Date == shift.EndUtc.Date || shift.StartUtc.CompareTo(shift.EndUtc) > 0) //Wenn nur einen Tag lang oder Ende vor Start liegt.
            {
                shifts.Add(shift);
                return shifts;
            }
            else
            {
                while (localEnd.Date != shift.EndUtc.Date)
                {
                    do
                    {
                        localEnd = localEnd.AddDays(1);
                    }
                    while (localEnd.Date != shift.EndUtc.Date && localEnd.DayOfWeek != DayOfWeek.Monday);

                    if (localEnd.Date == shift.EndUtc.Date)
                        localEnd = shift.EndUtc; //ausgewählte End-Uhrzeit beibehalten 
                    else
                        localEnd = Sql.ShiftEndTimeUtc(localEnd.ToLocalTime()); //Standard-End-Uhrzeit nehmen

                    shifts.Add(new Shift
                    {
                        Id = shift.Id,
                        PersonId = shift.PersonId,
                        StartUtc = localStart,
                        EndUtc = localEnd
                    });

                    localStart = Sql.ShiftStartTimeUtc(localEnd.ToLocalTime());
                }
            }

            return shifts;
        }

        /// <summary>
        /// Erstellt eine neue Bereitschaft in der Datenbank
        /// </summary>
        /// <param name="shift"></param>
        /// <returns></returns>
        internal static bool InsertShift(Shift shift)
        {
            if (shift.StartUtc.CompareTo(shift.EndUtc) > 0) //Wenn Ende vor Start liegt, abbrechen.            
                return false;

            Dictionary<string, object> args = new Dictionary<string, object>
            {
                { "@PersonId", shift.PersonId},
                { "@Start", shift.StartUtc.ToString("yyyy-MM-dd HH:mm:ss")},
                { "@End", shift.EndUtc.ToString("yyyy-MM-dd HH:mm:ss")}
            };

            const string query = "INSERT INTO Shift (PersonId, Start, End) VALUES (@PersonId, @Start, @End); ";

            return NonQuery(query, args);
        }

        internal static bool UpdateShift(Shift shift)
        {

            //BAUSTELLE: Shift aufteilen, wenn sie über eine Kalenderwoche geht ?!

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
            {
                s.StartUtc = start;
                if (payload.ContainsKey("StartTime") && payload.TryGetValue("StartTime", out string startTStr) && DateTime.TryParse(startTStr, out DateTime startTime))
                    s.StartUtc = s.StartUtc.AddHours(startTime.Hour).AddMinutes(startTime.Minute).ToUniversalTime();
            }

            if (payload.ContainsKey("End") && payload.TryGetValue("End", out string endStr) && DateTime.TryParse(endStr, out DateTime end))
            {
                s.EndUtc = end;
                if (payload.ContainsKey("EndTime") && payload.TryGetValue("EndTime", out string endTStr) && DateTime.TryParse(endTStr, out DateTime endTime))
                    s.EndUtc = s.EndUtc.AddHours(endTime.Hour).AddMinutes(endTime.Minute).ToUniversalTime();
            }

            return s;
        }

        internal static Shift GetShift(DataTable dt)
        {
            Shift s = new Shift();

            if (dt.Rows.Count == 0)
                return s;

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
