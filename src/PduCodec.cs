using System;
using System.Collections.Generic;

namespace SmsPing
{
    // Kết quả giải mã 1 bản tin REPORT của lệnh PING
    public struct KETQUA
    {
        public bool ER;          // true = giải mã được (đúng là report)
        public string MR;        // message reference
        public string sdt_dcping;// số ĐT đã ping
        public string t_ping;    // thời điểm SMSC nhận
        public string t_report;  // thời điểm phát report
        public string kq;        // kết quả (đã ánh xạ sang mô tả)
        public string kq_sms;    // kết quả gốc
    }

    public static class PduCodec
    {
        private struct KqEntry
        {
            public string Vi;
            public string Sms;
            public KqEntry(string vi, string sms) { Vi = vi; Sms = sms; }
        }

        // 84 = mã VN; SwapDigits hoán vị nửa-octet 9 số thuê bao (bỏ số 0 đầu)
        public static string SwapDigits(string sdt)
        {
            return string.Concat(new string[]
            {
                sdt[2].ToString(), sdt[1].ToString(),
                sdt[4].ToString(), sdt[3].ToString(),
                sdt[6].ToString(), sdt[5].ToString(),
                sdt[8].ToString(), sdt[7].ToString(),
                "F",               sdt[9].ToString()
            });
        }

        // PDU "ping thầm" (special SMS indication) tới 1 số VN.
        // smscPrefix nhét địa chỉ SMSC vào đầu PDU -> modem không cần có SMSC sẵn.
        // "00" = dùng SMSC lưu trên SIM (mặc định cũ). AT+CMGS=19 GIỮ NGUYÊN (chỉ đếm phần TPDU).
        public static string BuildPingPdu(string sdt, string smscPrefix = "00")
        {
            if (string.IsNullOrEmpty(smscPrefix)) smscPrefix = "00";
            return smscPrefix + "71000B9148" + SwapDigits(sdt) + "000800050401020000";
        }

        // Mã hoá SMSC vào đầu PDU: [độ dài octet][91=quốc tế][số đã hoán vị nửa-octet].
        // "+84900000023" -> "07914809000020F3". Rỗng -> "00".
        public static string EncodeSmscPrefix(string intlNumber)
        {
            if (string.IsNullOrEmpty(intlNumber)) return "00";
            string d = intlNumber.StartsWith("+") ? intlNumber.Substring(1) : intlNumber;
            System.Text.StringBuilder only = new System.Text.StringBuilder();
            foreach (char c in d) if (char.IsDigit(c)) only.Append(c);
            d = only.ToString();
            if (d.Length == 0) return "00";
            string padded = (d.Length % 2 == 0) ? d : d + "F";
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < padded.Length; i += 2) { sb.Append(padded[i + 1]); sb.Append(padded[i]); }
            string swapped = sb.ToString();
            int octets = 1 + swapped.Length / 2;
            return octets.ToString("X2") + "91" + swapped;
        }

        private static string Hx(string s, int i)
        {
            return s[i].ToString() + s[i + 1].ToString();
        }

        private static string C(string s, int i) { return s[i].ToString(); }

        public static KETQUA Decode(string textCodeInput)
        {
            string s = textCodeInput;
            if (!string.IsNullOrEmpty(s) && (s.StartsWith("06") || s.StartsWith("07")))
                s = "00" + s;

            KETQUA r = new KETQUA();
            int len = string.IsNullOrEmpty(s) ? 0 : s.Length;

            switch (len)
            {
                case 66: // 0x42
                    r.ER = true;
                    r.MR = Convert.ToInt32(Hx(s, 18), 16).ToString();
                    r.sdt_dcping = "0" + C(s,27)+C(s,26)+C(s,29)+C(s,28)+C(s,31)+C(s,30)+C(s,33)+C(s,32)+C(s,35);
                    r.t_ping   = C(s,43)+C(s,42)+":"+C(s,45)+C(s,44)+":"+C(s,47)+C(s,46)+", ngay "+C(s,41)+C(s,40)+"/"+C(s,39)+C(s,38)+"/20"+C(s,37)+C(s,36);
                    r.t_report = C(s,57)+C(s,56)+":"+C(s,59)+C(s,58)+":"+C(s,61)+C(s,60)+", ngay "+C(s,55)+C(s,54)+"/"+C(s,53)+C(s,52)+"/20"+C(s,51)+C(s,50);
                    r.kq = Hx(s, 64);
                    r.kq_sms = r.kq;
                    break;

                case 64: // 0x40
                    r.ER = true;
                    r.MR = Convert.ToInt32(Hx(s, 16), 16).ToString();
                    r.sdt_dcping = "0" + C(s,25)+C(s,24)+C(s,27)+C(s,26)+C(s,29)+C(s,28)+C(s,31)+C(s,30)+C(s,33);
                    r.t_ping   = C(s,41)+C(s,40)+":"+C(s,43)+C(s,42)+":"+C(s,45)+C(s,44)+", ngay "+C(s,39)+C(s,38)+"/"+C(s,37)+C(s,36)+"/20"+C(s,35)+C(s,34);
                    r.t_report = C(s,55)+C(s,54)+":"+C(s,57)+C(s,56)+":"+C(s,59)+C(s,58)+", ngay "+C(s,53)+C(s,52)+"/"+C(s,51)+C(s,50)+"/20"+C(s,49)+C(s,48);
                    r.kq = Hx(s, 62);
                    r.kq_sms = r.kq;
                    break;

                case 52: // 0x34
                    r.ER = true;
                    r.MR = Convert.ToInt32(Hx(s, 4), 16).ToString();
                    r.sdt_dcping = "0" + C(s,13)+C(s,12)+C(s,15)+C(s,14)+C(s,17)+C(s,16)+C(s,19)+C(s,18)+C(s,21);
                    r.t_ping   = C(s,29)+C(s,28)+":"+C(s,31)+C(s,30)+":"+C(s,33)+C(s,32)+", ngay "+C(s,27)+C(s,26)+"/"+C(s,25)+C(s,24)+"/20"+C(s,23)+C(s,22);
                    r.t_report = C(s,43)+C(s,42)+":"+C(s,45)+C(s,44)+":"+C(s,47)+C(s,46)+", ngay "+C(s,40)+C(s,39)+"/"+C(s,38)+C(s,37)+"/20"+C(s,36)+C(s,35);
                    r.kq = Hx(s, 50);
                    r.kq_sms = r.kq;
                    break;

                case 54: // 0x36
                    r.ER = true;
                    r.MR = Convert.ToInt32(Hx(s, 4), 16).ToString();
                    r.sdt_dcping = "0" + C(s,13)+C(s,12)+C(s,15)+C(s,14)+C(s,17)+C(s,16)+C(s,19)+C(s,18)+C(s,21);
                    r.t_ping   = C(s,29)+C(s,28)+":"+C(s,31)+C(s,30)+":"+C(s,33)+C(s,32)+", ngay "+C(s,27)+C(s,26)+"/"+C(s,25)+C(s,24)+"/20"+C(s,23)+C(s,22);
                    r.t_report = C(s,43)+C(s,42)+":"+C(s,45)+C(s,44)+":"+C(s,47)+C(s,46)+", ngay "+C(s,41)+C(s,40)+"/"+C(s,39)+C(s,38)+"/20"+C(s,37)+C(s,36);
                    r.kq = Hx(s, 52);
                    r.kq_sms = r.kq;
                    break;

                default:
                    r.ER = false;
                    r.MR = ""; r.sdt_dcping = ""; r.t_ping = ""; r.t_report = ""; r.kq = ""; r.kq_sms = "";
                    break;
            }

            if (!string.IsNullOrEmpty(r.kq) && KqTable.TryGetValue(r.kq, out KqEntry e))
            {
                r.kq = e.Vi;
                r.kq_sms = e.Sms;
            }
            return r;
        }

        // Bảng mô tả mã kết quả (TP-Status của STATUS-REPORT)
        private static readonly Dictionary<string, KqEntry> KqTable =
            new Dictionary<string, KqEntry>(StringComparer.OrdinalIgnoreCase)
        {
            { "00", new KqEntry("SDT PING ONLINE.", "SDT PING ONLINE.") },
            { "01", new KqEntry("SMSC can not send.", "SMSC can not send.") },
            { "02", new KqEntry("SMS replace SMSC.", "SMS replace SMSC.") },
            { "03", new KqEntry("Lower End of the Reserved Values in This Sector.", "Lower End of the Reserved Values in This Sector.") },
            { "0F", new KqEntry("High End of the Reserved Values in This Sector.", "High End of the Reserved Values in This Sector.") },
            { "10", new KqEntry("Lower End of Values Specific to each SMSC.", "Lower End of Values Specific to each SMSC.") },
            { "1F", new KqEntry("High End of Values Specific to each SMSC in This Sector.", "High End of Values Specific to each SMSC in This Sector.") },
            { "20", new KqEntry("Congestion.", "Congestion.") },
            { "60", new KqEntry("Congestion.", "Congestion.") },
            { "21", new KqEntry("SDT ban.", "SDT ban.") },
            { "61", new KqEntry("SDT ban.", "SDT ban.") },
            { "22", new KqEntry("SDT Khong hoi dap.", "SDT Khong hoi dap.") },
            { "62", new KqEntry("SDT Khong hoi dap.", "SDT Khong hoi dap.") },
            { "23", new KqEntry("Service rejected.", "Service rejected.") },
            { "63", new KqEntry("Service rejected.", "Service rejected.") },
            { "24", new KqEntry("service not available.", "service not available.") },
            { "64", new KqEntry("service not available.", "service not available.") },
            { "25", new KqEntry("Loi o DT dich.", "Loi o DT dich.") },
            { "65", new KqEntry("Loi o DT dich.", "Loi o DT dich.") },
            { "26", new KqEntry("Lower End of the Reserved Values in This Sector.", "Lower End of the Reserved Values in This Sector.") },
            { "66", new KqEntry("Lower End of the Reserved Values in This Sector.", "Lower End of the Reserved Values in This Sector.") },
            { "2F", new KqEntry("High End of the Reserved Values in This Sector.", "High End of the Reserved Values in This Sector.") },
            { "6F", new KqEntry("High End of the Reserved Values in This Sector.", "High End of the Reserved Values in This Sector.") },
            { "30", new KqEntry("Lower End of Values Specific to each SMSC.", "Lower End of Values Specific to each SMSC.") },
            { "70", new KqEntry("Lower End of Values Specific to each SMSC.", "Lower End of Values Specific to each SMSC.") },
            { "3F", new KqEntry("High End of Values Specific to each SMSC in This Sector.", "High End of Values Specific to each SMSC in This Sector.") },
            { "7F", new KqEntry("High End of Values Specific to each SMSC in This Sector.", "High End of Values Specific to each SMSC in This Sector.") },
            { "40", new KqEntry("Remote procedure error.", "Remote procedure error.") },
            { "41", new KqEntry("Incompatible destination.", "Incompatible destination.") },
            { "42", new KqEntry("Connection rejected by DT dich.", "Connection rejected by DT dich.") },
            { "43", new KqEntry("Not obtainable.", "Not obtainable.") },
            { "44", new KqEntry("Quality of service not available.", "Quality of service not available.") },
            { "45", new KqEntry("SDT PING KHONG CO THUC.", "SDT PING KHONG CO THUC.") },
            { "46", new KqEntry("Het han. SMS xoa TN", "Het han. SMS xoa TN") },
            { "47", new KqEntry("SMS Deleted by originating DT dich.", "SMS Deleted by originating DT dich.") },
            { "48", new KqEntry("SMS Deleted by SMSC Administration.", "SMS Deleted by SMSC Administration.") },
            { "49", new KqEntry("SMS does not exist.", "SMS does not exist.") },
            { "4A", new KqEntry("Lower End of the Reserved Values in This Sector.", "Lower End of the Reserved Values in This Sector.") },
            { "4F", new KqEntry("High End of the Reserved Values in This Sector.", "High End of the Reserved Values in This Sector.") },
            { "50", new KqEntry("Lower End of Values Specific to each SMSC.", "Lower End of Values Specific to each SMSC.") },
            { "5F", new KqEntry("High End of Values Specific to each SMSC in This Sector.", "High End of Values Specific to each SMSC in This Sector.") },
        };
    }
}
