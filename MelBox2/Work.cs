using MelBoxGsm;
using System;
using System.Collections.Generic;
using static MelBoxGsm.Gsm;

namespace MelBox2
{
    static partial class Program
    {
        public static string SmsTestTrigger { get; set; } = "SMSAbruf";
       
        public static string[] LifeMessageTrigger { get; set; } = { "MelSysOK", "SgnAlarmOK" };
               
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
                bool isLifeMessage = IsLifeMessage(smsIn);
                bool isMessageBlocked = Sql.IsMessageBlockedNow(smsIn.Message);
               
                if (isWatchTime && !isLifeMessage && !isMessageBlocked)
                    SendSmsToShift(smsIn);

                SendEmailToShift(smsIn, isWatchTime, isLifeMessage, isMessageBlocked);
            }
        }

        /// <summary>
        /// Prüft, ob die Nachricht 'SmsTestTrigger' z.B 'SmsAbruf' enthält und sendet die Nachricht zurück an den Sender. 
        /// </summary>
        /// <param name="sms"></param>
        /// <returns></returns>
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

        private static bool IsLifeMessage(SmsIn sms)
        {
            foreach (string trigger in LifeMessageTrigger)
            {
                if (sms.Message.ToLower().Contains(trigger.ToLower())) return true;
            }

            return false;
        }

        private static void SendSmsToShift(SmsIn sms)
        {
            foreach (string phone in Sql.GetCurrentShiftPhoneNumbers())
            {
                //Protokollierung in DB nach Absenden von Modem!
                SmsSend(phone, sms.Message);
            }
        }

        private static void SendEmailToShift(SmsIn smsIn, bool isWatchTime, bool isLifeMessage , bool isMessageBlocked)
        {
            string body = $"Absender \t>{smsIn.Phone}<\r\n" +
                   $"Text \t\t>{smsIn.Message}<\r\n" +
                   $"Sendezeit \t>{DateTime.Now:G}<\r\n\r\n" +
                   (
                   isLifeMessage ? $"Keine Weiterleitung an Bereitschaftshandy bei Schlüsselworten >{string.Join(", ", LifeMessageTrigger)}<." :
                   isMessageBlocked ? "Keine Weiterleitung an Bereitschaftshandy da SMS gesperrt." :
                   isWatchTime ? "Weiterleitung an Bereitschaftshandy außerhalb Geschäftszeiten ist erfolgt." :
                   "Keine Weiterleitung an Bereitschaftshandy während der Geschäftszeiten."
                   );

            Person p = Sql.SelectOrCreatePerson(smsIn);
            
        string subject = $"SMS-Eingang >{p.Name}<{ (p.Company?.Length == 0 ? string.Empty : $", >{p.Company}<")}, SMS-Text >{smsIn.Message}<";

            //Email An: nur an Bereitschaft
            System.Net.Mail.MailAddressCollection mc = (isWatchTime && !isLifeMessage && !isMessageBlocked)  ? Sql.GetCurrentShiftEmailAddresses() : new System.Net.Mail.MailAddressCollection();

            if (mc != null && mc.Count > 0)
            {
                int emailId = new Random().Next(256, int.MaxValue);

                Sql.InsertSent(mc[0], smsIn.Message, emailId);  //Protokollierung nur einmal pro mail, nicht für jden Empfänger einzeln! ok?
                Email.Send(mc, body, subject, true, emailId);
            }
        }



    }
}
