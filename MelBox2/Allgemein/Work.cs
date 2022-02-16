﻿using MelBoxGsm;
using System;
using System.Collections.Generic;
using static MelBoxGsm.Gsm;

namespace MelBox2
{
    static partial class Program
    {
        public static string SmsTestTrigger { get; set; } = "SMSAbruf";
       
        public static string[] LifeMessageTrigger { get; set; } = { "MelSysOK", "SgnAlarmOK" };

        private static bool isFirstParseNewSmsAfterStartup = true; //Dinge, die nur beim ersten Abfragen des SMS-Speichers getan werden sollen

        /// <summary>
        /// Bearbeitet die vom Modem eingelesenen, eingegangenen SMS-Nachrichten: 
        /// - protokolliert den Eingang in der Datenbank 
        /// - leitet das Löschen der SMS aus dem Modemspeicher ein
        /// - prüft auf SMS-Inhalt 'SMS-Abruf'
        /// - prüft auf SMS-Inhalt 'MelSysOK' oder 'SgnAlarmOK'
        /// - prüft, ob die Weiterleitung dieser Nachricht gesperrt ist
        /// - prüft, ob zum Eingangszeitpunkt an die Bereitschaft weitergeleitet werden soll
        /// - leitet das Senden von SMS oder EMail an die Berietschaft ein
        /// </summary>
        /// <param name="smsenIn">Eingegangene SMS</param>
        private static void ParseNewSms(List<SmsIn> smsenIn)
        {            
            if (smsenIn.Count == 0) return;
           
            bool isWatchTime = Sql.IsWatchTime();
#if DEBUG
            if (!isWatchTime)
            {
                Console.WriteLine($"DEBUG: Bei Debug-Kompilat Weiterleitung auch während der Geschäftszeit.");
                isWatchTime = true;
            }
#endif
            foreach (SmsIn smsIn in smsenIn)
            {
                if (Sql.InsertRecieved(smsIn)) //Empfang in Datenbank protokollieren                
                    Gsm.SmsDelete(smsIn.Index);
                
                bool isSmsTest = IsSmsTest(smsIn);
                if (isSmsTest) continue; //'SmsAbruf' nicht an Bereitschaft und Emailverteiler senden. 
                bool isLifeMessage = IsLifeMessage(smsIn); //Meldung mit 'MelSysOK' oder 'SgnAlarmOK'?
                bool isMessageBlocked = Sql.IsMessageBlockedNow(smsIn.Message);
               
                if (isWatchTime && !isLifeMessage && !isMessageBlocked && !isFirstParseNewSmsAfterStartup)
                    SendSmsToShift(smsIn);

                SendEmailToShift(smsIn, isWatchTime, isLifeMessage, isMessageBlocked);
            }

            isFirstParseNewSmsAfterStartup = false; 
        }

        private static void ParseNewEmail(System.Net.Mail.MailMessage mail)
        {
            if (!Sql.IsInWhitelist(mail.From)) //nur bekannte Absender verarbeiten
            {
                Log.Warning($"Email von unbekanntem Absender >{mail.From.Address}< wird ignoriert. Inhalt:\r\n" + Sql.RemoveHTMLTags(mail.Body) , 7698);
                Sql.InsertLog(2, $"Email von unbekanntem Absender >{mail.From.Address}< wird ignoriert.");
                return;
            }
            
            if (Sql.InsertRecieved(mail)) //Empfang in Datenbank protokolliere
            {
                //TODO Email löschen?
            }

            bool isLifeMessage = Program.IsLifeMessage(mail); //Meldung mit 'MelSysOK' oder 'SgnAlarmOK'?
            bool isMessageBlocked = Sql.IsMessageBlockedNow(Sql.RemoveHTMLTags(mail.Body));
            bool isWatchTime = Sql.IsWatchTime();

            if (isWatchTime && !isLifeMessage && !isMessageBlocked && !isFirstParseNewSmsAfterStartup)
                SendSmsToShift( Sql.RemoveHTMLTags(mail.Body) ); //Email ohne HTML an aktuelle Bereitschaft per Sms senden
           
            SendEmailToShift(mail, isWatchTime, isLifeMessage, isMessageBlocked); //Empfangene Email an Bereitschaft/Service Email

            isFirstParseNewSmsAfterStartup = false;
        }

        /// <summary>
        /// Prüft, ob die Nachricht 'SmsTestTrigger' z.B 'SmsAbruf' enthält und sendet die Nachricht zurück an den Sender. 
        /// </summary>
        /// <param name="sms">Eingegangene SMS</param>
        /// <returns>true = SMS enthält das in 'SmsTestTrigger' definierten Triggerwort</returns>
        private static bool IsSmsTest(SmsIn sms)
        {
            if (!sms.Message.ToLower().StartsWith(SmsTestTrigger.ToLower())) return false;

            Person p = Sql.SelectOrCreatePerson(sms);
            SmsSend(sms.Phone, $"{DateTime.Now:G} {SmsTestTrigger}");
            string txt = $"SMS-Abruf von [{p.Id}] >{p.Phone}< >{p.Name}< >{p.Company}<";
            Log.Info(txt, 50829);
            Sql.InsertLog(3, txt);

            return true; //Dies war 'SMSAbruf'
        }

        /// <summary>
        /// Prüft, ob der Inhalt der Nachricht eine Routinemeldung ist z.B. 'MelSysOK'
        /// </summary>
        /// <param name="sms">Eingegangene SMS</param>
        /// <returns>true = SMS enthält die in 'LifeMessageTrigger' definierten Triggerworte</returns>
        private static bool IsLifeMessage(SmsIn sms)
        {
            foreach (string trigger in LifeMessageTrigger)
            {
                if (sms.Message.ToLower().Contains(trigger.ToLower())) return true;
            }

            return false;
        }

        internal static bool IsLifeMessage(System.Net.Mail.MailMessage mail)
        {
            foreach (string trigger in LifeMessageTrigger)
            {
                if (mail.Body.ToLower().Contains(trigger.ToLower())) return true;
            }

            return false;
        }

        /// <summary>
        /// Leitet die empfangene SMS an die aktuelle Bereitschaft (wenn SMS-Benachrichtigung für den Empänger(n) freigegeben ist)
        /// </summary>
        /// <param name="sms">Eingegangene SMS</param>
        private static void SendSmsToShift(SmsIn sms)
        {
            foreach (string phone in Sql.GetCurrentShiftPhoneNumbers())
            {
                //Protokollierung in DB nach Absenden von Modem!
                SmsSend(phone, sms.Message);
            }
        }

        private static void SendSmsToShift(string smsText)
        {
            foreach (string phone in Sql.GetCurrentShiftPhoneNumbers())
            {
                //Protokollierung in DB nach Absenden von Modem!
                SmsSend(phone, smsText);
            }
        }

        /// <summary>
        /// Leitet die empfangene SMS als EMail an die aktuelle Bereitschaft bzw. den Verteiler.
        /// Erzeugt in Abhänigkeit 
        /// </summary>
        /// <param name="smsIn">Eingegangene SMS</param>
        /// <param name="isWatchTime">zur Zeit soll an Bereitschaft gesendet werden.</param>
        /// <param name="isLifeMessage">eingegangen SMS ist eine Routinemeldung</param>
        /// <param name="isMessageBlocked">eingegangene SMS ist zur Zeit für Weiterleitung gesperrt</param>
        private static void SendEmailToShift(SmsIn smsIn, bool isWatchTime, bool isLifeMessage, bool isMessageBlocked)
        {
            string body = $"Absender \t>{smsIn.Phone}<\r\n" +
                           $"Text \t\t>{smsIn.Message}<\r\n" +
                           $"Sendezeit \t>{DateTime.Now:G}<\r\n\r\n" +
                           (
                           isFirstParseNewSmsAfterStartup ? "SMS bei Neustart GSM - Modem ! Keine Weiterleitung an Bereitschaft." :
                           isLifeMessage ? $"Keine Weiterleitung an Bereitschaftshandy bei Schlüsselworten >{string.Join(", ", LifeMessageTrigger)}<." :
                           isMessageBlocked ? "Keine Weiterleitung an Bereitschaftshandy da SMS gesperrt." :
                           isWatchTime ? "Weiterleitung an Bereitschaftshandy außerhalb Geschäftszeiten ist erfolgt." :
                           "Keine Weiterleitung an Bereitschaftshandy während der Geschäftszeiten."
                           );

            Person p = Sql.SelectOrCreatePerson(smsIn);

            string subject = $"SMS-Eingang >{p.Name}<{ (p.Company?.Length == 0 ? string.Empty : $", >{p.Company}<")}, SMS-Text >{smsIn.Message}<";

            //Email An: nur an eingeteilte Bereitschaft
            System.Net.Mail.MailAddressCollection mc = (isWatchTime && !isLifeMessage && !isMessageBlocked && !isFirstParseNewSmsAfterStartup)
                                                        ? Sql.GetCurrentShiftEmailAddresses()
                                                        : new System.Net.Mail.MailAddressCollection();

            if (mc.Count == 0) mc.Add(Email.Admin); //Keine Email-Bereitschaft eingeteilt, Email geht an Admin und Dauerempfänger

            int emailId = new Random().Next(256, 9999);

            Sql.InsertSent(mc[0], smsIn.Message, emailId);  //Protokollierung nur einmal pro mail, nicht für jden Empfänger einzeln! ok?
            Email.Send(mc, body, subject, true, emailId);

        }

        private static void SendEmailToShift(System.Net.Mail.MailMessage mail, bool isWatchTime, bool isLifeMessage, bool isMessageBlocked)
        {
            string recText = Sql.RemoveHTMLTags(mail.Body);

            string body = $"Absender \t>{mail.From.Address}<\r\n" +
                           $"Text \t\t>{recText}<\r\n" +
                           $"Sendezeit \t>{DateTime.Now:G}<\r\n\r\n" +
                           (
                           isFirstParseNewSmsAfterStartup ? "Email bei Neustart MelBox2! Keine Weiterleitung an Bereitschaft." :
                           isLifeMessage ? $"Keine SMS-Weiterleitung an Bereitschaftshandy bei Schlüsselworten >{string.Join(", ", LifeMessageTrigger)}<." :
                           isMessageBlocked ? "Keine Weiterleitung an Bereitschaftshandy da Nachricht gesperrt." :
                           isWatchTime ? "Weiterleitung an Bereitschaft außerhalb Geschäftszeiten ist erfolgt." :
                           "Keine Weiterleitung an Bereitschaft während der Geschäftszeiten."
                           );

            Person p = Sql.SelectOrCreatePerson(mail.From);

            string subject = $"Email-Eingang >{p.Name}<{ (p.Company?.Length == 0 ? string.Empty : $", >{p.Company}<")}, Email-Text >{mail.Body}<";

            //Email An: nur an eingeteilte Bereitschaft
            System.Net.Mail.MailAddressCollection mc = (isWatchTime && !isLifeMessage && !isMessageBlocked && !isFirstParseNewSmsAfterStartup)
                                                        ? Sql.GetCurrentShiftEmailAddresses()
                                                        : new System.Net.Mail.MailAddressCollection();

            if (mc.Count == 0) mc.Add(Email.Admin); //Keine Email-Bereitschaft eingeteilt, Email geht an Admin und Dauerempfänger

            int emailId = new Random().Next(256, 9999);

            Sql.InsertSent(mc[0], recText, emailId);  //Protokollierung nur einmal pro mail, nicht für jden Empfänger einzeln! ok?
            Email.Send(mc, body, subject, true, emailId);
        }

    }
}