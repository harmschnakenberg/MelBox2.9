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

            InsertRecieved(smsenIn); //Empfang in Datenbank protokollieren

            foreach (SmsIn smsIn in smsenIn)
            {
                bool isSmsTest = IsSmsTest(smsIn);
                if (isSmsTest) continue;
                bool isLifeMessage = IsLifeMessage(smsIn);
                bool isMessageBlocked = IsMessageBlockedNow(smsIn.Message);
                bool isWatchTime = IsWatchTime();

                if (isWatchTime && !isLifeMessage && !isMessageBlocked)
                    SendSmsToShift(smsIn);

                SendEmailToShift(smsIn, isWatchTime, isLifeMessage, isMessageBlocked);
            }
        }

        private static bool IsSmsTest(SmsIn sms)
        {
            if (!sms.Message.ToLower().StartsWith(SmsTestTrigger.ToLower())) return false;

            Person p = SelectOrCreatePerson(sms);
            SendSms(sms.Phone, DateTime.Now.ToString("G") + sms.Message);
            InsertLog(3, $"SMS-Abruf von [{p.Id}] >{p.Phone}< >{p.Name}< >{p.Company}<");

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
            foreach (string phone in GetCurrentShiftPhoneNumbers())
            {
                //Protokollierung in DB nach Absenden von Modem!
                SendSms(phone, sms.Message);
            }
        }

        private static void SendEmailToShift(SmsIn smsIn, bool isWatchTime, bool isLifeMessage , bool isMessageBlocked)
        {
            string body = $"Absender >{smsIn.Phone}<\r\n" +
                   $"Text >{smsIn.Message}<\r\n" +
                   $"Sendezeit >{DateTime.Now:G}<\r\n\r\n" +
                   (
                   isLifeMessage ? $"Keine Weiterleitung an Bereitschaftshandy bei Schlüsselworten >{string.Join(", ", LifeMessageTrigger)}<." :
                   isMessageBlocked ? "Keine Weiterleitung an Bereitschaftshandy da SMS gesperrt." :
                   isWatchTime ? "Weiterleitung an Bereitschaftshandy außerhalb Geschäftszeiten ist erfolgt." :
                   "Keine Weiterleitung an Bereitschaftshandy während der Geschäftszeiten."
                   );

            Person p = SelectOrCreatePerson(smsIn);

            string subject = $"SMS-Eingang >{p.Name}< >{p.Company}<, SMS-Text >{smsIn.Message}<";

            Email.Send(GetCurrentShiftEmailAddresses(), body, subject, true);
        }

    }
}
