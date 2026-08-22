using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO.Ports;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace SmsPing
{
    public partial class frmMain : Form
    {
        private string _myImei = "";       // buffer nhận dữ liệu từ modem
        private bool _flagCheckImei = false;

        // ================== BẢNG SMSC THEO NHÀ MẠNG (MCC = 452) ==================
        // 01 Mobifone | 02 Vinaphone | 04 Viettel | 05 Vietnamobile | 07 Gmobile
        private static readonly Dictionary<string, string> SmscByNetwork =
            new Dictionary<string, string>
        {
            { "01", "+84900000023" }, // Mobifone (miền Nam). Bắc: +84900000011 | Trung: +84900000017
            { "02", "+8491020005"  }, // Vinaphone
            { "04", "+84980200030" }, // Viettel
            { "05", "+84925252525" }, // Vietnamobile
            { "07", "+84995252525" }, // Gmobile
        };

        // Danh sách IMEI thiết bị hợp lệ. Thêm IMEI modem mới vào đây nếu muốn dùng máy khác.
        private static readonly string[] AllowedImei = new string[]
        {
            "862636051970828","862636051979746","862636054171572","862636054064009",
            "862636054182835","862636054166416","866506050985885","862636056523887",
            "862636057265306"
        };

        public frmMain()
        {
            InitializeComponent();
        }

        private void frmMain_Load(object sender, EventArgs e)
        {
            try
            {
                string[] ports = SerialPort.GetPortNames();
                cmbPort.Items.AddRange(ports);
                if (ports.Length != 0)
                    cmbPort.SelectedIndex = ports.Length - 1;

                rbPing.Checked = true;
                ckbCr.Checked = true;
                btnDisconnect.Enabled = false;
                SetConnected(false);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không liệt kê được cổng COM: " + ex.Message, "Lỗi",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ============================ KẾT NỐI ============================
        private void btnConnect_Click(object sender, EventArgs e)
        {
            if (cmbPort.SelectedItem == null)
            {
                MessageBox.Show("Chọn cổng COM trước.", "Thông báo",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            try
            {
                lblStatus.Text = "Đang kết nối...";
                lblStatus.ForeColor = Color.DarkOrange;
                TimerConnect.Enabled = false;

                SerialPort1.PortName = cmbPort.Text;
                SerialPort1.BaudRate = 9600;
                SerialPort1.Parity = Parity.None;
                SerialPort1.StopBits = StopBits.One;
                SerialPort1.DataBits = 8;
                SerialPort1.Open();

                btnConnect.Enabled = false;
                btnDisconnect.Enabled = true;

                _myImei = "";
                _flagCheckImei = true;

                SerialPort1.Write("AT\r\n");
                for (int i = 0; i < 10 && !_myImei.Contains("OK"); i++)
                {
                    SerialPort1.Write("AT\r\n");
                    Thread.Sleep(1500);
                    Application.DoEvents();
                }

                SerialPort1.Write("AT+CGSN\r\n");
                for (int i = 0; i < 20 && !Regex.IsMatch(_myImei, "\\d{15}"); i++)
                {
                    Thread.Sleep(500);
                    Application.DoEvents();
                }

                CheckImei();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi kết nối: " + ex.Message);
            }
        }

        private void btnDisconnect_Click(object sender, EventArgs e)
        {
            try { SerialPort1.Close(); }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
            finally { SetConnected(false); }
        }

        // =================== KIỂM TRA IMEI + TỰ GẮN SMSC ===================
        private void CheckImei()
        {
            Match m = Regex.Match(_myImei, "\\d{15}");
            if (!m.Success)
            {
                MessageBox.Show("Không đọc được IMEI thiết bị:\r\n" + _myImei, "Lỗi",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                SerialPort1.Close();
                SetConnected(false);
                _flagCheckImei = false;
                return;
            }

            string imei = m.Value;
            if (!AllowedImei.Contains(imei))
            {
                MessageBox.Show("Thiết bị không hợp lệ.", "Lỗi",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                SerialPort1.Close();
                SetConnected(false);
                _flagCheckImei = false;
                return;
            }

            // Ưu tiên mạng LTE
            SerialPort1.Write("AT+CNMP=38\r\n"); Thread.Sleep(300); Application.DoEvents();

            // ---- Dọn bộ nhớ SMS (tránh "+CMS ERROR: Memory full") ----
            // Xóa sạch mọi vùng nhớ: SIM (SM), modem (ME), report (SR)
            foreach (string mem in new[] { "SM", "ME", "SR" })
            {
                try
                {
                    SerialPort1.Write("AT+CPMS=\"" + mem + "\",\"" + mem + "\",\"" + mem + "\"\r\n");
                    Thread.Sleep(250); Application.DoEvents();
                    SerialPort1.Write("AT+CMGD=1,4\r\n");
                    Thread.Sleep(500); Application.DoEvents();
                }
                catch { }
            }
            // Ưu tiên lưu tin đến vào bộ nhớ modem cho các lần sau
            SerialPort1.Write("AT+CPMS=\"ME\",\"ME\",\"ME\"\r\n"); Thread.Sleep(250); Application.DoEvents();

            // ---- Nhận diện nhà mạng của SIM rồi set SMSC ----
            // 1) IMSI (chuẩn nhất, bám theo SIM)
            _myImei = "";
            SerialPort1.Write("AT+CIMI\r\n"); Thread.Sleep(600); Application.DoEvents();
            string smsc = DetectSmscByNetwork(_myImei);

            // 2) Dự phòng: mạng đang bắt
            if (string.IsNullOrEmpty(smsc))
            {
                _myImei = "";
                SerialPort1.Write("AT+CPSI?\r\n"); Thread.Sleep(900); Application.DoEvents();
                smsc = DetectSmscByNetwork(_myImei);
            }

            if (!string.IsNullOrEmpty(smsc))
            {
                SerialPort1.Write("AT+CSCA=\"" + smsc + "\",145\r\n");
                Thread.Sleep(400); Application.DoEvents();

                _myImei = "";
                SerialPort1.Write("AT+CSCA?\r\n"); Thread.Sleep(400); Application.DoEvents();
                if (string.IsNullOrEmpty(ExtractCurrentSmsc(_myImei)))
                    MessageBox.Show("Đã gửi set SMSC (" + smsc + ") nhưng modem chưa xác nhận.\r\n" +
                                    "Cứ thử PING; nếu vẫn lỗi thì rút/cắm lại SIM rồi Connect lại.",
                                    "SMSC", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            else
            {
                _myImei = "";
                SerialPort1.Write("AT+CSCA?\r\n"); Thread.Sleep(300); Application.DoEvents();
                if (string.IsNullOrEmpty(ExtractCurrentSmsc(_myImei)))
                    MessageBox.Show("Chưa dò được nhà mạng của SIM. Vào tab AT Command gõ 1 dòng:\r\n\r\n" +
                                    "Viettel      : AT+CSCA=\"+84980200030\"\r\n" +
                                    "Vinaphone    : AT+CSCA=\"+8491020005\"\r\n" +
                                    "Mobifone     : AT+CSCA=\"+84900000023\"\r\n" +
                                    "Vietnamobile : AT+CSCA=\"+84925252525\"\r\n" +
                                    "Gmobile      : AT+CSCA=\"+84995252525\"",
                                    "Set SMSC", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            SerialPort1.Write("AT+CNMI=1,0,0,1,0\r\n"); Thread.Sleep(200);
            SerialPort1.Write("AT+CLIP=1\r\n");

            txtTarget.Focus();
            _flagCheckImei = false;
            SetConnected(true);
        }

        // Đọc SMSC hiện tại từ phản hồi AT+CSCA?
        private static string ExtractCurrentSmsc(string data)
        {
            Match m = Regex.Match(data ?? "", "\\+CSCA:\\s*\"([^\"]*)\"");
            return m.Groups[1].Success ? m.Groups[1].Value : "";
        }

        // Dò SMSC theo nhà mạng: ưu tiên IMSI (AT+CIMI), dự phòng chuỗi mạng (CPSI/COPS)
        private static string DetectSmscByNetwork(string data)
        {
            data = data ?? "";
            string mnc = null;

            Match mi = Regex.Match(data, "452(\\d\\d)\\d{10}"); // IMSI: 452 + MNC(2) + 10 số
            if (mi.Success) mnc = mi.Groups[1].Value;

            if (mnc == null)
            {
                Match mc = Regex.Match(data, "452\\s*[- ]\\s*0(\\d)"); // "452-01" / "452 01"
                if (mc.Success) mnc = "0" + mc.Groups[1].Value;
            }

            if (mnc != null && SmscByNetwork.TryGetValue(mnc, out string smsc)) return smsc;
            return null;
        }

        // ============================ NHẬN DỮ LIỆU ============================
        private void SerialPort1_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                string data = SerialPort1.ReadExisting();
                if (txtRaw.InvokeRequired)
                    txtRaw.Invoke(new Action<string>(ReceivedText), new object[] { data });
                else
                    ReceivedText(data);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void ReceivedText(string data)
        {
            txtRaw.AppendText(data);
            if (_flagCheckImei) _myImei = _myImei + data;
        }

        // ============================ PING ============================
        private static bool ValidPhone(string sdt)
        {
            return sdt.Length >= 10 && sdt.All(char.IsDigit) && sdt[0] == '0';
        }

        private void btnSend_Click(object sender, EventArgs e)
        {
            if (!SerialPort1.IsOpen)
            {
                MessageBox.Show("Connect COM port trước khi sử dụng lệnh.", "Thông báo",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            string sdt = txtTarget.Text;
            if (!ValidPhone(sdt))
            {
                MessageBox.Show("Kiểm tra lại định dạng SĐT cần PING (10 số, bắt đầu bằng 0).",
                    "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            SendPdu(PduCodec.BuildPingPdu(sdt));
        }

        private void SendPdu(string pdu)
        {
            SerialPort1.Write("AT+CMGF=0\r\n"); Thread.Sleep(200);
            SerialPort1.Write("AT+CMGS=19\r\n"); Thread.Sleep(300);
            SerialPort1.Write(pdu); Thread.Sleep(200);
            SerialPort1.Write("\u001a"); // Ctrl+Z
        }

        // ============================ AT COMMAND ============================
        private void btnSendAt_Click(object sender, EventArgs e)
        {
            if (!SerialPort1.IsOpen)
            {
                MessageBox.Show("Connect COM port trước khi sử dụng lệnh.", "Thông báo",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            SerialPort1.Write(ckbCr.Checked ? txtAt.Text + "\r" : txtAt.Text);
        }

        private void btnCtrlZ_Click(object sender, EventArgs e)
        {
            if (!SerialPort1.IsOpen)
            {
                MessageBox.Show("Connect COM port trước khi sử dụng lệnh.", "Thông báo",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            SerialPort1.Write("\u001a");
        }

        private void btn_TK_Click(object sender, EventArgs e)
        {
            if (!SerialPort1.IsOpen)
            {
                MessageBox.Show("Connect COM port trước khi sử dụng lệnh.", "Thông báo",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            SerialPort1.Write("AT+CMGF=1\r\n"); Thread.Sleep(200);
            SerialPort1.Write("AT+CUSD=1,\"*101#\"\r\n"); Thread.Sleep(200);
            SerialPort1.Write("AT+CMGF=0\r\n");
        }

        private void btn_chkHard_Click(object sender, EventArgs e)
        {
            if (!SerialPort1.IsOpen)
            {
                MessageBox.Show("Connect COM port trước khi sử dụng lệnh.", "Thông báo",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            SerialPort1.Write("AT\r\n"); Thread.Sleep(200);
            SerialPort1.Write("AT+CSQ\r\n");
        }

        // ============================ DECODE ============================
        private void btnClr_Click(object sender, EventArgs e)
        {
            txtRaw.Clear();
            txtDecode.Clear();
        }

        private void btnDecodeSel_Click(object sender, EventArgs e)
        {
            string sel = txtRaw.SelectedText;
            if (string.IsNullOrWhiteSpace(sel))
            {
                MessageBox.Show("Bạn phải chọn (bôi đen) phần REPORT cần decode.", "Thông báo",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            sel = sel.Trim().Replace("\r\n", "").Replace("\r", "").Replace("\n", "");
            KETQUA kq = PduCodec.Decode(sel);
            if (!kq.ER)
            {
                MessageBox.Show("Chỉ chọn phần kết quả REPORT của lệnh PING để DECODE.", "Thông báo",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            txtDecode.AppendText(
                "PING SMS có CMGS: " + kq.MR +
                "\r\nĐến SĐT " + kq.sdt_dcping +
                "\r\nĐược SMSC nhận lúc: " + kq.t_ping +
                ", phát lúc: " + kq.t_report +
                "\r\nCó kết quả: " + kq.kq + "\r\n\r\n");
        }

        // ============================ MISC ============================
        private void btnHelp_Click(object sender, EventArgs e)
        {
            MessageBox.Show(
                "- Bạn có thể PING cho một hoặc nhiều số ĐT đang tắt máy.\r\n" +
                "Thiết bị sẽ SMS báo cho bạn khi SĐT đó online trở lại.\r\n" +
                "- Bôi đen phần REPORT trong RAW CODE rồi bấm DECODE để đọc kết quả.",
                "Trợ giúp");
        }

        private void btnAbout_Click(object sender, EventArgs e)
        {
            MessageBox.Show(
                "Thiết bị do tác giả và PTH thực hiện.\r\nPhiên bản viết lại gọn từ mã gốc.",
                "About");
        }

        private void cmbPort_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (SerialPort1.IsOpen)
            {
                MessageBox.Show("Disconnect trước khi chọn cổng COM.", "Thông báo",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            SerialPort1.PortName = cmbPort.Text;
        }

        private void rbPing_CheckedChanged(object sender, EventArgs e)
        {
            panelPing.Visible = rbPing.Checked;
            panelAt.Visible = !rbPing.Checked;
            if (rbPing.Checked) txtTarget.Focus();
        }

        private void rbAt_CheckedChanged(object sender, EventArgs e)
        {
            panelAt.Visible = rbAt.Checked;
            panelPing.Visible = !rbAt.Checked;
            if (rbAt.Checked) txtAt.Focus();
        }

        private void SetConnected(bool connected)
        {
            btnConnect.Enabled = !connected;
            btnDisconnect.Enabled = connected;
            TimerConnect.Enabled = connected;
            lblStatus.Text = connected ? "Đã kết nối" : "Chưa kết nối";
            lblStatus.ForeColor = connected ? Color.Green : Color.Red;
        }

        private void TimerConnect_Tick(object sender, EventArgs e)
        {
            bool open = SerialPort1.IsOpen;
            lblStatus.Text = open ? "Đã kết nối" : "Chưa kết nối";
            lblStatus.ForeColor = open ? Color.Green : Color.Red;
        }
    }
}
